"""Independent PLC commissioning app. All device I/O runs in one async queue."""
import argparse
import asyncio
import copy
import csv
import hashlib
import io
import json
import math
import os
from pathlib import Path
import secrets
import sys
import threading
import time
import uuid
import webbrowser
import zipfile
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse
from recipe import Recipe
from transport import Transport, ModbusError, encode, decode, pdu_address, real_interpretations, real_order

VERSION = '1.1.6'
ROOT = Path(sys.executable).resolve().parent if getattr(sys, 'frozen', False) else Path(__file__).resolve().parent.parent
AXES = {
    'X': ('X轴', 2024, 2001, 6040, 6064),
    'Y': ('Y轴', 2028, 2002, 6042, 6076),
    'ZCamera': ('检测Z', 2032, 2003, 6044, 6084),
    'ZScan': ('扫码Z', 2036, 2004, 6046, 6088),
    'ZGrab': ('抓取Z', 2040, 2005, 6048, 6092),
    'R': ('旋转R', 2044, 2000, 6060, 6068),
}
AXIS_ALARMS = {'X':6028,'Y':6029,'ZCamera':6030,'ZScan':6031,'ZGrab':6032,'R':6033}


def json_bytes(data):
    return json.dumps(data, ensure_ascii=False, allow_nan=False, indent=2).encode('utf-8')


def timestamp():
    return time.strftime('%Y-%m-%dT%H:%M:%S%z')


def error_info(error):
    code=getattr(error,'code',None) or getattr(error,'winerror',None) or getattr(error,'errno',None)
    if isinstance(error,asyncio.TimeoutError):return 'TCP连接或操作超时（TIMEOUT）','TIMEOUT'
    if isinstance(error,OSError):return 'TCP连接失败：'+str(error),code or type(error).__name__
    return str(error),code or type(error).__name__


def validate_config(cfg):
    if not isinstance(cfg.get('host'), str) or not cfg['host'].strip() or len(cfg['host']) > 253:
        raise ValueError('请填写 PLC IP 或主机名')
    cfg['host'] = cfg['host'].strip()
    for key, low, high in [('port',1,65535),('unitId',0,255),('pcBase',0,65508),('plcBase',0,65488),('pollMs',100,10000)]:
        if type(cfg.get(key)) is not int or not low <= cfg[key] <= high:
            raise ValueError(f'{key} 必须为 {low}..{high} 的整数')
    if cfg.get('feedbackFc') not in (3,4) or cfg.get('boolOrder') not in ('EvenLow','EvenHigh') or cfg.get('realOrder') not in ('Abcd','Badc','Cdab','Dcba') or cfg.get('byteWrite') not in ('RMW','FC22'):
        raise ValueError('功能码、字节顺序或写入模式无效')
    for key in ('realWriteOrder','realReadOrder'):
        if key in cfg and cfg[key] not in ('Abcd','Badc','Cdab','Dcba'):
            raise ValueError('REAL发送或反馈字序无效')
    for key in ('timeout','actionTimeout'):
        if type(cfg.get(key)) not in (float,int) or not math.isfinite(cfg[key]) or not 0.1 <= cfg[key] <= 3600:
            raise ValueError(f'{key} 必须为 0.1..3600 秒')
    for axis in AXES:
        a = cfg['axes'][axis]
        for key in ('minimum','maximum','tolerance','safeHeight'):
            value = a.get(key)
            if value is not None and (type(value) not in (int,float) or not math.isfinite(value)):
                raise ValueError('轴参数必须为空或有效数字')
        if a.get('tolerance') is not None and a['tolerance'] < 0:
            raise ValueError('容差不能为负数')
        if a.get('minimum') is not None and a.get('maximum') is not None and a['minimum'] > a['maximum']:
            raise ValueError('轴最小值不能大于最大值')
    return cfg


class App:
    def __init__(self, root=ROOT):
        self.root = Path(root)
        self.protocol = json.loads((self.root/'sources/protocol.json').read_text(encoding='utf-8'))
        self.points = self.protocol['points']
        self.by_id = {p['id']: p for p in self.points}
        self.by_mb = {p['mb']: p for p in self.points}
        for source in self.protocol['profile']['sourceFiles']:
            if hashlib.sha256((self.root/'sources'/source['file']).read_bytes()).hexdigest() != source['sha256']:
                raise ValueError('源表与协议快照SHA256不一致：'+source['file'])
        path = self.root/'config/local.json'
        self.cfg = validate_config(json.loads((path if path.exists() else self.root/'config/default.json').read_text(encoding='utf-8')))
        self.session = time.strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:6]
        self.logdir = self.root/'logs'/self.session
        self.logdir.mkdir(parents=True)
        (self.logdir/'config.snapshot.json').write_bytes(json_bytes(self.cfg))
        (self.logdir/'protocol.snapshot.json').write_bytes(json_bytes(self.protocol))
        self.lock = asyncio.Lock()
        self.data_lock = threading.RLock()
        self.events, self.values, self.sent, self.actions = [], {}, {}, []
        self.signal_timeline = []
        self.transport = None
        self.status, self.error, self.error_code = 'disconnected', '', None
        self.last_poll = self.poll_ms = None
        self.timeout_total = 0
        self.heartbeat = dict(enabled=False, changes=0, responses=0, timeouts=0, lastRequest=None,
                              lastResponse=None, lastEdge=None, lastAttempt=None, state='off', error='')
        self.running = True
        self.last_heartbeat_request = None
        self.last_hb_observed = None
        self.axis_map = AXES
        self.recipe = Recipe(self)
        self.event('SESSION', '联调工作台已启动，默认只读', version=VERSION, logPath=str(self.logdir))

    def event(self, kind, title, **fields):
        with self.data_lock:
            record = dict(id=uuid.uuid4().hex[:10], at=timestamp(), epoch=time.time(), kind=kind, title=title, **fields)
            recipe=getattr(self,'recipe',None)
            if recipe and recipe.active():
                record.setdefault('recipeRunId',recipe.run['id'])
                record.setdefault('recipeStep',recipe.run['step'])
                if kind=='SIGNAL' and fields.get('mb') not in (2011,6038,6064,6068,6072,6076,6084,6088,6092,6080):
                    recipe.run.setdefault('signals',[]).append(copy.deepcopy(record))
                    recipe.run['signals']=recipe.run['signals'][-1000:]
            self.events.append(record)
            if kind in ('SIGNAL','READBACK') and fields.get('mb') not in (2011,6038,6064,6068,6072,6076,6084,6088,6092,6080):
                entry=copy.deepcopy(record)
                if kind=='READBACK':entry['value']=entry.get('readback')
                self.signal_timeline.append(entry)
                self.signal_timeline=self.signal_timeline[-1000:]
            self.events = self.events[-1500:]
            with (self.logdir/'events.jsonl').open('ab') as file:
                file.write(json.dumps(record, ensure_ascii=False, allow_nan=False).encode('utf-8')+b'\n')
            return record

    def fresh(self):
        return self.transport is not None and self.transport.writer is not None and self.last_poll is not None and time.time()-self.last_poll < max(3, self.cfg['pollMs']/1000*3)

    def value(self, mb):
        point = self.by_mb[mb]
        value = self.values.get(point['id'], {})
        return value.get('value') if self.fresh() and value.get('quality') == 'Good' else None

    def snapshot(self):
        with self.data_lock:
            tr = self.transport
            points = [dict(p, pdu=pdu_address(p,self.cfg)) for p in self.points]
            axes = {a:dict(label=v[0], targetId=self.by_mb[v[1]]['id'],startId=self.by_mb[v[2]]['id'],
                           feedbackId=self.by_mb[v[3]]['id'],actualId=self.by_mb[v[4]]['id'],alarmId=self.by_mb[AXIS_ALARMS[a]]['id'],config=self.cfg['axes'][a]) for a,v in AXES.items()}
            return copy.deepcopy(dict(app='Gaode Independent PLC Commissioning',version=VERSION,processId=os.getpid(),
                recipe=self.recipe.snapshot(),session=self.session,rootPath=str(self.root),config=self.cfg,points=points,axes=axes,values=self.values,sent=self.sent,
                actions=self.actions,events=self.events,signalTimeline=self.signal_timeline,heartbeat=self.heartbeat,status=self.status,error=self.error,
                localEndpoint=tr.local_endpoint if tr and tr.writer else None,remoteEndpoint=tr.remote_endpoint if tr and tr.writer else None,
                errorCode=self.error_code,tcpConnected=bool(tr and tr.writer),fresh=self.fresh(),lastPoll=self.last_poll,
                lastModbusResponse=tr.last_response if tr else None,pollMs=self.poll_ms,timeoutCount=self.timeout_total+(tr.timeouts if tr else 0),
                interpretationValid=bool(self.values) and all(v['quality']=='Good' for v in self.values.values()),
                logPath=str(self.logdir),configPath=str(self.root/'config/local.json'),protocolHash=self.protocol['profile']['sha256']))

    def context(self, operation, action=None):
        if self.transport:
            self.transport.context = dict(operationId=operation, actionId=action or operation)

    async def disconnect(self):
        self.recipe.disconnected()
        if self.transport:
            self.timeout_total += self.transport.timeouts
            await self.transport.close()
            self.transport = None
        self.heartbeat.update(enabled=False,state='off')
        for action in self.actions:
            if action['state'] in ('observing','pending'):
                action.update(state='interrupted',plcCompleted=False,reason='通信中断，动作结果未知；启动信号需现场核对')
                self.event('ACTION_INTERRUPTED','通信中断，本次动作结果未知',operationId=action['operationId'],actionId=action['actionId'])
        self.status = 'disconnected'

    async def poll(self):
        started = time.monotonic()
        self.context('poll-'+uuid.uuid4().hex[:8])
        updates = []
        for direction, count in [('PC->PLC',28),('PLC->PC',48)]:
            base = self.cfg['pcBase' if direction=='PC->PLC' else 'plcBase']
            fc = 3 if direction=='PC->PLC' else self.cfg['feedbackFc']
            registers = await self.transport.read(base,count,fc)
            for p in self.points:
                if p['direction'] != direction:
                    continue
                address = pdu_address(p,self.cfg)
                raw = registers[address-base:address-base+(2 if p['type']=='REAL' else 1)]
                value, quality = decode(p,raw,self.cfg)
                updates.append((p,dict(value=value,quality=quality,registers=raw,
                    rawHex=' '.join(f'{r:04X}' for r in raw),realAlternatives=real_interpretations(raw) if p['type']=='REAL' else None,
                    receivedAt=timestamp(),receivedEpoch=time.time())))
        with self.data_lock:
            for point,item in updates:
                old = self.values.get(point['id'])
                changed = old is not None and (old['value']!=item['value'] or old['quality']!=item['quality'])
                item['changedAt'] = time.time() if changed else (old or {}).get('changedAt')
                item['changes'] = (old or {}).get('changes',0)+int(changed)
                if changed:
                    related=[a for a in self.actions if a['state'] in ('observing','pending') and point['mb'] in AXES[a['axis']][1:]]
                    link=dict(operationId=related[0]['operationId'],actionId=related[0]['actionId']) if len(related)==1 else {}
                    self.event('SIGNAL','信号变化：'+point['name'],**link,idSignal=point['id'],mb=point['mb'],before=old['value'],value=item['value'],rawRegisters=item['registers'])
                self.values[point['id']] = item
            self.last_poll = time.time()
            self.poll_ms = round((time.monotonic()-started)*1000,2)
            self.status, self.error, self.error_code = 'polling','',None
        request = self.value(6038)
        if request in (0,1):
            if self.last_hb_observed is not None and request != self.last_hb_observed:
                self.heartbeat['changes'] += 1
                self.heartbeat['lastEdge'] = time.time()
                self.event('HEARTBEAT_EDGE','PLC 心跳字节翻转',mb=6038,value=request)
            self.last_hb_observed = request
            self.heartbeat['lastRequest'] = request
        await self.follow_heartbeat()
        await self.observe_actions()
        await self.recipe.tick()

    async def write_checked(self, mb, value, operation, action=None):
        before=self.value(mb)
        p = self.by_mb[mb]
        encode(p,value,self.cfg)
        if not self.fresh():
            raise ValueError('当前没有有效通信，请连接并获得新鲜应答')
        self.context(operation,action)
        self.transport.context.update(signalId=p['id'],signalName=p['name'],mb=mb,dataType=p['type'],
            targetValue=value,byteInterpretation=f"BOOL {self.cfg['boolOrder']} / INT signed16 / REAL {real_order(p,self.cfg)}")
        self.event('WRITE_ACCEPTED','受理发送：'+p['name'],operationId=operation,actionId=action or operation,mb=mb,value=value)
        result = dict(value=value,at=timestamp(),operationId=operation,writeResponded=False,readbackMatched=None,readback=None)
        self.sent[p['id']] = result
        try:
            raw = await self.transport.write(p,value)
            result['writeResponded'] = True
            self.event('WRITE_RESPONSE','PLC 已应答写请求',operationId=operation,actionId=action or operation,mb=mb,value=value,rawRegisters=raw)
            registers = await self.transport.read(pdu_address(p,self.cfg),2 if p['type']=='REAL' else 1,3)
            actual,quality = decode(p,registers,self.cfg)
            expected = decode(p,encode(p,value,self.cfg),self.cfg)[0] if p['type']!='BOOL' else value
            result.update(readback=actual,readbackMatched=quality=='Good' and actual==expected,rawRegisters=registers)
            self.event('READBACK','写入读回相符' if result['readbackMatched'] else '写入读回不符',operationId=operation,actionId=action or operation,
                mb=mb,before=before,sent=value,readback=actual,matched=result['readbackMatched'],rawRegisters=registers)
            if not result['readbackMatched']:
                raise ValueError('PLC 已应答写请求，但读回与发送值不符；未自动重发')
            return copy.deepcopy(result)
        except Exception as error:
            result['error'] = str(error)
            if self.transport and not self.transport.writer:
                self.error,self.error_code=error_info(error)
                self.status='error'
                self.heartbeat.update(enabled=False,state='failed',error=self.error)
            self.event('WRITE_FAILED','写入或读回失败；不自动重发',operationId=operation,actionId=action or operation,mb=mb,
                writeResponded=result['writeResponded'],error=str(error),errorCode=getattr(error,'code',None))
            raise

    async def follow_heartbeat(self):
        hb = self.heartbeat
        if not hb['enabled']:
            return
        request = self.value(6038)
        if request not in (0,1):
            hb.update(state='invalid',error='PLC心跳不是有效0/1字节，未发送')
            return
        if request == self.last_heartbeat_request:
            if hb['lastResponse'] is not None and self.value(2011) != hb['lastResponse']:
                hb.update(enabled=False,state='failed',error='应答被其他来源改变，心跳停用')
                self.event('HEARTBEAT_FAILED',hb['error'])
                return
            edge = hb['lastEdge'] or hb['enabledAt']
            if time.time()-edge>3 and hb['state']!='no_edge':
                hb['timeouts']+=1
                hb['state']='no_edge'
                self.event('HEARTBEAT_TIMEOUT','3秒未观察到PLC心跳变化；不能判握手通过')
            return
        try:
            hb['lastAttempt']=time.time()
            result = await self.write_checked(2011,request,'heartbeat-'+uuid.uuid4().hex[:8])
            self.last_heartbeat_request=request
            hb.update(lastResponse=result['readback'],responses=hb['responses']+1,state='following',error='')
        except Exception as error:
            hb.update(enabled=False,state='failed',error=str(error))

    def fault(self,axis=None):
        stop,fault = self.value(6020),self.value(6035)
        if stop == 1:
            return '物理急停触发（MB6020=1）'
        if fault == 1:
            return 'PLC 系统故障（MB6035=1）'
        if stop is None or fault is None:
            return '急停/故障字节无有效解释，请核对当前编码及现场设备'
        if axis in AXIS_ALARMS:
            alarm=self.value(AXIS_ALARMS[axis])
            if alarm==1:return f"本轴报警（MB{AXIS_ALARMS[axis]}=1）"
            if alarm is None:return f"本轴报警字节MB{AXIS_ALARMS[axis]}解释无效，请核对现场状态"
        return None

    def position_satisfied(self, axis, target, tolerance):
        _, _, start, feedback, actual = AXES[axis]
        position = self.value(actual)
        return (self.value(start) == 0 and self.value(feedback) == 1 and
                position is not None and math.isfinite(position) and abs(position-target) <= tolerance)

    async def begin_axis(self, axis, target, operation, target_result=None, position_tolerance=None):
        if axis not in AXES:
            raise ValueError('未知轴')
        if any(a['axis']==axis and a['state'] in ('observing','pending') for a in self.actions):
            raise ValueError('该轴已有本次动作正在观察，请清零或等待结束')
        await self.poll() # Fresh reads from this connection before deciding to reuse a position.
        label,tmb,smb,fmb,amb = AXES[axis]
        tolerance = position_tolerance if position_tolerance is not None else self.cfg['axes'][axis].get('tolerance')
        reuse_tolerance = 0 if tolerance is None else tolerance
        if not math.isfinite(reuse_tolerance) or reuse_tolerance < 0:
            raise ValueError('位置复用容差无效')
        encode(self.by_mb[tmb],target,self.cfg)
        setting=self.cfg['axes'][axis]
        if setting.get('minimum') is not None and target<setting['minimum'] or setting.get('maximum') is not None and target>setting['maximum']:
            raise ValueError('目标超出已配置轴范围')
        reason=self.fault(axis)
        if reason:
            raise ValueError(reason)
        if self.value(smb) != 0:
            raise ValueError('启动字节未为0或无有效解释；请先清零并核对现场状态')
        if operation.startswith('recipe-') and (self.value(6015)!=1 or self.value(6016)!=1):
            raise ValueError('PLC未就绪或不在自动模式，未执行定位')
        action=dict(operationId=operation,actionId=uuid.uuid4().hex[:10],axis=axis,label=label,target=target,
            state='accepted',startedAt=timestamp(),startEpoch=time.time(),baselineFeedback=self.value(fmb),baselineActual=self.value(amb),
            writeResponded=False,readbackMatched=False,startWriteResponded=None,startReadbackMatched=None,plcCompleted=None,motionEvidence=False,busySeen=False,previousFeedback=None,completionEvidence=None,cleared=False,coordinateAcceptance=None)
        self.actions.append(action)
        self.actions=self.actions[-100:]
        action.update(requirePositionMatch=position_tolerance is not None, positionTolerance=tolerance,
                      reusedPosition=False, startDispatched=False, clearRequired=True)
        self.event('ACTION_ACCEPTED','受理单轴动作：'+label,operationId=operation,actionId=action['actionId'],target=target)
        if self.position_satisfied(axis, target, reuse_tolerance):
            if target_result is not None:
                action.update(writeResponded=target_result['writeResponded'],readbackMatched=target_result['readbackMatched'],encodedTarget=target_result['readback'])
            action.update(state='completed', reusedPosition=True, clearRequired=False, cleared=True,
                actual=self.value(amb), delta=self.value(amb)-target, feedback=1, coordinateAcceptance=True,
                completionEvidence='fresh arrived=1, start=0 and actual within tolerance; position reused without dispatch',
                reason='当前位置已满足目标，沿用位置；未发送启动，无需清零')
            self.event('POSITION_REUSED', action['reason'], operationId=operation, actionId=action['actionId'],
                       axis=axis, actual=action['actual'], target=target, tolerance=reuse_tolerance)
            return action
        try:
            result=target_result if target_result is not None else await self.write_checked(tmb,target,operation,action['actionId'])
            action.update(writeResponded=result['writeResponded'],readbackMatched=result['readbackMatched'],encodedTarget=result['readback'])
            # Recheck the real fault immediately before start, through the same queue.
            await self.poll()
            if self.fault(axis):
                raise ValueError(self.fault(axis))
            if operation.startswith('recipe-') and (self.value(6015)!=1 or self.value(6016)!=1):
                raise ValueError('启动前PLC就绪/自动模式失效，未发送启动')
            if self.value(smb)!=0:
                raise ValueError('目标写入后启动字节已改变，未再次发送启动')
            action.update(previousFeedback=self.value(fmb),startBaselineFeedback=self.value(fmb),startBaselineActual=self.value(amb))
            action['startDispatched']=True
            start_result=await self.write_checked(smb,1,operation,action['actionId'])
            action.update(startWriteResponded=start_result['writeResponded'],startReadbackMatched=start_result['readbackMatched'])
            action.update(state='observing',startEpoch=time.time(),reason='启动已读回，等待本次运动证据')
            self.event('START','启动 BYTE=1 已读回；观察本次运动',operationId=operation,actionId=action['actionId'],mb=smb)
            await self.poll()
            return action
        except Exception as error:
            sent=self.sent.get(self.by_mb[smb]['id'])
            if sent and sent['operationId']==operation:
                action.update(startWriteResponded=sent['writeResponded'],startReadbackMatched=sent['readbackMatched'])
            action.update(state='failed',reason=str(error),plcCompleted=False)
            self.event('ACTION_FAILED','动作未完成',operationId=operation,actionId=action['actionId'],error=str(error))
            raise

    async def observe_actions(self):
        for action in self.actions:
            if action['state'] not in ('observing','pending'):
                continue
            axis=action['axis'];label,tmb,smb,fmb,amb=AXES[axis]
            feedback,actual=self.value(fmb),self.value(amb)
            action.update(feedback=feedback,actual=actual,delta=(actual-action.get('encodedTarget',action['target'])) if actual is not None else None)
            if self.fault(axis):
                action.update(state='failed',plcCompleted=False,reason=self.fault(axis)+'；启动信号需现场核对/清零')
                self.event('ACTION_FAILED',action['reason'],operationId=action['operationId'],actionId=action['actionId'])
                continue
            if feedback not in (0,1,2):
                action.update(state='failed',plcCompleted=False,reason='本次到位反馈无效；未确认完成，启动信号需现场核对/清零')
                self.event('ACTION_FAILED',action['reason'],operationId=action['operationId'],actionId=action['actionId'],feedback=feedback)
                continue
            previous=action.get('previousFeedback')
            action['previousFeedback']=feedback
            action['positionChanged']=actual is not None and action['baselineActual'] is not None and actual!=action['baselineActual']
            if feedback==0 and previous==1 and not action['busySeen']:
                action.update(busySeen=True,motionEvidence=True,busyObservedAt=timestamp(),completionEvidence='post-start PLC feedback 1->0')
                self.event('MOTION_EVIDENCE','启动后观察到本轴PLC反馈1→0（运动中）；坐标变化不作完成依据',operationId=action['operationId'],actionId=action['actionId'],feedback=feedback,previousFeedback=previous,actual=actual)
            status_position_ok=(action.get('requirePositionMatch') is True and action.get('startWriteResponded') is True and action.get('startReadbackMatched') is True and action.get('writeResponded') is True and action.get('readbackMatched') is True and action.get('delta') is not None and abs(action['delta'])<=action.get('positionTolerance',0.1))
            if feedback==1 and (action['busySeen'] or status_position_ok):
                action['completionEvidence']='post-start PLC feedback 1->0->1' if action['busySeen'] else 'post-start arrived=1 and actual within recipe tolerance; motion transition not observed'

                tolerance=action.get('positionTolerance',self.cfg['axes'][axis].get('tolerance'))
                acceptance=abs(action['delta'])<=tolerance if tolerance is not None and action['delta'] is not None else None
                if action.get('requirePositionMatch') and acceptance is not True:
                    action.update(state='pending',coordinateAcceptance=acceptance,reason='PLC本次到位，但实际位置未落入配方容差；未清零、未继续')
                    if time.time()-action['startEpoch']>self.cfg['actionTimeout']:
                        action.update(state='timeout',plcCompleted=False,reason='实际位置匹配超时；启动未自动清零')
                        self.event('ACTION_TIMEOUT',action['reason'],operationId=action['operationId'],actionId=action['actionId'],actual=actual,delta=action['delta'])
                    continue
                action.update(state='completed',plcCompleted=True,coordinateAcceptance=acceptance,reason='PLC到位=1且实际位置符合配方容差；未要求运动中跳变，机械执行由现场观察确认' if not action['busySeen'] else 'PLC 反馈本次到位；机械执行由现场观察确认')
                self.event('ACTION_COMPLETE','PLC到位且坐标匹配' if not action['busySeen'] else 'PLC 反馈本次动作完成',operationId=action['operationId'],actionId=action['actionId'],actual=actual,delta=action['delta'],coordinateAcceptance=acceptance)
                try:
                    await self.write_checked(smb,0,action['operationId'],action['actionId'])
                    action['cleared']=True
                    self.event('CLEAR','到位后启动 BYTE=0 已读回',operationId=action['operationId'],actionId=action['actionId'],mb=smb)
                except Exception as error:
                    action['clearError']=str(error)
                    self.event('CLEAR_FAILED','本次到位，但启动清零失败',operationId=action['operationId'],actionId=action['actionId'],error=str(error))
            elif feedback==2 or time.time()-action['startEpoch']>self.cfg['actionTimeout']:
                action.update(state='timeout',plcCompleted=False,reason='PLC 超时反馈；未自动清零，请核对设备' if feedback==2 else ('观察超时，未同时满足PLC到位=1和配方坐标容差；启动未自动清零，请现场核对并手动清零' if action.get('requirePositionMatch') else '观察超时，未确认本次PLC运动中→到位；启动未自动清零，请现场核对并手动清零'))
                self.event('ACTION_TIMEOUT',action['reason'],operationId=action['operationId'],actionId=action['actionId'],feedback=feedback)
            else:
                action.update(state='pending' if feedback==1 and not action['motionEvidence'] else 'observing',
                    reason=('等待PLC到位=1且实际位置进入配方容差' if action.get('requirePositionMatch') else '未观察启动后本轴PLC反馈1→0→1；旧到位及坐标波动不作完成依据，待确认') if not action['busySeen'] else '已观察本次运动中=0，等待到位=1')

    async def command(self, name, data):
        async with self.lock:
            operation=uuid.uuid4().hex[:12]
            if name in ('write','axis','clear') and self.recipe.active():
                raise ValueError('配方正在使用动作队列，请先暂停并结束本盘再进行手工写入；监视仍可用')
            if name.startswith('recipe-'):
                await self.recipe.command(name,data)
                return dict(operationId=operation,message='配方操作已记录')
            if name=='connect':
                await self.disconnect()
                self.last_hb_observed=None
                self.last_heartbeat_request=None
                cfg=copy.deepcopy(self.cfg)
                cfg.update(host=data.get('host',cfg['host']),port=data.get('port',cfg['port']))
                self.cfg=validate_config(cfg)
                self.save_config()
                self.transport=Transport(self.cfg,self.event)
                self.last_poll=None
                self.status='connecting'
                self.context(operation)
                try:
                    await self.transport.connect()
                    self.event('TCP_CONNECTED','TCP 已连通；尚不代表数据解释或动作成功',operationId=operation,endpoint=f"{self.cfg['host']}:{self.cfg['port']}")
                    await self.poll()
                except Exception as error:
                    self.error,self.error_code=error_info(error)
                    self.status='error'
                    self.event('CONNECT_ERROR','连接/只读轮询失败',operationId=operation,error=self.error,errorCode=self.error_code)
                    raise
            elif name=='disconnect':
                await self.disconnect()
                self.event('DISCONNECT','用户断开，保留最后一次数据但标记过期',operationId=operation)
            elif name=='settings':
                if self.transport:
                    await self.disconnect()
                cfg=copy.deepcopy(self.cfg);cfg.update(data)
                self.cfg=validate_config(cfg)
                self.save_config()
                self.event('CONFIG','解释配置已保存，请重新连接；未声明现场校准',operationId=operation,config=self.cfg)
            elif name=='heartbeat':
                if type(data.get('enabled')) is not bool:
                    raise ValueError('enabled 需要布尔值')
                if data['enabled'] and not self.fresh():
                    raise ValueError('请先连接并获得有效应答')
                self.last_heartbeat_request=None
                self.heartbeat.update(enabled=data['enabled'],state='waiting' if data['enabled'] else 'off',enabledAt=time.time(),error='')
                self.event('HEARTBEAT_SWITCH','用户启用心跳' if data['enabled'] else '用户停用心跳',operationId=operation)
                if data['enabled']:
                    await self.follow_heartbeat()
            elif name=='write':
                p=self.by_id.get(data.get('id'))
                if not p or p['direction']!='PC->PLC':
                    raise ValueError('只能发送PC方向字段')
                if p['mb']==2011:
                    self.heartbeat.update(enabled=False,state='off')
                return dict(operationId=operation,result=await self.write_checked(p['mb'],data.get('value'),operation))
            elif name=='axis':
                return dict(operationId=operation,action=await self.begin_axis(data.get('axis'),data.get('target'),operation))
            elif name=='clear':
                axis=data.get('axis')
                if axis not in AXES:
                    raise ValueError('未知轴')
                result=await self.write_checked(AXES[axis][2],0,operation)
                for action in self.actions:
                    if action['axis']==axis and action['state'] in ('pending','observing','timeout','failed'):
                        action.update(state='cancelled',cleared=True,reason='用户清零；未宣布本次动作完成')
                self.event('CLEAR','用户清零启动信号',operationId=operation,axis=axis)
                return dict(operationId=operation,result=result)
            elif name=='shutdown':
                await self.disconnect()
                self.running=False
                self.event('SHUTDOWN','用户停止本独立包服务',operationId=operation)
            elif name=='note':
                text=str(data.get('text','')).strip()
                if not text or len(text)>4000:
                    raise ValueError('备注请填写1..4000字')
                self.event('NOTE','操作员现场观察',operationId=data.get('operationId') or operation,text=text)
            else:
                raise ValueError('未知操作')
            return dict(operationId=operation,message='操作已受理，查看各阶段记录')

    def save_config(self):
        temporary=self.root/'config/local.tmp'
        temporary.write_bytes(json_bytes(self.cfg))
        temporary.replace(self.root/'config/local.json')
        (self.logdir/'config.snapshot.json').write_bytes(json_bytes(self.cfg))

    async def worker(self):
        while self.running:
            async with self.lock:
                if self.transport and self.transport.writer and self.status in ('polling','connecting'):
                    try:
                        await self.poll()
                    except Exception as error:
                        self.recipe.disconnected()
                        self.error,self.error_code=error_info(error)
                        self.event('POLL_ERROR','轮询失败，数据过期',error=self.error,errorCode=self.error_code)
                        await self.disconnect()
                        self.status='error'
            moving=any(a['state'] in ('observing','pending') for a in self.actions)
            await asyncio.sleep(min(self.cfg['pollMs'],50)/1000 if moving else self.cfg['pollMs']/1000)
        async with self.lock:
            await self.disconnect()

    def export(self):
        snapshot=self.snapshot()
        files={'snapshot.json':json_bytes(snapshot),'未测事项.txt':('真实PLC地址解释/字节顺序尚待现场校准；真实设备动作由操作员验证。\n'
            '模拟器测试不等于现场通过。机械执行、坐标误差（无容差）、自动翻面取放链的未知参数均不能凭软件读回宣称通过。\n').encode('utf-8')}
        for folder in ('sources','config','specs','logs/'+self.session):
            for path in (self.root/folder).rglob('*'):
                if path.is_file():
                    files[path.relative_to(self.root).as_posix()]=path.read_bytes()
        table=io.StringIO();writer=csv.writer(table)
        writer.writerow(['方向','信号','MB','PDU','类型','原值','原寄存器','质量','接收时间'])
        for p in snapshot['points']:
            v=snapshot['values'].get(p['id'],{})
            writer.writerow([p['direction'],p['name'],p['address'],p['pdu'],p['type'],v.get('value'),v.get('rawHex'),v.get('quality'),v.get('receivedAt')])
        files['信号快照.csv']=table.getvalue().encode('utf-8-sig')
        files['动作记录.json']=json_bytes(snapshot['actions'])
        files['manifest.sha256.json']=json_bytes({name:hashlib.sha256(b).hexdigest() for name,b in files.items()})
        output=io.BytesIO()
        with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as archive:
            for name,data in files.items():archive.writestr(name,data)
        return output.getvalue()


class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass

    def reply(self,status,data,mime='application/json; charset=utf-8',download=None):
        body=data if isinstance(data,bytes) else json_bytes(data)
        self.send_response(status)
        self.send_header('Content-Type',mime)
        self.send_header('Content-Length',str(len(body)))
        self.send_header('Cache-Control','no-store')
        self.send_header('X-Content-Type-Options','nosniff')
        self.send_header('Content-Security-Policy',"default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'")
        if download:self.send_header('Content-Disposition',f'attachment; filename="{download}"')
        self.end_headers()
        try:self.wfile.write(body)
        except (BrokenPipeError,ConnectionResetError):pass

    def valid_host(self):return self.headers.get('Host')==f'127.0.0.1:{self.server.server_port}'

    def do_GET(self):
        if not self.valid_host():return self.reply(403,dict(error='仅支持本机访问'))
        path=urlparse(self.path).path
        if path=='/api/state':return self.reply(200,self.server.app.snapshot())
        if path=='/api/session':return self.reply(200,dict(token=self.server.token))
        if path=='/api/export':return self.reply(200,self.server.app.export(),'application/zip',f'plc-session-{self.server.app.session}.zip')
        name={'/':'index.html','/app.js':'app.js','/style.css':'style.css','/recipe':'recipe.html','/recipe.js':'recipe.js','/recipe.css':'recipe.css'}.get(path)
        if not name:return self.reply(404,dict(error='未找到页面'))
        mime='text/html' if name.endswith('.html') else 'text/javascript' if name.endswith('.js') else 'text/css'
        return self.reply(200,(self.server.app.root/'src/web'/name).read_bytes(),mime+'; charset=utf-8')

    def do_POST(self):
        origin=f'http://127.0.0.1:{self.server.server_port}'
        if not self.valid_host() or self.headers.get('Origin') not in (None,origin) or self.headers.get('X-Console-Token')!=self.server.token:
            return self.reply(403,dict(error='本机会话校验失败，请刷新'))
        try:
            if self.headers.get('Content-Type','').split(';')[0]!='application/json':raise ValueError('需要JSON请求')
            length=int(self.headers.get('Content-Length','0'))
            if not 0<length<=32768:raise ValueError('请求长度无效')
            data=json.loads(self.rfile.read(length))
            if not isinstance(data,dict):raise ValueError('需要JSON对象')
            path=urlparse(self.path).path
            if not path.startswith('/api/'):raise ValueError('无效接口')
            future=asyncio.run_coroutine_threadsafe(self.server.app.command(path[5:],data),self.server.loop)
            result=future.result(timeout=60)
            self.reply(200,result)
            if path=='/api/shutdown':
                threading.Thread(target=self.server.shutdown,daemon=True).start()
            return
        except Exception as error:
            return self.reply(400,dict(error=error_info(error)[0],errorCode=error_info(error)[1]))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--port',type=int,default=18770)
    parser.add_argument('--no-browser',action='store_true')
    args=parser.parse_args()
    url=f'http://127.0.0.1:{args.port}'
    # A second double-click reuses only this independent app's existing service.
    import urllib.request
    try:
        state=json.load(urllib.request.urlopen(url+'/api/state',timeout=1))
        if state.get('app')=='Gaode Independent PLC Commissioning' and state.get('version')==VERSION:
            if not args.no_browser:webbrowser.open(url)
            return
    except OSError:pass
    app=App()
    server=ThreadingHTTPServer(('127.0.0.1',args.port),Handler)
    server.app,server.token=app,secrets.token_hex(24)
    loop=asyncio.new_event_loop();server.loop=loop
    thread=threading.Thread(target=lambda:loop.run_until_complete(app.worker()),daemon=True);thread.start()
    if not args.no_browser:webbrowser.open(url)
    try:server.serve_forever()
    except KeyboardInterrupt:pass
    finally:
        app.running=False
        thread.join(timeout=5)
        server.server_close()


if __name__=='__main__':
    try:
        main()
    except Exception:
        import traceback
        (ROOT/'startup-error.log').write_text(traceback.format_exc(),encoding='utf-8')
        raise
