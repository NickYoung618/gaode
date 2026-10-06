// Reads only the exited-process evidence packages; never calls live APIs.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const read = (root, file) => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const check = (condition, message) => { if (!condition) throw new Error(message); };
const sha256 = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase();
function verifyBusinessFacts(facts, kind) {
  check(facts.run.capture === 0 && facts.run.algorithm === 0, `${kind}: dependent work continued`);
  const diagnostic = facts.run.startupDiagnostic;
  check(diagnostic?.schemaVersion === 'device-semantics/1', `${kind}: current diagnostic schema missing`);
  if (kind === 'communication') {
    check(diagnostic.safetyAssessment === 'Unconfirmed' &&
      diagnostic.reasonCodes.includes('PlcHeartbeatLost') && diagnostic.stopStage === 'WaitingClamp' &&
      diagnostic.disposition === 'UnknownHeldNoAutomaticRetry' &&
      (!diagnostic.semanticObservation || diagnostic.semanticObservation.reliability !== 'Reliable'),
      'communication: GET diagnosis incorrect');
  } else {
    check(kind === 'unsafe' && diagnostic.safetyAssessment === 'ExplicitUnsafe' &&
      diagnostic.reasonCodes.includes('SafetyInterlockDenied') &&
      diagnostic.semanticObservation?.reliability === 'Reliable' &&
      diagnostic.semanticObservation?.connection === 'Connected' &&
      diagnostic.semanticObservation?.safetyAssessment === 'ExplicitUnsafe' &&
      diagnostic.semanticObservation?.alarms?.some(a => a.name === 'EmergencyStop' &&
        a.severity === 'Critical' && a.reliability === 'Reliable') &&
      diagnostic.stopStage === 'StartupReadiness' && diagnostic.disposition === 'BlockedNoDeviceAction',
      'unsafe: reliable GET diagnosis incorrect');
  }
}
function verify(root, kind) {
  const page = read(root, 'webview2-page-evidence.json');
  const facts = read(root, 'page-api-device-facts.json');
  const index = read(root, 'diagnostic-index.json');
  const desktop = read(root, 'interactive-desktop.json');
  const process = read(root, 'process.json');
  const post = page.network.filter(e => e.method === 'POST' && e.url.endsWith('/api/v1/station01/runs'));
  check(desktop.sessionId > 0 && desktop.mainWindowHandle !== '0', `${kind}: WPF interactive window missing`);
  check(page.target.url.startsWith('https://appassets.local/'), `${kind}: not WebView2 local page`);
  check(post.length === 1 && post[0].status === 202, `${kind}: page POST/202 count incorrect`);
  check(post[0].requestBody.requestId === facts.requestId &&
    post[0].responseBody.commandId === facts.commandId &&
    post[0].responseBody.runId === facts.runId, `${kind}: page/API identities differ`);
  check(index.requestId === facts.requestId && index.commandId === facts.commandId &&
    index.runId === facts.runId, `${kind}: index identities differ`);
  if (kind === 'communication')
    check(page.beforeStatus?.host === 'Ready' && page.beforeStatus.plc.connection === 'Connected' &&
      page.beforeStatus.plc.reliability === 'Reliable' && page.beforeStatus.plc.safetyAssessment === 'Clear',
      `${kind}: pre-click PLC not reliably ready`);
  check(page.after.verdict !== '完成' && page.afterReload.verdict !== '完成' &&
    facts.run.finalOutcome === 0, `${kind}: receipt or fault misrepresented as completion`);
  check(page.afterSecondClick.fault.includes('不能再次启动') &&
    page.afterSecondClick.fault.includes(facts.runId), `${kind}: repeat-click restriction missing`);
  verifyBusinessFacts(facts, kind);
  check(process.configuration.protocolContract && process.configuration.hostDllSha256 &&
    process.configuration.plcDllSha256 && desktop.frontendRuntimeSha256,
    `${kind}: version provenance missing`);
  for (const name of ['02-before-start.png', '03-after-receipt.png',
    '04-failure-displayed.png', '05-second-click.png', '06-after-reload.png']) {
    const entry = index.files.find(f => f.path === name);
    check(entry && entry.sha256 === sha256(path.join(root, name)), `${kind}: screenshot hash missing or wrong: ${name}`);
  }
  const diagnostic = facts.run.startupDiagnostic;
  if (kind === 'communication') {
    const receiptAt = page.interactions.find(e => e.action === 'observed-start-response').atUtc;
    check(Date.parse(receiptAt) < Date.parse(page.faultAtUtc), 'communication: injection preceded 202');
    const hostLog = fs.readFileSync(path.join(root, 'logs', 'host.out.log'), 'utf8');
    const firstFailure = hostLog.match(/PLC failure latched at ([0-9T:.\-]+(?:\+[0-9:]+|Z)): origin=heartbeat/);
    check(firstFailure && Date.parse(firstFailure[1]) > Date.parse(page.faultAtUtc),
      'communication: heartbeat failure preceded injection or is absent');
    check(page.after.fault.includes('无法确认设备安全') &&
      page.afterReload.fault.includes('无法确认设备安全'), 'communication: page diagnosis incorrect');
    check(index.operationId, 'communication: actual operation evidence missing');
  } else {
    check(page.after.fault.includes('设备可靠反馈明确不安全') &&
      !page.after.fault.includes('无法确认设备安全') &&
      page.afterReload.fault.includes('设备可靠反馈明确不安全'), 'unsafe: page diagnosis incorrect');
    check(!index.operationId, 'unsafe: invented operation found');
  }
  return { root, requestId: facts.requestId, commandId: facts.commandId, runId: facts.runId,
    operationId: index.operationId || null, epoch: diagnostic.connectionEpoch,
    stopStage: diagnostic.stopStage, reasonCodes: diagnostic.reasonCodes,
    safetyAssessment: diagnostic.safetyAssessment, disposition: diagnostic.disposition,
    desktopPid: desktop.processId, desktopSession: desktop.sessionId,
    screenshotCount: index.files.filter(f => f.path.endsWith('.png')).length,
    source: 'Test/WPF-WebView2;SoftwareLoopOnly' };
}
module.exports = { verifyBusinessFacts, verify };
if (require.main === module) {
const [communicationRoot, unsafeRoot, outputPath] = process.argv.slice(2);
if (!communicationRoot || !unsafeRoot || !outputPath) throw new Error('communicationRoot unsafeRoot outputPath required');
const result = { verifiedAtUtc: new Date().toISOString(), liveApisCalled: false, scope: 'BusinessAndPageOnly',
  communication: verify(communicationRoot, 'communication'), unsafe: verify(unsafeRoot, 'unsafe'),
  realDeviceVerified: false, originalManualFailureFixed: false };
fs.writeFileSync(outputPath, JSON.stringify(result, null, 2), { flag: 'wx' });
console.log(outputPath);
}
