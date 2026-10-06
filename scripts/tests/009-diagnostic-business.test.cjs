// Component contract checks only; these fixtures are not captured page evidence.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { verifyBusinessFacts } = require('../verify-station01-page-diagnostics.cjs');
function facts(kind) {
  return { run: { capture: 0, algorithm: 0, startupDiagnostic: {
    schemaVersion: 'device-semantics/1', connectionEpoch: 1,
    safetyAssessment: kind === 'unsafe' ? 'ExplicitUnsafe' : 'Unconfirmed',
    reasonCodes: [kind === 'unsafe' ? 'SafetyInterlockDenied' : 'PlcHeartbeatLost'],
    stopStage: kind === 'unsafe' ? 'StartupReadiness' : 'WaitingClamp',
    disposition: kind === 'unsafe' ? 'BlockedNoDeviceAction' : 'UnknownHeldNoAutomaticRetry',
    semanticObservation: kind === 'unsafe' ? { schemaVersion: 'device-semantics/1', reliability: 'Reliable',
      connection: 'Connected', safetyAssessment: 'ExplicitUnsafe',
      alarms: [{ name: 'EmergencyStop', severity: 'Critical', reliability: 'Reliable' }] } : null
  } } };
}
for (const kind of ['unsafe', 'communication']) test(`DIAG-SCRIPT/${kind}-semantics`, () => {
  assert.doesNotThrow(() => verifyBusinessFacts(facts(kind), kind));
});
for (const row of ['wrong-reason', 'unreliable-unsafe', 'continued-capture', 'missing-alarm'])
  test(`DIAG-SCRIPT/${row}`, () => {
    const value = facts('unsafe');
    if (row === 'wrong-reason') value.run.startupDiagnostic.reasonCodes = [];
    if (row === 'unreliable-unsafe') value.run.startupDiagnostic.semanticObservation.reliability = 'Unavailable';
    if (row === 'continued-capture') value.run.capture = 1;
    if (row === 'missing-alarm') value.run.startupDiagnostic.semanticObservation.alarms = [];
    assert.throws(() => verifyBusinessFacts(value, 'unsafe'));
  });
