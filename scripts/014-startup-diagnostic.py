"""014 T016: hypothesis-led bounded startup comparisons, never workflow acceptance or device control."""
from pathlib import Path
import ctypes
from ctypes import wintypes as W
import hashlib,json,os,socket,subprocess,sys,threading,time,urllib.request,uuid

ROOT=Path(__file__).resolve().parents[1]
BASE=(ROOT/'artifacts/014-012-joint').resolve(strict=True)
collector=sys.argv[1]
if collector not in ('full','reduced') or len(sys.argv)!=3:raise ValueError('Explicit collector and fresh attempt required')
hypothesis=os.environ.get('GAODE_014_DIAGNOSTIC_HYPOTHESIS')
variable=os.environ.get('GAODE_014_DIAGNOSTIC_VARIABLE')
if not hypothesis or not variable:raise ValueError('Explicit hypothesis and principal contrast variable required')
root=(BASE/('startup-'+sys.argv[2]+'-'+collector)).resolve()
if root.parent!=BASE or root.exists():raise ValueError('Fresh owned diagnostic root required')
if os.name!='nt' or os.environ.get('GAODE_013_ATTEMPT_ROOT'):raise ValueError('Windows owned startup diagnostic only')
root.mkdir();control=BASE/('control-'+root.name);control.mkdir();pages=BASE/'pages'/root.name
fixture_path=ROOT/'specs/014-special-part-rotation/examples/software-joint/run-special.json'
fixture=json.loads(fixture_path.read_text(encoding='utf-8-sig'));inputs=fixture_path.parent
def save(name,value):
 with (root/name).open('x',encoding='utf-8') as stream:json.dump(value,stream,ensure_ascii=False,indent=2)
def local(key):
 p=(inputs/fixture[key]).resolve(strict=True)
 if not p.is_relative_to(inputs):raise ValueError('Declared fixture path required')
 return p
def free_port():
 with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
def get(url,token=None):
 req=urllib.request.Request(url,headers={'Authorization':'Bearer '+token} if token else {})
 with urllib.request.urlopen(req,timeout=3) as response:return json.load(response)
def wait(check,seconds):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  try:
   result=check()
   if result:return result
  except (OSError,ValueError):pass
  time.sleep(.1)
 raise TimeoutError('Diagnostic preparation exceeded its finite budget')

kernel=ctypes.WinDLL('kernel32',use_last_error=True)
class FT(ctypes.Structure):_fields_=[('low',W.DWORD),('high',W.DWORD)]
class PE(ctypes.Structure):
 _fields_=[('size',W.DWORD),('usage',W.DWORD),('pid',W.DWORD),('heap',ctypes.c_size_t),('module',W.DWORD),('threads',W.DWORD),('parent',W.DWORD),('priority',W.LONG),('flags',W.DWORD),('name',W.WCHAR*260)]
kernel.CreateToolhelp32Snapshot.argtypes=[W.DWORD,W.DWORD];kernel.CreateToolhelp32Snapshot.restype=W.HANDLE
kernel.Process32FirstW.argtypes=[W.HANDLE,ctypes.POINTER(PE)];kernel.Process32NextW.argtypes=[W.HANDLE,ctypes.POINTER(PE)]
kernel.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];kernel.OpenProcess.restype=W.HANDLE
kernel.GetProcessTimes.argtypes=[W.HANDLE,*([ctypes.POINTER(FT)]*4)]
kernel.GetSystemTimes.argtypes=[ctypes.POINTER(FT)]*3
kernel.CloseHandle.argtypes=[W.HANDLE]
def ticks(v):return (v.high<<32)|v.low
def resources():
 idle,kern,user=FT(),FT(),FT();kernel.GetSystemTimes(ctypes.byref(idle),ctypes.byref(kern),ctypes.byref(user))
 counter=ctypes.c_longlong();kernel.QueryPerformanceCounter(ctypes.byref(counter))
 result=dict(monotonicTick=counter.value,utc=time.time(),idle100ns=ticks(idle),kernel100ns=ticks(kern),user100ns=ticks(user),processes=[])
 snapshot=kernel.CreateToolhelp32Snapshot(2,0);entry=PE();entry.size=ctypes.sizeof(PE)
 try:
  ok=kernel.Process32FirstW(snapshot,ctypes.byref(entry))
  while ok:
   handle=kernel.OpenProcess(0x1000,False,entry.pid)
   if handle:
    try:
     created,exited,kt,ut=FT(),FT(),FT(),FT()
     if kernel.GetProcessTimes(handle,ctypes.byref(created),ctypes.byref(exited),ctypes.byref(kt),ctypes.byref(ut)):
      result['processes'].append(dict(pid=entry.pid,parent=entry.parent,name=entry.name,creation100ns=ticks(created),cpu100ns=ticks(kt)+ticks(ut),threads=entry.threads))
    finally:kernel.CloseHandle(handle)
   ok=kernel.Process32NextW(snapshot,ctypes.byref(entry))
 finally:kernel.CloseHandle(snapshot)
 return result

kernel.CreateNamedPipeW.argtypes=[W.LPCWSTR,W.DWORD,W.DWORD,W.DWORD,W.DWORD,W.DWORD,W.DWORD,W.LPVOID];kernel.CreateNamedPipeW.restype=W.HANDLE
kernel.ConnectNamedPipe.argtypes=[W.HANDLE,W.LPVOID];kernel.WriteFile.argtypes=[W.HANDLE,W.LPCVOID,W.DWORD,ctypes.POINTER(W.DWORD),W.LPVOID]
def token_server(name,token):
 handle=kernel.CreateNamedPipeW('\\\\.\\pipe\\'+name,2,0,1,1024,1024,0,None)
 if handle==ctypes.c_void_p(-1).value:raise OSError(ctypes.get_last_error())
 def serve():
  try:
   connected=kernel.ConnectNamedPipe(handle,None)
   if not connected and ctypes.get_last_error()!=535:return
   data=(token+'\n').encode();written=W.DWORD();kernel.WriteFile(handle,data,len(data),ctypes.byref(written),None)
  finally:kernel.CloseHandle(handle)
 threading.Thread(target=serve,daemon=True).start()

logs=[];owned=[]
def start(name,command,env,stdin=False):
 log=(root/(name+'.log')).open('x',encoding='utf-8');logs.append(log)
 p=subprocess.Popen(command,cwd=ROOT,env=env,stdin=subprocess.PIPE if stdin else subprocess.DEVNULL,stdout=log,stderr=log,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
 owned.append((name,p));return p
env={k:v for k,v in os.environ.items() if not k.lower().startswith(('gaode__','simulation__','modbus__','recipestore__','gaode_013_','gaode_014_','gaode_012_'))}
env.update(PYTHONUTF8='1',PYTHONDONTWRITEBYTECODE='1',GAODE_014_JOINT_ROOT=str(BASE),SPECIFY_FEATURE_DIRECTORY='specs/014-special-part-rotation')
save('identity.json',dict(kind='StartupDiagnosticNotAcceptance',collector=collector,root=str(root),fixture=str(fixture_path),fixtureSha256=hashlib.sha256(fixture_path.read_bytes()).hexdigest(),binaries={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for directory in [ROOT/'backend/src/Gaode.Host/bin/Debug/net10.0',ROOT/'VirtualPlc/bin/Debug/net10.0',ROOT/'backend/tools/Gaode.StorePrep/bin/Debug/net10.0'] for p in directory.glob('*.dll')},observerSources={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in ['scripts/014-startup-diagnostic.py','scripts/014-012-joint-page.mjs','scripts/014-page-proxy.mjs']},normalPageApiNotifications=True,runCommandsSent=0,ioDeadlineMs=1000,heartbeatTimeoutMs=3000,hypothesis=hypothesis,contrastVariable=variable,runtimeConfiguration=dict(windowsThreadPool=os.environ.get('DOTNET_ThreadPool_UseWindowsThreadPool','runtime-default'),inlineIoCompletions=os.environ.get('DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS','runtime-default'),threadPoolOverrides={k:v for k,v in os.environ.items() if k.startswith(('DOTNET_ThreadPool','COMPlus_ThreadPool'))}),output='owned console stdout files in both groups; different from historical xUnit relay'))
samplings=[];sampler_stop=threading.Event()
def sample():
 while not sampler_stop.is_set():samplings.append(resources());sampler_stop.wait(.5)
sampler=threading.Thread(target=sample,daemon=True);sampler.start()
host=None;observer=None
try:
 observer=start('observer',['node',str(ROOT/'scripts/014-012-joint-page.mjs'),str(root/'page-connection.json'),str(control)],env)
 wait(lambda:(control/'browser-prewarm.json').exists(),20)
 prep=ROOT/'backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll'
 for name,args in [('store-prep',[str(BASE),str(root/'store')]),('recipe-prepare',['--prepare-recipes',str(BASE),str(root/'recipes')]),('recipe-seed',['--seed-test-recipes',str(BASE),str(root/'recipes'),str(local('recipeInputPath')),fixture['recipeInputSha256']])]:
  p=start(name,['dotnet',str(prep),*args],env);p.wait(timeout=60)
  if p.returncode!=0:raise RuntimeError(name+' failed')
 port,api,plc_api=free_port(),free_port(),free_port();token=uuid.uuid4().hex
 simulation=json.loads((local('configRoot')/'simulation.json').read_text(encoding='utf-8-sig'))
 env.update(ASPNETCORE_ENVIRONMENT='VirtualPlc',Gaode__Tokens__Operator=uuid.uuid4().hex,Gaode__Tokens__ProcessEngineer=token,Gaode__Mode='VirtualPlcIntegration',Gaode__PlcProvider='Virtual',Gaode__TestRoot=str(root/'store'),Gaode__AllowedTestRoot=str(BASE),Gaode__ConfigRoot=str(local('configRoot')),Gaode__SchemaRoot=str(ROOT/'specs/001-station01-public-preparation/contracts'),Gaode__PlcPort=str(port),Modbus__Port=str(port),Gaode__PlcIoTimeoutMs='1000',Simulation__HeartbeatTimeoutMs='3000',Simulation__MotionDurationMs=str(simulation['stages']['xyCompletion']['delayMs']),Simulation__ActionDurationJitterMs='0',Dashboard__OpenBrowserOnStart='false',Gaode__ImageManifestPath=str(local('imageManifestPath')),Gaode__WorkerExecutablePath=sys.executable,Gaode__WorkerScriptPath=str(ROOT/fixture['workerScriptPath']),Gaode__WorkerManifestPath=str(local('workerManifestPath')),Gaode__PlcMechanicsPath=str(local('plcMechanicsPath')),RecipeStore__DatabasePath=str(root/'recipes/recipes.db'),RecipeStore__ReadWriteTimeoutMs='10000',RecipeStore__DbLockTimeoutSeconds='5',Simulation__PutBackDurationMs=str(fixture['simulation']['putBackDurationMs']),Simulation__FlipPutBackSafeZ=str(fixture['simulation']['flipPutBackSafeZ']),GAODE_013_MEASUREMENT_ROOT=str(root/'application-timing'))
 for key,prefix in [('publicConfigRef','Public'),('budgetRef','Budget'),('simulationRef','Simulation')]:
  env['Gaode__'+prefix+'Id']=fixture[key]['id'];env['Gaode__'+prefix+'Version']=fixture[key]['version']
 plc=start('plc',['dotnet',str(ROOT/'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll'),'--urls',f'http://127.0.0.1:{plc_api}'],env)
 wait(lambda:get(f'http://127.0.0.1:{plc_api}/health')['status']=='ok',25)
 host=start('host',['python',str(ROOT/'scripts/011-owned-host.py'),str(root),'host',str(ROOT/'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll'),f'http://127.0.0.1:{api}'],env,stdin=True)
 start_status=wait(lambda:get(f'http://127.0.0.1:{api}/api/v1/station01/status',token),30)
 save('startup-status.json',start_status)
 pipe='gaode-014-startup-'+uuid.uuid4().hex;token_server(pipe,token)
 save('page-connection.json',dict(apiBaseUrl=f'http://127.0.0.1:{api}',tokenPipeName=pipe,evidenceRoot=str(pages),observationTimeoutMs=35000,diagnosticOnly='014-startup-comparison',diagnosticCollector=collector))
 wait(lambda:(pages/'ready.json').exists(),20)
 save('processes.json',dict(host=json.loads((root/'host-process.json').read_text(encoding='utf-8-sig')),plcPid=plc.pid,observerPid=observer.pid,browser=json.loads((control/'browser-prewarm.json').read_text()),ports=dict(modbus=port,host=api,plc=plc_api),windowStartedMonotonic=time.monotonic()))
 time.sleep(20)
 end_status=get(f'http://127.0.0.1:{api}/api/v1/station01/status',token);save('end-status.json',end_status)
 (control/'startup-diagnostic-stop.json').write_text('{"diagnosticOnly":true,"acceptance":false}',encoding='utf-8')
 observer.wait(timeout=15)
 save('result.json',dict(kind='StartupDiagnosticNotAcceptance',collector=collector,runCommandsSent=0,finalClaimed=False,observerExit=observer.returncode,plc=end_status.get('plc'),windowSeconds=20,screenshotCount=len(list(pages.glob('*.png')))))
except Exception as e:
 save('failure.json',dict(kind='StartupDiagnosticFailureNotAcceptance',error=repr(e)));raise
finally:
 if observer is not None and observer.poll() is None:
  (control/'observer-abort.json').write_text('{"businessSuccess":false}',encoding='utf-8')
  try:observer.wait(timeout=12)
  except subprocess.TimeoutExpired:observer.kill()
 if host is not None and host.poll() is None:
  host.stdin.write('stop\n');host.stdin.flush();host.stdin.close()
  try:host.wait(timeout=45)
  except subprocess.TimeoutExpired:
   # Only the exact child recorded by this owned console supervisor, never a process search.
   metadata=json.loads((root/'host-process.json').read_text(encoding='utf-8-sig'));subprocess.run(['taskkill','/PID',str(metadata['hostPid']),'/T','/F'],capture_output=True);host.kill()
 for name,p in reversed(owned):
  if p.poll() is None:p.kill();p.wait(timeout=10)
 sampler_stop.set();sampler.join(timeout=2);save('resources.json',samplings)
 for log in logs:log.close()
print(root)
