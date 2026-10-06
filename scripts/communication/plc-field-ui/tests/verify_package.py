"""Run the actual self-contained launcher from its staged directory, no installed .NET lookup."""
from pathlib import Path
import hashlib,json,os,subprocess,socket,time,urllib.request,urllib.error
ROOT=Path(__file__).resolve().parents[4]
ART=ROOT/'artifacts/017-confirmed-plc-addresses'
stage=Path(json.loads((ART/'stage-result.json').read_text())['stage'])
manifest=json.loads((stage/'package-manifest.json').read_text(encoding='utf-8'))
for f in manifest['files']:assert hashlib.sha256((stage/f['path']).read_bytes()).hexdigest()==f['sha256']
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
env=os.environ.copy()
env.update(DOTNET_ROOT=str(ART/'no-dotnet-runtime'),DOTNET_ROOT_X64=str(ART/'no-dotnet-runtime'),DOTNET_MULTILEVEL_LOOKUP='0')
env['PATH']=str(Path(os.environ['SystemRoot'])/'System32')
log=open(ART/'packaged-start.log','w',encoding='utf-8')
p=subprocess.Popen([str(stage/'Gaode.PlcFieldUi.exe'),'--port',str(port),'--no-browser'],cwd=stage,env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
base=f'http://127.0.0.1:{port}'
result={}
try:
    for _ in range(100):
        try:
            with urllib.request.urlopen(base+'/api/state') as r:state=json.load(r)
            break
        except Exception:time.sleep(.1)
    else:raise AssertionError('packaged app did not start')
    assert state['status']=='Disconnected'
    with urllib.request.urlopen(base) as r:assert 'PLC 现场联调台' in r.read().decode()
    with urllib.request.urlopen(base+'/api/config') as r:config=json.load(r)
    assert not config['connection']['host'] and config['connection']['port']==502
    assert config['mapping']['pcPduBase']==1000 and config['mapping']['plcPduBase']==3000
    assert not config['mapping']['confirmed'] and config['layoutId']=='confirmed-20261006-v2'
    assert all(not x['writeEnabled'] for x in config['signals'])
    assert len(config['signals'])==85 and len(config['axes'])==6
    assert all(a['movingValue']==0 and a['doneValue']==1 and not a['confirmed'] for a in config['axes'])
    request=urllib.request.Request(base+'/api/connect',data=b'{}',headers={'Content-Type':'application/json'})
    try:urllib.request.urlopen(request);raise AssertionError('blank field configuration connected')
    except urllib.error.HTTPError as e:assert e.code==400;blocked=json.load(e)['error']
    assert not (stage/'runs').exists() and not (stage/'site.json').exists()
    result=dict(passed=True,hardwareTested=False,version=manifest['toolVersion'],stage=str(stage),filesHashed=len(manifest['files']),
                selfContainedWithoutDotnetLookup=True,blankFieldConnectBlocked=blocked,sixAxesFromSource=True,signals=len(config['signals']),
                executableSha256=hashlib.sha256((stage/'Gaode.PlcFieldUi.exe').read_bytes()).hexdigest(),assemblySha256=hashlib.sha256((stage/'Gaode.PlcFieldUi.dll').read_bytes()).hexdigest())
finally:
    p.terminate();p.wait(timeout=10);log.close()
    (ART/'packaged-entry-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
