// Tool-only failure probes; no PLC, WPF, algorithm or business success is simulated.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { spawn } = require('node:child_process');
const WebSocketServer = require('../frontend/node_modules/ws').Server;
const root = process.argv[2];
if (!root || fs.existsSync(root)) throw new Error('New evidence directory required');
fs.mkdirSync(root, { recursive: true });
(async () => {
  const results = [];
  for (const mode of ['http-stall', 'cdp-request-stall', 'cdp-disconnect']) {
    const dir = path.join(root, mode); fs.mkdirSync(dir);
    const connections = new Set();
    const server = http.createServer((req, res) => {
      if (mode === 'http-stall') return;
      res.setHeader('Content-Type', 'application/json');
      res.end(JSON.stringify([{ type: 'page', id: 'controlled-tool-probe', url: 'https://appassets.local/login.html',
        webSocketDebuggerUrl: `ws://127.0.0.1:${server.address().port}/cdp` }]));
    });
    server.on('connection', socket => { connections.add(socket); socket.on('close', () => connections.delete(socket)); });
    const ws = new WebSocketServer({ server });
    ws.on('connection', socket => { if (mode === 'cdp-disconnect') setTimeout(() => socket.terminate(), 100); });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const started = Date.now();
    const child = spawn(process.execPath, [path.join(__dirname, 'capture-station01-webview2-normal.cjs'),
      String(server.address().port), dir, 'Normal'], { windowsHide: true });
    let stderr = ''; child.stderr.on('data', chunk => { stderr += chunk; });
    let forced = false;
    const guard = setTimeout(() => { forced = true; child.kill(); }, 15000);
    const code = await new Promise(resolve => child.on('exit', resolve)); clearTimeout(guard);
    for (const socket of connections) socket.destroy();
    ws.close(); await new Promise(resolve => server.close(resolve));
    const evidence = path.join(dir, 'normal-webview2-page-evidence.json');
    const report = fs.existsSync(evidence) ? JSON.parse(fs.readFileSync(evidence)) : null;
    const passed = !forced && code === 1 && !!report?.error && !!report?.disconnectedAtUtc;
    fs.writeFileSync(path.join(dir, 'stderr.log'), stderr);
    results.push({ mode, elapsedMs: Date.now() - started, code, forced, passed });
  }
  fs.writeFileSync(path.join(root, 'result.json'), JSON.stringify(results, null, 2));
  console.log(JSON.stringify(results));
  if (results.some(x => !x.passed)) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
