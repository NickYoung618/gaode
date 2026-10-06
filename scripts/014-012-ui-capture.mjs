import {reviewRenderedEditor,renderedEditorExpression} from '../frontend/scripts/recipe-layout-review.mjs';
// 012 T045: observe the actual page/API/SQLite only. No device or workflow producer.
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import net from 'node:net';
import crypto from 'node:crypto';
import cp from 'node:child_process';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const require=createRequire(import.meta.url), WebSocket=require('../frontend/node_modules/ws');
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const evidence=path.join(root,'artifacts/recipe-layout-012',process.argv[2]||`page-${Date.now()}`);
if(fs.existsSync(evidence))throw Error('Fresh evidence directory required');
fs.mkdirSync(evidence,{recursive:true});
const children=[],token=crypto.randomBytes(32).toString('hex'),ledger=[];
const waitMs=ms=>new Promise(r=>setTimeout(r,ms));
let backend,browserSocket,pageServer;
function launch(command,args,options={}){
  const child=cp.spawn(command,args,{cwd:root,windowsHide:true,...options});children.push(child);return child;
}
async function command(args,name){
  const log=fs.openSync(path.join(evidence,name+'.log'),'a');
  const child=launch('dotnet',args,{stdio:['ignore',log,log]});
  const code=await new Promise((resolve,reject)=>{child.once('error',reject);child.once('exit',resolve);});
  fs.closeSync(log);if(code!==0)throw Error(name+' exited '+code);
}
async function freePort(){const server=net.createServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));const port=server.address().port;await new Promise(r=>server.close(r));return port;}
function assert(value,message){if(!value)throw Error(message);}
async function main(){
  const navigationMode=process.argv[3]?.startsWith('--navigation');
  const cellsOnly=process.argv[3]==='--navigation-cells';
  const reused=!navigationMode&&process.argv[3]?path.resolve(process.argv[3]):null;
  if(reused&&(!reused.startsWith(path.join(root,'artifacts/recipe-layout-012')+path.sep)||!fs.existsSync(path.join(reused,'save-read-ledger.json'))))throw Error('Previously verified private UI input required');
  const data=path.join(reused||evidence,'data'),runtime=path.join(data,'runtime'),recipes=path.join(data,'recipes');
  if(!reused)fs.mkdirSync(data);
  const prep='backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll';
  if(!reused){
  await command([prep,data,runtime],'runtime-prepare');
  await command([prep,'--prepare-recipes',data,recipes],'recipe-prepare');
  for(const name of ['ordinary-source','special-rotation-source']){
    const file=path.join(root,'configuration/recipe-authoring',name+'.json');
    await command([prep,'--prepare-authoring-source',recipes,file,crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')],name);
  }
  }
  const hostPort=await freePort(),cdpPort=await freePort();
  const env=Object.fromEntries(Object.entries(process.env).filter(([k])=>!/^gaode__|^recipestore__/i.test(k)));
  Object.assign(env,{ASPNETCORE_ENVIRONMENT:'RecipeLayoutVerification',Gaode__Mode:'FullSimulation',Gaode__PlcProvider:'Virtual',
    Gaode__TestRoot:runtime,Gaode__AllowedTestRoot:data,Gaode__ConfigRoot:path.join(root,'specs/001-station01-public-preparation/examples'),
    Gaode__SchemaRoot:path.join(root,'specs/001-station01-public-preparation/contracts'),
    Gaode__PublicId:'s01-public-dev',Gaode__PublicVersion:'1.0.0',Gaode__BudgetId:'s01-budget-dev',Gaode__BudgetVersion:'3.0.0',
    Gaode__SimulationId:'s01-sim-normal',Gaode__SimulationVersion:'3.0.0',Gaode__Tokens__Operator:crypto.randomBytes(32).toString('hex'),Gaode__Tokens__SystemAdministrator:token,
    RecipeStore__DatabasePath:path.join(recipes,'recipes.db'),RecipeStore__ReadWriteTimeoutMs:'30000',RecipeStore__DbLockTimeoutSeconds:'10'});
  const hostArgs=['backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll','--urls',`http://127.0.0.1:${hostPort}`];
  const startHost=()=>{const log=fs.openSync(path.join(evidence,'host.log'),'a');return launch('dotnet',hostArgs,{env,stdio:['ignore',log,log]});};
  async function api(url,method='GET',body,headers={}){
    const r=await fetch(`http://127.0.0.1:${hostPort}${url}`,{method,headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json',...headers},body:body?JSON.stringify(body):undefined});
    const value=await r.json();const result={status:r.status,value,etag:r.headers.get('etag')};
    ledger.push({url,method,status:r.status,requestId:body?.requestId,recipeId:value.definition?.recipeId,version:value.definition?.version});return result;
  }
  async function hostReady(){for(let i=0;i<100;i++){try{if((await api('/api/v1/recipes/catalog')).status===200)return;}catch{}await waitMs(250);}throw Error('Actual Host unavailable');}
  backend=startHost();await hostReady();
  const catalog=(await api('/api/v1/recipes/catalog')).value.items;
  const ordinary=catalog.find(c=>c.inspectionKind==='ordinary'),special=catalog.find(c=>c.inspectionKind==='specialRotation');
  assert(ordinary&&special,'Both actual same-model source types required');
  const draftRequest={model:special.model,scenarioId:special.scenarioId,unitKind:special.unitKind,inspectionKind:'specialRotation'};
  assert((await api('/api/v1/recipes/editor-draft','POST',{...draftRequest,sourceRecipeId:ordinary.recipeId})).status===400,'Wrong source must not fall back');
  const draft=await api('/api/v1/recipes/editor-draft','POST',{...draftRequest,sourceRecipeId:special.recipeId});
  assert(draft.status===200&&draft.value.definition.positions.length===0,'Real empty special candidate missing');
  assert(draft.value.definition.sortingGripperId===null||draft.value.definition.sortingGripperId===undefined,'No gripper default permitted');
  const source=await api('/api/v1/recipes/'+special.recipeId);
  pageServer=http.createServer((request,response)=>{
    if(request.url.startsWith('/api/')){
      const proxy=http.request({host:'127.0.0.1',port:hostPort,path:request.url,method:request.method,headers:request.headers},r=>{response.writeHead(r.statusCode,r.headers);r.pipe(response);});
      proxy.once('error',()=>{response.writeHead(502);response.end();});request.pipe(proxy);return;
    }
    const url=new URL(request.url,'http://localhost');
    const file=path.resolve(root,'frontend/dist',url.pathname==='/'?'a.html':'.'+url.pathname);
    if(!file.startsWith(path.join(root,'frontend/dist')+path.sep)){response.writeHead(404);response.end();return;}
    try{let content=fs.readFileSync(file);
      if(file.endsWith('a.html'))content=Buffer.from(content.toString().replace('<head>','<head><script>window.__GAODE_HOST_CONFIG__='+JSON.stringify({apiBaseUrl:'',mode:'Test',testToken:token,resourceVersion:'014-layout'})+';</script>'));
      response.setHeader('Content-Type',file.endsWith('.html')?'text/html; charset=utf-8':file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':'application/octet-stream');response.end(content);
    }catch{response.writeHead(404);response.end();}
  });
  await new Promise(r=>pageServer.listen(0,'127.0.0.1',r));const pagePort=pageServer.address().port;
  const edge=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Microsoft/Edge/Application/msedge.exe'].find(fs.existsSync);
  assert(edge,'Actual renderer required');
  launch(edge,['--headless=new','--disable-gpu','--no-first-run',`--remote-debugging-port=${cdpPort}`,`--user-data-dir=${path.join(evidence,'browser')}`,`http://127.0.0.1:${pagePort}/a.html`],{stdio:'ignore'});
  let target;for(let i=0;i<80;i++){try{target=(await(await fetch(`http://127.0.0.1:${cdpPort}/json/list`)).json()).find(t=>t.url.endsWith('/a.html'));if(target)break;}catch{}await waitMs(250);}assert(target,'Browser page absent');
  const ws=new WebSocket(target.webSocketDebuggerUrl);browserSocket=ws;await new Promise((r,j)=>{ws.once('open',r);ws.once('error',j);});
  let id=0;const pending=new Map();ws.on('message',b=>{const d=JSON.parse(b.toString());if(d.id){const p=pending.get(d.id);pending.delete(d.id);d.error?p.reject(Error(d.error.message)):p.resolve(d.result);}});
  const cdp=(method,params={})=>new Promise((resolve,reject)=>{const key=++id;pending.set(key,{resolve,reject});ws.send(JSON.stringify({id:key,method,params}));});
  const evaluate=async expression=>{const r=await cdp('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
  const wait=async expression=>{for(let i=0;i<100;i++){if(await evaluate(expression))return;await waitMs(100);}fs.writeFileSync(path.join(evidence,'page-failure-dom.json'),JSON.stringify(await evaluate("({notice:document.querySelector('#recipeAuthoringNotice')?.textContent,form:document.querySelector('#recipeAuthoringForm')?.textContent,catalog:document.querySelector('#recipeAuthoringCatalog')?.value})"),null,2));const picture=await cdp('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(evidence,'page-failure.png'),Buffer.from(picture.data,'base64'));throw Error('Page condition failed: '+expression);};
  const shot=async name=>{const view=await evaluate(renderedEditorExpression);const review=reviewRenderedEditor(view);fs.writeFileSync(path.join(evidence,name+'-render-review.json'),JSON.stringify({view,review},null,2));if(!review.passed)throw Error('Rendered editor rejected: '+review.errors.join(','));const r=await cdp('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(evidence,name+'.png'),Buffer.from(r.data,'base64'));};
  await cdp('Emulation.setDeviceMetricsOverride',{width:1600,height:1100,deviceScaleFactor:1,mobile:false});
  await wait("typeof document.querySelector('#recipeAuthoringNew')?.onclick==='function'");
  if(navigationMode){await verifyNavigation();console.log(evidence);return;}
  await evaluate("document.querySelector('#btnRecipe').click()");
  await wait("document.querySelector('#recipeAuthoringCatalog').options.length>=3");
  await evaluate(`(()=>{const e=document.querySelector('#recipeAuthoringCatalog');e.value=${JSON.stringify(special.recipeId)};e.dispatchEvent(new Event('change'));})()`);
  await wait("!!document.querySelector('[aria-label=\"旋转上料夹爪\"]')");
  assert(await evaluate("document.querySelectorAll('#recipeAuthoringForm [data-cell-id]').length===100"),'Actual first matrix must contain100 cells');
  const forbidden=['配置来源','路线引用','Pattern','配置键','阶段引用','内容摘要','准入依据','算法结果合同','OK目标','是否回原槽'];
  const text=await evaluate("document.querySelector('#recipeAuthoringForm').textContent");
  assert(forbidden.every(word=>!text.includes(word)),'Unauthorized operator content');
  await shot('step1-basic');
  await evaluate("document.querySelector('[data-authoring-section=points]').click()");
  await wait("!!document.querySelector('.recipe-matrix-empty')");await shot('step2-points');
  assert(JSON.stringify(JSON.parse(fs.readFileSync(path.join(evidence,'step1-basic-render-review.json'))).view.selected)===JSON.stringify(JSON.parse(fs.readFileSync(path.join(evidence,'step2-points-render-review.json'))).view.selected),'Actual selected region/ordinal/position must remain the same in both steps');
  await evaluate("document.querySelector('[data-authoring-section=review]').click()");await shot('step3-review');
  if(reused){
    await wait("document.querySelector('#stationOperationalStatus')?.textContent.includes('未运行')");
    const status=await api('/api/v1/station01/status');
    assert(status.value.currentRun===null,'Backend must explicitly report no current run');
    const header=await evaluate("document.querySelector('#stationOperationalStatus').textContent");
    assert(!header.includes('检测中'),'No inferred detecting header permitted');
    fs.writeFileSync(path.join(evidence,'page-read-only-proof.json'),JSON.stringify({source:reused,actualThreeSteps:true,actual100PositionsAndHoles:true,businessSavesSent:0,backendStatus:status.value,renderedHeader:header,workflowEvidence:'NotRun',DUI02:'Approved20261006',DUI03:'Approved20261006'},null,2));
    console.log(evidence);return;
  }
  await evaluate("document.querySelector('#recipeAuthoringSave').click()");
  await wait("document.querySelector('#recipeAuthoringNotice').textContent.includes('已保存并重读')");
  const saved=await api('/api/v1/recipes/'+special.recipeId);
  assert(saved.status===200&&saved.value.definition.schemaVersion==='recipe-definition/4','Actual page save/full read missing');
  assert((await api('/api/v1/recipes/'+special.recipeId,'PUT',{requestId:crypto.randomUUID(),definition:source.value.definition},{'If-Match':source.etag})).status===412,'Stale edit must be refused');
  const invalid=structuredClone(saved.value.definition);invalid.sortingGripperId=null;
  assert((await api('/api/v1/recipes/'+special.recipeId,'PUT',{requestId:crypto.randomUUID(),definition:invalid},{'If-Match':saved.etag})).status===422,'Missing gripper must be refused');
  await evaluate("document.querySelector('[data-authoring-section=basic]').click()");
  await evaluate("[...document.querySelectorAll('.recipe-paint-tools button')].find(b=>b.textContent==='OK').click()");
  await evaluate("document.querySelector('[data-cell-id=\"r4:c6\"]').click()");
  await wait("!document.querySelector('#recipeAuthoringSave').disabled");
  const mapped=await api('/api/v1/recipes/editor-layout','POST',{definition:saved.value.definition,sourceRecipeId:special.recipeId,sourceVersion:saved.value.definition.version,
    trayLayout:{rows:10,columns:10,cells:[...saved.value.definition.trayLayout.cells,{cellId:'r4:c6',row:4,column:6,region:'OK'}]}});
  assert(mapped.status===200,'Unfilled editing matrix must map without substituting zeros');
  const newPosition=mapped.value.definition.positions.find(p=>p.cellId==='r4:c6');
  assert(newPosition.physicalSlotIndex===null&&mapped.value.definition.executionPositions[newPosition.slotId].physicalEntity.coordinates.every(c=>c.point.x===null),'New cell must be blank and unmapped');
  assert(mapped.value.definition.positions.find(p=>p.cellId==='r2:c4').slotId===saved.value.definition.positions[0].slotId,'Other cell identity must survive');
  backend.kill();await new Promise(r=>backend.once('exit',r));backend=startHost();await hostReady();
  const restarted=await api('/api/v1/recipes/'+special.recipeId);
  assert(JSON.stringify(restarted.value.definition)===JSON.stringify(saved.value.definition),'Restart must read committed full content');
  // Actual New button, manual matrix and direct card inputs; never inject a model into the page.
  await evaluate("document.querySelector('#recipeAuthoringNew').click()");
  await wait("document.querySelector('#recipeAuthoringNotice').textContent.includes('请填写')");
  const set=async(label,value,card=null)=>evaluate(`(()=>{const container=${card?`[...document.querySelectorAll('.recipe-point-card')].find(c=>c.querySelector('h3').textContent===${JSON.stringify(card)})`:'document'};const e=container.querySelector('[aria-label='+${JSON.stringify(JSON.stringify(label))}+']');if(!e)throw Error('Input missing: '+${JSON.stringify(label)});e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  assert(await evaluate("document.querySelector('[aria-label=\"分拣夹爪\"]').value===''&&document.querySelector('[aria-label=\"旋转上料夹爪\"]').value===''") ,'New grippers must remain explicitly unset');
  const newCode='014-UI-'+Date.now();await set('料盘编号',newCode);await set('分拣夹爪',2);await set('旋转上料夹爪',1);
  for(const [region,cell] of [['Pending','r2:c10'],['OK','r2:c4'],['NG','r2:c1']]){
    await evaluate(`[...document.querySelectorAll('.recipe-paint-tools button')].find(b=>b.textContent===${JSON.stringify(region)}).click()`);
    await evaluate(`document.querySelector('[data-cell-id=${JSON.stringify(cell)}]').click()`);
    await wait("document.querySelector('#recipeAuthoringNotice').textContent.includes('内容已修改')&&!document.querySelector('#recipeAuthoringCatalog').disabled");
  }
  await shot('new-step1-manual');await evaluate("document.querySelector('[data-authoring-section=points]').click()");
  const navigate=async name=>evaluate(`[...document.querySelectorAll('.recipe-nav-button')].find(b=>b.textContent.startsWith(${JSON.stringify(name)})).click()`);
  const point=async(title,x,y,z)=>{for(const [label,value] of [['X (mm)',x],['Y (mm)',y],['抓取Z (mm)',z]])await set(label,value,title);};
  await navigate('旋转工位');await point('旋转工位放料点',210,100,50);await point('旋转工位取料点',210,100,52);
  await navigate('料盘取料');const pickTitle=await evaluate("document.querySelector('.recipe-point-card h3').textContent");await point(pickTitle,110,100,50);
  await navigate('原槽放料');const putTitle=await evaluate("document.querySelector('.recipe-point-card h3').textContent");await point(putTitle,110,100,52);
  for(const [name,angle,offset] of [['第一组检测',90,0],['第二组检测',180,2]]){
    await navigate(name);await set('R目标角度 (°)',angle);
    const titles=await evaluate("[...document.querySelectorAll('.recipe-point-card h3')].map(x=>x.textContent)");
    for(let i=0;i<titles.length;i++)for(const [label,value] of [['X (mm)',210+offset+i],['Y (mm)',100],['检测Z (mm)',51],['曝光 (µs)',500+10*(offset+i)],['增益',1+(offset+i)/10],['光源亮度 (%)',30+offset+i]])await set(label,value,titles[i]);
  }
  for(const [name,x] of [['NG区域',300],['Pending区域',400]]){await navigate(name);const title=await evaluate("document.querySelector('.recipe-point-card h3').textContent");await point(title,x,100,50);}
  await shot('new-step2-cards');await evaluate("document.querySelector('[data-authoring-section=review]').click()");await shot('new-step3-review');
  await evaluate("document.querySelector('#recipeAuthoringSave').click()");await wait("document.querySelector('#recipeAuthoringNotice').textContent.includes('已保存并重读')");
  const newItem=(await api('/api/v1/recipes/catalog')).value.items.find(i=>i.fCode===newCode);assert(newItem,'New actual page save must be catalog-visible');
  const newRead=await api('/api/v1/recipes/'+newItem.recipeId);const created=newRead.value.definition;
  assert(created.rotationLoadingGripperId===1&&created.sortingGripperId===2,'Both explicit choices must round trip');
  const actual=created.executionPositions[created.positions[0].slotId].physicalEntity;
  assert(actual.coordinates.length===4&&new Set(actual.coordinates.map(c=>created.captureProfiles[c.captureProfile].settings.exposureUs)).size===4,'Stage/camera local parameters must round trip independently');
  fs.writeFileSync(path.join(evidence,'new-page-saved-definition.json'),JSON.stringify(created,null,2));
  fs.writeFileSync(path.join(evidence,'new-page-proof.json'),JSON.stringify({actualNewButton:true,manualSelectionOrder:['Pending','OK','NG'],newCode,newItem,
    pageSave:true,apiFullRead:true,sqlite:true,grippers:[1,2],independentCaptureSettings:true,productionApproved:false,hardwareApplied:'NotVerified'},null,2));
  fs.writeFileSync(path.join(evidence,'saved-definition.json'),JSON.stringify(restarted.value.definition,null,2));
  fs.writeFileSync(path.join(evidence,'save-read-ledger.json'),JSON.stringify({ledger,sqliteDatabase:path.join(recipes,'recipes.db'),sourcePrepared:true,
    actualPageSave:true,completeReread:true,staleVersionRejected:true,missingGripperRejected:true,restartRead:true,
    renderer:'Edge',DUI02:'Approved20261006',DUI03:'Approved20261006',workflowEvidence:'NotRun',hardwareApplied:'NotVerified'},null,2));
  console.log(evidence);

  async function verifyNavigation(){
    const basis=(await api('/api/v1/recipes/'+ordinary.recipeId)).value.definition;
    const receipts=[];
    // Explicit virtual configuration for editing/storage only, derived from the already declared source.
    // Never starts a run or grants approval; formal common validation decides whether the input is legal.
    function input(kind){
      const d=structuredClone(basis), materials=['part','second'];
      d.recipeId='';d.version='';d.definitionDigest='';d.catalogDigest='';
      d.fCode='012-NAV-'+kind+'-'+Date.now();d.unitKind=kind;
      d.route=kind==='assembledEntity'?'ordinaryAssembly':'ordinaryBatch';
      d.disposition.physicalUnit=kind==='assembledEntity'?'wholeAssembly':'allGroupMembersIndividuallyToSameReservedGroupCell';
      d.composition=materials.map(material=>({material,localFaces:[1,2]}));
      d.positions[0].members=materials.map(material=>({material,memberPattern:'{UnitId}/'+material,handling:kind==='looseGroup'?'individual':'wholeAssembly'}));
      d.stages=[1,2].map(number=>({number,action:number===1?'none':'flipAffectedMembersOneByOne',angleDeg:null,
        targets:materials.map(material=>({material,localFace:number,cameraPair:number===1?'AB':'CD',captureProfile:'detect',algorithmProfile:'defect'}))}));
      const physical=d.executionPositions[d.positions[0].slotId].physicalEntity;
      const proto=structuredClone(physical.coordinates[0]);
      const object=(material,index)=>{
        const obj=structuredClone(physical);
        obj.coordinates=d.stages.flatMap(stage=>stage.targets[0].cameraPair.split('').map((camera,i)=>{
          const c=structuredClone(proto);c.localFace=stage.number;c.camera=camera;c.stageId='stage:'+stage.number;
          c.pointRef='navigation-'+stage.number+'-'+camera;c.objectPattern='{UnitId}/'+material;
          const ref=kind+'/'+material+'/'+c.stageId+'/'+camera;
          d.captureProfiles[ref]=structuredClone(d.captureProfiles.detect);
          const profile=d.captureProfiles[ref];profile.id=ref;profile.settings.profileId=ref;
          profile.settings.exposureUs=500+index*100+stage.number*10+i;profile.settings.gain=1+index+stage.number/10+i/100;profile.settings.brightnessPercent=20+index*10+stage.number+i;
          c.captureProfile=ref;return c;
        }));
        obj.purposePoints={};
        for(const [ref,purpose] of [['nav-pick','FlipPick'],['nav-put','FlipPutBack']])
          obj.purposePoints[ref]={purpose,point:structuredClone(proto.point),fixed:structuredClone(proto.fixed),coordinateEvidenceReference:'012 navigation explicit virtual source; not production'};
        obj.flip={stages:{'stage:2':{targetPose:{profileId:d.motionProfile,profileVersion:'011-Test-1',poseKey:'navigation-software-face2'},pickPointRef:'nav-pick',putBackPointRef:'nav-put'}}};
        return obj;
      };
      const slot=d.executionPositions[d.positions[0].slotId];
      slot.physicalEntity=object('physical',2);slot.members=Object.fromEntries(materials.map((material,i)=>[material,object(material,i)]));
      // Existing whole entity and distinct inspection part inputs coexist, never part-level handling at runtime.
      if(cellsOnly){
        const second=structuredClone(d.positions[0]);second.slotId='s2';second.cellId='r4:c6';second.physicalSlotIndex=2;second.unitPattern='{TrayRunId}/s2';
        d.positions.push(second);d.capacity=2;d.trayLayout.cells.push({cellId:'r4:c6',row:4,column:6,region:'OK'});
        d.traySlotMapping.bindings.push({cellId:'r4:c6',physicalSlotIndex:2});
        const secondInput=structuredClone(slot);secondInput.slotId='s2';d.executionPositions.s2=secondInput;
        for(const obj of [secondInput.physicalEntity,...Object.values(secondInput.members)])for(const c of obj.coordinates){
          c.slotId='s2';c.physicalSlotIndex=2;const ref='s2/'+c.captureProfile;
          d.captureProfiles[ref]=structuredClone(d.captureProfiles[c.captureProfile]);d.captureProfiles[ref].id=ref;d.captureProfiles[ref].settings.profileId=ref;c.captureProfile=ref;
        }
      }
      return d;
    }
    const inputs=['looseGroup','assembledEntity'].map(input);
    fs.writeFileSync(path.join(evidence,'declared-navigation-inputs.json'),JSON.stringify({source:'configuration/recipe-authoring/ordinary-source.json',
      sourceSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'configuration/recipe-authoring/ordinary-source.json'))).digest('hex'),
      scope:'Explicit virtual storage/editor validation inputs only; no production approval or device run',definitions:inputs},null,2));
    const actualInputs=[];
    for(const definition of inputs){
      const r=await api('/api/v1/recipes','POST',{requestId:crypto.randomUUID(),definition});
      if(r.status!==201){fs.writeFileSync(path.join(evidence,'source-rejection.json'),JSON.stringify(r,null,2));throw Error('Common validator refused navigation source: '+r.status);}
      actualInputs.push(r.value.definition);
    }
    await evaluate("document.querySelector('#btnRecipe').click()");
    await wait("document.querySelector('#recipeAuthoringCatalog').options.length>=5");
    const click=async expression=>evaluate(expression);
    const purpose=async name=>click(`[...document.querySelectorAll('.recipe-nav-button')].find(b=>b.textContent.startsWith(${JSON.stringify(name)})).click()`);
    const selectObject=async name=>click(`document.querySelector('[data-object-material=${JSON.stringify(name)}]').click()`);
    const selectFace=async face=>click(`document.querySelector('[data-local-face="${face}"]').click()`);
    const set=async(stage,camera,label,value)=>click(`(()=>{const card=document.querySelector('.recipe-point-card[data-stage-id="${stage}"][data-camera="${camera}"]');const e=[...card.querySelectorAll('input')].find(e=>e.getAttribute('aria-label')===${JSON.stringify(label)});e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
    const value=async(stage,camera,label)=>evaluate(`(()=>{const card=document.querySelector('.recipe-point-card[data-stage-id="${stage}"][data-camera="${camera}"]');return [...card.querySelectorAll('input')].find(e=>e.getAttribute('aria-label')===${JSON.stringify(label)}).value})()`);
    for(const source of actualInputs){
      await click("document.querySelector('[data-authoring-section=basic]').click()");
      await click(`(()=>{const e=document.querySelector('#recipeAuthoringCatalog');e.value=${JSON.stringify(source.recipeId)};e.dispatchEvent(new Event('change'));})()`);
      await wait(`document.querySelector('[aria-label="检测场景"]')?.value===${JSON.stringify(source.unitKind)}`);
      assert(await evaluate("document.querySelectorAll('[data-cell-id]').length===100"),'First step matrix missing');
      await shot(source.unitKind+'-step1');
      await click("document.querySelector('[data-authoring-section=points]').click()");await purpose('拍照位置');
      assert(await evaluate("document.querySelectorAll('.recipe-detail [data-object-material]').length===2&&document.querySelectorAll('.recipe-slots [data-object-material]').length===0"),'Approved object rail must be in detail');
      const heading=source.unitKind==='looseGroup'?'成员':'检测部位';
      assert(await evaluate(`document.querySelector('.recipe-detail h3').textContent===${JSON.stringify(heading)}`),'Wrong member/part heading');
      await selectObject('part');await selectFace(1);
      if(cellsOnly){
        await click("document.querySelector('[data-cell-id=\"r2:c4\"]').click()");await selectObject('part');await selectFace(1);
        await set('stage:1','A','曝光 (µs)',744);
        await click("document.querySelector('[data-cell-id=\"r4:c6\"]').click()");await selectObject('part');await selectFace(1);
        assert(await value('stage:1','A','曝光 (µs)')!=='744','Cell navigation aliases another physical slot');
        await set('stage:1','A','曝光 (µs)',955);
        await click("document.querySelector('[data-cell-id=\"r2:c1\"]').click()");
        assert(await evaluate("document.querySelector('.recipe-detail h2').textContent.startsWith('NG区')"),'Region navigation lost NG identity');
        await click("document.querySelector('[data-cell-id=\"r2:c4\"]').click()");await selectObject('part');await selectFace(1);
        assert(await value('stage:1','A','曝光 (µs)')==='744','Return from another cell/region lost its values');
        await shot(source.unitKind+'-cell-return');
        await click("document.querySelector('#recipeAuthoringSave').click()");await wait("document.querySelector('#recipeAuthoringNotice').textContent.includes('已保存并重读')");
        const saved=await api('/api/v1/recipes/'+source.recipeId),d=saved.value.definition;
        for(const [id,expected] of [['s1',744],['s2',955]]){
          const c=d.executionPositions[id].members.part.coordinates.find(c=>c.stageId==='stage:1'&&c.camera==='A');
          assert(d.captureProfiles[c.captureProfile].settings.exposureUs===expected,'Saved cell values aliased');
        }
        fs.writeFileSync(path.join(evidence,source.unitKind+'-saved-definition.json'),JSON.stringify(d,null,2));
        receipts.push({kind:source.unitKind,recipeId:d.recipeId,version:d.version,cellSwitchPreserved:true,regionSwitchPreserved:true,fullGet:true,pagePut:true});continue;
      }
      const unchangedB=await value('stage:1','B','曝光 (µs)');
      await set('stage:1','A','曝光 (µs)',711);await set('stage:1','A','增益',1.71);await set('stage:1','A','光源亮度 (%)',41);
      assert(await value('stage:1','B','曝光 (µs)')===unchangedB,'AB must not share edits');
      await selectFace(2);await set('stage:2','C','曝光 (µs)',822);await set('stage:2','C','增益',1.82);await set('stage:2','C','光源亮度 (%)',42);
      await selectObject('second');await selectFace(1);
      assert(await value('stage:1','A','曝光 (µs)')!=='711','Member/part edit leaked');
      await set('stage:1','A','曝光 (µs)',933);await set('stage:1','A','增益',1.93);await set('stage:1','A','光源亮度 (%)',43);
      await selectObject('part');await selectFace(1);assert(await value('stage:1','A','曝光 (µs)')==='711','Switch lost first input');
      await shot(source.unitKind+'-photo-member-part');
      await selectFace(2);assert(await value('stage:2','C','曝光 (µs)')==='822','Face switch lost second input');
      await shot(source.unitKind+'-photo-face2');
      await purpose('翻面取放');
      const whole=source.unitKind==='assembledEntity';
      assert(await evaluate(`document.querySelectorAll('[data-object-material]').length===${whole?0:2}`),'Assembly handling must not select parts');
      assert(await evaluate("document.querySelectorAll('[data-local-face]').length===0"),'Mechanical input must not gain face selectors');
      if(!whole)await selectObject('part');
      await click("(()=>{const e=document.querySelector('.recipe-point-card [aria-label=\"X (mm)\"]');e.value='177';e.dispatchEvent(new Event('change',{bubbles:true}));})()");
      if(!whole){await selectObject('second');assert(await evaluate("document.querySelector('.recipe-point-card [aria-label=\"X (mm)\"]').value!=='177'"),'Independent member handling leaked');await selectObject('part');}
      assert(await evaluate("document.querySelector('.recipe-point-card [aria-label=\"X (mm)\"]').value==='177'"),'Handling value lost');
      await shot(source.unitKind+'-flip');
      await click("document.querySelector('[data-authoring-section=review]').click()");await shot(source.unitKind+'-step3');
      await click("document.querySelector('#recipeAuthoringSave').click()");await wait("document.querySelector('#recipeAuthoringNotice').textContent.includes('已保存并重读')");
      const saved=await api('/api/v1/recipes/'+source.recipeId);assert(saved.status===200&&saved.value.definition.version!==source.version,'Actual page PUT/read required');
      const d=saved.value.definition,slot=d.executionPositions[d.positions[0].slotId];
      const settings=(material,stage,camera)=>{const c=slot.members[material].coordinates.find(c=>c.stageId===stage&&c.camera===camera);return d.captureProfiles[c.captureProfile].settings;};
      for(const [material,stage,camera,exposureUs,gain,brightnessPercent] of [['part','stage:1','A',711,1.71,41],['part','stage:2','C',822,1.82,42],['second','stage:1','A',933,1.93,43]])
        assert(JSON.stringify(['exposureUs','gain','brightnessPercent'].map(k=>settings(material,stage,camera)[k]))===JSON.stringify([exposureUs,gain,brightnessPercent]),'Complete API read aliases capture values');
      assert((whole?slot.physicalEntity:slot.members.part).purposePoints['nav-pick'].point.x===177,'Mechanical owner did not round trip');
      assert((whole?slot.members.part:slot.members.second).purposePoints['nav-pick'].point.x!==177,'Wrong mechanical owner edited');
      // Only three referenced profiles and one actual mechanical point may differ, plus server identities.
      const expected=structuredClone(source),expectedSlot=expected.executionPositions[expected.positions[0].slotId];
      const changedProfiles=[];
      for(const [material,stage,camera] of [['part','stage:1','A'],['part','stage:2','C'],['second','stage:1','A']]){
        const old=expectedSlot.members[material].coordinates.find(c=>c.stageId===stage&&c.camera===camera),now=slot.members[material].coordinates.find(c=>c.stageId===stage&&c.camera===camera);
        expected.captureProfiles[now.captureProfile]=structuredClone(d.captureProfiles[now.captureProfile]);old.captureProfile=now.captureProfile;changedProfiles.push(now.captureProfile);
      }
      (whole?expectedSlot.physicalEntity:expectedSlot.members.part).purposePoints['nav-pick'].point.x=177;
      for(const key of ['version','definitionDigest','catalogDigest'])expected[key]=d[key];
      assert(JSON.stringify(expected)===JSON.stringify(d),'Save dropped/changed unrelated full-body data');
      await click("document.querySelector('[aria-label=\"关闭配方配置\"]').click()");
      await click("document.querySelector('#btnRecipe').click()");await wait("document.querySelector('#recipeAuthoringCatalog').options.length>=5");
      await click(`(()=>{const e=document.querySelector('#recipeAuthoringCatalog');e.value=${JSON.stringify(source.recipeId)};e.dispatchEvent(new Event('change'));})()`);
      await wait("document.querySelector('#recipeAuthoringNotice').textContent.includes('已读取保存')");
      await click("document.querySelector('[data-authoring-section=points]').click()");await purpose('拍照位置');
      await selectObject('second');await selectFace(1);assert(await value('stage:1','A','曝光 (µs)')==='933','Close/reopen must read saved second');
      await selectObject('part');await selectFace(2);assert(await value('stage:2','C','曝光 (µs)')==='822','Close/reopen must read saved first second face');
      await shot(source.unitKind+'-reopened');
      fs.writeFileSync(path.join(evidence,source.unitKind+'-saved-definition.json'),JSON.stringify(d,null,2));
      receipts.push({kind:source.unitKind,recipeId:d.recipeId,version:d.version,definitionDigest:d.definitionDigest,pagePut:true,fullGet:true,closeReopen:true,
        relatedProfiles:changedProfiles,wholeMechanical:whole,unrelatedFieldsPreserved:true,sourceVersion:source.version});
    }
    backend.kill();await new Promise(r=>backend.once('exit',r));backend=startHost();await hostReady();
    for(const receipt of receipts){const read=await api('/api/v1/recipes/'+receipt.recipeId);assert(JSON.stringify(read.value.definition)===JSON.stringify(JSON.parse(fs.readFileSync(path.join(evidence,receipt.kind+'-saved-definition.json')))),'Normal Host restart read differs');receipt.normalRestartRead=true;}
    fs.writeFileSync(path.join(evidence,'navigation-ledger.json'),JSON.stringify({result:'Passed',receipts,apiLedger:ledger,sqliteDatabase:path.join(recipes,'recipes.db'),
      approvedPreviews:['dui-02-group-navigation.html','dui-03-assembly-navigation.html'],hardware:'NotVerified',workflowRunsStarted:0},null,2));
    await cdp('Browser.close').catch(()=>{});
  }

}
try{await main();}catch(error){fs.writeFileSync(path.join(evidence,'failure.json'),JSON.stringify({message:error.message,ledger},null,2));console.error(error);process.exitCode=1;}
finally{browserSocket?.close();pageServer?.close();for(const child of children.reverse())if(child.exitCode===null)child.kill();}
