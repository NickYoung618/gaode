// Read-only CDP inspection of an already running Test WPF page.
const WebSocket = require('../frontend/node_modules/ws');
const fs = require('node:fs');
const path = require('node:path');
const port = Number(process.argv[2]);
if (!Number.isInteger(port) || port < 1) throw new Error('CDP port required');
(async () => {
  const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  const target = targets.find(x => x.type === 'page' && x.url.startsWith('https://appassets.local/'));
  if (!target) throw new Error('Actual WPF page absent');
  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { socket.once('open', resolve); socket.once('error', reject); });
  const result = await new Promise((resolve, reject) => {
    socket.on('message', raw => {
      const data = JSON.parse(raw.toString());
      if (data.id !== 1) return;
      data.error ? reject(new Error(data.error.message)) : resolve(data.result.result.value);
    });
    socket.send(JSON.stringify({id:1,method:'Runtime.evaluate',params:{returnByValue:true,
      expression:`({url:location.href,fault:document.getElementById('faultList')?.innerText,
        stages:Array.from(document.querySelectorAll('#moduleGrid > div')).map(x=>x.innerText),
        final:document.getElementById('verdictBig')?.innerText})`}}));
  });
  console.log(JSON.stringify(result));
  if (process.argv[3]) {
    const root = path.resolve(process.argv[3]);
    fs.writeFileSync(path.join(root, 'final-page-readonly.json'), JSON.stringify({
      source:'Test/actual-WPF-WebView2-CDP-readonly', atUtc:new Date().toISOString(), ...result
    }, null, 2));
    const screenshot = await new Promise((resolve, reject) => {
      socket.on('message', raw => {
        const data = JSON.parse(raw.toString());
        if (data.id !== 2) return;
        data.error ? reject(new Error(data.error.message)) : resolve(data.result.data);
      });
      socket.send(JSON.stringify({id:2,method:'Page.captureScreenshot',
        params:{format:'png',captureBeyondViewport:false}}));
    });
    fs.writeFileSync(path.join(root, 'final-page-readonly.png'), Buffer.from(screenshot, 'base64'));
  }
  socket.close();
})().catch(error => { console.error(error); process.exitCode = 1; });
