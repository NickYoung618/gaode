"""Real TCP software fixture and browser checks. No field hardware is contacted."""
from pathlib import Path
import concurrent.futures, hashlib, json, os, shutil, socket, socketserver, struct, subprocess, sys, threading, time, urllib.request, urllib.error, zipfile
ROOT=Path(__file__).resolve().parents[4]
TOOL=Path(__file__).resolve().parents[1]
OUT=Path(os.environ.get('PLC_UI_EVIDENCE_ROOT',str(ROOT/'artifacts/016-plc-field-ui')))/time.strftime('%Y%m%d-%H%M%S')
OUT.mkdir(parents=True,exist_ok=True)
EXE=ROOT/'artifacts/016-plc-field-ui/publish/Gaode.PlcFieldUi.exe'
inputs=[*TOOL.joinpath('source').glob('*.cs'),*TOOL.joinpath('wwwroot').glob('*'),TOOL/'site.template.json',TOOL/'confirmed-points.json',EXE,EXE.with_suffix('.dll'),ROOT/'backend/src/Gaode.Plc.Protocol/Float32Codec.cs',ROOT/'backend/src/Gaode.Plc.Protocol/BoolByteCodec.cs']
(OUT/'source-hashes.json').write_text(json.dumps([dict(path=str(p.relative_to(ROOT)).replace('\\','/'),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in inputs],indent=2))

class Plc(socketserver.ThreadingTCPServer):
    allow_reuse_address=True
    daemon_threads=True
    def __init__(self,order='Abcd',byte_order='EvenLow',failure=''):
        super().__init__(('127.0.0.1',0),Handler)
        self.order,self.byte_order,self.failure=order,byte_order,failure
        self.regs={i:0 for i in list(range(100,128))+list(range(1000,1048))}
        self.audit=[];self.requests=[];self.lock=threading.Lock();self.moves=[];self.started=time.monotonic();self.ignore_start=False;self.fired=False
        self.setbyte(103,0,1);self.setbyte(103,1,1);self.setbyte(105,0,1)
        self.setbyte(1007,1,1);self.setbyte(1008,0,1)
        self.regs[1020]=1;self.regs[1030]=1;self.regs[109]=65534;self.setfloat(1038,12.5)
        self.setbyte(1019,1,165) # Unassigned MB6039 is not a second BOOL.
        threading.Thread(target=self.serve_forever,daemon=True).start()
    def setbyte(self,address,parity,value):
        shift=0 if (parity==0)==(self.byte_order=='EvenLow') else 8
        self.regs[address]=(self.regs[address]&~(255<<shift))|(value<<shift)
    def byte(self,address,parity):
        return (self.regs[address]>>(0 if (parity==0)==(self.byte_order=='EvenLow') else 8))&255
    def setfloat(self,address,value):
        bs=struct.pack('>f',value);ix={'Abcd':[0,1,2,3],'Cdab':[2,3,0,1],'Badc':[1,0,3,2],'Dcba':[3,2,1,0]}[self.order]
        self.regs[address],self.regs[address+1]=struct.unpack('>HH',bytes(bs[i] for i in ix))
    def update(self):
        self.setbyte(1019,0,int((time.monotonic()-self.started)/.23)%2)
        for due,feedback,target,actual in self.moves:
            if time.monotonic()>due:
                self.regs[feedback]=1;self.regs[actual]=self.regs[target];self.regs[actual+1]=self.regs[target+1]
    def close(self):self.shutdown();self.server_close()

class Handler(socketserver.BaseRequestHandler):
    def handle(self):
        self.request.settimeout(12)
        def exact(n):
            result=b''
            while len(result)<n:
                part=self.request.recv(n-len(result))
                if not part:raise EOFError()
                result+=part
            return result
        try:
            while True:
                h=exact(7);tid,_,length,unit=struct.unpack('>HHHB',h);p=exact(length-1)
                fc=p[0];address,n=struct.unpack('>HH',p[1:5]);s=self.server
                with s.lock:
                    s.update();s.requests.append(dict(fc=fc,address=address,n=n,hex=(h+p).hex()))
                    if s.failure and not s.fired:
                        s.fired=True
                        if s.failure=='timeout':time.sleep(1.5);return
                        if s.failure=='exception':body=bytes([fc|128,2])
                        else:body=bytes([fc,2,0,0]);tid=(tid+1)&65535
                    elif fc in (3,4):
                        if any(i not in s.regs for i in range(address,address+n)):body=bytes([fc|128,2])
                        else:body=bytes([fc,n*2])+b''.join(struct.pack('>H',s.regs[i]) for i in range(address,address+n))
                    elif fc in (6,16):
                        values=[n] if fc==6 else list(struct.unpack('>'+'H'*n,p[6:]))
                        old_x,old_r=s.byte(100,1),s.byte(100,0)
                        for i,v in enumerate(values):s.regs[address+i]=v
                        s.audit.append(dict(fc=fc,address=address,values=values,at=time.time()))
                        if address==100 and not s.ignore_start:
                            if old_x==0 and s.byte(100,1)==1:s.regs[1020]=0;s.moves.append((time.monotonic()+.55,1020,112,1032))
                            if old_r==0 and s.byte(100,0)==1:s.regs[1030]=0;s.moves.append((time.monotonic()+.55,1030,122,1034))
                        body=p[:5]
                    else:body=bytes([fc|128,1])
                response=struct.pack('>HHHB',tid,0,len(body)+1,unit)+body
                self.request.sendall(response[:4]);self.request.sendall(response[4:])
        except (EOFError,OSError,ConnectionError):pass

def freeport():
    with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]

def config_for(plc):
    c=json.loads((TOOL/'site.template.json').read_text(encoding='utf-8'))
    c.update(purpose='Virtual',writesConfirmed=True,singleWriterConfirmed=True,plcProgramVersion='TCP-FIXTURE-0.4')
    c['connection'].update(host='127.0.0.1',port=plc.server_address[1],unitId=7,pollMs=80,timeoutMs=700)
    c['mapping'].update(pcPduBase=100,plcPduBase=1000,boolByteOrder=plc.byte_order,floatOrder=plc.order,confirmed=True,source='Software fixture only; not field mapping')
    c['heartbeat']['confirmed']=True
    for p in c['signals']:
        if p['id'] in ['PC_Heartbeat_Resp','PC_System_Ready','Soft_Stop_Cmd','System_Reset_Cmd','Camera_Target_X','X_Move_Start','Rotate_Target_R','Rotate_Start']:p['writeEnabled']=True
        if p['id'] in ['Camera_Target_X','Rotate_Target_R']:p.update(min=-100,max=100)
    c['axes'][0].update(confirmed=True,min=-100,max=100,tolerance=.01,unit='fixture-mm',frame='fixture-origin',timeoutMs=4000,startValue=1,idleValue=0)
    c['axes'][5].update(confirmed=True,min=-100,max=100,tolerance=.01,unit='fixture-degree',frame='fixture-origin',timeoutMs=4000,startValue=1,idleValue=0)
    return c

class App:
    def __init__(self,name,config=None):
        self.root=OUT/name;self.root.mkdir();shutil.copy2(TOOL/'site.template.json',self.root/'site.template.json');shutil.copy2(TOOL/'confirmed-points.json',self.root/'confirmed-points.json')
        shutil.copytree(TOOL/'wwwroot',self.root/'wwwroot');shutil.copytree(TOOL/'source',self.root/'source',ignore=shutil.ignore_patterns('bin','obj'))
        if config:(self.root/'site.json').write_text(json.dumps(config),encoding='utf-8')
        self.url=f'http://127.0.0.1:{freeport()}';self.log=open(OUT/(name+'.backend.log'),'w',encoding='utf-8')
        self.process=subprocess.Popen([str(EXE),'--root',str(self.root),'--port',self.url.rsplit(':',1)[1],'--no-browser'],stdout=self.log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
        for _ in range(100):
            try:self.api('state');break
            except Exception:time.sleep(.1)
        else:raise RuntimeError('server did not start')
    def api(self,path,value=None,expect_error=False):
        req=urllib.request.Request(self.url+'/api/'+path,data=None if value is None else json.dumps(value).encode(),headers={'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(req,timeout=20) as r:result=json.load(r)
        except urllib.error.HTTPError as e:
            if expect_error:return json.load(e)
            raise AssertionError(e.read().decode()) from e
        assert not expect_error,(path,result)
        return result
    def wait(self,check,timeout=6):
        start=time.monotonic()
        while time.monotonic()-start<timeout:
            state=self.api('state')
            if check(state):return state
            time.sleep(.1)
        raise AssertionError(state)
    def start_heartbeat(self):
        self.api('heartbeat',{'enabled':True});edge=self.api('state')['heartbeatEdges'];self.wait(lambda s:s['heartbeatEdges']>edge)
    def close(self):
        try:self.api('disconnect',{})
        finally:self.process.terminate();self.process.wait(timeout=10);self.log.close()

def check_export(app,status=None):
    export=app.api('export',{})
    with zipfile.ZipFile(export['path']) as z:
        manifest=json.loads(z.read('return-manifest.json'))
        for f in manifest['files']:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
        if status:
            sid=app.api('state')['sessionId'];assert json.loads(z.read('runs/'+sid+'/summary.json'))['status']==status
    return dict(export=export['filename'],filesHashed=len(manifest['files']))

def field_layout():
    plc=Plc();c=config_for(plc);c['purpose']='Field';c['mapping'].update(confirmed=False,source='')
    c.update(writesConfirmed=False,singleWriterConfirmed=False)
    app=App('field-layout',c)
    try:
        app.api('connect',{});state=app.api('state')
        assert len(state['samples'])==85 and not plc.audit
        assert any(e['kind']=='MappingNotCalibrated' for e in state['events'])
        app.api('disconnect',{})
        count=len(plc.requests);c['layoutId']='old-0.3';app.api('config',c)
        assert '点表版本过旧' in app.api('connect',{},True)['error']
        assert len(plc.requests)==count
        c['layoutId']='confirmed-20261006-v2'
        next(p for p in c['signals'] if p['id']=='PLC_Heartbeat_Req')['mb']=6037
        app.api('config',c);assert '85' in app.api('connect',{},True)['error'];assert len(plc.requests)==count
        return dict(unconfirmedMappingReadable=True,samples=85,oldLayoutRejectedBeforeIo=True,oldHeartbeatRejectedBeforeIo=True)
    finally:app.close();plc.close()

def primary():
    plc=Plc();app=App('main-flow',config_for(plc))
    try:
        app.api('connect',{});time.sleep(.5);assert not plc.audit
        v={s['signal']:s['value'] for s in app.api('state')['samples']}
        assert len(v)==85 and v['Grab_ID']==-2 and v['Machine_Current_Pos_Y']==12.5
        app.api('write',{'signal':'PC_System_Ready','value':0},True);app.api('writes',{'enabled':True})
        for signal,value in [('PLC_System_Fault',0),('Model_Number',65),('X_Move_Start',1)]:app.api('write',dict(signal=signal,value=value),True)
        app.start_heartbeat()
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:list(pool.map(lambda x:app.api('write',dict(signal='PC_System_Ready',value=x)),[0,1,0,1]))
        app.api('write',dict(signal='PC_System_Ready',value=1));assert plc.byte(103,1)==1 and plc.byte(105,0)==1
        app.api('move',dict(axis='X',target=1000),True);assert not any(w['address']==100 for w in plc.audit)
        accepted=app.api('move',dict(axis='X',target=12.5));state=app.wait(lambda s:s['move'] and s['move']['phase'] in ('Completed','Unknown'))
        assert state['move']['phase']=='Completed' and plc.regs[100]==0
        starts=[w for w in plc.audit if w['address']==100];assert [w['values'] for w in starts]==[[256],[0]]
        app.api('move',dict(axis='R',target=-30.25));state=app.wait(lambda s:s['move'] and s['move']['phase'] in ('Completed','Unknown'))
        assert state['move']['phase']=='Completed' and plc.regs[100]==0
        assert [w['values'] for w in plc.audit if w['address']==100]==[[256],[0],[1],[0]]
        app.api('disconnect',{});result=check_export(app,'Stopped')
        (app.root/'fixture-audit.json').write_text(json.dumps(plc.audit,indent=2));(app.root/'fixture-requests.json').write_text(json.dumps(plc.requests,indent=2))
        return dict(samples=85,readonlyWrites=0,actionId=accepted['actionId'],axes=['X','R'],axisStartWrites=starts,**result)
    finally:app.close();plc.close()

def codecs():
    facts=[]
    for order in ['Abcd','Cdab','Badc','Dcba']:
        plc=Plc(order,'EvenHigh');app=App('codec-'+order,config_for(plc))
        try:
            app.api('connect',{});v={s['signal']:s['value'] for s in app.api('state')['samples']}
            assert v['PC_System_Ready']==v['PLC_Mode_Auto']==v['PLC_Ready_State']==1 and v['Machine_Current_Pos_Y']==12.5
            app.api('writes',{'enabled':True});app.api('write',dict(signal='PC_Heartbeat_Resp',value=1));assert plc.regs[105]==0x101
            app.api('write',dict(signal='PC_System_Ready',value=0));assert plc.regs[103]==0x1
            app.api('write',dict(signal='Camera_Target_X',value=-12.5));facts.append(dict(floatOrder=order,boolByteOrder='EvenHigh'))
        finally:app.close();plc.close()
    return facts

def failures():
    facts=[]
    for failure in ['exception','transaction','timeout']:
        plc=Plc(failure=failure);app=App('failure-'+failure,config_for(plc))
        try:
            error=app.api('connect',{},True);s=app.api('state');assert s['status']=='Failed' and s['statistics']['writeCount']==0
            log=[json.loads(x) for x in (Path(s['runDirectory'])/'wire.jsonl').read_text().splitlines()]
            assert any(x['direction']=='TX' for x in log) and any(x['direction']=='ERROR' for x in log)
            check_export(app,'Failed');facts.append(dict(failure=failure,error=error))
        finally:app.close();plc.close()
    return facts

def motion_failure():
    plc=Plc();config=config_for(plc);config['axes'][0]['timeoutMs']=700;app=App('motion-failure',config)
    try:
        app.api('connect',{});app.api('writes',{'enabled':True});app.start_heartbeat()
        with plc.lock:plc.setbyte(1007,1,0)
        app.api('move',dict(axis='X',target=10),True);assert not any(w['address'] in (100,112) for w in plc.audit)
        with plc.lock:plc.setbyte(1007,1,1);plc.ignore_start=True
        app.api('move',dict(axis='X',target=10));s=app.wait(lambda x:x['status']=='Failed')
        assert s['move']['phase']=='Unknown' and plc.regs[100]==256
        starts=[w for w in plc.audit if w['address']==100];assert [w['values'] for w in starts]==[[256]]
        check_export(app,'Failed');return dict(notReadyMotionWrites=0,staleDoneRejected=True,startWrites=starts,result='Unknown')
    finally:app.close();plc.close()

def browser():
    from playwright.sync_api import sync_playwright
    plc=Plc();app=App('browser')
    try:
        with sync_playwright() as p:
            b=p.chromium.launch(channel='msedge',headless=True);page=b.new_page(viewport={'width':1600,'height':1100});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
            page.goto(app.url);page.wait_for_selector('#pcSignals tr');assert page.locator('#pcSignals tr').count()==26 and page.locator('#plcSignals tr').count()==59
            page.locator('#connect').click();page.wait_for_function("document.querySelector('#notice').classList.contains('error')");assert not plc.requests
            page.locator('[data-tab=config]').click();page.locator('#advanced').fill(json.dumps(config_for(plc),ensure_ascii=False));page.locator('#saveJson').click()
            page.wait_for_function("document.querySelector('#notice').textContent.includes('配置已保存')");page.wait_for_function('!busy')
            page.locator('[data-tab=live]').click();page.locator('#connect').click();page.wait_for_function("document.querySelector('#status').textContent==='已连接'");assert not plc.audit
            page.locator('#writes').click();page.wait_for_function("document.querySelector('#writes').textContent==='关闭写入'");page.wait_for_function('!busy')
            row=page.locator('[data-signal=PC_System_Ready]')
            for value in ('0','1'):
                row.locator('input').fill(value);row.locator('button').click();page.wait_for_function(f"document.querySelector('#lastWrite').textContent.includes('读回 {value}')");page.wait_for_function('!busy')
            page.locator('#heartbeat').click();page.wait_for_function("document.querySelector('#heartbeat').textContent==='停用心跳应答'")
            edge=app.api('state')['heartbeatEdges'];app.wait(lambda s:s['heartbeatEdges']>edge);page.wait_for_function('!busy')
            assert page.locator('#coordinateRows tr').count()==6 and '反馈未提供' not in page.locator('[data-coordinate=R]').inner_text()
            coord=page.locator('[data-coordinate=X]');coord.locator('[data-coordinate-input]').fill('12.5');coord.locator('[data-coordinate-write]').click()
            page.wait_for_function("document.querySelector('#lastWrite').textContent.includes('本次没有发送轴启动信号')");page.wait_for_function('!busy')
            coord.locator('[data-use-target]').click();assert page.locator('#target').input_value()=='12.5'
            page.locator('#move').click();page.wait_for_function("document.querySelector('#moveResult').textContent.includes('观察通过')",timeout=10000)
            page.locator('#hidePoll').uncheck();page.locator('#wire details').first.click();page.screenshot(path=str(OUT/'ui-live.png'),full_page=True)
            page.locator('#disconnect').click();page.wait_for_function("document.querySelector('#status').textContent.includes('已停止')");page.wait_for_function('!busy')
            page.locator('[data-tab=notes]').click();page.locator('#note-observations').fill('软件验证：独立TCP测试PLC，非现场真机。X目标12.5。')
            page.locator('#exportNotes').click();page.wait_for_selector('#exportResult a',timeout=20000)
            with page.expect_download() as d:page.locator('#exportResult a').click()
            d.value.save_as(OUT/'browser-return.zip');page.screenshot(path=str(OUT/'ui-return.png'),full_page=True);assert not errors;b.close()
        return dict(browser='Microsoft Edge headless',rows=85,coordinateRows=6,separateTargetWrite=True,rFeedbackFromPlc=True,pageErrors=errors,download='browser-return.zip',screenshots=['ui-live.png','ui-return.png'])
    finally:app.close();plc.close()

checks=[]
try:
    for name in sys.argv[1:] or ['field_layout','primary','codecs','failures','motion_failure','browser']:
        try:facts=globals()[name]();checks.append(dict(name=name,passed=True,facts=facts));print('PASS '+name,flush=True)
        except Exception as e:checks.append(dict(name=name,passed=False,error=str(e)));raise
        finally:(OUT/'results.json').write_text(json.dumps(dict(hardwareTested=False,toolVersion='0.4.0',checks=checks),ensure_ascii=False,indent=2),encoding='utf-8')
finally:print('EVIDENCE '+str(OUT),flush=True)
