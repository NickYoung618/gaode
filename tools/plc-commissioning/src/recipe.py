"""Operator-started recipe; real PLC I/O uses the commissioning queue only."""
import copy
import json
import math
import time
import uuid


def finite(value, low, high, name):
    if type(value) not in (int, float) or not math.isfinite(value) or not low <= value <= high:
        raise ValueError(f'{name} 必须为 {low}～{high}')


def validate(profile):
    if 'z' in profile['steps'][2] or 'z' in profile['steps'][13]:
        raise ValueError('F扫码与最终下料仅允许XY定位，不能配置扫码Z或抓取Z')
    if profile.get('flowTestOnly') and (profile.get('cameraMode')!='simulated' or any(profile.get('simulation',{}).get(k)!='OK' for k in ('pose','poseReview','A','B','C','D'))):
        raise ValueError('本轮流程测试固定模拟有料、姿态正常、OK，不执行分拣')
    finite(profile.get('positionTolerance',0.1),0,10,'到位容差')
    if profile.get('name') != '模拟翻面件-001' or len(profile.get('steps', [])) != 14:
        raise ValueError('本配方必须保留14步和配方名称')
    kinds = ['load','pose','scan','photo','photo','position','flip','position','unload','poseReview','photo','photo','result','finish']
    for i, step in enumerate(profile['steps']):
        if step.get('id') != i+1 or step.get('kind') != kinds[i]:
            raise ValueError('配方步骤顺序不能改变')
        for key in ('x','y'):
            if key in step: finite(step[key],0,100,f'步骤{i+1} {key}')
        if 'z' in step: finite(step['z'],0,10,f'步骤{i+1} Z')
        if i in (3,4,10,11) and step.get('camera') != {3:'A',4:'B',10:'C',11:'D'}[i]:
            raise ValueError('本配方相机固定为A/B/C/D，不使用E相机')
        if step.get('zAxis') not in (None,'ZCamera','ZScan','ZGrab'):
            raise ValueError('本配方不使用R轴')
    if profile['steps'][6].get('targetFace') != 2:
        raise ValueError('本配方翻到第二面，面编号必须为2')
    for axis in ('ZCamera','ZScan','ZGrab'):
        value = profile['safeZ'].get(axis)
        if value is not None: finite(value,0,10,axis+'安全高度')
    for key,value in profile['sorting'].items():
        finite(value,0,10 if key.endswith('Z') else 100,'分拣'+key)
    if profile.get('modelCode') is not None:
        finite(profile['modelCode'],-3.4028234e38,3.4028234e38,'现场型号REAL原码')
    if profile.get('grabId') not in (None,1,2): raise ValueError('现场抓手编号只能为1或2')
    for field in ('pose','poseReview','A','B','C','D'):
        if profile.get('simulation',{}).get(field,'OK') not in ('OK','NG','Pending','NoMaterial'):
            raise ValueError('模拟相机结果无效')
    if profile.get('cameraMode') not in ('simulated','operator'): raise ValueError('相机模式无效')
    return profile


def member_gripper_jobs(profile):
    """Resolve member requirements before I/O; the transport never reads this profile."""
    members=profile.get('gripperMembers',[])
    if not members:raise ValueError('请先配置成组成员及各自夹爪')
    seen=set();materials={};jobs=[]
    for member in members:
        identity=member.get('memberId');material=member.get('material');grip=member.get('grabId')
        if not isinstance(identity,str) or not identity.strip() or identity in seen:
            raise ValueError('成员名称必须填写且不能重复')
        if not isinstance(material,str) or not material.strip() or type(grip) is not int or grip not in (1,2):
            raise ValueError('每个成员必须填写零件类型和夹爪1或2')
        if material in materials and materials[material]!=grip:
            raise ValueError('同一零件类型的夹爪配置必须一致')
        seen.add(identity);materials[material]=grip
        jobs.append(dict(type='grab',memberId=identity,material=material,grabId=grip))
    return jobs


class Recipe:
    def __init__(self, app):
        self.app = app
        self.path = app.root/'config/recipe.local.json'
        source = self.path if self.path.exists() else app.root/'config/recipe.default.json'
        self.profile = validate(json.loads(source.read_text(encoding='utf-8-sig')))
        self.run = None
        self.jobs = []
        self.wait = None
        self.ticking = False
        self.last_targets = {}

    def active(self):
        return self.run is not None and self.run['state'] in ('running','waiting','paused')

    def snapshot(self):
        return copy.deepcopy(dict(profile=self.profile, run=self.run, wait=self.wait,
                                  currentJob=self.jobs[0] if self.jobs else None))

    def log(self, kind, message, **fields):
        if self.run:
            fields.update(recipeRunId=self.run['id'], recipeStep=self.run['step'])
        record=self.app.event(kind,message,**fields)
        if self.run:
            self.run.setdefault('milestones',[]).append(copy.deepcopy(record))
            self.run['milestones']=self.run['milestones'][-300:]
        if self.run:
            (self.app.logdir/'recipe-run.json').write_text(json.dumps(self.run,ensure_ascii=False,indent=2),encoding='utf-8')

    def checkpoint(self, kind, message):
        return dict(type='operator',kind=kind,message=message)

    def axis(self, axis, value, force=False): return dict(type='axis',axis=axis,value=value,force=force)

    def position(self, x, y, transfer=False, force=False):
        jobs=[]
        # A consecutive positioning step may reuse the position just confirmed by this recipe.
        if not force and self.last_targets.get('X')==x and self.last_targets.get('Y')==y:
            return [dict(type='reuse',x=x,y=y,message='沿用本配方刚完成的XY定位，复核PLC当前到位反馈，不重复启动')]
        missing=[a for a,v in self.profile['safeZ'].items() if v is None and not (transfer and a=='ZGrab')]
        if missing:
            jobs.append(self.checkpoint('safe','跨点移动前确认各Z已处于安全位置；未配置安全高度的Z不会自动移动'))
        for axis,value in self.profile['safeZ'].items():
            if transfer and axis=='ZGrab': value=self.profile['sorting']['transferZ']
            if value is not None: jobs.append(self.axis(axis,value))
        jobs.extend([self.axis('X',x,force),self.axis('Y',y,force)])
        return jobs

    def prepare(self):
        step=self.profile['steps'][self.run['step']-1]
        jobs=[]
        if step['id']==6: jobs += [dict(type='write',mb=2012,value=2),dict(type='model')]
        if step['id']==1 and self.profile.get('flowTestOnly'):
            jobs.append(dict(type='loaded',message='核对人工上料位置(0,0)'))
        elif 'x' in step and self.profile.get('flowTestOnly'):
            jobs.append(dict(type='xy',x=step['x'],y=step['y']))
        elif step['id'] in (3,8,14):
            jobs += [self.axis('X',step['x'],True),self.axis('Y',step['y'],True)]
        elif 'x' in step: jobs += self.position(step['x'],step['y'])
        if 'z' in step: jobs.append(self.axis(step['zAxis'],step['z'],self.profile.get('flowTestOnly',False)))
        kind=step['kind']
        if kind in ('load','pose','scan','photo','poseReview','finish'):
            messages={'load':'确认已放盘、一盘一件，PLC设备就绪', 'pose':'确认3D有料且姿态正常',
                      'scan':'记录F扫码结果并确认匹配本配方', 'photo':'确认拍照及处理完成',
                      'poseReview':'确认第二面姿态；此处不重新扫码绑定', 'finish':'确认设备到位、人工取盘，本盘结束'}
            if kind=='photo':messages[kind]=f"第{step['face']}面{step['camera']}拍照与检测完成"
            checkpoint=self.checkpoint(kind,messages[kind])
            if self.profile['cameraMode']=='simulated' and kind not in ('load','finish'):
                checkpoint['type']='simulation'
            if not (self.profile.get('flowTestOnly') and kind in ('load','finish')):jobs.append(checkpoint)
        if kind=='flip':
            jobs.append(dict(type='command',mb=2014,value=1,feedback=6050,busy=1,done=2))
        if kind=='unload':
            jobs.extend([dict(type='command',mb=2014,value=2,feedback=6052,busy=1,done=2),
                         dict(type='write',mb=2014,value=0)])
        if kind=='result':
            checkpoint=self.checkpoint('result','汇总结果，保存后按OK/NG/Pending处置')
            if self.profile['cameraMode']=='simulated':checkpoint['type']='simulation'
            jobs.append(checkpoint)
        self.jobs=jobs
        self.run['steps'][self.run['step']-1]['state']='running'
        self.log('RECIPE_STEP','开始步骤：'+step['name'])

    def sorting_jobs(self, result):
        s=self.profile['sorting']
        if result=='OK':
            return [dict(type='reuse',message='OK工件留原槽，不发送公共取放料指令')]
        x,y=(s['ngX'],s['ngY']) if result=='NG' else (s['pendingX'],s['pendingY'])
        return [dict(type='grab')]+self.position(s['sourceX'],s['sourceY'],True)+[
            self.axis('ZGrab',s['pickZ']),dict(type='command',mb=2016,value=1,feedback=6054,busy=0,done=1),
            self.axis('ZGrab',s['transferZ'])]+self.position(x,y,True)+[
            self.axis('ZGrab',s['placeZ']),dict(type='command',mb=2016,value=2,feedback=6054,busy=0,done=2),
            dict(type='write',mb=2016,value=0),dict(type='idle',mb=6054),self.axis('ZGrab',s['transferZ'])]

    async def command(self, name, data):
        if name=='recipe-settings':
            profile=validate(copy.deepcopy(data['profile']))
            if self.active():
                # Only late-bound site codes may be supplied to an already started recipe.
                old=copy.deepcopy(self.profile);new=copy.deepcopy(profile)
                for key in ('modelCode','grabId'):old.pop(key,None);new.pop(key,None)
                if old!=new:raise ValueError('配方运行中只能补充现场型号和抓手编号；其他参数请结束本盘后修改')
                if self.run.get('frozenProfile'):
                    for key in ('modelCode','grabId'):
                        if self.profile.get(key) is not None and profile.get(key)!=self.profile.get(key):
                            raise ValueError('F扫码已冻结本轮参数，已确认的现场编码不能改变')
            self.profile=profile
            self.path.write_text(json.dumps(profile,ensure_ascii=False,indent=2),encoding='utf-8')
            self.log('RECIPE_CONFIG','配方已保存；未发送PLC指令')
        elif name in ('recipe-start','recipe-gripper-check'):
            member_jobs=member_gripper_jobs(self.profile) if name=='recipe-gripper-check' else None
            if self.active():raise ValueError('已有本盘配方，请暂停或结束后再开始')
            if not self.app.fresh():raise ValueError('请先连接PLC，取得真实反馈')
            if any(a['state'] in ('pending','observing') for a in self.app.actions):raise ValueError('还有单轴动作在观察，请先处理')
            self.run=dict(id=uuid.uuid4().hex[:12],name=self.profile['name'],state='running',step=1,
                          started=time.time(),steps=[dict(id=i+1,name=s['name'],state='queued') for i,s in enumerate(self.profile['steps'])],
                          barcode=None,inspections=[],signals=[],milestones=[],trail=[],result=None,simulated=self.profile['cameraMode']=='simulated',recordPurpose='流程测试',realInspectionResult=False,reason='等待上料定位')
            self.last_targets={};self.wait=None
            if member_jobs is not None:
                self.run.update(step=13,gripperCheck=True,selectionMembers=copy.deepcopy(self.profile['gripperMembers']),
                                reason='仅验证成组夹爪选择，不执行轴运动、取放料或检测')
                self.jobs=member_jobs
                self.log('GRIPPER_CHECK_START',self.run['reason'],members=self.run['selectionMembers'])
            else:self.prepare()
            await self.app.write_checked(2006,1,'recipe-'+self.run['id']+'-ready')
            self.app.last_heartbeat_request=None
            self.app.heartbeat.update(enabled=True,state='waiting',enabledAt=time.time(),error='')
            await self.app.follow_heartbeat()
            self.log('RECIPE_START','用户开始本盘；真实PLC反馈推进，相机模式='+self.profile['cameraMode'])
        elif name=='recipe-pause':
            if not self.active():raise ValueError('没有正在运行的配方')
            self.run.update(state='paused',reason='暂停推进；在途动作不会被此按钮停止')
            self.log('RECIPE_PAUSE',self.run['reason'])
        elif name=='recipe-resume':
            if not self.run or self.run['state']!='paused':raise ValueError('本盘未暂停')
            self.run.update(state='running',reason='继续观察在途动作，不重发命令')
        elif name=='recipe-abort':
            if self.active():self.run.update(state='aborted',reason='用户结束本盘；在途信号需现场处理，不自动清零')
            self.jobs=[];self.wait=None;self.log('RECIPE_ABORT','本盘终止推进，未发送PLC停止或复位')
        elif name=='recipe-confirm':
            if not self.wait or self.run['state']!='waiting':raise ValueError('当前没有等待人工确认的步骤')
            if not self.app.fresh():raise ValueError('通信已失效，不能确认并继续')
            kind=self.wait['kind'];outcome=data.get('outcome','OK')
            if kind=='parameters':
                self.wait=None;self.run['state']='running';return
            if kind=='load' and self.app.value(6015)!=1:raise ValueError('PLC就绪MB6015尚未为1')
            if kind=='scan':
                barcode=str(data.get('barcode','')).strip()
                if not barcode:raise ValueError('请填写本盘扫码字符串（模拟也需明确记录）')
                if outcome!='OK':raise ValueError('扫码未匹配配方；本盘不能进入翻面流程')
                self.run['barcode']=barcode
                self.run['frozenProfile']=copy.deepcopy(self.profile)
            if kind in ('pose','poseReview','photo','result') and outcome not in ('OK','NG','Pending','NoMaterial'):
                raise ValueError('结果无效')
            if kind in ('pose','poseReview') and outcome=='NoMaterial':
                self.run.update(state='failed',reason='确认无料，未继续检测或分拣');self.wait=None;self.log('RECIPE_FAILED',self.run['reason']);return
            simulated=self.profile['cameraMode']=='simulated' and kind in ('pose','poseReview','scan','photo','result')
            self.log('SIMULATION' if simulated else 'RECIPE_CONFIRM',('流程测试（模拟）：' if simulated else '人工确认：')+self.wait['message'],outcome=outcome,barcode=self.run['barcode'],recordPurpose='流程测试' if simulated else '现场确认',realInspectionResult=False)
            self.jobs.pop(0);self.wait=None;self.run['state']='running'
            if kind=='photo' and outcome=='NoMaterial':outcome='Pending'
            if kind in ('photo','pose','poseReview'):
                detail={}
                if simulated and kind=='pose':detail=dict(material='P1有料',pose='正常' if outcome=='OK' else outcome,fBarcodePosition=dict(x=65,y=50))
                if simulated and kind=='poseReview':detail=dict(pose='第二面姿态正常' if outcome=='OK' else outcome)
                self.run['inspections'].append(dict(step=self.run['step'],outcome=outcome,simulated=simulated,recordPurpose='流程测试' if simulated else '现场确认',realInspectionResult=False,**detail))
            if kind in ('pose','poseReview') and outcome!='OK':
                for i in range(self.run['step'],12):self.run['steps'][i].update(state='skipped',reason='姿态异常，跳过后续拍照')
                self.run['steps'][self.run['step']-1]['state']='completed'
                self.run.update(result='Pending',step=13);self.prepare()
            if kind=='result':
                if self.run.get('result')=='Pending':outcome='Pending'
                self.run['result']=outcome
                self.log('RECIPE_RESULT','保存流程测试结果（非真实检测结果）：'+outcome,simulated=simulated,recordPurpose='流程测试',realInspectionResult=False,barcode=self.run['barcode'],inspections=self.run['inspections'])
                self.jobs=self.sorting_jobs(outcome)
        else:raise ValueError('未知配方操作')

    def disconnected(self):
        if self.active():
            self.run.update(state='failed',reason='通信断开；本盘不自动恢复、重试或清零')
            self.log('RECIPE_FAILED',self.run['reason'])

    async def tick(self):
        if self.ticking or not self.run or self.run['state'] not in ('running',):return
        self.ticking=True
        try:
            reason=self.app.fault()
            if reason:raise ValueError(reason)
            if self.app.value(6015)!=1 or self.app.value(6016)!=1:
                if self.run['step']==1:
                    self.run['reason']='等待PLC就绪及自动模式，未发送运动启动';return
                raise ValueError('PLC未就绪或不在自动模式（MB6015/MB6016），停止推进')
            if self.app.heartbeat['state'] in ('failed','invalid','no_edge'):
                raise ValueError('心跳应答失败或3秒未观察到PLC心跳变化，停止推进')
            if not self.jobs:
                if self.run.get('gripperCheck'):
                    self.run.update(state='completed',reason='成组夹爪选择验证完成；未执行运动或取放料，不代表成组检测验收通过')
                    self.log('GRIPPER_CHECK_COMPLETE',self.run['reason']);return
                self.run['steps'][self.run['step']-1]['state']='completed'
                self.log('RECIPE_STEP_COMPLETE','本步已完成')
                if self.run['step']==14:
                    self.run.update(state='completed',reason='本轮流程测试完成，请人工取走料盘。模拟结果不作为真实检测结果。')
                    self.log('RECIPE_COMPLETE',self.run['reason']);return
                self.run['step']+=1;self.prepare();return
            job=self.jobs[0];kind=job['type'];operation='recipe-'+self.run['id']+'-'+str(self.run['step'])
            if kind=='loaded':
                tolerance=self.profile.get('positionTolerance',0.1)
                if any(self.app.value(mb) is None or abs(self.app.value(mb))>tolerance for mb in (6064,6076)):
                    raise ValueError('人工上料位置不在(0,0)容差内，未启动后续移动')
                self.jobs.pop(0);self.log('RECIPE_LOADED','启动按钮已确认人工放料，核对XY上料位完成');return
            self.run['reason']=job.get('message') or {'axis':'等待本次轴运动反馈','command':'等待本次PLC取放/翻面反馈','grab':'等待抓手有效选择'}.get(kind,'发送配方参数并读回')
            if kind=='simulation':
                if not job.get('started'):
                    job['started']=time.time();return
                delay=0 if job['kind']=='result' else 5
                elapsed=time.time()-job['started']
                label={'pose':'模拟3D拍照与姿态检测','scan':'模拟F扫码','poseReview':'模拟3D复查','photo':'模拟'+self.profile['steps'][self.run['step']-1].get('camera','')+'拍照与检测','result':'汇总模拟OK'}[job['kind']]
                self.run['reason']=f'流程测试 · {label}：{min(elapsed,delay):.1f}/{delay}秒'
                if elapsed<delay:return
                camera=self.profile['steps'][self.run['step']-1].get('camera')
                field={'pose':'pose','poseReview':'poseReview'}.get(job['kind'],camera)
                result=self.profile.get('simulation',{}).get(field,'OK')
                if job['kind']=='result':
                    outcomes=[x['outcome'] for x in self.run['inspections']]
                    result=self.run.get('result') or ('Pending' if 'Pending' in outcomes else 'NG' if 'NG' in outcomes else 'OK')
                self.wait=copy.deepcopy(job);self.run['state']='waiting'
                await self.command('recipe-confirm',dict(outcome=result,barcode=self.profile.get('simulation',{}).get('barcode','SIM-翻面件-001')))
                return
            if kind=='operator':
                self.wait=copy.deepcopy(job);self.run['state']='waiting';return
            if kind=='xy':
                if 'actionIds' not in job:
                    for axis in ('X','Y'):
                        if self.app.value(self.app.axis_map[axis][2])!=0:raise ValueError(axis+'启动字节未为0，未发送本次XY目标/启动')
                        if self.app.fault(axis):raise ValueError(self.app.fault(axis))
                    # Both REAL targets are verified before either start BYTE is sent.
                    await self.app.poll()
                    tolerance=self.profile.get('positionTolerance',0.1)
                    results={}
                    for axis,mb in (('X',2024),('Y',2028)):
                        if not self.app.position_satisfied(axis,job[axis.lower()],tolerance):
                            results[axis]=await self.app.write_checked(mb,job[axis.lower()],operation)
                    job['actionIds']=[]
                    for axis in ('X','Y'):
                        action=await self.app.begin_axis(axis,job[axis.lower()],operation,target_result=results.get(axis),position_tolerance=tolerance)
                        action.update(requirePositionMatch=True,positionTolerance=self.profile.get('positionTolerance',0.1))
                        job['actionIds'].append(action['actionId'])
                    return
                actions=[a for a in self.app.actions if a['actionId'] in job['actionIds']]
                for action in actions:
                    if action['state'] not in ('completed','observing','pending') or action['state']=='completed' and not action['cleared']:
                        raise ValueError(action['axis']+'动作未完成：'+action.get('reason','清零失败'))
                if len(actions)==2 and all(a['state']=='completed' and a['cleared'] for a in actions):
                    await self.app.poll() # Fresh snapshot after start-clear readback.
                    tolerance=self.profile.get('positionTolerance',0.1)
                    if not all(self.app.position_satisfied(axis,job[axis.lower()],tolerance) for axis in ('X','Y')):
                        raise ValueError('XY最终位置复核失败，未进入下一步')
                    self.run['trail'].append(dict(x=self.app.value(6064),y=self.app.value(6076),step=self.run['step'],at=time.time()))
                    self.jobs.pop(0)
                return
            if kind=='reuse':
                if 'x' in job:
                    if any(self.app.value(mb)!=1 for mb in (6040,6042)) or any(self.app.value(mb)!=0 for mb in (2001,2002)):
                        job.setdefault('started',time.time())
                        if time.time()-job['started']>self.app.cfg['actionTimeout']:raise ValueError('原槽定位复核超时，未继续放料')
                        return
                    tolerance=self.profile.get('positionTolerance',0.1)
                    if any(self.app.value(mb) is None or abs(self.app.value(mb)-job[key])>tolerance for mb,key in ((6064,'x'),(6076,'y'))):
                        raise ValueError('沿用XY实际位置超出配方容差，停止推进')
                self.log('RECIPE_REUSE',job['message']);self.jobs.pop(0);return
            if kind=='axis':
                axis=job['axis'];value=job['value']
                if 'actionId' not in job:
                    tmb,smb,fmb,amb=self.app.axis_map[axis][1:]
                    tolerance=self.profile.get('positionTolerance',0.1)
                    action=await self.app.begin_axis(axis,value,operation,position_tolerance=tolerance)
                    action.update(requirePositionMatch=True,positionTolerance=tolerance)
                    job['actionId']=action['actionId'];return
                action=next(a for a in self.app.actions if a['actionId']==job['actionId'])
                if action['state']=='completed' and action['cleared']:
                    await self.app.poll()
                    if not self.app.position_satisfied(axis,value,self.profile.get('positionTolerance',0.1)):
                        raise ValueError(axis+'最终位置复核失败，未进入下一步')
                    self.last_targets[axis]=value
                    if axis in ('X','Y'):
                        self.run['trail'].append(dict(x=self.app.value(6064),y=self.app.value(6076),step=self.run['step'],at=time.time()))
                    self.jobs.pop(0)
                elif action['state'] not in ('observing','pending'):
                    raise ValueError(axis+'动作未完成：'+action.get('reason','未知状态'))
                return
            if kind=='model':
                if self.profile['modelCode'] is None:
                    self.wait=self.checkpoint('parameters','请在配方参数填写现场确认的型号REAL原码，再点继续；不使用ASCII转换');self.run['state']='waiting';return
                await self.app.write_checked(2048,self.profile['modelCode'],operation);self.jobs.pop(0);return
            if kind=='grab':
                grab=job.get('grabId',self.profile['grabId'])
                if grab is None:
                    self.wait=self.checkpoint('parameters','请在配方参数填写现场抓手编号1/2，再点继续');self.run['state']='waiting';return
                if self.app.value(6062)==grab:
                    self.jobs.pop(0)
                    self.log('RECIPE_GRAB','PLC有效抓手已确认' if job.get('started') else 'PLC有效抓手选择已一致，沿用',
                             memberId=job.get('memberId'),material=job.get('material'),grabId=grab,
                             actual=self.app.value(6062),reused=not bool(job.get('started')))
                    return
                if not job.get('started'):
                    self.log('RECIPE_GRAB_SELECT','按本件要求选择夹爪',memberId=job.get('memberId'),
                             material=job.get('material'),grabId=grab,actual=self.app.value(6062))
                    await self.app.write_checked(2018,grab,operation);job['started']=time.time()
                elif time.time()-job['started']>self.app.cfg['actionTimeout']:raise ValueError('抓手有效选择反馈超时')
                return
            if kind=='write':
                await self.app.write_checked(job['mb'],job['value'],operation);self.jobs.pop(0);return
            if kind=='idle':
                if self.app.value(job['mb'])==0:self.jobs.pop(0);return
                job.setdefault('started',time.time())
                if time.time()-job['started']>self.app.cfg['actionTimeout']:raise ValueError('分拣清零后反馈未回空闲0，停止推进')
                return
            if kind=='command':
                if not job.get('started'):
                    current=self.app.value(job['mb'])
                    allowed=(0,1) if job['mb'] in (2014,2016) and job['value']==2 else (0,)
                    if current not in allowed:raise ValueError('指令MB'+str(job['mb'])+'未处于允许状态，请现场处理；未自动清零')
                    baseline=self.app.value(job['feedback'])
                    expected=(1,) if job['mb']==2016 and job['value']==2 else (0,)
                    if baseline not in expected:raise ValueError('反馈MB'+str(job['feedback'])+'不是本次所需前态，不能把旧完成作为本次完成')
                    await self.app.write_checked(job['mb'],job['value'],operation)
                    job.update(started=time.time(),seenBusy=job['busy']==0,previous=baseline);return
                feedback=self.app.value(job['feedback'])
                if feedback==3 or feedback is None:raise ValueError('PLC翻面/取放反馈失败或无效：MB'+str(job['feedback']))
                if feedback==job['busy']:job['seenBusy']=True
                if feedback==job['done'] and job['seenBusy'] and job['previous']!=job['done']:
                    self.log('RECIPE_PLC_COMPLETE','PLC反馈本次翻面/取放完成',mb=job['feedback'],before=job['previous'],value=feedback)
                    if job['feedback'] in (6050,6052):
                        for axis in ('ZCamera','ZScan','ZGrab'):self.last_targets.pop(axis,None)
                    self.jobs.pop(0);return
                job['previous']=feedback
                if time.time()-job['started']>self.app.cfg['actionTimeout']:raise ValueError('PLC本次翻面/取放反馈超时；未自动清零')
        except Exception as error:
            self.run.update(state='failed',reason=str(error));self.wait=None
            self.log('RECIPE_FAILED','配方停在本步：'+str(error))
        finally:self.ticking=False
