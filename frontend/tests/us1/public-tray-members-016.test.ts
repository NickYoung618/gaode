import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

test('016 loose members preserve independent cells through editor read edit save and reread', async () => {
  // Declared presentation component API double; real API/SQLite evidence is recorded separately.
  const context: any={structuredClone,AbortController,setTimeout,clearTimeout};
  vm.runInNewContext(readFileSync(new URL('../../src/recipe-authoring.js',import.meta.url),'utf8'),context);
  const editor=context.GaodeRecipeAuthoring;
  let stored: any={schemaVersion:'recipe-definition/5',recipeId:'declared-group',version:'1',definitionDigest:'declared',
    unitKind:'looseGroup',positions:[{slotId:'group',cellId:'r2:c4',physicalSlotIndex:1,unitPattern:'{TrayRunId}/group',members:[
      {material:'upper',memberPattern:'{UnitId}/upper',handling:'individual',cellId:'r2:c4',physicalSlotIndex:1},
      {material:'lower',memberPattern:'{UnitId}/lower',handling:'individual',cellId:'r2:c5',physicalSlotIndex:2}]}],
    stages:[],eCode:{enabled:false},executionPositions:{group:{physicalEntity:{coordinates:[]},members:{
      upper:{coordinates:[],source:{point:{id:'source-upper',x:110,y:100,z:150,unit:'mm',frame:'declared'}}},
      lower:{coordinates:[],source:{point:{id:'source-lower',x:130,y:100,z:150,unit:'mm',frame:'declared'}}}}}}};
  const copy=(value: any)=>JSON.parse(JSON.stringify(value));
  let sent: any;
  const session=editor.createSession({requestTimeoutMs:1000,requestId:()=> 'declared-request',send:async(path: string,init: any={})=> {
    if(path.endsWith('/catalog'))return {value:{items:[],authoringAccess:{canSave:true,canValidate:true}}};
    if(init.method==='PUT'){sent=JSON.parse(init.body);stored={...copy(sent.definition),version:'2',definitionDigest:'declared-saved'};}
    return {value:{definition:copy(stored)},etag:'"declared-'+stored.version+'"'};
  }});
  await session.open();await session.load('declared-group');
  assert.deepEqual(copy(session.state.unsupported),[]);
  assert.deepEqual(copy(editor.objects(session.state.definition)).map((o: any)=>[o.slot,o.cellId]),[[1,'r2:c4'],[2,'r2:c5']]);
  assert.equal(editor.objects(session.state.definition)[1].name.includes('2'),true);
  session.edit((d: any)=>{d.executionPositions.group.members.lower.source.point.x=131;});
  await session.save();
  assert.equal(session.state.phase,'Saved');
  assert.deepEqual(sent.definition.positions[0].members,stored.positions[0].members);
  assert.equal(session.state.definition.executionPositions.group.members.upper.source.point.x,110);
  assert.equal(session.state.definition.executionPositions.group.members.lower.source.point.x,131);
  assert.equal(session.state.definition.positions[0].members[1].physicalSlotIndex,2);
});
