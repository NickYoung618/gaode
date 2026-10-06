// Isolated Test evidence: operate only existing WPF/WebView2 controls through CDP mouse input.
// Never persist authorization headers, SignalR token URLs, or host-injected configuration.
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const WebSocket = require('../frontend/node_modules/ws');
const { notificationsFromResponse } = require('./page-notification-evidence.cjs');
const [portArg, root, mode = 'Normal', recipeId] = process.argv.slice(2);
if (!portArg || !root || !['Preflight', 'Normal', 'Recipe', 'Auth401', 'Auth403'].includes(mode) ||
    (['Recipe', 'Auth401', 'Auth403'].includes(mode) && !recipeId))
  throw new Error('port root Normal|Recipe|Auth401|Auth403 [recipeId] required');
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
// Q02 has four captures and six real worker calls. This is only the evidence
// collector's wait limit; business deadlines and the 3-second heartbeat stay unchanged.
const pageCasePath = path.join(root, 'page-case.json');
const pageCase = fs.existsSync(pageCasePath) ? JSON.parse(fs.readFileSync(pageCasePath, 'utf8').replace(/^\uFEFF/, '')) : {};
const collectorWaitMs = mode === 'Preflight' ? 30000 : pageCase.collectorWaitMs || (mode === 'Recipe' ? 420000 : mode === 'Normal' ? 210000 : 30000);
const timeoutAt = Date.now() + collectorWaitMs;
const report = { source: 'Test/actual-WPF-WebView2-CDP-mouse', mode, startedAtUtc: new Date().toISOString(), interactions: [], network: [], notifications: [], pageDiagnostics: [], states: [] };
report.collectorWaitMs = collectorWaitMs;
report.recipeHeaderCheck = mode === 'Recipe';
const prefix = mode.toLowerCase();
;(async () => {
let socket;
const pending = new Map(), requests = new Map();
const lastQueryBody = new Map();
function rejectPending(error) {
  for (const p of pending.values()) { clearTimeout(p.timer); p.reject(error); }
  pending.clear();
}
async function boundedFetch(url, options = {}) {
  return fetch(url, { ...options, signal: AbortSignal.timeout(10000) });
}
try {
const targets = await (await boundedFetch(`http://127.0.0.1:${Number(portArg)}/json`)).json();
const target = targets.find(t => t.type === 'page' && t.url.startsWith('https://appassets.local/'));
if (!target) throw new Error('Actual WPF WebView2 appassets target missing');
report.target = { id: target.id, title: target.title, url: target.url };
socket = new WebSocket(target.webSocketDebuggerUrl, { handshakeTimeout: 10000 });
socket.on('error', error => rejectPending(error));
socket.on('close', () => rejectPending(new Error('CDP connection closed')));
await new Promise((resolve, reject) => {
  const timer = setTimeout(() => { socket.terminate(); reject(new Error('CDP connection timeout')); }, 10000);
  socket.once('open', () => { clearTimeout(timer); resolve(); });
  socket.once('error', error => { clearTimeout(timer); reject(error); });
  socket.once('close', () => { clearTimeout(timer); reject(new Error('CDP closed before open')); });
});
let nextId = 0;
function call(method, params = {}) {
  return new Promise((resolve, reject) => {
    if (socket.readyState !== WebSocket.OPEN) return reject(new Error('CDP not connected'));
    const id = ++nextId;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error(`CDP request timeout: ${method}`)); }, 10000);
    pending.set(id, { resolve, reject, timer });
    socket.send(JSON.stringify({ id, method, params }), error => {
      if (error && pending.delete(id)) { clearTimeout(timer); reject(error); }
    });
  });
}
async function evaluate(expression) {
  const value = await call('Runtime.evaluate', { expression, returnByValue: true });
  if (value.exceptionDetails) throw new Error(value.exceptionDetails.text);
  return value.result.value;
}
async function until(fn) {
  while (Date.now() < timeoutAt) { const value = await fn(); if (value) return value; await wait(200); }
  throw new Error('WebView2 evidence deadline exceeded');
}
async function screenshot(name) {
  const data = await call('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  fs.writeFileSync(path.join(root, `${prefix}-${name}.png`), Buffer.from(data.data, 'base64'));
}
async function state(name) {
  const dom = await evaluate(`({url:location.href,recipeName:document.getElementById('selectedRecipeName')?.innerText||null,recipeVersion:document.getElementById('selectedRecipeVersion')?.innerText||null,verdict:document.getElementById('verdictBig')?.innerText||null,fault:document.getElementById('faultList')?.innerText||null,items:document.getElementById('itemList')?.innerText||null,parameters:document.getElementById('paramList')?.innerText||null,defects:document.getElementById('defectList')?.innerText||null,stages:Array.from(document.querySelectorAll('#moduleGrid > div')).map(x=>x.innerText.slice(0,180))})`);
  report.states.push({ atUtc: new Date().toISOString(), name, ...dom });
  return dom;
}
async function click(selector, name) {
  const point = await evaluate(`(() => {const e=document.querySelector(${JSON.stringify(selector)});if(!e)return null;const r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()`);
  if (!point) throw new Error(`${name} control missing`);
  await call('Input.dispatchMouseEvent', { type:'mouseMoved', ...point });
  await call('Input.dispatchMouseEvent', { type:'mousePressed', ...point, button:'left', clickCount:1 });
  await call('Input.dispatchMouseEvent', { type:'mouseReleased', ...point, button:'left', clickCount:1 });
  report.interactions.push({ atUtc:new Date().toISOString(), action:name, point });
}
async function key(keyName, code) {
  await call('Input.dispatchKeyEvent', { type:'keyDown', key:keyName, code });
  await call('Input.dispatchKeyEvent', { type:'keyUp', key:keyName, code });
}
socket.on('message', raw => {
  const m = JSON.parse(raw.toString());
  if (m.id) { const p = pending.get(m.id); if (p) { clearTimeout(p.timer); pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); } return; }
  const p = m.params || {};
  if (m.method === 'Fetch.requestPaused') {
    const deniedStart = p.request.method === 'POST' && new URL(p.request.url).pathname === '/api/v1/station01/runs';
    const headers = Object.entries(p.request.headers).filter(([name]) => !deniedStart || name.toLowerCase() !== 'authorization')
      .map(([name, value]) => ({ name, value: String(value) }));
    if (deniedStart && mode === 'Auth403') headers.push({ name: 'Authorization', value: `Bearer ${process.env.GAODE_TEST_READ_ONLY_TOKEN}` });
    if (deniedStart) report.authorizationOverride.requestCount++;
    call('Fetch.continueRequest', { requestId: p.requestId, headers }).catch(rejectPending);
  }
  if (m.method === 'Runtime.consoleAPICalled' && p.args?.[0]?.value === 'GaodePageDiagnostic') {
    try { report.pageDiagnostics.push({ atUtc:new Date().toISOString(), ...JSON.parse(p.args[1].value) }); } catch {}
  }
  if (m.method === 'Network.requestWillBeSent') {
    const url = p.request?.url || '';
    const api = url.includes('/api/v1/station01/');
    const hub = url.includes('/hubs/station01');
    if (!api && !hub) return;
    const entry = { atUtc:new Date().toISOString(), requestId:p.requestId, method:p.request.method,
      path:new URL(url).pathname, kind:api?'api':'signalr' };
    if (api) entry.url = new URL(url).origin + entry.path;
    if (api && p.request.postData && entry.path === '/api/v1/station01/runs') {
      try { entry.requestBody = JSON.parse(p.request.postData); } catch { entry.requestBody = 'unparseable'; }
    }
    requests.set(p.requestId, entry); report.network.push(entry);
  }
  if (m.method === 'Network.responseReceived' && requests.has(p.requestId)) requests.get(p.requestId).status = p.response.status;
  if (m.method === 'Network.loadingFinished' && requests.has(p.requestId)) {
    const entry = requests.get(p.requestId);
    if (entry.kind !== 'api' && entry.kind !== 'signalr') return;
    call('Network.getResponseBody', { requestId:p.requestId }).then(body => {
      if (entry.kind === 'api') {
        if (body.base64Encoded) body.body = Buffer.from(body.body, 'base64').toString('utf8');
        const queryKey = `${entry.path}:${entry.status}`;
        const digest = createHash('sha256').update(body.body).digest('hex');
        const previous = entry.method === 'GET' ? lastQueryBody.get(queryKey) : null;
        if (previous?.digest === digest) {
          entry.responseBodySha256 = digest;
          entry.repeatedBodyOf = previous.requestId;
        } else {
          try { entry.responseBody = JSON.parse(body.body); } catch { entry.responseBody = body.body.slice(0,1000); }
          if (entry.method === 'GET') lastQueryBody.set(queryKey, { digest, requestId: entry.requestId });
        }
      }
      else if (entry.path === '/hubs/station01' && entry.method === 'GET' && entry.status === 200) {
        for (const event of notificationsFromResponse(body))
          report.notifications.push({ atUtc:new Date().toISOString(), requestId:p.requestId, ...event });
      }
    }).catch(error => {
      if (entry.kind === 'signalr' && entry.method === 'GET' && entry.status === 200)
        (report.notificationReadErrors ??= []).push({ requestId:p.requestId, reason:error.message });
    });
  }
});
  await Promise.all(['Page.enable','Runtime.enable','Network.enable'].map(x => call(x)));
  await until(async () => (await evaluate('location.pathname')) === '/login.html');
  await screenshot('01-login');
  await click('button[type="submit"]', 'click-login');
  await until(async () => (await evaluate('location.pathname')) === '/prototype.html');
  await until(async () => await evaluate('!!document.getElementById("verdictBig")'));
  await wait(500);
  await state('before-start'); await screenshot('02-before-start');
  if (mode === 'Preflight') {
    report.outcome = 'ConnectedAndExited';
    report.completedAtUtc = new Date().toISOString();
    return;
  }
  if (['Recipe', 'Auth401', 'Auth403'].includes(mode)) {
    await click('#btnRecipe', 'open-recipe-selection');
    const selectedIndex = await until(async () => evaluate(`(() => {
      const e=document.getElementById('recipeCatalogSelect');
      if (!e || !e.options.length) return null;
      const index=Array.from(e.options).findIndex(x=>x.textContent.includes(${JSON.stringify(recipeId)}));
      return index<0 ? null : index+1;
    })()`)) - 1;
    if (selectedIndex < 0) throw new Error(`Recipe ${recipeId} absent from catalog`);
    await click('#recipeCatalogSelect', 'focus-recipe-list');
    await key('Home', 'Home');
    for (let i = 0; i < selectedIndex; i++) await key('ArrowDown', 'ArrowDown');
    await key('Enter', 'Enter');
    await click('#recipeChooseButton', 'choose-recipe');
    if (await evaluate(`(() => {
      const m=document.getElementById('recipeModal');
      return !!m && getComputedStyle(m).display !== 'none';
    })()`)) await key('Escape', 'close-recipe-modal-after-choice');
    report.recipeSelection = await evaluate(`({selected:document.getElementById('selectedRecipeName')?.innerText||null,
      version:document.getElementById('selectedRecipeVersion')?.innerText||null,
      restriction:document.getElementById('recipeCatalogState')?.value||null})`);
    await state('after-recipe-choice'); await screenshot('02-recipe-choice');
    if (!report.recipeSelection.selected || report.recipeSelection.selected === '未选用配方')
      throw new Error('RecipeRestrictedOrSelectionNotCommitted');
  }
  const selector = 'button.bg-emerald-600';
  if (mode === 'Auth401' || mode === 'Auth403') {
    if (pageCase.authorizationMode !== mode || pageCase.caseId !== 'Q01') throw new Error('Explicit Q01 authorization Test mode required');
    if (mode === 'Auth403' && !process.env.GAODE_TEST_READ_ONLY_TOKEN) throw new Error('Owned Test read-only credential missing');
    const platform = JSON.parse(fs.readFileSync(path.join(root, 'process.json'), 'utf8').replace(/^\uFEFF/, ''));
    report.authorizationOverride = { mode, method: 'POST', path: '/api/v1/station01/runs', requestCount: 0, responseSubstitution: false };
    await call('Fetch.enable', { patterns: [{ urlPattern: `${platform.apiBase}/api/v1/station01/runs`, requestStage: 'Request' }] });
  }
  await click(selector, 'click-start-once');
  const post = await until(async () => report.network.find(x => x.method === 'POST' && x.path === '/api/v1/station01/runs' && x.status));
  if (mode === 'Normal' || mode === 'Recipe') await until(async () => post.responseBody);
  else {
    await until(async () => report.pageDiagnostics.find(x => x.event === 'StartFailed' && x.httpStatus === post.status));
    await wait(1000);
  }
  report.receipt = { status:post.status, body:post.responseBody || null };
  await state('after-post'); await screenshot('03-after-post');
  if (mode === 'Normal' || mode === 'Recipe') {
    let injectedManualOccupancy = false;
    const recoveryClicks = new Set();
    const platformPath = path.join(root, 'process.json');
    const platform = fs.existsSync(platformPath) ? JSON.parse(fs.readFileSync(platformPath, 'utf8').replace(/^\uFEFF/, '')) : null;
    const apiHeaders = { Authorization: `Bearer ${process.env.GAODE_TEST_OPERATOR_TOKEN}` };
    async function apiJson(relative, body, waitingForFirstMedia = false) {
      const response = await boundedFetch(platform.apiBase + relative, {
        headers: { ...apiHeaders, ...(body ? { 'Content-Type': 'application/json' } : {}) },
        ...(body ? { method: 'POST', body: JSON.stringify(body) } : {}) });
      if (waitingForFirstMedia && response.status === 404) return null;
      if (!response.ok) throw new Error(`Auxiliary API ${relative}: ${response.status}`);
      return response.json();
    }
    async function oldMediaProof(name) {
      const oldRun = report.receipt.body.runId;
      const catalog = await apiJson(`/api/v1/station01/runs/${oldRun}/media`);
      const readyIds = catalog.items.filter(x => x.role === 'ThreeD' && x.readiness === 'Ready').map(x => x.mediaId);
      if (!readyIds.length) throw new Error('Old actual 3D media not ready');
      const displayed = await until(async () => {
        const ids = await evaluate(`Array.from(document.querySelectorAll('#camGrid > div img')).filter(e=>e.complete&&e.naturalWidth>0).map(e=>e.dataset.mediaId).filter(Boolean)`);
        return readyIds.every(id => ids.includes(id)) ? ids : null;
      });
      const items = [];
      for (const item of catalog.items.filter(x => x.role === 'ThreeD' && x.readiness === 'Ready')) {
        const response = await boundedFetch(`${platform.apiBase}/api/v1/station01/media/${item.mediaId}`, { headers: apiHeaders });
        if (!response.ok) throw new Error('Old actual 3D media unavailable');
        const bytes = Buffer.from(await response.arrayBuffer());
        items.push({ mediaId: item.mediaId, byteLength: bytes.length,
          sha256: require('node:crypto').createHash('sha256').update(bytes).digest('hex').toUpperCase(),
          displayed: displayed.includes(item.mediaId) });
      }
      const proof = { name, runId: oldRun, items, page: await state(name) };
      await screenshot('03-' + name);
      return proof;
    }
    while (Date.now() < timeoutAt) {
      await wait(1500);
      const current = await state('running');
      if ((pageCase.afterThreeDMoveTimeoutInjection || pageCase.normalPauseInjection) && !report.auxiliaryInjection) {
        const runPath = `/api/v1/station01/runs/${report.receipt.body.runId}`;
        const actual = await apiJson(runPath);
        const catalog = await apiJson(runPath + '/media', null, true);
        if (actual.executionState === 'Running3D' && catalog?.items.some(x => x.role === 'ThreeD' && x.readiness === 'Ready')) {
          if (pageCase.afterThreeDMoveTimeoutInjection) {
            report.oldMediaBeforeFault = await oldMediaProof('old-media-before-f-motion-fault');
            const response = await boundedFetch(`${platform.plcApiBase}/api/simulator/faults/MoveTimeout`, { method: 'POST' });
            if (!response.ok) throw new Error('F motion timeout injection rejected');
            report.auxiliaryInjection = { source: 'Test/VirtualPlc', action: 'MoveTimeoutAfterActual3DMedia', runId: actual.runId };
          } else {
            await apiJson(runPath + '/pause', { requestId: 'pause-' + require('node:crypto').randomUUID(),
              expectedRevision: actual.observedRevision, reason: 'Test ordinary pause after actual 3D capture' });
            const paused = await until(async () => { const value = await apiJson(runPath); return value.executionState === 'Paused' ? value : null; });
            report.normalPause = { source: 'Test/auxiliary-control-API-with-actual-WPF', runId: actual.runId,
              pausedState: paused.executionState, page: await state('normal-paused-before-f') };
            const check = await apiJson(runPath + '/recovery-checks', { requestId: 'check-' + require('node:crypto').randomUUID(),
              expectedRevision: paused.observedRevision, sameTray: true, loadingUnchanged: true, snapshotStillApplicable: true });
            const latest = await apiJson(runPath);
            await apiJson(runPath + '/continue', { requestId: 'continue-' + require('node:crypto').randomUUID(),
              expectedRevision: latest.observedRevision, checkId: check.checkId });
            report.normalPause.checkId = check.checkId;
            report.auxiliaryInjection = { action: 'NormalPauseContinueSameRun', runId: actual.runId };
          }
        }
      }
      if (pageCase.manualOccupancyInjection && !injectedManualOccupancy) {
        const status = await (await boundedFetch(`${platform.apiBase}/api/v1/station01/status`, {
          headers: { Authorization: `Bearer ${process.env.GAODE_TEST_OPERATOR_TOKEN}` }
        })).json();
        if (status.plc?.reliability === 'Reliable' && status.plc?.manualHandling === 'Waiting') {
          const response = await boundedFetch(`${platform.plcApiBase}/api/simulator/faults/ManualZoneOccupied`, { method: 'POST' });
          if (!response.ok) throw new Error('Controlled manual occupancy injection failed');
          injectedManualOccupancy = true;
          report.interactions.push({ atUtc: new Date().toISOString(), action: 'virtual-physical-manual-zone-entry-after-position', source: 'Test/VirtualPlc' });
        }
      }
      if (mode === 'Recipe' && await evaluate(`(() => {
        const b=document.getElementById('manualRemovalButton'); return !!b && !b.disabled;
      })()`)) {
        if (await evaluate(`(() => {
          const m=document.getElementById('recipeModal');
          return !!m && getComputedStyle(m).display !== 'none';
        })()`)) await key('Escape', 'close-recipe-modal-before-removal');
        const label = await evaluate(`document.getElementById('manualRemovalButton')?.textContent`);
        if (label !== '确认换面') {
          await click('#manualRemovalReason', 'focus-confirmation-reason');
          await call('Input.insertText', { text: label === '复位' ? 'Test failed movement fault cleared; reset device' :
            label === '初始核验' ? 'Test same physical tray and original task verified after reset; loading and frozen configuration unchanged' :
            'Test virtual tray removed after unlock' });
        }
        if (label === '复位' || label === '初始核验') {
          if (pageCase.afterThreeDMoveTimeoutInjection && !report.oldMediaAtFault)
            report.oldMediaAtFault = await oldMediaProof('old-media-at-fault');
          if (recoveryClicks.has(label)) throw new Error('Recovery operation did not advance: ' + label);
          recoveryClicks.add(label);
        }
        const action = label === '确认换面' ? 'confirm-manual-flip-on-page' : label === '复位' ? 'reset-device-on-page' :
          label === '初始核验' ? 'verify-initial-state-on-page' : 'confirm-tray-removal-on-page';
        await click('#manualRemovalButton', action);
        await state('after-' + action);
        await screenshot('03-' + action);
      }
      if ((pageCase.initialMoveTimeoutInjection || pageCase.afterThreeDMoveTimeoutInjection) && !report.restartSubmitted && await evaluate(`window.station01 && document.getElementById('faultList')?.textContent.includes('显式启动')`)) {
        const ready = await evaluate(`(() => { const text=document.getElementById('manualRemovalButton')?.textContent; return text === '确认已取盘' && document.getElementById('manualRemovalButton')?.disabled; })()`);
        if (ready) {
          if (pageCase.afterThreeDMoveTimeoutInjection) report.oldMediaAfterReset = await oldMediaProof('old-media-after-reset-before-new-start');
          await click(selector, 'explicit-new-run-start-on-page'); report.restartSubmitted = true; report.faultReceipt = report.receipt;
          const restarted = await until(async () => report.network.find(x => x !== post && x.method === 'POST' && x.path === '/api/v1/station01/runs' && x.responseBody?.runId));
          report.receipt = { status: restarted.status, body: restarted.responseBody };
          await state('after-new-run-start'); continue; }
      }
      if (current.stages.some(x => /FinalUnloadCompletion/.test(x) && /已提交|已完成/.test(x))) {
        report.outcome = 'FinalPageDisplayed';
        report.resultBeforeRefresh = current;
        await call('Page.reload', { ignoreCache: true });
        await until(async () => await evaluate(`document.getElementById('verdictBig')?.innerText === ${JSON.stringify(current.verdict)}`));
        await wait(1200);
        report.resultAfterRefresh = await state('final-after-refresh');
        const loginUrl = await evaluate("location.origin + '/login.html'");
        await call('Page.navigate', { url: loginUrl });
        await until(async () => (await evaluate('location.pathname')) === '/login.html');
        await click('button[type="submit"]', 'reopen-existing-page-after-final');
        await until(async () => await evaluate(`document.getElementById('verdictBig')?.innerText === ${JSON.stringify(current.verdict)}`));
        report.resultAfterReopen = await state('final-after-page-reopen');
        const displayed = await evaluate(`Array.from(document.querySelectorAll('#camGrid > div img')).map((e,i)=>({slot:i, mediaId:e.dataset.mediaId, camera:e.dataset.businessCamera}))`);
        const mediaCatalog = report.network.filter(x => x.method === 'GET' && x.path === `/api/v1/station01/runs/${report.receipt.body.runId}/media` && x.responseBody?.items).at(-1)?.responseBody;
        const switchable = displayed.find(d => mediaCatalog?.items.filter(m => m.businessCamera === d.camera && m.role === 'Detection' && m.readiness === 'Ready').some(m => m.objectId !== mediaCatalog.items.find(x => x.mediaId === d.mediaId)?.objectId));
        if (switchable) {
          await click(`#camGrid > div:nth-child(${switchable.slot + 1})`, 'switch-existing-object-media');
          await wait(1200);
          const selectedMedia = await evaluate(`document.querySelector('#camGrid > div:nth-child(${switchable.slot + 1}) img')?.dataset.mediaId`);
          report.objectSwitch = { mediaId: selectedMedia, objectId: mediaCatalog.items.find(x => x.mediaId === selectedMedia)?.objectId,
            page: await state('final-after-existing-object-switch') };
        }

        break;
      }
      const waitingRecovery = (pageCase.initialMoveTimeoutInjection || pageCase.afterThreeDMoveTimeoutInjection) && /复位|初始核验/.test(
        await evaluate(`document.getElementById('manualRemovalButton')?.textContent||''`));
      const observedState = report.pageDiagnostics.findLast(x => x.event === 'StateObserved' &&
        x.runId === report.receipt.body.runId);
      if (!waitingRecovery && observedState?.state === 20 && report.receipt.status === 202) {
        report.outcome = 'StoppedOrUnknown'; break;
      }
      if (!waitingRecovery && /无法确认设备安全|阻断|超时|失败/.test(current.fault || '') &&
          report.receipt.status === 202) { report.outcome = 'StoppedOrUnknown'; break; }
      if (!waitingRecovery && current.stages.some(x => /公共准备|Detection|Sorting|UnloadPreparation/.test(x) &&
          /受限|失败|需恢复|阻断/.test(x))) { report.outcome = 'StoppedOrUnknown'; break; }
    }
    if (!report.outcome) report.outcome = 'DeadlineExceeded';
  } else report.outcome = `HTTP_${post.status}`;
  await state('final'); await screenshot('04-final');
  report.completedAtUtc = new Date().toISOString();
} catch (error) { report.error = String(error.stack || error); throw error; }
finally {
  rejectPending(new Error('CDP collector finished'));
  if (socket && socket.readyState !== WebSocket.CLOSED) {
    await new Promise(resolve => {
      const timer = setTimeout(() => { socket.terminate(); resolve(); }, 2000);
      socket.once('close', () => { clearTimeout(timer); resolve(); });
      socket.close();
    });
  }
  report.disconnectedAtUtc = new Date().toISOString();
  fs.writeFileSync(path.join(root, `${prefix}-webview2-page-evidence.json`), JSON.stringify(report,null,2));
}
})().catch(error => { console.error(error); process.exitCode = 1; });
