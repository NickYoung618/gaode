// Read-only dashboard regression checks. No Host, PLC or browser process is started.
const test = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const scriptPath = process.env.GAODE_MONITOR_SCRIPT || path.resolve(__dirname, '../../VirtualPlc/wwwroot/app.js');
const source = readFileSync(scriptPath, 'utf8');
// Current independent-axis fixture; values are explicit UI component input, not device evidence.
function axisAudit(receipt = true) {
  return { latestSequence: 2, oldestSequence: 1, actionLatestSequence: 2, actionOldestSequence: 1,
    writes: receipt ? [{ sequence:1, documentNumber:3, accepted:true }, { sequence:2, documentNumber:34, accepted:true }] : [],
    actions: ['accepted','completed'].map((phase,i) => ({ sequence:i+1, generation:1, actionSequence:7,
      kind:'AxisMove', phase, command:1, axisRole:'X', target:{value:0}, actual:{value:0.25},
      writeSequenceRefs:[1,2], occurredAtUtc:'2026-10-04T00:00:00Z' })) };
}
test('independent axis shows zero target and distinct actual with no duplicate or replay', async () => {
  const view=await mount(source,sampleSnapshot(),null,200,axisAudit());
  const rows=vm.runInContext('monitor.history.filter(x=>x.axis)',view.context);
  assert.equal(rows.length,2);assert.equal(rows.find(x=>x.writer==='PC').value,'0');
  assert.equal(rows.find(x=>x.writer==='PLC').value,'0.25');
  assert.equal(rows.find(x=>x.writer==='PLC').address,'4x000D');
  vm.runInContext('monitor.nextAuditAt=0',view.context);await view.timers.shift()();
  assert.equal(vm.runInContext('monitor.history.length',view.context),2);
  view.elements.get('clearHistoryButton').listeners.click();
  vm.runInContext('monitor.nextAuditAt=0',view.context);await view.timers.shift()();
  assert.equal(vm.runInContext('monitor.history.length',view.context),0);
});
test('missing current axis write does not invent target or hide actual feedback', async () => {
  const view=await mount(source,sampleSnapshot(),null,200,axisAudit(false));
  const rows=vm.runInContext('monitor.history.filter(x=>x.axis)',view.context);
  assert.equal(rows.find(x=>x.writer==='PC').value,'缺少本次记录');
  assert.equal(rows.find(x=>x.writer==='PLC').value,'0.25');
});
test('pause viewing preserves GET acquisition and current filtering', async () => {
  const audit=axisAudit();const view=await mount(source,sampleSnapshot(),null,200,audit);
  view.elements.get('pauseButton').listeners.click();
  const frozen=view.elements.get('historyList').innerHTML;const count=view.calls.length;
  audit.actions.push({...audit.actions[1],sequence:3,actionSequence:8});audit.actionLatestSequence=3;
  vm.runInContext('monitor.nextAuditAt=0',view.context);await view.timers.shift()();
  assert.ok(view.calls.length>count);assert.equal(view.elements.get('historyList').innerHTML,frozen);
  assert.match(view.elements.get('historyStatus').textContent,/新增 1 条/);
  view.elements.get('historyDirection').value='PlcToPc';view.elements.get('historyDirection').listeners.change();
  assert.doesNotMatch(view.elements.get('historyList').innerHTML,/实际发送/);
  view.elements.get('historyDirection').value='';view.elements.get('pauseButton').listeners.click();
  assert.notEqual(view.elements.get('historyList').innerHTML,frozen);
});

class Element {
  constructor() {
    this.dataset = {};
    this.attributes = {};
    this.scrollTop = 0;
    this.classList = { toggle: () => true };
    this.children = [];
    this.textContent = '';
    this.value = '';
    this.hidden = false;
    this.htmlWrites = 0;
    this.listeners = {};
  }
  set innerHTML(value) {
    this.html = value;
    this.htmlWrites++;
    if (value.includes('class="value-cell"')) this.valueCell = new Element();
  }
  get innerHTML() { return this.html || ''; }
  setAttribute(name, value) { this.attributes[name] = value; }
  addEventListener(name, callback) { this.listeners[name] = callback; }
  replaceChildren() { this.children = []; }
  appendChild(child) { child.parent = this; this.children.push(child); }
  remove() { this.parent.children = this.parent.children.filter(child => child !== this); }
  querySelectorAll(selector) {
    assert.equal(selector, 'tr[data-point-key]');
    return this.children.filter(child => child.dataset.pointKey);
  }
  querySelector(selector) {
    if (selector === '.value-cell') return this.valueCell;
    assert.equal(selector, 'tr[data-empty-row]');
    return this.children.find(child => child.dataset.emptyRow);
  }
}

function sampleSnapshot() {
  return {
    timestamp: '2026-09-27T10:00:00Z', activeAction: null, activeFaults: [], communicationTimedOut: false,
    coils: [
      { name: 'PLC_Heartbeat_Req', address: '0x0001', pduOffset: 0, direction: 'PlcToPc', value: false },
      { name: 'PC_Heartbeat_Resp', address: '0x0002', pduOffset: 1, direction: 'PcToPlc', value: false }
    ],
    holdingRegisters: [
      { name: 'Flip_Sorting', address: '4x0060', pduOffset: 95, direction: 'PcToPlc', rawValue: 0, signedValue: 0 },
      { name: 'X_Pos_Confirmed', address: '4x0080', pduOffset: 127, direction: 'PlcToPc', rawValue: 0, signedValue: 0 },
      { name: 'Machine_Current_Pos_Z', address: '4x0011', pduOffset: 16, direction: 'PlcToPc',
        valueType: 'Float32', floatValue: 123.25, words: [0x42F6, 0x8000] }
    ]
  };
}

async function mount(script = source, snapshot = sampleSnapshot(), changes = null, status = 200, audit = null, layout = {}) {
  const elements = new Map();
  const timers = [];
  const calls = [];
  const redirects = [];
  const page = { snapshot, status };
  const context = vm.createContext({
    document: {
      getElementById(id) {
        if (layout.missingIds?.includes(id)) return null;
        if (!elements.has(id)) { const el = new Element(); if (id === "historyList") el.dataset.historyVersion = layout.historyVersion || "unlock-history-r9"; elements.set(id, el); }
        return elements.get(id);
      },
      createElement() { return new Element(); }
    },
    URL,
    window: {
      location: { href: layout.href || 'http://127.0.0.1:5080/', replace(url) { redirects.push(url); } },
      setTimeout(callback) { timers.push(callback); return timers.length; }
    },
    async fetch(url, options) {
      calls.push(url);
      assert.ok(!options.method || options.method === 'GET', 'monitor must remain read-only');
      if (url === '/api/simulator/state') {
        return { ok: page.status === 200, status: page.status, json: async () => page.snapshot };
      }
      if (url.startsWith('/api/simulator/audit?')) return { ok: !page.auditStatus || page.auditStatus === 200, status: page.auditStatus || 200, json: async () => audit || { latestSequence: 0, oldestSequence: 1, writes: [], actions: [], actionLatestSequence: 0 } };
      assert.match(url, /^\/api\/simulator\/changes\?after=\d+$/);
      return { ok: true, json: async () => changes || { oldestSequence: 0, latestSequence: 0, gap: false, changes: [] } };
    }
  });
  await vm.runInContext(script, context, { filename: scriptPath, timeout: 1000 });
  return { elements, timers, calls, page, context, redirects };
}

test('old history HTML with current JS reloads once and never oscillates between false connected/disconnected states', async () => {
  const missingIds = ['historyList'];
  const old = await mount(source, sampleSnapshot(), null, 200, null, { missingIds });
  assert.equal(old.elements.get('connectionText').textContent, '页面版本不一致');
  assert.equal(old.calls.length, 0);
  assert.equal(old.timers.length, 0);
  assert.equal(old.redirects.length, 1);
  const brokenPackage = await mount(source, sampleSnapshot(), null, 200, null, { missingIds, href: old.redirects[0] });
  assert.equal(brokenPackage.redirects.length, 0, 'A mixed package must not reload forever');
  assert.equal(brokenPackage.timers.length, 0);
  assert.equal(brokenPackage.elements.get('connectionText').textContent, '页面版本不一致');
});

test('action audit failure remains visible between audit polls without inventing a PLC disconnection', async () => {
  const view = await mount();
  view.page.auditStatus = 503;
  vm.runInContext('monitor.nextAuditAt = 0', view.context);
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时');
  assert.match(view.elements.get('lastRefresh').textContent, /动作记录异常.*503/);
  await view.timers.shift()();
  assert.match(view.elements.get('lastRefresh').textContent, /动作记录异常.*503/);
  view.page.auditStatus = 200;
  vm.runInContext('monitor.nextAuditAt = 0', view.context);
  await view.timers.shift()();
  assert.match(view.elements.get('lastRefresh').textContent, /本次状态读取及显示正常/);
});

test('numeric change directions are correct and unknown direction is not claimed as PLC feedback', async () => {
  const view = await mount();
  assert.equal(vm.runInContext('directionText(0)', view.context), '上位机 → PLC');
  assert.equal(vm.runInContext('directionText(1)', view.context), 'PLC → 上位机');
  assert.equal(vm.runInContext('directionText(7)', view.context), '方向未提供');
});

function auditFixture(targets, kind = 'Sort', role = 'GrabZ', commandValue = 1) {
  const writes = [], actions = []; let seq = 0;
  targets.forEach((target, index) => {
    const refs = []; const time = `2026-09-27T00:00:${String(index).padStart(2, '0')}.000Z`;
    [3, 5, { DetectionZ: 7, ScanZ: 9, GrabZ: 11 }[role]].forEach((address, axis) => {
      const bytes = Buffer.alloc(4); bytes.writeFloatBE(target[axis]);
      const write = { sequence: ++seq, documentNumber: address, connectionId: 'connection-a', transactionId: seq,
        accepted: true, function: 16, pduOffset: address - 1, rawWords: [bytes.readUInt16BE(0), bytes.readUInt16BE(2)],
        byteOrder: 'ABCD', occurredAtUtc: time };
      writes.push(write); refs.push(seq);
    });
    writes.push({ sequence: ++seq, documentNumber: 0x21, value: commandValue,
      accepted: true, connectionId: 'connection-a', transactionId: seq, occurredAtUtc: time }); refs.push(seq);
    for (const phase of ['accepted', 'completed']) actions.push({ sequence: actions.length + 1, generation: 0,
      actionSequence: index + 1, kind, command: commandValue, phase, writeSequenceRefs: refs, axisRole: role,
      occurredAtUtc: time, actual: { x: target[0] + 0.25, y: target[1], z: target[2] }, handshake: { xy: 1, z: 2, sort: 3 } });
  });
  return { latestSequence: seq, oldestSequence: 1, actionLatestSequence: actions.length, actionOldestSequence: 1, writes, actions };
}

test('current sorting history retains repeated coordinates and zero with distinct actual feedback', async () => {
  for (const [kind, role, command] of [['Sort','GrabZ',1],['Sort','GrabZ',2]]) {
    const audit = auditFixture([[100,100,150],[200,100,90],[210,110,90],[210,110,90],[0,0,0]], kind, role, command);
    const view = await mount(source, sampleSnapshot(), null, 200, audit);
    const rows = vm.runInContext('monitor.history.filter(x => x.axis)', view.context);
    assert.equal(rows.length, 30, `${kind}/${role}/${command}`);
    for (let action = 1; action <= 5; action++) {
      const selected = rows.filter(x => x.actionKey === `0:${action}`);
      assert.equal(selected.length, 6);
      assert.equal(selected.filter(x => x.direction === 'PcToPlc').length, 3);
      assert.equal(selected.filter(x => x.direction === 'PlcToPc').length, 3);
    }
    assert.equal(rows.find(x => x.actionKey === '0:5' && x.axis === 'y' && x.writer === 'PC').value, '0');
    assert.equal(rows.find(x => x.actionKey === '0:1' && x.axis === 'x' && x.writer === 'PLC').value, '100.25', 'feedback must not be target');
    const expectedZ = { ScanZ: '4x0009', DetectionZ: '4x0007', GrabZ: '4x000B' }[role];
    assert.ok(rows.some(x => x.axis === 'z' && x.writer === 'PC' && x.address === expectedZ));
    assert.match(view.elements.get('historyList').innerHTML, /Camera_Target_Y/);
    assert.ok(!view.elements.has('motionList'));
    vm.runInContext('monitor.nextAuditAt=0',view.context); await view.timers.shift()();
    assert.equal(vm.runInContext('monitor.history.length',view.context),30,'poll must not duplicate');
    view.elements.get('clearHistoryButton').listeners.click();
    vm.runInContext('monitor.nextAuditAt=0',view.context); await view.timers.shift()();
    assert.equal(vm.runInContext('monitor.history.length',view.context),0,'clear must not replay old actions');
  }
});

test('missing receipt, wrong connection and previous target never fill current sorting coordinates', async () => {
  const audit = auditFixture([[100,100,150],[200,100,150]]);
  audit.writes = audit.writes.filter(w => w.sequence !== 6);
  audit.actions[2].writeSequenceRefs[1] = 2; // retained old Y is not a new write
  audit.writes.find(w => w.sequence === 5).connectionId = 'another-connection';
  const changes = { latestSequence: 2, oldestSequence: 1, changes: [
    { sequence: 1, occurredAtUtc:'2026-09-27T00:00:00Z',address:'4x0005', name:'Camera_Target_Y', previous:'0',current:'100',writer:'PC',direction:0 },
    { sequence: 2, occurredAtUtc:'2026-09-27T00:00:01Z',address:'4x0022', name:'Sorting_Exec_Status', previous:'4',current:'0',writer:'PC',direction:0 }] };
  const view=await mount(source,sampleSnapshot(),changes,200,audit);
  const rows=vm.runInContext('monitor.history',view.context);
  for(const axis of ['x','y']) assert.equal(rows.find(x=>x.actionKey==='0:2'&&x.axis===axis&&x.writer==='PC').value,'缺少本次记录');
  assert.ok(rows.some(x=>x.name==='Sorting_Exec_Status'&&x.to==='0'));
  assert.equal(rows.filter(x=>x.name==='Camera_Target_Y').length,2,'no duplicate coordinate changes');
});

test('history cache remains bounded and missing audit range is visible', async () => {
 const audit=auditFixture([[0,0,0]]);audit.actionOldestSequence=3;
 const view=await mount(source,sampleSnapshot(),null,200,audit);
 assert.match(view.elements.get('historyList').innerHTML,/缓存存在缺口/);
 vm.runInContext('for(let i=0;i<8300;i++) addHistory({sequence:i,time:new Date(i),name:"test"});',view.context);
 assert.equal(vm.runInContext('monitor.history.length',view.context),8192);
 assert.ok(vm.runInContext('monitor.history.some(x=>x.gap)',view.context));
});

function cell(view, table, key) {
  const row = view.elements.get(table).children.find(item => item.dataset.pointKey === key);
  assert.ok(row, `missing signal ${key}`);
  return row.valueCell;
}

test('initial monitor poll renders both directions, zero values and Float32 without a script error', async () => {
  const view = await mount();
  assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时', view.elements.get('lastRefresh').textContent);
  assert.equal(view.elements.get('signalCount').textContent, 5);
  assert.match(cell(view, 'plcToPcCoilRows', 'coil:0x0001').innerHTML, />0<\/span>/);
  assert.match(cell(view, 'pcToPlcCoilRows', 'coil:0x0002').innerHTML, />0<\/span>/);
  assert.match(cell(view, 'pcToPlcRegisterRows', 'register:4x0060').innerHTML, />0<\/span>/);
  assert.match(cell(view, 'plcToPcRegisterRows', 'register:4x0080').innerHTML, />0<\/span>/);
  assert.match(cell(view, 'plcToPcRegisterRows', 'register:4x0011').innerHTML, /123\.25/);
  assert.equal(view.timers.length, 1, 'next poll must be scheduled');
});

test('successive polls reuse unchanged cells and update heartbeat, integer and Float32 values', async () => {
  const view = await mount();
  const heartbeat = cell(view, 'plcToPcCoilRows', 'coil:0x0001');
  const writes = heartbeat.htmlWrites;
  await view.timers.shift()();
  assert.equal(heartbeat.htmlWrites, writes, 'unchanged values should not rewrite the DOM');
  view.page.snapshot.coils[0].value = true;
  view.page.snapshot.holdingRegisters[0].rawValue = 4;
  view.page.snapshot.holdingRegisters[0].signedValue = 4;
  view.page.snapshot.holdingRegisters[2].floatValue = 0;
  view.page.snapshot.holdingRegisters[2].words = [0, 0];
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时', view.elements.get('lastRefresh').textContent);
  assert.strictEqual(cell(view, 'plcToPcCoilRows', 'coil:0x0001'), heartbeat);
  assert.match(heartbeat.innerHTML, />1<\/span>/);
  assert.match(cell(view, 'pcToPlcRegisterRows', 'register:4x0060').innerHTML, />4<\/span>/);
  assert.match(cell(view, 'plcToPcRegisterRows', 'register:4x0011').innerHTML, />0<\/span>/);
});

test('an actual HTTP failure remains visible and polling can recover', async () => {
  const view = await mount(source, sampleSnapshot(), null, 503);
  assert.equal(view.elements.get('connectionText').textContent, '监控读取失败');
  assert.match(view.elements.get('lastRefresh').textContent, /HTTP 503/);
  view.page.status = 200;
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时', view.elements.get('lastRefresh').textContent);
});

// Optional one-shot check of already-running static assets and GET responses.
// It never starts, stops or writes to the simulator.
if (process.env.GAODE_PLC_MONITOR_CHECK_URL) {
  test('served monitor script matches source and renders the running simulator GET responses', async () => {
    const base = new URL(process.env.GAODE_PLC_MONITOR_CHECK_URL);
    assert.ok(['127.0.0.1', 'localhost'].includes(base.hostname));
    async function read(route, asJson) {
      const response = await fetch(new URL(route, base), { cache: 'no-store', signal: AbortSignal.timeout(5000) });
      assert.equal(response.status, 200, route);
      return asJson ? response.json() : response.text();
    }
    const served = await read('/app.js', false);
    assert.equal(served, source, 'running monitor must serve the updated script');
    const snapshot = await read('/api/simulator/state', true);
    const changes = await read('/api/simulator/changes?after=0', true);
    const audit = await read('/api/simulator/audit?after=0', true);
    const view = await mount(served, snapshot, changes, 200, audit);
    assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时', view.elements.get('lastRefresh').textContent);
    assert.equal(view.elements.get('signalCount').textContent, snapshot.coils.length + snapshot.holdingRegisters.length);
  });
}


test('successful HTTP and render failure retain prior display timestamp without inventing PLC disconnection; recovery clears error', async () => {
  const view = await mount();
  vm.runInContext('globalThis.originalRender = renderSnapshot; renderSnapshot = () => { throw new Error("render-broken"); }', view.context);
  view.page.snapshot = { ...sampleSnapshot(), timestamp: '2026-09-27T10:00:01Z' };
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时');
  assert.match(view.elements.get('lastRefresh').textContent, /页面显示失败：render-broken/);
  assert.match(view.elements.get('lastRefresh').textContent, /最近成功显示 2026-09-27T10:00:00Z/);
  assert.match(view.elements.get('lastRefresh').textContent, /旧状态/);
  vm.runInContext('renderSnapshot = originalRender', view.context);
  await view.timers.shift()();
  assert.doesNotMatch(view.elements.get('lastRefresh').textContent, /render-broken/);
  assert.match(view.elements.get('lastRefresh').textContent, /最近成功显示 2026-09-27T10:00:01Z/);
});

test('actual heartbeat communication failure is shown despite successful monitor HTTP; missing status is unknown', async () => {
  const view = await mount(source, { ...sampleSnapshot(), communicationTimedOut: true });
  assert.equal(view.elements.get('connectionText').textContent, 'PLC通信失效（心跳超时）');
  view.page.snapshot = { ...sampleSnapshot(), communicationTimedOut: undefined };
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, 'PLC通信状态未提供');
  view.page.snapshot = sampleSnapshot();
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时');
});

test('HTTP failure preserves last known state and timestamp as stale, never reports recovery until real success', async () => {
  const view = await mount(source, { ...sampleSnapshot(), communicationTimedOut: true });
  view.page.status = 503;
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控读取失败');
  assert.equal(vm.runInContext('monitor.stateSnapshot.communicationTimedOut', view.context), true);
  assert.match(view.elements.get('lastRefresh').textContent, /状态读取失败.*503.*最近成功显示.*旧状态/);
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控读取失败');
  view.page.status = 200; view.page.snapshot = sampleSnapshot();
  await view.timers.shift()();
  assert.equal(view.elements.get('connectionText').textContent, '监控可读取 · PLC心跳未超时');
  assert.doesNotMatch(view.elements.get('lastRefresh').textContent, /503/);
});

