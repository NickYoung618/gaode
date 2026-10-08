import test from 'node:test';import assert from 'node:assert/strict';import {readFileSync} from 'node:fs';import vm from 'node:vm';
const sandbox={module:{exports:{}}};vm.runInNewContext(readFileSync(new URL('../../src/commissioning-console.js',import.meta.url),'utf8'),sandbox);const api=sandbox.module.exports;
test('each explicit click creates independent request and tray identities from saved selection',()=>{
const template={schemaVersion:'commissioning-console-template/1',mode:'RealDeviceCommissioning',id:'t',version:'1',sourceReference:'OFFLINE',contextTemplate:{purpose:'Commissioning'},publicConfigRef:{id:'p',version:'1'},budgetRef:{id:'b',version:'1'},simulationRef:{id:'s',version:'1'}};
let id=0;const selected={recipeId:'saved',version:'2',catalogDigest:'catalog',scenarioId:'scene'};
const a=api.newStart(template,selected,()=>String(++id)),b=api.newStart(template,selected,()=>String(++id));
assert.notEqual(a.requestId,b.requestId);assert.notEqual(JSON.parse(a.contextJson).trayId,JSON.parse(b.contextJson).trayId);
assert.deepEqual(JSON.parse(a.contextJson).expectedRecipeRef,{recipeId:'saved',version:'2',catalogDigest:'catalog'});
assert.throws(()=>api.newStart(template,null,()=>String(++id)));assert.throws(()=>api.newStart({...template,sourceReference:null},selected,()=>String(++id)));
});
test('full reset proof and available admission are both required before a new run',()=>{
const run={state:'Cancelled',commissioningRecovery:{status:'ClosedAfterVerifiedReset',recoveryWriteId:'sqlite-write'}};
assert.equal(api.canStartAfterRecovery(run,{state:'Available'}),true);
assert.equal(api.canStartAfterRecovery(run,{state:'Held'}),false);
assert.equal(api.canStartAfterRecovery({state:'Cancelled'},{state:'Available'}),false);
assert.match(api.recoveryNotice(run),/旧任务已结束/);
assert.match(api.recoveryNotice({state:'RecoveryRequired',allowedActions:['CommissioningRecoveryReset']}),/复位并结束旧任务/);
});
test('recovery notice distinguishes an unavailable restart recovery path from PLC reset and new-run admission',()=>{
assert.equal(api.recoveryNotice({state:'Completed'}),null);
assert.match(api.recoveryNotice({runId:'retained',state:'RecoveryRequired',allowedActions:[]}),/没有可用的恢复操作入口/);
assert.match(api.recoveryNotice({state:'RecoveryRequired',allowedActions:['RecoveryCheck']}),/旧任务仍待核验/);
assert.match(api.recoveryNotice({state:'Blocked',faultRestart:{status:'InitialReady'},allowedActions:[]}),/显式点击启动/);
});
