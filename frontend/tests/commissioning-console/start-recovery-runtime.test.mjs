import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { webcrypto } from 'node:crypto';
import vm from 'node:vm';
const root=new URL('../../',import.meta.url);
const helpers={module:{exports:{}}};
vm.runInNewContext(readFileSync(new URL('src/commissioning-console.js',root),'utf8'),helpers);
const runtime=readFileSync(new URL('src/runtime.js',root),'utf8');
// Execute the actual click binding with offline API/DOM boundaries, not a copied decision.
const binding=runtime.slice(runtime.indexOf("  if (startButton) startButton.addEventListener('click'"),runtime.indexOf("  if (document.getElementById('moduleGrid')"));
async function click({held=false,missingProof=false}={}) {
 const run=JSON.parse(readFileSync(new URL('tests/commissioning-console/fixtures/recovered-run-r8.json',root),'utf8'));
 if(missingProof) run.commissioningRecovery=null;
 const posts=[],events=[];let handler;
 const scope={apiBase:'http://offline.invalid',latestEvidence:null,commissioning:true,latestRun:run,nextAdmission:{state:held?'Held':'Available'},
  pendingOperation:{subjectId:'operator',requestId:'old-request',runId:run.runId},confirmedIdentity:{subjectId:'operator'},
  startSubmitted:true,commandPending:false,prepared:null,runId:run.runId,receipt:null,latestStatus:null,
  latestMedia:null,resultFocus:null,resultFocusRun:null,lastStartFailure:null,lastStartHttpStatus:null,
  host:{mode:'RealDeviceCommissioning',commissioningTemplate:{schemaVersion:'commissioning-console-template/1',mode:'RealDeviceCommissioning',
   id:'offline',version:'1',sourceReference:'OFFLINE TEST ONLY',contextTemplate:{purpose:'Commissioning',stationId:'station01'},
   publicConfigRef:{id:'p',version:'1'},budgetRef:{id:'b',version:'1'},simulationRef:{id:'s',version:'1'}}},
  selectedRecipe:{recipeId:'saved-recipe',version:'saved-version',catalogDigest:'saved-catalog',scenarioId:'single-flip'},
  crypto:webcrypto,TextEncoder,CustomEvent:class{},startButton:{addEventListener:(_,fn)=>{handler=fn;}},
  window:{GaodeCommissioningConsole:helpers.module.exports,station01:{start:async body=>{posts.push(body);return {runId:'new-run',commandId:'new-command'};}},dispatchEvent(){}},
  diagnostic:(event,facts)=>events.push({event,...facts}),setText(){},render(){},rememberRun(){},refresh:async()=>{},
  preparedSelection:()=>scope.prepared,legalPreparedRequest:p=>!!p?.requestId&&!!p?.contextJson,
  savePending:value=>{scope.pendingOperation=value;}};
 vm.runInNewContext(binding,scope);await handler();return {posts,events,scope,run};
}
test('actual start click after persisted reset sends a fresh new-run request with recovery link',async()=>{
 const {posts,events,run}=await click();assert.equal(posts.length,1);
 const request=posts[0];assert.notEqual(request.requestId,'old-request');
 assert.equal(request.commissioningRestartFrom.runId,run.runId);
 assert.equal(request.commissioningRestartFrom.recoveryWriteId,run.commissioningRecovery.recoveryWriteId);
 const context=JSON.parse(request.contextJson);assert.equal(context.expectedRecipeRef.recipeId,'saved-recipe');
 assert.equal(context.expectedRecipeRef.version,'saved-version');assert.ok(context.trayId);
 assert.ok(events.some(e=>e.event==='StartPosting'));assert.ok(events.some(e=>e.event==='StartAccepted'));
});
test('actual click keeps the old run held when backend admission is held',async()=>{
 const {posts,scope}=await click({held:true});assert.equal(posts.length,0);assert.equal(scope.startSubmitted,true);
});
test('actual click does not replace a run without persistent recovery proof',async()=>{
 const {posts,scope}=await click({missingProof:true});assert.equal(posts.length,0);assert.equal(scope.startSubmitted,true);
});
