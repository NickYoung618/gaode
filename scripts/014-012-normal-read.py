"""012 T045: actual normal Host shutdown/restart reads from the already verified private UI database."""
from pathlib import Path
import hashlib,json,os,socket,subprocess,sys,time,urllib.request,uuid
ROOT=Path(__file__).resolve().parents[1]
source=Path(sys.argv[1]).resolve(strict=True)
allowed=(ROOT/'artifacts/recipe-layout-012').resolve(strict=True)
if not source.is_relative_to(allowed):raise ValueError('Private012InputRequired')
evidence=allowed/('normal-read-'+uuid.uuid4().hex)
evidence.mkdir()
expected=json.loads((source/'saved-definition.json').read_text(encoding='utf-8-sig'))
ledger=json.loads((source/'save-read-ledger.json').read_text(encoding='utf-8-sig'))
database=Path(ledger['sqliteDatabase']).resolve(strict=True)
if not database.is_relative_to(source):raise ValueError('DeclaredDatabaseRequired')
def save(name,value):
 with (evidence/name).open('x',encoding='utf-8') as f:json.dump(value,f,ensure_ascii=False,indent=2)
save('input.json',dict(source=str(source),database=str(database),databaseSha256=hashlib.sha256(database.read_bytes()).hexdigest(),recipeId=expected['recipeId'],version=expected['version'],deviceExecution='NotRun'))
for cycle in range(2):
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
 token=uuid.uuid4().hex
 env={k:v for k,v in os.environ.items() if not k.lower().startswith(('gaode__','recipestore__','gaode_013_','gaode_014_'))}
 env.update(GAODE_012_AUTHORING_EVIDENCE_ROOT=str(evidence),ASPNETCORE_ENVIRONMENT='RecipeLayoutVerification',Gaode__Mode='FullSimulation',Gaode__PlcProvider='Virtual',Gaode__TestRoot=str(source/'data/runtime'),Gaode__AllowedTestRoot=str(source/'data'),Gaode__ConfigRoot=str(ROOT/'specs/001-station01-public-preparation/examples'),Gaode__SchemaRoot=str(ROOT/'specs/001-station01-public-preparation/contracts'),Gaode__PublicId='s01-public-dev',Gaode__PublicVersion='1.0.0',Gaode__BudgetId='s01-budget-dev',Gaode__BudgetVersion='3.0.0',Gaode__SimulationId='s01-sim-normal',Gaode__SimulationVersion='3.0.0',Gaode__Tokens__Operator=uuid.uuid4().hex,Gaode__Tokens__SystemAdministrator=token,RecipeStore__DatabasePath=str(database),RecipeStore__ReadWriteTimeoutMs='30000',RecipeStore__DbLockTimeoutSeconds='10')
 name='host' if cycle==0 else 'host-reread'
 with (evidence/(name+'.log')).open('x',encoding='utf-8') as log:
  process=subprocess.Popen([sys.executable,str(ROOT/'scripts/011-owned-host.py'),str(evidence),name,str(ROOT/'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll'),f'http://127.0.0.1:{port}'],cwd=ROOT,env=env,stdin=subprocess.PIPE,stdout=log,stderr=log,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
  try:
   deadline=time.monotonic()+30
   while True:
    try:
     request=urllib.request.Request(f'http://127.0.0.1:{port}/api/v1/recipes/'+expected['recipeId'],headers={'Authorization':'Bearer '+token})
     with urllib.request.urlopen(request,timeout=5) as response:actual=json.load(response)
     break
    except Exception:
     if process.poll() is not None or time.monotonic()>deadline:raise
     time.sleep(.25)
   if actual['definition']!=expected:raise ValueError('FullBodyChangedAfterNormalRestart')
   save(name+'-full-read.json',actual)
   process.stdin.write('stop\n');process.stdin.flush();process.stdin.close()
   code=process.wait(timeout=45)
   record=json.loads((evidence/(name+'-process.json')).read_text(encoding='utf-8-sig'))
   if code!=0 or record.get('normalShutdown') is not True or record.get('exitCode')!=0:raise ValueError('NormalShutdownRequired')
  finally:
   if process.poll() is None:subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=log,stderr=log,check=False)
save('normal-restart-proof.json',dict(source=str(source),actualNormalShutdowns=2,fullRereads=2,recipeId=expected['recipeId'],version=expected['version'],deviceExecution='NotRun',hardwareApplied='NotVerified'))
print(evidence)
