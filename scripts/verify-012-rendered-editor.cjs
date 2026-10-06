// Real current page -> HTTP Host -> common validator -> isolated SQLite. No mock API or seed privilege.
// Input coordinates/capabilities are declared verification data; this is not device/full-chain evidence.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const crypto = require('node:crypto');
const cp = require('node:child_process');
const WebSocket = require('../frontend/node_modules/ws');
const root = path.resolve(__dirname, '..');
if (root.toLowerCase() !== 'e:\\dzk\\gaode-012-ui-fix') throw Error('Independent workspace required');
const evidence = path.join(root, 'artifacts/recipe-ui-fix-012/rendered', process.argv[2] || String(Date.now()));
fs.mkdirSync(evidence, {recursive:true});
const children = [];
let browserSocket;
const token = crypto.randomBytes(32).toString('hex');
const hostPort=16531, pagePort=16532, cdpPort=16533;
const input = JSON.parse(fs.readFileSync(path.join(root,'artifacts/recipe-ui-fix-012/declared-api-input.json'),'utf8'));
input.fCode = '012-界面保存验收'; input.model = '界面验收对象';
function launch(command,args,options={}) {
  const child=cp.spawn(command,args,{cwd:root,windowsHide:true,...options}); children.push(child);
  fs.writeFileSync(path.join(evidence,'owned-processes.json'),JSON.stringify({owner:process.pid,children:children.map(p=>p.pid)},null,2));return child;
}
async function run(args) {
  const p=launch('dotnet',args,{stdio:'pipe'}); let output='';
  p.stdout.on('data', b=>output+=b);p.stderr.on('data',b=>output+=b);
  await new Promise((resolve,reject)=>{p.on('error',reject);p.on('exit',code=>code===0?resolve():reject(Error(output)));});
}
const pause=ms=>new Promise(r=>setTimeout(r,ms));
async function api(url,method='GET',body,headers={}) {
  const response=await fetch(`http://127.0.0.1:${hostPort}${url}`,{method,headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json',...headers},body:body?JSON.stringify(body):undefined});
  const value=await response.json();return {status:response.status,value,etag:response.headers.get('etag')};
}
function assert(condition,message) {if(!condition)throw Error(message);}
function verifyOperatorDOM(snapshot) {
  const allowed=['料盘编号','检测场景','零件型号','分拣夹爪','检测面数','待检槽数量','相同组合数量','NG槽数量','Pending槽数量','每组成员数','额外E扫码姿态'];
  assert(snapshot.labels.every(label=>allowed.includes(label)||label.endsWith('检测面数')),'Unexpected operator field');
  assert(snapshot.details===0,'Nested backend model editor returned');
}
async function main() {
  const data=path.join(evidence,'data'),runtime=path.join(data,'runtime'),recipes=path.join(data,'recipes');
  assert(!fs.existsSync(data),'Use a fresh evidence directory; do not overwrite a prior database');
  const prep='backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll';
  await run([prep,data,runtime]);await run([prep,'--prepare-recipes',data,recipes]);
  const env=Object.fromEntries(Object.entries(process.env).filter(([k])=>!/^gaode__|^recipestore__/i.test(k)));
  Object.assign(env,{
    ASPNETCORE_ENVIRONMENT:'RecipeUiVerification',Gaode__Mode:'FullSimulation',Gaode__PlcProvider:'Virtual',
    Gaode__TestRoot:runtime,Gaode__AllowedTestRoot:data,
    Gaode__ConfigRoot:path.join(root,'specs/001-station01-public-preparation/examples'),
    Gaode__SchemaRoot:path.join(root,'specs/001-station01-public-preparation/contracts'),
    Gaode__PublicId:'s01-public-dev',Gaode__PublicVersion:'1.0.0',
    Gaode__BudgetId:'s01-budget-dev',Gaode__BudgetVersion:'3.0.0',
    Gaode__SimulationId:'s01-sim-normal',Gaode__SimulationVersion:'3.0.0',
    Gaode__Tokens__Operator:crypto.randomBytes(32).toString('hex'),Gaode__Tokens__SystemAdministrator:token,
    RecipeStore__DatabasePath:path.join(recipes,'recipes.db'),RecipeStore__ReadWriteTimeoutMs:'30000',RecipeStore__DbLockTimeoutSeconds:'10'
  });
  const log=fs.openSync(path.join(evidence,'host.log'),'a');
  const hostArgs=['backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll','--urls',`http://127.0.0.1:${hostPort}`];
  let backend=launch('dotnet',hostArgs,{env,stdio:['ignore',log,log]});
  let ready=false;
  for(let i=0;i<120;i++){try{ready=(await api('/api/v1/recipes/catalog')).status===200;if(ready)break;}catch{}await pause(500);}
  assert(ready,'Actual Host did not become ready');
  const first=await api('/api/v1/recipes','POST',{requestId:crypto.randomUUID(),definition:input});
  assert(first.status===201,`Actual initial ordinary save failed: ${JSON.stringify(first.value)}`);
  const recipeId=first.value.definition.recipeId;
  const server=http.createServer((request,response)=>{
    if(request.url.startsWith('/api/')){
      const proxy=http.request({host:'127.0.0.1',port:hostPort,path:request.url,method:request.method,headers:request.headers},r=>{response.writeHead(r.statusCode,r.headers);r.pipe(response);});
      proxy.on('error',()=>{response.writeHead(502);response.end();});request.pipe(proxy);return;
    }
    const name=request.url.split('?')[0];
    const file=name==='/v3-reference.html' ? 'C:/Users/codexsandboxonline.10_3_0_13/.codex/visualizations/2026/10/03/01a10010-9507-7c21-aa9c-4da07b94a448/recipe-editor-v3.html' : path.join(root,'frontend/dist',name==='/'?'a.html':name.slice(1));
    try{
      let content=fs.readFileSync(file);
      if(name==='/'||name==='/a.html')content=Buffer.from(content.toString().replace('<head>','<head><script>window.__GAODE_HOST_CONFIG__='+JSON.stringify({apiBaseUrl:'',mode:'Test',testToken:token,resourceVersion:'012-ui-fix'})+';</script>'));
      response.setHeader('Content-Type',file.endsWith('.html')?'text/html; charset=utf-8':file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':'application/octet-stream');response.end(content);
    }catch{response.writeHead(404);response.end();}
  });
  await new Promise(r=>server.listen(pagePort,'127.0.0.1',r));
  try {
    const edge=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Microsoft/Edge/Application/msedge.exe'].find(fs.existsSync);
    assert(edge,'Browser unavailable');
    launch(edge,['--headless=new','--disable-gpu','--no-first-run',`--remote-debugging-port=${cdpPort}`,`--user-data-dir=${path.join(evidence,'browser-profile')}`,`http://127.0.0.1:${pagePort}/a.html`],{stdio:'ignore'});
    let targets;
    for(let i=0;i<60;i++){try{targets=await(await fetch(`http://127.0.0.1:${cdpPort}/json/list`)).json();if(targets.some(t=>t.url.includes('/a.html')))break;}catch{}await pause(500);}
    const target=targets.find(t=>t.type==='page'&&t.url.includes('/a.html'));
    const ws=new WebSocket(target.webSocketDebuggerUrl);browserSocket=ws;await new Promise((r,j)=>{ws.once('open',r);ws.once('error',j);});
    let id=0;const pending=new Map();
    ws.on('message',b=>{const d=JSON.parse(b.toString());if(d.id){const p=pending.get(d.id);pending.delete(d.id);d.error?p.reject(Error(d.error.message)):p.resolve(d.result);}});
    const cdp=(method,params={})=>new Promise((resolve,reject)=>{const key=++id;pending.set(key,{resolve,reject});ws.send(JSON.stringify({id:key,method,params}));});
    const evaluate=async expression=>{const r=await cdp('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
    const wait=async expression=>{for(let i=0;i<100;i++){if(await evaluate(expression))return;await pause(100);}throw Error('Page condition failed: '+expression);};
    const shot=async name=>{const r=await cdp('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(evidence,name+'.png'),Buffer.from(r.data,'base64'));};
    await cdp('Emulation.setDeviceMetricsOverride',{width:1600,height:1000,deviceScaleFactor:1,mobile:false});
    await wait("!!window.GaodeRecipeAuthoring && typeof document.querySelector('#recipeAuthoringNew')?.onclick==='function'");
    await evaluate("document.querySelector('#btnRecipe').click()");
    await wait("!document.querySelector('#recipeAuthoringCatalog').disabled && document.querySelector('#recipeAuthoringCatalog').options.length>1");
    await evaluate(`(()=>{const e=document.querySelector('#recipeAuthoringCatalog');e.value=${JSON.stringify(recipeId)};e.dispatchEvent(new Event('change'));})()`);
    await wait("!!document.querySelector('[aria-label=\"分拣夹爪\"]')");
    const basic=await evaluate("({labels:[...document.querySelectorAll('#recipeAuthoringForm label')].map(x=>x.querySelector(':scope > span')?.textContent||x.innerText),text:document.querySelector('#recipeAuthoringForm').innerText,details:document.querySelectorAll('#recipeAuthoringForm details').length})");
    for(const field of ['配置来源','路线引用','Pattern','配置键','阶段引用','内容摘要','准入依据','算法','ROI','光源通道','高级设置'])assert(!basic.text.includes(field),'Forbidden operator content: '+field);
    verifyOperatorDOM(basic);
    const domSnapshot="({labels:[...document.querySelectorAll('#recipeAuthoringForm label')].map(x=>x.querySelector(':scope > span')?.textContent||x.innerText),details:document.querySelectorAll('#recipeAuthoringForm details').length})";
    await evaluate("(()=>{const e=document.createElement('label');e.id='negative-field';e.textContent='配置来源';e.append(document.createElement('input'));document.querySelector('#recipeAuthoringForm').append(e);})()");
    let rejected=false;try{verifyOperatorDOM(await evaluate(domSnapshot));}catch{rejected=true;}assert(rejected,'Actual extra technical input was not rejected');await evaluate("document.querySelector('#negative-field').remove()");
    await evaluate("(()=>{const e=document.createElement('details');e.id='negative-nesting';document.querySelector('#recipeAuthoringForm').append(e);})()");
    rejected=false;try{verifyOperatorDOM(await evaluate(domSnapshot));}catch{rejected=true;}assert(rejected,'Actual nested model container was not rejected');await evaluate("document.querySelector('#negative-nesting').remove()");
    await shot('01-basic');
    await evaluate("document.querySelector('[data-authoring-section=points]').click()");
    await wait("document.querySelectorAll('.recipe-point-card').length>=2");
    const coordinate=await evaluate("({cards:[...document.querySelectorAll('.recipe-point-card h3')].map(x=>x.innerText),columns:document.querySelector('.recipe-editor-layout').children.length,navigation:document.querySelector('.recipe-nav').innerText,objects:document.querySelector('.recipe-slots').innerText})");
    assert(coordinate.columns===3,'Prototype object/face/card navigation replaced by another form');
    assert(coordinate.cards.includes('C相机拍照点')&&coordinate.cards.includes('D相机拍照点'),'Actual pair photo cards absent');
    await shot('02-coordinates');
    await evaluate("(()=>{const e=document.querySelector('.recipe-point-card input[aria-label=\"曝光 (µs)\"]');e.value='777';e.dispatchEvent(new Event('change'));})()");
    await evaluate("document.querySelector('[data-authoring-section=basic]').click();(()=>{const e=document.querySelector('[aria-label=\"分拣夹爪\"]');e.value='2';e.dispatchEvent(new Event('change'));})()");
    await evaluate("document.querySelector('#recipeAuthoringCheck').click()");
    await wait("document.querySelector('#recipeAuthoringNotice').innerText.includes('检查通过')");
    await shot('03-check-save');
    await evaluate("document.querySelector('#recipeAuthoringSave').click()");
    await wait("document.querySelector('#recipeAuthoringNotice').innerText.includes('已保存并重读')");
    const saved=await api('/api/v1/recipes/'+recipeId);
    assert(saved.status===200 && saved.value.definition.sortingGripperId===2,'Real gripper edit did not persist');
    const edited=saved.value.definition.executionPositions['slot-one'].physicalEntity.coordinates[0];
    assert(saved.value.definition.captureProfiles[edited.captureProfile].settings.exposureUs===777,'Real page parameter did not persist');
    assert(first.value.definition.definitionDigest!==saved.value.definition.definitionDigest,'Content identity failed to change');
    const reject=await api('/api/v1/recipes/'+recipeId,'PUT',{requestId:crypto.randomUUID(),definition:first.value.definition},{'If-Match':first.etag});
    assert(reject.status===412,'Actual stale version was not rejected');
    await evaluate("document.querySelector('#recipeAuthoringNew').click()");
    const setInput=async(label,value)=>evaluate(`(()=>{const e=document.querySelector('[aria-label='+${JSON.stringify(JSON.stringify(label))}+']');if(!e)throw Error('Input absent');e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('change'));})()`);
    await wait("document.querySelector('#recipeAuthoringNotice').innerText.includes('请填写当前配方')");
    await setInput('料盘编号','012-界面新建验收');
    assert(await evaluate("document.querySelector('[aria-label=\"分拣夹爪\"]').value===''"),'New draft silently chose a gripper');
    await setInput('分拣夹爪',1);
    await evaluate("document.querySelector('[data-authoring-section=points]').click()");
    const sectionCount=await evaluate("document.querySelectorAll('.recipe-nav-button').length");
    for(let section=0;section<sectionCount;section++) {
      await evaluate(`document.querySelectorAll('.recipe-nav-button')[${section}].click()`);
      const count=await evaluate("document.querySelectorAll('.recipe-point-card').length");
      for(let card=0;card<count;card++) {
        const countInputs=await evaluate(`document.querySelectorAll('.recipe-point-card')[${card}].querySelectorAll('input').length`);
        for(let field=0;field<countInputs;field++) await evaluate(`(()=>{const e=document.querySelectorAll('.recipe-point-card')[${card}].querySelectorAll('input')[${field}];const label=e.getAttribute('aria-label');e.value=label.startsWith('曝光')?String(400+${section}*23+${card}):label==='增益'?String(1+${card}/10):label.startsWith('光源')?String(20+${section}+${card}):String(10+${section}+${field}/10);e.dispatchEvent(new Event('change'));})()`);
      }
    }
    await evaluate("document.querySelector('#recipeAuthoringCheck').click()");
    await wait("document.querySelector('#recipeAuthoringNotice').innerText.includes('检查通过')");
    await evaluate("document.querySelector('#recipeAuthoringSave').click()");
    await wait("document.querySelector('#recipeAuthoringNotice').innerText.includes('已保存并重读')");
    const catalog=await api('/api/v1/recipes/catalog');
    const newId=catalog.value.items.find(i=>i.fCode==='012-界面新建验收')?.recipeId;
    assert(newId,'New modal did not save to the real catalog');const newSaved=await api('/api/v1/recipes/'+newId);
    assert(newSaved.value.definition.sortingGripperId===1,'New modal explicit gripper did not persist');
    const beforeRestart=backend.pid;await new Promise(resolve=>{backend.once('exit',resolve);backend.kill();});
    backend=launch('dotnet',hostArgs,{env,stdio:['ignore',log,log]});
    let afterRestart;
    for(let i=0;i<120;i++){try{afterRestart=await api('/api/v1/recipes/'+newId);if(afterRestart.status===200)break;}catch{}await pause(500);}
    assert(afterRestart?.status===200 && JSON.stringify(afterRestart.value.definition)===JSON.stringify(newSaved.value.definition),'Normal Host restart lost the saved complete definition');
    const restart={previousOwnedHost:beforeRestart,currentOwnedHost:backend.pid,status:afterRestart.status,completeDefinitionUnchanged:true};
    // Read-only reference rendering, with its own original JavaScript. No demo data enters the Host.
    await cdp('Page.navigate',{url:`http://127.0.0.1:${pagePort}/v3-reference.html`});await wait("!!document.querySelector('.setup-grid')");await shot('v3-reference-basic');
    const reference=await evaluate("({steps:[...document.querySelectorAll('.step')].map(x=>x.innerText),basic:document.querySelector('.setup-grid').innerText})");
    await evaluate('step=2;render()');await shot('v3-reference-coordinates');
    reference.coordinate=await evaluate("({navigation:document.querySelector('#sectionNav')?.innerText,objects:document.querySelector('#slotList')?.innerText,cards:[...document.querySelectorAll('.point-card h3')].map(x=>x.innerText)})");
    await evaluate('step=3;render()');await shot('v3-reference-check');
    reference.review=await evaluate("document.querySelector('.review-table').innerText");
    fs.writeFileSync(path.join(evidence,'page-evidence.json'),JSON.stringify({source:'RealRenderedCurrentModal/ActualHostAPI/CommonValidation/SQLite',hardwareApplied:false,basic,coordinate,reference,negativeTechnicalInputRejected:true,negativeNestedEditorRejected:true,restart,recipeId,status:saved.status,staleVersionStatus:reject.status,savedDefinition:saved.value.definition,initialDefinition:first.value.definition,newDefinition:newSaved.value.definition},null,2));
    ws.close();console.log('Actual modal three steps, parameter/gripper edit, HTTP save/read, and stale rejection passed');
  }finally{browserSocket?.terminate();server.closeAllConnections();await new Promise(r=>server.close(r));}
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{browserSocket?.terminate();for(const child of children.reverse())if(child.exitCode===null)child.kill();});
