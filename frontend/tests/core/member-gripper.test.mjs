import test from 'node:test';
import assert from 'node:assert/strict';
import '../../src/recipe-authoring.js';

test('member gripper fields survive read, edit and save through the existing API session', async () => {
  const definition={recipeId:'group',unitKind:'looseGroup',composition:[
    {material:'A',localFaces:[1],sortingGripperId:1},
    {material:'B',localFaces:[1],sortingGripperId:2}]};
  let sent;
  const session=globalThis.GaodeRecipeAuthoring.createSession({requestId:()=> 'core',requestTimeoutMs:1000,
    send:async (path,init)=> {
      if(path.endsWith('/catalog'))return {value:{items:[],authoringAccess:{canSave:true,canValidate:true}}};
      if(init?.method==='PUT'){sent=JSON.parse(init.body);return {value:{definition:sent.definition},etag:'v2'};}
      return {value:{definition},etag:'v1'};
    }});
  await session.open();await session.load('group');
  assert.deepEqual(session.state.unsupported,[]);
  session.edit(d=>d.composition[1].sortingGripperId=1);
  await session.save();
  assert.deepEqual(sent.definition.composition.map(m=>m.sortingGripperId),[1,1]);
  assert.equal(definition.composition[1].sortingGripperId,2);
});
