import {reviewRenderedEditor,renderedEditorExpression} from '../frontend/scripts/recipe-layout-review.mjs';
// 012 T047: migrated observer helper only, current main assets. Original stable helper SHA e352b6fea23307b57bc9233b2fd9de0e2dc916a5ecd86d02d2db5fd60262ee5e; not a product baseline replacement.
// Read-only same-run page observer. 014 T016 owns Host/PLC/worker startup and commands.
import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import net from 'node:net';
import {forwardPageRequest} from './014-page-proxy.mjs';
async function servePageAsset(req,res,dist){const file=path.resolve(dist,decodeURIComponent(req.url.split('?')[0]).replace(/^\//,'')||'a.html');if(!file.startsWith(dist+path.sep)){res.writeHead(403).end();return;}try{const bytes=await fs.readFile(file);res.setHeader('content-type',({'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.png':'image/png','.svg':'image/svg+xml'})[path.extname(file)]||'application/octet-stream');res.end(bytes);}catch(e){if(e.code==='ENOENT'){res.writeHead(404).end();return;}throw e;}}

const workspace = path.resolve(import.meta.dirname, '..');
// Browser cold start is preparation, before the owned Host and business run.
const control=path.resolve(process.argv[3]);
const owned016=process.env.GAODE_016_TEST_ROOT!==undefined;
if(owned016&&path.resolve(process.env.GAODE_016_TEST_ROOT)!==path.join(workspace,'artifacts/016-public-tray-flow'))throw Error('016 owned observer root mismatch');
if(owned016&&(process.env.GAODE_014_JOINT_ROOT||process.env.GAODE_013_ATTEMPT_ROOT))throw Error('Mixed observer ownership rejected');
const controlAllowed=path.join(workspace,owned016?'artifacts/016-public-tray-flow':'artifacts/014-012-joint')+path.sep;
if(!control.startsWith(controlAllowed)||!path.basename(control).startsWith('control-'))throw Error('Owned observer control required');
const abortPath=path.join(control,'observer-abort.json');
async function checkAbort(){try{await fs.access(abortPath);}catch{return;}throw Error('Owned driver requested observer cleanup; not a successful run');}
const profile=path.join(control,'edge-profile');
const edge=spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',[
 '--headless=new','--remote-debugging-port=0','--user-data-dir='+profile,
 '--no-first-run','--no-default-browser-check','--disable-background-networking',
 '--window-size=1680,1050','about:blank'],{windowsHide:true,stdio:'ignore'});
try {
 const coldEnd=Date.now()+15000;
 while(true){try{await fs.access(path.join(profile,'DevToolsActivePort'));break;}catch{if(Date.now()>coldEnd)throw Error('Browser prewarm timed out');await new Promise(r=>setTimeout(r,100));}}
 await fs.writeFile(path.join(control,'browser-prewarm.json'),JSON.stringify({state:'BrowserPreparedOnly',pid:edge.pid,profile,apiObserved:false,businessStarted:false}));
 const preparationEnd=Date.now()+180000;
 while(true){await checkAbort();try{await fs.access(process.argv[2]);break;}catch{if(Date.now()>preparationEnd)throw Error('Actual driver connection missing');await new Promise(r=>setTimeout(r,100));}}
} catch(error){edge.kill();throw error;}
const config = JSON.parse((await fs.readFile(process.argv[2], 'utf8')).replace(/^\uFEFF/, ''));
const startupDiagnostic = config.diagnosticOnly === '014-startup-comparison';
if (startupDiagnostic && !['full', 'reduced'].includes(config.diagnosticCollector))
  throw Error('Explicit diagnostic collector required');
const token=await new Promise((resolve,reject)=>{const pipe=net.connect('\\\\.\\pipe\\'+config.tokenPipeName);let text='';pipe.on('data',b=>{text+=b.toString();if(text.includes('\n')){resolve(text.trim());pipe.end();}});pipe.on('error',reject);});
const upstream = new URL(config.apiBaseUrl);
if (upstream.hostname !== '127.0.0.1' || !token || !Number.isInteger(config.observationTimeoutMs) || config.observationTimeoutMs <= 0)
  throw new Error('Actual loopback API, token and finite observation timeout required');
const evidence = path.resolve(config.evidenceRoot);
const allowed = path.join(workspace, owned016?'artifacts/016-public-tray-flow/pages':'artifacts/014-012-joint/pages') + path.sep;
if (!evidence.startsWith(allowed)) throw new Error('012 isolated evidence root required');
await fs.mkdir(path.dirname(evidence), { recursive: true });
await fs.mkdir(evidence); // Never overwrite an earlier attempt.
const dist = path.join(workspace, 'frontend/dist');
const network = [], rendered = [], notifications = [], errors = [], captures = [];
const hashes = {};
for (const name of ['a.html', 'runtime.js', 'recipe-authoring.js'])
  hashes[name] = createHash('sha256').update(await fs.readFile(path.join(dist, name))).digest('hex');
const server = http.createServer(async (req, res) => {
  try {
    if (req.url.startsWith('/api/') || req.url.startsWith('/hubs/')) {
      await forwardPageRequest(req,res,upstream,item=>network.push(item));return;
    }
    await servePageAsset(req, res, dist);
  } catch (error) { errors.push({ kind: 'ProxyFailure', message: error.message }); res.writeHead(502).end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const origin = 'http://127.0.0.1:' + server.address().port;
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
let socket, id = 0, finalObserved = false, sameRun = null, modalRead = null;
const pending = new Map();
async function wait(check, milliseconds) {
  const until = Date.now() + milliseconds;
  while (Date.now() < until) { await checkAbort(); const result = await check(); if (result) return result; await delay(100); }
  throw new Error('Page observer preparation timed out');
}
try {
  const port = await wait(async () => { try { return (await fs.readFile(path.join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]; } catch { return null; } }, 15000);
  const targets = await (await fetch('http://127.0.0.1:' + port + '/json/list')).json();
  socket = new WebSocket(targets.find(target => target.type === 'page').webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); });
  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data);
    if (message.id) { const task = pending.get(message.id); if (task) { pending.delete(message.id); message.error ? task.reject(new Error(message.error.message)) : task.resolve(message.result); } }
    else if (message.method === 'Runtime.exceptionThrown') errors.push({ kind: 'PageException', details: message.params.exceptionDetails });
  });
  const cdp = (method, params = {}) => new Promise((resolve, reject) => {
    const request = ++id;
    const timeout=setTimeout(()=>{pending.delete(request);reject(Error('Observer CDP operation timed out: '+method));},5000);
    pending.set(request,{resolve:value=>{clearTimeout(timeout);resolve(value);},reject:error=>{clearTimeout(timeout);reject(error);}});
    socket.send(JSON.stringify({ id: request, method, params }));
  });
  const evaluate = async expression => {
    const result = await cdp('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    return result.result.value;
  };
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source:
    'window.__GAODE_HOST_CONFIG__=' + JSON.stringify({ mode: 'Test', apiBaseUrl: origin, signalrUrl: origin + '/hubs/station01', testToken: token }) +
    (config.expectedRunId ? ';window.localStorage.setItem(' + JSON.stringify('gaode:station01:view-run:' + origin + ':Test:') + ',' + JSON.stringify(config.expectedRunId) + ')' : '') +
    ';window.__jointRendered=[];window.__jointNotifications=[];' +
    'window.addEventListener("station01:rendered",e=>window.__jointRendered.push({at:new Date().toISOString(),...e.detail}));' +
    'window.addEventListener("station01:notification",e=>window.__jointNotifications.push({at:new Date().toISOString(),...e.detail}));' });
  await cdp('Page.navigate', { url: origin + '/a.html' });
  await wait(async () => network.some(item => item.path === '/api/v1/station01/status' && item.status === 200) && await evaluate('!!window.station01'), 15000);
  // Publish only after the complete readiness document is closed. Exists/read
  // must not race an open Windows writer or observe a partially written JSON.
  const readyPending=path.join(evidence,'ready.pending.json');
  await fs.writeFile(readyPending, JSON.stringify({ state: 'ObserverReadyBeforeRun', apiBaseUrl: config.apiBaseUrl, sourceHashes: hashes, evidenceRoot: evidence }), {flag:'wx'});
  await fs.rename(readyPending,path.join(evidence,'ready.json'));
  let last = '', count = 0;
  const until = Date.now() + config.observationTimeoutMs;
  while (Date.now() < until) {
    await checkAbort();
    if (startupDiagnostic) {
      try { await fs.access(path.join(control, 'startup-diagnostic-stop.json')); break; } catch {}
      // The actual page/API/SignalR remain active in both comparisons. Reduced
      // removes only this extra automation read, never a product subscription.
      if (config.diagnosticCollector === 'reduced') { await delay(250); continue; }
    }
    const view = await evaluate('({rendered:window.__jointRendered.splice(0),notifications:window.__jointNotifications.splice(0),header:document.getElementById("selectedRecipeName")?.textContent,version:document.getElementById("selectedRecipeVersion")?.textContent,diagnosis:document.getElementById("faultList")?.textContent})');
    rendered.push(...view.rendered); notifications.push(...view.notifications);
    const current = rendered.at(-1);
    if(!startupDiagnostic&&current&&['Blocked','Failed','Cancelled','TimedOut','RecoveryRequired'].includes(current.state))throw Error('Actual same-run state '+current.state+'; '+view.diagnosis);
    if (current?.runId) {
      sameRun ||= current.runId;
      if (current.runId !== sameRun || config.expectedRunId && current.runId !== config.expectedRunId) throw new Error('Page run identity mismatch');
      const frozenRef = network.filter(item => item.path === '/api/v1/station01/runs/' + sameRun && item.status === 200).at(-1)?.response?.recipeExecution;
      const frozenId = frozenRef?.recipeId;
      const terminalRendered = current.final === true && current.facts.some(([name, value]) => name === '运行状态' && value === 'Completed') && current.facts.some(([name, value]) => name === '整盘状态' && value === 'FinalUnloadCompletion');
      // Keep all API/notification/render facts during motion; perform the expensive
      // read-only modal review after the actual terminal state, in this same run.
      if (!modalRead && frozenId && terminalRendered) {
        await evaluate('document.getElementById("btnRecipe").click()');
        await wait(() => evaluate('Array.from(document.getElementById("recipeAuthoringCatalog").options).some(option=>option.value===' + JSON.stringify(frozenId) + ')'), 10000);
        await evaluate('(()=>{const select=document.getElementById("recipeAuthoringCatalog");select.value=' + JSON.stringify(frozenId) + ';select.dispatchEvent(new Event("change",{bubbles:true}));})()');
        const read = await wait(async () => network.find(item => item.path === '/api/v1/recipes/' + frozenId && item.status === 200), 10000);
        // The API and DOM refresh asynchronously. Observe the actual frozen header
        // after rendering; do not label the preceding pre-bind view as frozen.
        const frozenHeader = await wait(() => evaluate('(()=>{const version=document.getElementById("selectedRecipeVersion")?.textContent;return version===' + JSON.stringify(frozenRef.version) + '?{header:document.getElementById("selectedRecipeName")?.textContent,version}:null;})()'), 10000);
        modalRead = { recipeId: frozenId, etag: read.etag, definition: read.response.definition,
          runFrozenHeader: frozenHeader.header, runFrozenVersion: frozenHeader.version };
        await delay(100);
        const modalScreenshot = await cdp('Page.captureScreenshot', { format: 'png' });
        await fs.writeFile(path.join(evidence, 'modal-same-run-read.png'), Buffer.from(modalScreenshot.data, 'base64'));
        for(const [tab,name] of [['basic','same-run-step1'],['points','same-run-step2'],['review','same-run-step3']]) {
          await evaluate(`document.querySelector('[data-authoring-section=${tab}]').click()`);
          const view=await evaluate(renderedEditorExpression);const review=reviewRenderedEditor(view);if(!review.passed)throw Error('Rendered editor rejected: '+review.errors.join(','));await fs.writeFile(path.join(evidence,name+'-render-review.json'),JSON.stringify({view,review},null,2));const screen=await cdp('Page.captureScreenshot',{format:'png'});
          await fs.writeFile(path.join(evidence,name+'.png'),Buffer.from(screen.data,'base64'));
          if(tab==='basic'){
            await evaluate("document.querySelector('#recipeAuthoringForm').scrollTop=document.querySelector('#recipeAuthoringForm').scrollHeight");
            const matrix=await cdp('Page.captureScreenshot',{format:'png'});await fs.writeFile(path.join(evidence,'same-run-step1-matrix.png'),Buffer.from(matrix.data,'base64'));
            await evaluate("document.querySelector('#recipeAuthoringForm').scrollTop=0");
          }
        }
        await evaluate('closeRecipe()');
      }
      const signature = JSON.stringify([current.state, view.header, view.version, terminalRendered]);
      if (signature !== last) {
        last = signature;
        captures.push({ observedAtUtc: new Date().toISOString(), runId: sameRun, state: current.state, header: view.header, version: view.version, diagnosis: view.diagnosis });
        const screenshot = await cdp('Page.captureScreenshot', { format: 'png' });
        await fs.writeFile(path.join(evidence, 'page-' + (++count) + '.png'), Buffer.from(screenshot.data, 'base64'));
      }
      // Final evidence may arrive before the following run query finishes.
      // Keep observing until the original UI also renders its actual terminal
      // snapshot; do not substitute the earlier awaiting-removal rows.
      if (current.final === true && current.facts.some(([name, value]) => name === '运行状态' && value === 'Completed') &&
          current.facts.some(([name, value]) => name === '整盘状态' && value === 'FinalUnloadCompletion')) {
        finalObserved = true; break;
      }
    }
    await delay(250);
  }
  if (startupDiagnostic) {
    const view=await evaluate('({rendered:window.__jointRendered.splice(0),notifications:window.__jointNotifications.splice(0)})');
    rendered.push(...view.rendered);notifications.push(...view.notifications);
  } else if (!finalObserved) throw new Error('No actual same-run Final page observed within the observer budget');
} catch (error) { errors.push({ kind: 'ObserverFailure', message: error.message }); process.exitCode = 1; }
finally {
  await fs.writeFile(path.join(evidence, 'page-evidence.json'), JSON.stringify({ source: 'Actual Edge page via transparent real API proxy',
    sourceHashes: hashes, runId: sameRun, finalObserved, modalRead, network, rendered, notifications, captures, errors,
    businessWritesSentByObserver: false, jointLaunchOwner: owned016 ? '016 received014 actual process' : '014 T016', diagnosticOnly: startupDiagnostic,
    diagnosticCollector: startupDiagnostic ? config.diagnosticCollector : null,
    status: startupDiagnostic ? 'StartupDiagnosticNotAcceptance' : finalObserved && errors.length === 0 ? 'CapturedPendingJointReconciliation' : 'Incomplete' }, null, 2));
  socket?.close(); edge.kill(); server.closeAllConnections(); server.close();
}
