import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// Component-only HTTP/notification doubles. Actual shared-run proof is supplied by011 joint verification.
const runtime = readFileSync(new URL('../../src/runtime.js', import.meta.url), 'utf8');
const sample = (name: string) => JSON.parse(readFileSync(
  new URL(`../../../specs/007-station01-integrated-loop/examples/${name}.virtual-loop.json`, import.meta.url), 'utf8'));
const publicSample = sample('public');
const budgetSample = sample('budget');
const simulationSample = sample('simulation');
const ref = (config: { id: string; version: string }) => ({ id: config.id, version: config.version });
const prepareScript = readFileSync(new URL('../../../scripts/simulate-station01-load.ps1', import.meta.url), 'utf8');
const startupScript = readFileSync(new URL('../../../scripts/start-station01-virtual-loop.ps1', import.meta.url), 'utf8');
const requestBody = {
  requestId: 's01-006-test-request',
  contextJson: JSON.stringify({ schemaVersion: 'station01-start-run-context/1.0',
    trayId: '11111111-1111-4111-8111-111111111111',
    stationId: '22222222-2222-4222-8222-222222222222',
    lineId: '33333333-3333-4333-8333-333333333333',
    scenarioId: 'S1', occupiedSlots: ['P01'], purpose: 'Test' }),
  publicConfigRef: ref(publicSample),
  budgetRef: ref(budgetSample),
  simulationRef: ref(simulationSample)
};

function page(authorized = true, preparedStartRequest: any = requestBody,
  outcome: 'normal' | 'configuration-blocked' | 'awaiting-removal' | 'manual-flip' | 'recovery' = 'normal', resultDisposition: string | null = null,
  recipeProjection: any = null, initialRunId: string | null = null, measurementDetails = false, deviceOrigin: string | null = 'Virtual', editor: any = null) {
  const ids = new Map<string, any>();
  const node = (id: string) => ids.get(id) ?? ids.set(id, { id, textContent: '', style: {}, classList: { add: () => {}, remove: () => {} },
    querySelectorAll: () => [], closest: () => null,
    get innerHTML() { return this.html || ''; },
    set innerHTML(value: string) { this.html = value; this.textContent = value.replace(/<[^>]*>/g, '').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&'); }
  }).get(id);
  const numberNode: any = { textContent: '' };
  const metricNodes = Array.from({length:4}, () => ({ textContent: '' }));
  const identityNodes = Array.from({length:6}, () => ({ textContent: '' }));
  const verdictPanel = { children:[{querySelector:()=>numberNode},{querySelectorAll:()=>metricNodes}],
    querySelectorAll:()=>[numberNode,node('verdictBig'),...metricNodes] };
  node('verdictBig').closest = (selector: string) => selector === '.panel' ? verdictPanel :
    selector === 'aside' ? {children:[null,{querySelectorAll:()=>identityNodes}]} : null;
  const cards = Array.from({ length: 8 }, () => ({
    addEventListener: (_event: string, _callback: () => void) => {},
    querySelector: (selector: string) => selector === 'img' ? null : ({ textContent: '' })
  }));
  let click: () => Promise<void> = async () => {};
  let confirmRemoval: () => Promise<void> = async () => {};
  let chooseRecipe: () => void = () => {};
  ids.set('recipeAuthoringCatalog', { addEventListener: (_: string, callback: () => void) => { chooseRecipe = callback; } });
  let phase = outcome === 'configuration-blocked' ? 'configuration-blocked' :
    outcome === 'awaiting-removal' ? 'awaiting-removal' : outcome === 'manual-flip' ? 'manual-flip' :
      outcome === 'recovery' ? 'recovery-reset' : 'accepted';
  const requests: any[] = [];
  const events: any[] = [];
  const diagnosticLogs: any[] = [];
  const timers: Array<() => void> = [];
  const callbacks: Record<string, (message: any) => void> = {};
  const startButton = { textContent: '启动', addEventListener: (_: string, cb: () => Promise<void>) => { click = cb; } };
  ids.set('manualRemovalButton', { id: 'manualRemovalButton', disabled: true, style: {},
    addEventListener: (_: string, cb: () => Promise<void>) => { confirmRemoval = cb; } });
  ids.set('manualRemovalReason', { id: 'manualRemovalReason', disabled: true, value: '已取盘', style: {} });
  const document = {
    getElementById: (id: string) => node(id),
    querySelectorAll: (selector: string) => selector === 'button' ? [startButton] :
      selector === '#moduleGrid > div' || selector === '#camGrid > div' ? cards : [],
  };
  class HubConnectionBuilder {
    withUrl(_url: string, options: any) { assert.equal(options.transport, 4); assert.equal(options.accessTokenFactory(), authorized ? 'test-secret' : ''); return this; }
    withAutomaticReconnect() { return this; }
    build() { return { on: (name: string, cb: (message: any) => void) => { callbacks[name] = cb; },
      onreconnected: () => {}, start: async () => {} }; }
  }
  const fetch = async (url: string, init: any) => {
    const method = init.method || 'GET';
    requests.push({ url, method, headers: init.headers, body: init.body });
    if (method === 'POST' && url.endsWith('/runs')) {
      return authorized ? new Response(JSON.stringify({ runId: 'run-1', commandId: 'cmd-1', accepted: true }), { status: 202 }) :
        new Response(JSON.stringify({ error: 'Unauthorized' }), { status: 401 });
    }
    if (method === 'POST' && url.endsWith('/manual-removal-confirmations'))
      return new Response(JSON.stringify({ state: 'FinalUnloadCompletion', finalEventId: 'event-1' }), { status: 202 });
    if (method === 'POST' && url.endsWith('/recovery-reset')) {
      phase = 'recovery-check';
      return new Response(JSON.stringify({ resetEpoch: 2 }), { status: 202 });
    }
    if (method === 'POST' && url.endsWith('/recovery-checks')) {
      phase = 'recovery-ready';
      return new Response(JSON.stringify({ checkId: 'verified-check', result: 'Accepted' }), { status: 202 });
    }
    if (method === 'POST' && url.endsWith('/continue')) {
      phase = 'recovery-resending';
      return new Response(JSON.stringify({ stopState: 'ResendPendingActualFeedback' }), { status: 202 });
    }
    if (!authorized) return new Response('{}', { status: 401 });
    if (url.endsWith('/status')) return new Response(JSON.stringify({ host: 'Ready',
      stage: phase === 'configuration-blocked' ? 'ConfigurationBlocked' : phase === 'final' ? 'Completed' : 'Detection',
      camera: { state: 'Ready', source: 'Test/FixedImage' }, algorithm: { state: 'Ready', source: 'Test/IndependentWorker' },
      storage: { state: 'Ready' }, plc: { connection: 'Connected', executionOrigin: deviceOrigin ? { provider: deviceOrigin } : null } }), { status: 200 });
    if (url.endsWith('/evidence')) return new Response(JSON.stringify({
      wholeTrayCompletionId: phase === 'configuration-blocked' ? null : 'whole-1',
      readyForRemovalSourceMatrix: phase === 'awaiting-removal' ? { matrixId: 'removal-matrix', components: [
        { component: 0, source: 3 }, { component: 1, source: 1 }] } : null,
      finalResult: phase === 'final' ? 'FinalUnloadCompletion' : 'AwaitingFinalUnloadCompletion',
      finalSourceMatrix: phase === 'final' ? { scope: 'SoftwareLoopOnly', components: [
        { component: 1, source: 1 }, { component: 4, source: 3 },
        { component: 2, source: 3 }, { component: 5, source: 3 }] } : null,
      stages: phase === 'configuration-blocked' ? [] : [
        { stage: 'Detection', eventType: 'Completed' }, { stage: 'Sorting', eventType: 'Completed' },
        { stage: 'UnloadPreparation', eventType: 'Completed' },
        { stage: 'ManualRemovalAdmission', eventType: 'WholeTrayCompleted' },
        { stage: 'ManualRemovalAdmission', eventType: 'ManualRemovalAllowed' }
      ] }), { status: 200 });
    if (phase === 'unconfirmed' || phase === 'unsafe') return new Response(JSON.stringify({
      requestId: requestBody.requestId, state: 'Blocked', errorCode: 'StartupNotReady',
      startupDiagnostic: { reasonCodes: [phase === 'unconfirmed' ? 'PlcHeartbeatLost' : 'SafetyInterlockDenied'],
        safetyAssessment: phase === 'unconfirmed' ? 'Unconfirmed' : 'ExplicitUnsafe',
        stopStage: 'StartupReadiness', disposition: 'BlockedNoDeviceAction', connectionEpoch: 2,
        observedAtUtc: '2026-09-24T00:00:00Z', executionOrigin: deviceOrigin ? { provider: deviceOrigin } : null } }), { status: 200 });
    if (phase === 'configuration-blocked') return new Response(JSON.stringify({
      requestId: preparedStartRequest.requestId, state: 'ConfigurationBlocked',
      errorCode: 'ConfigurationNotFound', events: ['Preparing', 'ConfigurationBlocked'] }), { status: 200 });
    if (phase === 'manual-flip') return new Response(JSON.stringify({
      state: 'Detection', observedRevision: 7, allowedActions: ['ConfirmManualFlip'],
      waitingManualFlip: { entityId: 'entity-1', stepSequence: 9, targetFace: 2 }, events: [] }), { status: 200 });
    if (phase === 'recovery-reset' || phase === 'recovery-check' || phase === 'recovery-ready') return new Response(JSON.stringify({
      state: 'RecoveryRequired', observedRevision: phase === 'recovery-reset' ? 8 : 10,
      allowedActions: [phase === 'recovery-reset' ? 'RecoveryReset' : phase === 'recovery-check' ? 'RecoveryCheck' : 'RestartFullRun'],
      faultRestart: { faultRunId: 'run-1', resetId: 'reset-1', initialCheckId: 'verified-check', status: phase === 'recovery-ready' ? 'InitialReady' : 'InitialBlocked' },
      failedCommandRecovery: { operationId: 'failed-3d', resetEpoch: phase === 'recovery-reset' ? null : 2 },
      events: [] }), { status: 200 });
    if (phase === 'awaiting-removal') return new Response(JSON.stringify({
      state: 'ReadyForRemoval', observedRevision: 7,
      manualRemovalAllowedEventId: 'removal-event', readyForRemovalSourceMatrixId: 'removal-matrix',
      allowedActions: ['ConfirmManualTrayRemoval'],
      events: ['Preparing', 'PublicHandoffV2Committed', 'AwaitingManualTrayRemoval'] }), { status: 200 });
    return new Response(JSON.stringify({ ...recipeProjection, resultContext: { kind: 'Single', id: 'object-1', localFace: 1 },
      results: resultDisposition == null ? [] : [{ ...(measurementDetails ? {confidence:88, confidenceUnit:'%', defectCount:2, inspectedAtUtc:'2026-09-27T09:00:00Z', inspectionDurationMs:321} : {}), kind: 'Single', id: 'object-1', availability: 'Committed', disposition: resultDisposition, source: 'Simulated', quality: 'Derived', completeness: 'Complete', saveState: 'Committed', inspections: [{ ...(measurementDetails ? { itemName: '<孔径>', measuredValue: 12.34, unit: 'mm', rule: '12±0.5', defects: [{type:'划痕',position:'A面',size:0.8,unit:'mm'}] } : {}), localFace: 1, businessCamera: 'A', itemId: 'SIM', disposition: resultDisposition, technicalState: 'Success', parameters: [{ name: 'ExposureTimeUs', value: 12000, kind: 'RequestedCapture' }], detailAvailability: { defects: 'NotProvided' } }] }],
      events: ['Preparing', 'PublicHandoffV2Committed', 'DetectionCompleted',
      'SortingCompleted', 'AwaitingManualTrayRemoval'], wholeTaskState: phase === 'final' ? 'FinalUnloadCompletion' : 'AwaitingManualRemoval' }), { status: 200 });
  };
  const window: any = { __GAODE_HOST_CONFIG__: { apiBaseUrl: 'http://127.0.0.1:5001', signalrUrl: 'http://127.0.0.1:5001/hubs/station01',
    mode: 'Test', testToken: authorized ? 'test-secret' : null, preparedStartRequest },
    signalR: { HubConnectionBuilder, HttpTransportType: { LongPolling: 4 } },
    dispatchEvent: (event: any) => { events.push(event); } };
  window.GaodeRecipeAuthoring = editor ? { mount: () => editor } : undefined;
  window.localStorage = { getItem: () => initialRunId, setItem: () => {} };
  const context = { window, document, fetch, Headers, Response,
    crypto: { randomUUID: () => '44444444-4444-4444-8444-444444444444' },
    console: { info: (marker: string, json: string) => { if (marker === 'GaodePageDiagnostic') diagnosticLogs.push(JSON.parse(json)); } },
    CustomEvent: class { type: string; options: any; constructor(type: string, options: any = {}) { this.type = type; this.options = options; } },
    setInterval: (cb: () => void) => { timers.push(cb); } };
  vm.runInNewContext(runtime, context);
  return { ids, requests, events, diagnosticLogs, numberNode, metricNodes, identityNodes, click: () => click(),
    notify: async (name: string, message: any) => { callbacks[name]?.(message); await new Promise(resolve => setTimeout(resolve, 10)); },
    subscriptions: () => Object.keys(callbacks),
    confirmRemoval: () => confirmRemoval(), chooseRecipe: () => chooseRecipe(),
    block: async (kind: 'unconfirmed' | 'unsafe') => { phase = kind; timers[0]?.(); await new Promise(resolve => setTimeout(resolve, 10)); },
    final: async () => { phase = 'final'; callbacks.StateChanged?.({ schemaVersion: 's01/notification/2.0', runId: 'run-1', summary: { executionState: 'Completed', wholeTaskState: 'FinalUnloadCompletion', errorCode: null } }); await new Promise(resolve => setTimeout(resolve, 10)); },
    poll: async () => { timers[0]?.(); await new Promise(resolve => setTimeout(resolve, 10)); }, window };
}

test('reopened run restores the frozen recipe header from the backend and preserves it at Final', async () => {
  const projection = { recipeExecution: { recipeId: 'R008-FROZEN', version: '1.2-test', catalogDigest: 'actual-digest' },
    recipeSelection: { recipeId: 'R008-SELECTED', version: '1.1-test', catalogDigest: 'selected-digest' } };
  const p = page(true, requestBody, 'normal', 'Pending', projection, 'run-1');
  await p.poll();
  assert.equal(p.ids.get('selectedRecipeName').textContent, 'R008-FROZEN');
  assert.equal(p.ids.get('selectedRecipeVersion').textContent, '1.2-test');
  await p.final();
  assert.equal(p.ids.get('selectedRecipeName').textContent, 'R008-FROZEN');
  assert.equal(p.ids.get('selectedRecipeVersion').textContent, '1.2-test');
  assert.equal(p.requests.filter(x => x.method === 'POST' && x.url.endsWith('/runs')).length, 0);
});

test('selected recipe header is restored before binding without inventing a model', async () => {
  const p = page(true, requestBody, 'normal', null,
    { recipeSelection: { recipeId: 'R008-SELECTED', version: '1.1-test', catalogDigest: 'selected-digest' } }, 'run-1');
  await p.poll();
  assert.equal(p.ids.get('selectedRecipeName').textContent, 'R008-SELECTED');
  assert.equal(p.ids.get('selectedRecipeVersion').textContent, '1.1-test');
});

test('historical manual flip fields cannot authorize a current confirmation or invent a measured face', async () => {
  const p = page(true, requestBody, 'manual-flip');
  await p.click();
  assert.equal(p.ids.get('manualRemovalButton').textContent, '确认已取盘');
  assert.equal(p.ids.get('manualRemovalButton').disabled, true);
  await p.confirmRemoval();
  assert.equal(p.requests.filter(x => x.url.endsWith('/manual-flip-confirmations')).length, 0);
  assert.equal(p.requests.filter(x => x.url.endsWith('/manual-removal-confirmations')).length, 0);
  assert.doesNotMatch(p.ids.get('faultList').textContent, /人工换面|命令目标/);
  assert.notEqual(p.ids.get('verdictBig').textContent, '完成');
});

test('fault recovery verifies initial state then requires a separate explicit new start; never continues old run', async () => {
  const p = page(true, requestBody, 'recovery');
  await p.click();
  assert.equal(p.ids.get('manualRemovalButton').textContent, '复位');
  await p.confirmRemoval();
  assert.equal(p.ids.get('manualRemovalButton').textContent, '初始核验');
  assert.equal(p.ids.get('manualRemovalReason').value, '');
  await p.confirmRemoval();
  assert.equal(p.requests.filter(x => x.url.endsWith('/recovery-checks')).length, 0);
  p.ids.get('manualRemovalReason').value = '同盘原任务已核查，装载及冻结配置未变';
  await p.confirmRemoval();
  const check = JSON.parse(p.requests.find(x => x.url.endsWith('/recovery-checks')).body);
  assert.equal(check.sameTray, true);
  assert.ok(check.evidenceRefs[0].includes('原任务已核查'));
  assert.equal(p.requests.filter(x => x.url.endsWith('/continue')).length, 0);
  await p.click();
  const restarted = JSON.parse(p.requests.filter(x => x.method === 'POST' && x.url.endsWith('/runs')).at(-1).body);
  assert.equal(restarted.restartFrom.faultRunId, 'run-1');
  assert.equal(restarted.restartFrom.initialCheckId, 'verified-check');
  assert.equal(p.requests.filter(x => x.method === 'POST' && x.url.endsWith('/runs')).length, 2);
  assert.equal(p.requests.filter(x => x.url.endsWith('/manual-removal-confirmations')).length, 0);
  assert.notEqual(p.ids.get('verdictBig').textContent, '完成');
});

test('007 frozen Test sample references remain internally consistent', () => {
  assert.equal(publicSample.purpose, 'Test');
  assert.equal(budgetSample.purpose, 'Test');
  assert.equal(simulationSample.purpose, 'Test');
  assert.deepEqual(simulationSample.publicConfigRef, requestBody.publicConfigRef);
  assert.deepEqual(simulationSample.budgetRef, requestBody.budgetRef);
});

test('007 prepared-load and Host startup references match the frozen samples', () => {
  for (const [field, prefix, config] of [
    ['publicConfigRef', 'Public', publicSample],
    ['budgetRef', 'Budget', budgetSample],
    ['simulationRef', 'Simulation', simulationSample]
  ] as const) {
    const prepared = prepareScript.slice(prepareScript.indexOf(field)).match(
      /@\{\s*id\s*=\s*'([^']+)'\s*;\s*version\s*=\s*'([^']+)'\s*\}/);
    const hostId = startupScript.match(new RegExp(`\\$env:Gaode__${prefix}Id\\s*=\\s*'([^']+)'`));
    const hostVersion = startupScript.match(new RegExp(`\\$env:Gaode__${prefix}Version\\s*=\\s*'([^']+)'`));
    assert.ok(prepared && hostId && hostVersion, `${field} must be supplied by both scripts`);
    assert.deepEqual({ id: prepared[1], version: prepared[2] }, ref(config));
    assert.deepEqual({ id: hostId[1], version: hostVersion[1] }, ref(config));
  }
});

test('prepared button sends frozen references once; acceptance and removal admission are not Final', async () => {
  const p = page();
  await p.poll();
  await p.click();
  const posts = p.requests.filter(x => x.method === 'POST');
  assert.equal(posts.length, 1);
  const post = posts[0];
  assert.ok(post);
  assert.equal(post.headers.get('Authorization'), 'Bearer test-secret');
  assert.deepEqual(JSON.parse(post.body), requestBody);
  await p.click();
  assert.equal(p.requests.filter(x => x.method === 'POST').length, 1);
  assert.notEqual(p.ids.get('verdictBig').textContent, '完成');
  const before = p.events.filter(x => x.type === 'station01:rendered').at(-1).options.detail;
  assert.equal(before.final, false);
  assert.equal(before.facts[1][1], '未提供'); // old event labels cannot manufacture an execution phase
  assert.equal(before.facts[7][1], 'AwaitingManualRemoval');
  assert.equal(p.window.__GAODE_HOST_CONFIG__, undefined);
  await p.final();
  const after = p.events.filter(x => x.type === 'station01:rendered').at(-1).options.detail;
  assert.equal(after.final, true);
  assert.equal(after.source, 'Plc:Virtual · Algorithm:Test · Camera:Test · ManualActor:Test');
  assert.doesNotMatch(p.ids.get('faultList').textContent, /Test\/Simulated/);
  assert.equal(p.ids.get('verdictBig').textContent, '尚无结果');
});

test('012 manual-removal notification refreshes committed admission without claiming Final or sending confirmation', async () => {
  const p = page(true, requestBody, 'awaiting-removal', null, null, 'run-1'); await p.poll();
  assert.ok(p.subscriptions().includes('ManualRemovalAllowed'));
  assert.ok(!p.subscriptions().includes('ObservedUnlocked'));
  const before = p.requests.length;
  await p.notify('ManualRemovalAllowed', { schemaVersion: 's01/notification/2.0', runId: 'run-1' });
  assert.ok(p.requests.length > before);
  assert.match(p.ids.get('faultList').textContent, /removal-event.*removal-matrix.*Host:Test.*Plc:Virtual/);
  assert.equal(p.ids.get('manualRemovalButton').disabled, false);
  assert.equal(p.events.filter(x => x.type === 'station01:rendered').at(-1).options.detail.final, false);
  assert.equal(p.requests.filter(x => x.method === 'POST').length, 0);
});

test('012 existing diagnosis displays observed axes and keeps missing positions unknown', async () => {
  const projection = { axisObservations: [
    { axis: 'ScanZ', position: 12.5, unit: 'mm', reliability: 'Reliable', observedAt: '2026-10-03T11:00:00Z', connectionEpoch: 4, evidenceRef: 'write://scan-observation' },
    { axis: 'GrabZ', position: null, unit: null, reliability: 'Unknown', observedAt: null, connectionEpoch: null, evidenceRef: null } ] };
  const p = page(true, requestBody, 'normal', null, projection, 'run-1'); await p.poll();
  assert.match(p.ids.get('faultList').textContent, /轴 ScanZ · 实测 12.5 mm · Reliable.*write:\/\/scan-observation/);
  assert.match(p.ids.get('faultList').textContent, /轴 GrabZ · 实测 未提供 未提供 · Unknown/);
  assert.doesNotMatch(p.ids.get('faultList').textContent, /轴 GrabZ · 实测 0/);
});

test('008 page confirms tray removal only when the backend allows it', async () => {
  const p = page(true, requestBody, 'awaiting-removal');
  await p.poll();
  assert.equal(p.ids.get('manualRemovalButton').disabled, true);
  await p.click();
  assert.equal(p.ids.get('manualRemovalButton').disabled, false);
  await p.confirmRemoval();
  const confirmation = p.requests.find(x => x.url.endsWith('/manual-removal-confirmations'));
  assert.ok(confirmation);
  assert.equal(confirmation.method, 'POST');
  assert.deepEqual(JSON.parse(confirmation.body), {
    requestId: '44444444-4444-4444-8444-444444444444',
    expectedRevision: 7, reason: '已取盘'
  });
  assert.notEqual(p.ids.get('verdictBig').textContent, '完成');
});

for (const [name, invalid] of [
  ['missing prepared request', null],
  ['missing config reference', { ...requestBody, budgetRef: null }],
  ['malformed config version', { ...requestBody, publicConfigRef: { ...requestBody.publicConfigRef, version: '../bad' } }],
  ['non-Test purpose', { ...requestBody, contextJson: JSON.stringify({ ...JSON.parse(requestBody.contextJson), purpose: 'Production' }) }],
  ['missing identity', { ...requestBody, contextJson: JSON.stringify({ ...JSON.parse(requestBody.contextJson), trayId: '' }) }]
] as const) {
  test(`page rejects ${name} before POST`, async () => {
    const p = page(true, invalid);
    await p.click();
    assert.equal(p.requests.filter(x => x.method === 'POST').length, 0);
    assert.match(p.ids.get('faultList').textContent, /Test上料请求未准备或配置不符/);
  });
}

test('well-formed unknown version reaches authoritative backend and its block is shown', async () => {
  const prepared = { ...requestBody,
    publicConfigRef: { ...requestBody.publicConfigRef, version: 'not-installed-version' } };
  const p = page(true, prepared, 'configuration-blocked');
  await p.click();
  const posts = p.requests.filter(x => x.method === 'POST');
  assert.equal(posts.length, 1);
  assert.deepEqual(JSON.parse(posts[0].body).publicConfigRef, prepared.publicConfigRef);
  assert.ok(p.requests.some(x => x.method === 'GET' && x.url.endsWith('/runs/run-1')));
  assert.match(p.ids.get('faultList').textContent, /ConfigurationNotFound/);
  assert.equal(p.ids.get('verdictBig').textContent, '尚无结果');
  assert.match(p.ids.get('itemList').textContent, /ConfigurationBlocked/);
  assert.notEqual(p.ids.get('verdictBig').textContent, '完成');
  await p.click();
  assert.equal(p.requests.filter(x => x.method === 'POST').length, 1);
});

test('diagnostic view separates unknown safety from reliable unsafe and blocks repeated start', async () => {
  const p = page();
  await p.click();
  await p.block('unconfirmed');
  assert.match(p.ids.get('faultList').textContent, /无法确认设备安全/);
  assert.match(p.ids.get('itemList').textContent, /StartupReadiness/);
  await p.click();
  assert.match(p.ids.get('faultList').textContent, /仅查询原任务/);
  assert.equal(p.requests.filter(x => x.method === 'POST').length, 1);
  await p.block('unsafe');
  assert.match(p.ids.get('faultList').textContent, /设备可靠反馈明确不安全/);
  assert.doesNotMatch(p.ids.get('faultList').textContent, /无法确认设备安全/);
});

test('missing credential produces explicit 401 without a success state', async () => {
  const p = page(false);
  await p.click();
  assert.equal(p.requests.find(x => x.method === 'POST').headers.get('Authorization'), null);
  assert.equal(p.ids.get('verdictBig').textContent, '权限受限');
  assert.notEqual(p.ids.get('verdictBig').textContent, 'OK');
  assert.notEqual(p.ids.get('verdictBig').textContent, '完成');
});

test('persistable page diagnostics distinguish local rejection from accepted request without credentials', async () => {
  const rejected = page(true, { ...requestBody, contextJson: '{}' });
  await rejected.click();
  assert.equal(rejected.requests.filter(x => x.method === 'POST').length, 0);
  assert.ok(rejected.diagnosticLogs.some(x => x.event === 'StartRejectedLocally' && x.postSent === false));
  const accepted = page();
  await accepted.click();
  assert.ok(accepted.diagnosticLogs.some(x => x.event === 'StartAccepted' && x.runId === 'run-1' &&
    x.commandId === 'cmd-1' && x.requestId === requestBody.requestId && x.acceptedNotCompleted));
  await accepted.block('unconfirmed');
  assert.ok(accepted.diagnosticLogs.some(x => x.event === 'StateObserved' && x.reasons?.includes('PlcHeartbeatLost')));
  const before = accepted.diagnosticLogs.filter(x => x.event === 'StateObserved').length;
  await accepted.poll();
  assert.equal(accepted.diagnosticLogs.filter(x => x.event === 'StateObserved').length, before);
  assert.doesNotMatch(JSON.stringify(accepted.diagnosticLogs), /test-secret|Bearer|contextJson|Authorization/);
});

for (const disposition of ['OK', 'NG', 'Pending']) {
  test(`actual runtime keeps ${disposition} separate from Final and missing details`, async () => {
    const p = page(true, requestBody, 'normal', disposition);
    await p.click();
    assert.equal(p.ids.get('verdictBig').textContent, disposition); // committed before Final
    await p.final();
    assert.equal(p.ids.get('verdictBig').textContent, disposition);
    assert.match(p.ids.get('paramList').textContent, /RequestedCapture/);
    assert.match(p.ids.get('paramList').textContent, /12000/);
    assert.match(p.ids.get('itemList').innerHTML, /grid-template-columns: 28px 1fr 80px 56px/);
    assert.match(p.ids.get('itemList').textContent, /完整性 Complete.*保存 Committed/);
    assert.match(p.ids.get('defectList').textContent, /未提供/);
    await p.poll();
    assert.equal(p.ids.get('verdictBig').textContent, disposition);
    assert.equal(p.requests.filter(x => x.url.endsWith('/continue')).length, 0);
  });
}

 test('provided measurements and defects fill the original columns and escape supplied text', async () => {
  const p = page(true, requestBody, 'normal', 'NG', null, null, true);
  await p.click();
  assert.match(p.ids.get('itemList').innerHTML, /&lt;孔径&gt;/);
  assert.doesNotMatch(p.ids.get('itemList').innerHTML, /<孔径>/);
  assert.match(p.ids.get('itemList').textContent, /12±0.5.*12.34 mm/s);
  assert.match(p.ids.get('defectList').textContent, /划痕.*A面.*0.8 mm.*NG/s);
  assert.equal(p.numberNode.textContent, 'object-1');
  assert.deepEqual(p.metricNodes.map(x=>x.textContent), ['88%', '2', '2026-09-27T09:00:00Z', '321 ms']);
  assert.equal(p.identityNodes[3].textContent, '未提供');
  assert.equal(p.identityNodes[4].textContent, '未提供');
});


for (const [provider, displayed] of [['Virtual', 'Test/VirtualPlc'], ['Real', 'Real'], [null, '来源未知']] as const) {
  test(`current semantic startup origin remains ${provider ?? 'missing'} without substituting Test mode`, async () => {
    const p = page(true, requestBody, 'normal', null, null, null, false, provider);
    await p.click();
    await p.block('unconfirmed');
    assert.ok(p.ids.get('faultList').textContent.includes(displayed));
    assert.equal(p.requests.filter(x => x.method === 'POST').length, 1);
  });
}


test('012 delivered projection preserves phase, sparse physical slots, pose exclusion and independent quality', async () => {
  const projection = { recipeExecution: { recipeId: 'frozen', recipeVersion: 'held-v', version: 'held-v', model: 'held-model',
      definitionDigest: 'held-digest', snapshotRef: 'execution-inputs://held', stage: 'Detection',
      executionPhase: { kind: 'PoseRecheck', state: 'Completed', stepSequence: 19, physicalSlotIndex: null, localFace: null,
        entityId: null, scanPoseId: null, transitionId: 'transition-1', observationRef: 'write://observation', evidenceRef: 'write://observation' } },
    observationCoverage: { state: 'Partial', lastObservationRef: 'write://observation' }, abnormalPhysicalSlotIndices: [2],
    slotStates: [{ physicalSlotIndex: 2, presence: 'Present', poseState: 'Abnormal', participation: 'PoseExcluded', reasonCodes: ['PoseAbnormal'], observationRef: 'write://observation' },
      { physicalSlotIndex: 7, presence: 'Absent', poseState: 'Unknown', participation: 'Absent', reasonCodes: [], observationRef: 'write://observation' }] };
  const p = page(true, requestBody, 'normal', 'OK', projection, 'run-1'); await p.poll();
  const facts = p.events.filter(x => x.type === 'station01:rendered').at(-1).options.detail;
  assert.equal(facts.final, false); assert.equal(facts.facts[1][1], 'Detection');
  assert.equal(facts.facts[2][1], '姿态复查 · Completed'); assert.equal(facts.facts[3][1], 'Partial');
  assert.equal(facts.facts[4][1], '2');
  assert.match(p.ids.get('faultList').textContent, /物理槽2：Present\/Abnormal\/PoseExcluded/);
  assert.match(p.ids.get('faultList').textContent, /物理槽7：Absent\/Unknown\/Absent/);
  assert.doesNotMatch(p.ids.get('faultList').textContent, /物理槽1：/);
  assert.equal(p.ids.get('verdictBig').textContent, 'OK'); // already committed quality is not overwritten by pose state
  assert.equal(p.ids.get('selectedRecipeName').textContent, 'held-model · frozen');
});

test('012 missing observation and phase stay unknown even when historical events suggest progress', async () => {
  const p = page(true, requestBody, 'normal', null, { abnormalPhysicalSlotIndices: null,
    observationCoverage: { state: 'NotObserved', lastObservationRef: null }, slotStates: [], recipeExecution: null }, 'run-1');
  await p.poll();
  const facts = p.events.filter(x => x.type === 'station01:rendered').at(-1).options.detail.facts;
  assert.equal(facts[2][1], '未提供'); assert.equal(facts[3][1], 'NotObserved'); assert.equal(facts[4][1], '未可靠观察');
  assert.equal(p.ids.get('selectedRecipeName').textContent, '运行配方未提供');
  assert.doesNotMatch(p.ids.get('faultList').textContent, /全部正常|无异常槽/);
});

test('012 selecting the next saved recipe during a run cannot replace its frozen header or start another run', async () => {
  const item = { recipeId: 'next', version: 'new-v', model: 'new-model', catalogDigest: 'new-catalog', availability: 'Available' };
  const editor = { state: { catalog: [item], recipeId: item.recipeId }, close: () => {} };
  const p = page(true, requestBody, 'normal', null, { recipeExecution: { recipeId: 'held', version: 'old-v', model: 'old-model' } }, 'run-1', false, 'Virtual', editor);
  await p.poll(); p.chooseRecipe(); await p.click();
  assert.equal(p.ids.get('selectedRecipeName').textContent, 'old-model · held');
  assert.equal(p.ids.get('selectedRecipeVersion').textContent, 'old-v');
  assert.equal(p.requests.filter(x => x.method === 'POST').length, 0);
});

test('012 selection sends its observed identity while F matching and newer saved content remain backend decisions', async () => {
  const item = { recipeId: 'saved', version: 'observed-v', model: 'model', catalogDigest: 'observed-digest', availability: 'Available' };
  const editor = { state: { catalog: [item], recipeId: item.recipeId }, close: () => {} };
  const prepared = { ...requestBody, contextJson: JSON.stringify({ ...JSON.parse(requestBody.contextJson), scenarioId: 'other-scenario', occupiedSlots: ['physical-2', 'physical-7'] }) };
  const p = page(true, prepared, 'normal', null, null, null, false, 'Virtual', editor);
  await p.poll(); p.chooseRecipe(); await p.click();
  const sent = JSON.parse(p.requests.find(x => x.method === 'POST' && x.url.endsWith('/runs')).body);
  const context = JSON.parse(sent.contextJson);
  assert.equal(context.schemaVersion, 'station01-start-run-context/2.0'); assert.equal(context.scenarioId, 'other-scenario');
  assert.deepEqual(context.occupiedSlots, ['physical-2', 'physical-7']);
  assert.deepEqual(context.expectedRecipeRef, { recipeId: item.recipeId, version: item.version, catalogDigest: item.catalogDigest });
  assert.equal(JSON.parse(prepared.contextJson).expectedRecipeRef, undefined);
});

test('012 well-formed prepared version observation is not compared to an active catalog version on the page', async () => {
  const prepared = { ...requestBody, contextJson: JSON.stringify({ ...JSON.parse(requestBody.contextJson),
    schemaVersion: 'station01-start-run-context/2.0', expectedRecipeRef: { recipeId: 'saved', version: 'old-observation', catalogDigest: 'old-view' } }) };
  const p = page(true, prepared); await p.click();
  assert.equal(p.requests.filter(x => x.method === 'POST' && x.url.endsWith('/runs')).length, 1);
});

for (const sortingState of ['Completed', 'UnknownHeld']) {
  test(`012 committed sorting projection displays ${sortingState} without event inference or removal permission`, async () => {
    const projection = { sortingState, allowedActions: [],
      recipeExecution: { recipeId: 'held', version: 'frozen', model: 'model', stage: 'Sorting' } };
    const p = page(true, requestBody, 'normal', null, projection, 'run-1');
    await p.poll();
    const view = p.events.filter(x => x.type === 'station01:rendered').at(-1).options.detail;
    assert.equal(view.facts.find((fact: string[]) => fact[0] === '分拣状态')[1], sortingState);
    assert.equal(view.final, false);
    await p.confirmRemoval();
    assert.equal(p.requests.filter(x => x.method === 'POST').length, 0);
  });
}

// 012 T046: component only; missing run facts never become prototype "detecting".
test('existing header binds actual run state and does not invent detection from a connected device',async()=>{
 const p=page();await p.poll();
 assert.match(p.ids.get('stationOperationalStatus').textContent,/运行状态未提供/);
 assert.doesNotMatch(p.ids.get('stationOperationalStatus').textContent,/检测中/);
 await p.click();assert.doesNotMatch(p.ids.get('stationOperationalStatus').textContent,/检测中/);
});
