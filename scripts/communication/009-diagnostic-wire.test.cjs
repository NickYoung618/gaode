// Independent literal fixtures exercise the real probe, not a production mapping.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { check } = require('./check-009-diagnostic-wire.cjs');
const oracle = JSON.parse(fs.readFileSync(path.join(__dirname,
  '../../backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json'), 'utf8'));
function facts(kind) {
  return { plcAudit: { gap: false, writes: kind === 'communication' ?
    [{ area: 0, documentNumber: 9, value: 1, accepted: true }] : [] },
    plcState: { holdingRegisters: [{ pduOffset: 79, rawValue: kind === 'unsafe' ? 2 : 0 }] } };
}
for (const kind of ['unsafe', 'communication']) test(`DIAG-WIRE/${kind}-accepted`, () => {
  assert.equal(check(facts(kind), kind, oracle).status, 'Passed');
});
for (const row of ['gap', 'wrong-alarm', 'start-after-unsafe', 'xy-command']) test(`DIAG-WIRE/${row}`, () => {
  const value = facts('unsafe');
  if (row === 'gap') value.plcAudit.gap = true;
  if (row === 'wrong-alarm') value.plcState.holdingRegisters[0].rawValue = 4;
  if (row === 'start-after-unsafe') value.plcAudit.writes.push({ area: 0, documentNumber: 9, value: 1, accepted: true });
  if (row === 'xy-command') value.plcAudit.writes.push({ area: 1, documentNumber: 1, value: 5, accepted: true });
  assert.throws(() => check(value, 'unsafe', oracle));
});
