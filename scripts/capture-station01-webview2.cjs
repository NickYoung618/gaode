// Test-only evidence capture against the WebView2 renderer of the running WPF process.
// Never writes request headers or bearer tokens to evidence.
const fs = require('node:fs');
const path = require('node:path');
const WebSocket = require('../frontend/node_modules/ws');

const [portText, scenario, evidenceRoot, plcApi] = process.argv.slice(2);
if (!portText || !['CommunicationAfterReceipt', 'UnsafeBeforeRequest'].includes(scenario) ||
    !evidenceRoot || !plcApi) throw new Error('port scenario evidenceRoot plcApi required');
const port = Number(portText);
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
const pages = awaitFetch(`http://127.0.0.1:${port}/json`);

async function awaitFetch(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return response.json();
}

(async () => {
  const targets = await pages;
  const target = targets.find(p => p.type === 'page' && p.url.startsWith('https://appassets.local/'));
  if (!target) throw new Error('No real WPF WebView2 appassets target');
  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { socket.once('open', resolve); socket.once('error', reject); });
  let nextId = 0;
  const pending = new Map();
  const requests = new Map();
  const network = [];
  socket.on('message', raw => {
    const message = JSON.parse(raw.toString());
    if (message.id) {
      const entry = pending.get(message.id);
      if (!entry) return;
      pending.delete(message.id);
      message.error ? entry.reject(new Error(message.error.message)) : entry.resolve(message.result);
      return;
    }
    const p = message.params || {};
    if (message.method === 'Network.requestWillBeSent' &&
        p.request?.url?.includes('/api/v1/station01/')) {
      const entry = { atUtc: new Date().toISOString(), requestId: p.requestId,
        method: p.request.method, url: p.request.url,
        requestBody: p.request.postData ? JSON.parse(p.request.postData) : undefined };
      requests.set(p.requestId, entry);
      network.push(entry);
    }
    if (message.method === 'Network.responseReceived' && requests.has(p.requestId))
      requests.get(p.requestId).status = p.response.status;
    if (message.method === 'Network.loadingFinished' && requests.has(p.requestId)) {
      call('Network.getResponseBody', { requestId: p.requestId }).then(body => {
        if (!body.base64Encoded) {
          try { requests.get(p.requestId).responseBody = JSON.parse(body.body); }
          catch { requests.get(p.requestId).responseBody = body.body.slice(0, 1000); }
        }
      }).catch(() => {});
    }
  });
  function call(method, params = {}) {
    const id = ++nextId;
    return new Promise((resolve, reject) => {
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });
  }
  async function evaluate(expression) {
    const result = await call('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    return result.result.value;
  }
  async function until(condition, maxMs = 12000) {
    const deadline = Date.now() + maxMs;
    while (Date.now() < deadline) {
      const value = await condition();
      if (value) return value;
      await wait(100);
    }
    throw new Error('Timed out waiting for WebView2 page state');
  }
  async function click(selector) {
    const rect = await evaluate(`(() => { const e = document.querySelector(${JSON.stringify(selector)}); if (!e) return null; const r=e.getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2}; })()`);
    if (!rect) throw new Error(`Page control missing: ${selector}`);
    await call('Input.dispatchMouseEvent', { type: 'mouseMoved', x: rect.x, y: rect.y });
    await call('Input.dispatchMouseEvent', { type: 'mousePressed', x: rect.x, y: rect.y, button: 'left', clickCount: 1 });
    await call('Input.dispatchMouseEvent', { type: 'mouseReleased', x: rect.x, y: rect.y, button: 'left', clickCount: 1 });
    return rect;
  }
  async function screenshot(name) {
    const result = await call('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    fs.writeFileSync(path.join(evidenceRoot, name), Buffer.from(result.data, 'base64'));
  }
  async function pageState() {
    return evaluate(`({url:location.href,title:document.title,verdict:document.getElementById('verdictBig')?.innerText,fault:document.getElementById('faultList')?.innerText,items:document.getElementById('itemList')?.innerText,config:{mode:window.station01?.config?.mode,prototypeSha256:window.station01?.config?.prototypeSha256}})`);
  }
  const report = { scenario, source: 'Test/WPF-WebView2', target: { url: target.url, title: target.title, id: target.id },
    startedAtUtc: new Date().toISOString(), interactions: [], network };
  try {
    await call('Page.enable');
    await call('Runtime.enable');
    await call('Network.enable');
    await until(async () => (await evaluate('location.pathname')) === '/login.html');
    await screenshot('01-login-before.png');
    report.interactions.push({ atUtc: new Date().toISOString(), action: 'click-login', point: await click('button[type="submit"]') });
    await until(async () => (await evaluate('location.pathname')) === '/prototype.html');
    await until(async () => await evaluate('!!(window.station01 && document.getElementById("faultList"))'));
    await wait(600);
    report.beforeStatus = await evaluate('window.station01.status()');
    if (report.beforeStatus?.host !== 'Ready' || report.beforeStatus?.plc?.connection !== 'Connected' ||
        report.beforeStatus?.plc?.reliability !== 'Reliable' || report.beforeStatus?.plc?.safetyAssessment !== 'Clear')
      throw new Error('Pre-click Host/PLC state is not reliably ready; this sample cannot prove post-receipt failure');
    report.before = await pageState();
    await screenshot('02-before-start.png');
    if (scenario === 'UnsafeBeforeRequest') {
      report.faultAtUtc = new Date().toISOString();
      report.faultInjection = await awaitFetch(`${plcApi}/api/simulator/faults/EmergencyAlarm`, { method: 'POST' });
      await wait(350);
    }
    const startSelector = 'button.bg-emerald-600';
    report.interactions.push({ atUtc: new Date().toISOString(), action: 'click-start', point: await click(startSelector) });
    const post = await until(async () => network.find(e => e.method === 'POST' && e.url.endsWith('/api/v1/station01/runs') && e.status), 12000);
    await until(async () => post.responseBody, 6000);
    report.receipt = { status: post.status, body: post.responseBody };
    report.interactions.push({ atUtc: new Date().toISOString(), action: 'observed-start-response', status: post.status });
    await screenshot('03-after-receipt.png');
    if (scenario === 'CommunicationAfterReceipt') {
      report.faultAtUtc = new Date().toISOString();
      report.faultInjection = await awaitFetch(`${plcApi}/api/simulator/faults/PauseHeartbeat`, { method: 'POST' });
    }
    await wait(5500);
    report.after = await pageState();
    await screenshot('04-failure-displayed.png');
    report.interactions.push({ atUtc: new Date().toISOString(), action: 'click-start-again', point: await click(startSelector) });
    await wait(300);
    report.afterSecondClick = await pageState();
    await screenshot('05-second-click.png');
    await call('Page.reload', { ignoreCache: true });
    await until(async () => (await evaluate('location.pathname')) === '/prototype.html');
    await wait(2600);
    report.afterReload = await pageState();
    await screenshot('06-after-reload.png');
    report.completedAtUtc = new Date().toISOString();
  } catch (error) { report.error = String(error.stack || error); throw error; }
  finally {
    await wait(200);
    fs.writeFileSync(path.join(evidenceRoot, 'webview2-page-evidence.json'), JSON.stringify(report, null, 2));
    socket.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
