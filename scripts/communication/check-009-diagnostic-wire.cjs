// Communication-only counterpart to the protected page/GET checks. The oracle
// was transcribed from the confirmed source, never imported from either endpoint.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const requireFact = (ok, reason) => { if (!ok) throw new Error(reason); };
function check(facts, kind, oracle) {
  requireFact(['communication', 'unsafe'].includes(kind), 'UnknownDiagnosticCase');
  requireFact(facts.plcAudit?.gap === false && Array.isArray(facts.plcAudit.writes), 'WriteAuditGap');
  const signal = id => {
    const found = oracle.signals.filter(s => s.id === id);
    requireFact(found.length === 1, `OracleSignalMissing:${id}`);
    return found[0];
  };
  const matches = (w, s) => w.area === (s.area === 'Coil' ? 0 : 1) &&
    w.documentNumber === parseInt(s.documentHex, 16);
  const start = signal('PC_Start_Cmd'), move = signal('XY_Move_Cmd');
  const startWrites = facts.plcAudit.writes.filter(w => matches(w, start) && w.value === 1).length;
  const xyMoves = facts.plcAudit.writes.filter(w => matches(w, move) && w.value !== 0).length;
  requireFact(xyMoves === 0, 'UnexpectedMotionWrite');
  requireFact(startWrites === (kind === 'communication' ? 1 : 0), 'UnexpectedStartWriteCount');
  if (kind === 'unsafe') {
    const alarm = signal('Alarm_Bits');
    const values = facts.plcState?.holdingRegisters?.filter(r => r.pduOffset === alarm.pduOffset);
    const bits = oracle.alarmBits.filter(b => b.meaning === 'EmergencyStop');
    requireFact(bits.length === 1 && values?.length === 1 &&
      values[0].rawValue === (1 << bits[0].bit), 'IndependentEmergencyAlarmMismatch');
  }
  return { status: 'Passed', scope: 'CommunicationOnly', kind, startWrites, xyMoves };
}
module.exports = { check };
if (require.main === module) {
  const [input, kind, output] = process.argv.slice(2);
  if (!input || !kind || !output) throw new Error('input communication|unsafe output required');
  const oraclePath = path.join(__dirname, '../../backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json');
  const bytes = fs.readFileSync(input), oracleBytes = fs.readFileSync(oraclePath);
  let result;
  try { result = check(JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/, '')), kind, JSON.parse(oracleBytes)); }
  catch (error) { result = { status: 'Failed', reason: error.message, kind, scope: 'CommunicationOnly' }; }
  result.inputs = Object.fromEntries([[input, bytes], [oraclePath, oracleBytes]].map(([name, data]) =>
    [path.resolve(name), crypto.createHash('sha256').update(data).digest('hex')]));
  fs.writeFileSync(output, JSON.stringify(result, null, 2), { flag: 'wx' });
  process.exitCode = result.status === 'Passed' ? 0 : 1;
}
