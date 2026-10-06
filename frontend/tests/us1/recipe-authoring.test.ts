import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// Explicit component doubles: these never constitute SQLite/API/joint evidence.
const context: any = { structuredClone, AbortController, setTimeout, clearTimeout };
vm.runInNewContext(readFileSync(new URL('../../src/recipe-authoring.js', import.meta.url), 'utf8'), context);
const createSession = context.GaodeRecipeAuthoring.createSession;
const editor = context.GaodeRecipeAuthoring;
const copy = (value: any) => JSON.parse(JSON.stringify(value));

test('approved navigation projects independent members and whole assembly handling without Stage/camera aliases', () => {
  const mechanical = {coordinates:[],purposePoints:{pick:{purpose:'FlipPick',point:{x:8,y:9},fixed:{z:10}},
    put:{purpose:'FlipPutBack',point:{x:18,y:19},fixed:{z:20}}},flip:{stages:{'stage:2':{pickPointRef:'pick',putBackPointRef:'put'}}}};
  const member = (name:string) => ({...copy(mechanical),coordinates:[1,2,3].flatMap(stage=>['A','B'].map(camera=>({
    pointRef:'same',stageId:'stage:'+stage,localFace:stage===3?2:1,camera,point:{x:stage,y:2},fixed:{z:3},objectPattern:'{UnitId}/'+name}))) });
  const d:any={unitKind:'looseGroup',composition:[{material:'one',localFaces:[1,2]},{material:'two',localFaces:[1,2]}],
    positions:[{slotId:'slot',cellId:'r4:c6',members:[{material:'one'},{material:'two'}]}],
    stages:[1,2,3].map(number=>({number,targets:['one','two'].map(material=>({material,localFace:number===3?2:1,cameraPair:'AB',captureProfile:'base'}))})),
    captureProfiles:{base:{id:'base',settings:{profileId:'base',exposureUs:100,gain:1,brightnessPercent:10}}},eCode:{enabled:false},
    executionPositions:{slot:{physicalEntity:copy(mechanical),members:{one:member('one'),two:member('two')}}}};
  let items=editor.objects(d);
  const a=editor.navigationCards(d,items[0],'photo',1),b=editor.navigationCards(d,items[1],'photo',1);
  assert.equal(a.length,4);assert.equal(editor.navigationCards(d,items[0],'photo',2).length,2);
  editor.editCapture(d,a[0],'exposureUs',711);editor.editCapture(d,a[2],'exposureUs',922);editor.editCapture(d,b[0],'gain',4);
  assert.equal(d.captureProfiles[a[0].point.captureProfile].settings.exposureUs,711);
  assert.equal(d.captureProfiles[a[2].point.captureProfile].settings.exposureUs,922);
  assert.equal(d.captureProfiles[b[0].point.captureProfile].settings.exposureUs,100);
  const oneFlip=editor.navigationCards(d,items[0],'flip',null),twoFlip=editor.navigationCards(d,items[1],'flip',null);
  oneFlip[0].point.point.x=71;assert.equal(twoFlip[0].point.point.x,8);
  d.unitKind='assembledEntity';items=editor.objects(d);
  const oneWhole=editor.navigationCards(d,items[0],'flip',null),twoWhole=editor.navigationCards(d,items[1],'flip',null);
  assert.equal(oneWhole.length,2);assert.equal(oneWhole[0].title,'整体翻面取料');
  assert.equal(oneWhole[0].point,twoWhole[0].point);
  oneWhole[0].point.point.x=81;assert.equal(twoWhole[0].point.point.x,81);
  assert.equal(d.executionPositions.slot.members.one.purposePoints.pick.point.x,71);
  assert.equal(editor.navigationCards(d,items[0],'photo',1)[0].point.captureProfile,a[0].point.captureProfile);
  d.eCode={enabled:true,representativeMaterial:'two',extraPose:{pickPointRef:'mark-pick',putBackPointRef:'mark-put'}};
  d.executionPositions.slot.physicalEntity.purposePoints['mark-pick']={purpose:'FlipPick',point:{x:91,y:92},fixed:{z:93}};
  d.executionPositions.slot.physicalEntity.purposePoints['mark-put']={purpose:'FlipPutBack',point:{x:94,y:95},fixed:{z:96}};
  // An E representative part other than the first must still expose the existing whole transition.
  assert.equal(editor.navigationCards(d,items[0],'flip',null).length,4);
  assert.equal(editor.navigationCards(d,items[1],'flip',null)[2].point,editor.navigationCards(d,items[0],'flip',null)[2].point);
});

test('manual matrix preserves holes and independent row-major region numbers without mutating cell data', () => {
  const d: any = {trayLayout:{rows:10,columns:10,cells:[
    {cellId:'r4:c6',row:4,column:6,region:'OK'},
    {cellId:'r2:c1',row:2,column:1,region:'NG'},
    {cellId:'r2:c4',row:2,column:4,region:'OK'},
    {cellId:'r2:c10',row:2,column:10,region:'Pending'}]},
    positions:[{cellId:'r4:c6',parameters:{exposureUs:731}}]};
  assert.deepEqual(copy(editor.orderedCells(d)).map(c=>[c.cellId,c.region,c.ordinal]),
    [['r2:c1','NG',1],['r2:c4','OK',1],['r2:c10','Pending',1],['r4:c6','OK',2]]);
  d.trayLayout=editor.changeCell(d,2,4,'OK');
  assert.equal(editor.orderedCells(d).find(c=>c.cellId==='r4:c6').ordinal,1);
  assert.equal(d.positions[0].parameters.exposureUs,731);
  assert.equal(d.trayLayout.cells.some(c=>c.cellId==='r3:c5'),false);
  d.trayLayout=editor.changeCell(d,4,6,'Pending');
  assert.equal(d.trayLayout.cells.filter(c=>c.cellId==='r4:c6').length,1);
  assert.equal(d.trayLayout.cells.find(c=>c.cellId==='r4:c6').region,'Pending');
});

test('same object and local face in repeated AB stages retain separate camera parameter ownership', () => {
  const d:any={unitKind:'independentPart',positions:[{slotId:'s',cellId:'r2:c4',members:[{material:'part'}]}],
    stages:[1,2].map(number=>({number,targets:[{material:'part',localFace:1,cameraPair:'AB',captureProfile:'base'}]})),
    eCode:{enabled:false},captureProfiles:{base:{id:'base',settings:{profileId:'base',exposureUs:100,gain:1,brightnessPercent:20}}},
    executionPositions:{s:{physicalEntity:{coordinates:[1,2].flatMap(n=>['A','B'].map(camera=>({
      pointRef:'same-reference',stageId:`stage:${n}`,localFace:1,camera,point:{x:1,y:2},fixed:{z:3}}))),purposePoints:{},sorting:{}},members:{}}}};
  const item=editor.objects(d)[0];
  const first=editor.cards(d,item,'group:1'),second=editor.cards(d,item,'group:2');
  assert.equal(first.length,2);assert.equal(second.length,2);
  editor.editCapture(d,first[0],'exposureUs',711);
  editor.editCapture(d,second[0],'exposureUs',922);
  editor.editCapture(d,first[1],'exposureUs',633);
  assert.equal(d.captureProfiles[first[0].point.captureProfile].settings.exposureUs,711);
  assert.equal(d.captureProfiles[second[0].point.captureProfile].settings.exposureUs,922);
  assert.equal(d.captureProfiles[first[1].point.captureProfile].settings.exposureUs,633);
  assert.equal(second[1].point.captureProfile,undefined);
});

test('per-capture edits keep objects, faces and AB/E camera values independent without exposing backend controls', () => {
  const coordinates = (slot: string) => [1, 2].flatMap(face => ['A','B'].map(camera => ({ pointRef:`p-${face}-${camera}`,localFace:face,camera,stageId:`stage:${face}`,slotId:slot,
    point:{x:10,y:20},fixed:{z:30} })));
  const object = (slot: string) => ({ coordinates:coordinates(slot),purposePoints:{scan:{purpose:'EScan',point:{x:4,y:5},fixed:{z:6}}},sorting:{},source:null });
  const d: any = { schemaVersion:'recipe-definition/2',unitKind:'looseGroup',composition:[{material:'part',localFaces:[1,2]}],
    positions:['s1','s2'].map((slotId,i)=>({slotId,physicalSlotIndex:i+1,members:[{material:'part'}]})),
    stages:[1,2].map(number=>({number,targets:[{material:'part',localFace:number,cameraPair:'AB',captureProfile:'base'}]})),
    eCode:{enabled:true,representativeMaterial:'part',scanPointRef:'scan',captureProfile:'base'},
    captureProfiles:{base:{id:'base',version:'existing',settings:{profileId:'base',exposureUs:100,gain:1,brightnessPercent:10,roiPixels:[0,0,4,4],lightChannel:'retained',settleMs:5}}},
    executionPositions:Object.fromEntries(['s1','s2'].map(slot=>[slot,{slotId:slot,physicalEntity:object(slot),members:{part:object(slot)}}])) };
  editor.migrateForSave(d);
  const items = editor.objects(d), card = editor.cards(d,items[0],'face:1')[0];
  editor.editCapture(d,card,'exposureUs',321);
  const own = d.captureProfiles[card.point.captureProfile]; assert.equal(own.settings.exposureUs,321);
  for (const other of [editor.cards(d,items[0],'face:1')[1],editor.cards(d,items[0],'face:2')[0],editor.cards(d,items[1],'face:1')[0],editor.cards(d,items[0],'E')[0]])
    assert.equal(d.captureProfiles[other.point.captureProfile].settings.exposureUs,100);
  assert.equal(own.settings.lightChannel,'retained'); assert.deepEqual(copy(own.settings.roiPixels),[0,0,4,4]);
  assert.equal(d.sortingGripperId,undefined); // migration never chooses gripper1 implicitly
  d.captureProfiles['photo-editor-shared'] = copy(d.captureProfiles.base);
  const a = editor.cards(d,items[0],'face:1')[0], b = editor.cards(d,items[0],'face:1')[1];
  a.point.captureProfile = 'photo-editor-shared'; b.point.captureProfile = 'photo-editor-shared';
  editor.editCapture(d,a,'exposureUs',456);
  assert.equal(d.captureProfiles[b.point.captureProfile].settings.exposureUs,100); // a server-shaped key is not proof of unique ownership
  const source = readFileSync(new URL('../../src/recipe-authoring.js', import.meta.url),'utf8');
  for(const forbidden of ['选择已有采集配置','配置来源','配置ID','算法需求','整体配置Pattern','内容摘要','准入依据','结果合同','ROI（','稳定等待（'])
    assert.equal(source.includes(forbidden),false,forbidden);
  assert.equal(source.includes("element('details'"),false);
});
const candidate = () => ({ schemaVersion: 'recipe-definition/2', recipeId: '', version: '', definitionDigest: '',
  fCode: ' component code ', model: 'component model', stages: [{ number: 9, targets: [{ localFace: 2, cameraPair: 'CD' }] }],
  executionPositions: { slot: { physicalEntity: { purposePoints: { pick: { purpose: 'FlipPick', fixed: { z: 12.3 } } },
    rotation: { entry: { target: { point: { z: 21 }, coordinateEvidenceReference: 'component-source' }, actionTimeoutMs: 120 }, poses: {}, exits: {} } } } } });
const saved = (version = 'version-1') => ({ ...candidate(), recipeId: 'identity', version, definitionDigest: 'component-digest' });
const envelope = (version = 'version-1') => ({ value: { definition: saved(version), savedAt: 'component-time', requestId: 'request-1' }, etag: `"read-${version}"` });
const access = { value: { items: [], authoringAccess: { canSave: true, canValidate: true } } };
function fixture(overrides: (path: string, init: any) => any = () => undefined) {
  const calls: any[] = [];
  const send = async (path: string, init: any = {}) => {
    calls.push({ path, method: init.method || 'GET', headers: init.headers, body: init.body });
    const overridden = overrides(path, init);
    if (overridden !== undefined) return await overridden;
    if (path.endsWith('/catalog')) return copy(access);
    if (path.endsWith('/validate')) return { value: { valid: false, issues: [{ code: 'FromCommonValidator', fieldPath: 'stages[0]', message: '共同校验问题' }] } };
    return envelope();
  };
  const session = createSession({ send, requestId: () => 'request-1', requestTimeoutMs: 1000 });
  return { session, calls };
}

test('component: three-section editing preserves the entire candidate and close performs no write', async () => {
  const { session, calls } = fixture();
  await session.open(); session.create(candidate());
  session.edit(d => { d.model = 'changed'; });
  session.section('points'); session.section('review'); session.section('basic');
  const expected = candidate(); expected.model = 'changed';
  assert.deepEqual(copy(session.state.definition), expected);
  const external = session.state; external.definition.model = 'external mutation';
  assert.equal(session.state.definition.model, 'changed');
  session.close();
  assert.equal(session.state.phase, 'Closed'); assert.equal(session.state.definition, null);
  assert.equal(calls.filter(c => c.method !== 'GET').length, 0);
});

test('component: load full value, retain unedited fields and use the original ETag on PUT', async () => {
  const { session, calls } = fixture(); await session.open(); await session.load('identity');
  session.edit(d => { d.fCode = ' new code '; });
  await session.save();
  const write = calls.find(c => c.method === 'PUT');
  assert.equal(write.path, '/api/v1/recipes/identity');
  assert.equal(write.headers['If-Match'], '"read-version-1"');
  const body = JSON.parse(write.body); assert.equal(body.requestId, 'request-1');
  assert.equal(body.definition.fCode, ' new code ');
  assert.deepEqual(body.definition.executionPositions, candidate().executionPositions);
  assert.deepEqual(body.definition.stages, candidate().stages);
  assert.equal(session.state.phase, 'Saved');
});

test('component: checking displays common issues without persistence or a local process validator', async () => {
  const { session, calls } = fixture(); await session.open(); session.create(candidate());
  await session.check();
  assert.equal(session.state.phase, 'Rejected');
  assert.equal(session.state.issues[0].code, 'FromCommonValidator');
  assert.equal(calls.filter(c => c.method !== 'GET').length, 1);
  assert.equal(calls.at(-1).path, '/api/v1/recipes/validate');
  assert.deepEqual(JSON.parse(calls.at(-1).body).definition, candidate());
});

test('component: 412 preserves user input and does not silently reload or retry', async () => {
  const { session, calls } = fixture((_path, init) => {
    if (init.method === 'PUT') { const error: any = new Error('版本冲突'); error.status = 412; return Promise.reject(error); }
  });
  await session.open(); await session.load('identity'); session.edit(d => { d.model = 'pending edit'; });
  await session.save();
  assert.equal(session.state.phase, 'Rejected'); assert.equal(session.state.definition.model, 'pending edit');
  assert.equal(session.state.etag, '"read-version-1"');
  assert.equal(calls.filter(c => c.method === 'PUT').length, 1);
  assert.equal(calls.filter(c => c.path.endsWith('/identity')).length, 2);
});

test('component: a lost save reply is unknown, no duplicate write while pending and no automatic replay', async () => {
  let reject!: (reason: unknown) => void;
  const pending = new Promise((_resolve, failure) => { reject = failure; });
  const { session, calls } = fixture((_path, init) => init.method === 'POST' ? pending : undefined);
  await session.open(); session.create(candidate());
  const save = session.save(); await session.save();
  assert.equal(session.state.phase, 'Saving');
  reject(new TypeError('network lost')); await save;
  assert.equal(session.state.phase, 'Unknown');
  assert.equal(calls.filter(c => c.method === 'POST').length, 1);
  assert.equal(session.state.definition.recipeId, '');
});

test('component: confirmed save followed by failed reread remains saved, with the new identity', async () => {
  const { session } = fixture((path, init) => {
    if (init.method === 'POST') return envelope('version-2');
    if (path.endsWith('/identity')) return Promise.reject(new TypeError('reread unavailable'));
  });
  await session.open(); session.create(candidate()); await session.save();
  assert.equal(session.state.phase, 'Saved'); assert.equal(session.state.definition.version, 'version-2');
  assert.equal(session.state.etag, '"read-version-2"');
  assert.match(session.state.message, /已保存.*重读失败/);
});

test('component: delayed reads cannot overwrite a later selection or reopen a closed editor', async () => {
  let resolve!: (value: any) => void;
  const slow = new Promise(done => { resolve = done; });
  const { session } = fixture(path => path.endsWith('/slow') ? slow : undefined);
  await session.open(); const loading = session.load('slow'); await session.load('identity');
  resolve({ value: { definition: { ...saved(), recipeId: 'slow' } }, etag: '"slow"' }); await loading;
  assert.equal(session.state.definition.recipeId, 'identity');
  session.close(); assert.equal(session.state.definition, null);
});

test('component: missing write permission and missing read ETag never dispatch a save', async () => {
  const denied = fixture(path => path.endsWith('/catalog')
    ? { value: { items: [], authoringAccess: { canValidate: true, canSave: false } } } : undefined);
  await denied.session.open(); denied.session.create(candidate()); await denied.session.save();
  assert.equal(denied.calls.filter(c => c.method === 'POST').length, 0);
  const noTag = fixture(path => path.endsWith('/identity') ? { value: { definition: saved() } } : undefined);
  await noTag.session.open(); await noTag.session.load('identity'); await noTag.session.save();
  assert.equal(noTag.calls.filter(c => c.method === 'PUT').length, 0);
  assert.equal(noTag.session.state.phase, 'Rejected');
});

test('component: a confirmed save still being reread cannot overwrite a newly enabled edit', async () => {
  let resolve!: (value: any) => void;
  const reread = new Promise(done => { resolve = done; });
  const { session, calls } = fixture((path, init) =>
    path.endsWith('/identity') && !init.method ? reread : undefined);
  await session.open(); session.create(candidate()); const saving = session.save();
  await new Promise(done => setImmediate(done));
  assert.equal(session.state.phase, 'Saved'); assert.equal(session.state.refreshing, true);
  session.edit(d => { d.model = 'must not be accepted while controls are disabled'; });
  await session.save();
  assert.equal(session.state.definition.model, 'component model');
  resolve(envelope()); await saving;
  assert.equal(session.state.refreshing, false);
  assert.equal(calls.filter(c => c.method === 'POST').length, 1);
});

test('component: basic editing sends no stage or execution requests', async () => {
  const { session, calls } = fixture();
  await session.open(); session.create(candidate());
  session.edit(d => { d.model = 'edited model'; });
  assert.equal(session.loadConfigurationSource, undefined);
  assert.equal(calls.some(c => c.path.endsWith('/editor-stage-identities') || c.path.endsWith('/plan')), false);
});
test('component: an unknown nested field is retained but cannot be edited or silently saved', async () => {
  const newer = envelope(); (newer.value.definition.executionPositions.slot.physicalEntity as any).futurePurpose = { value: 1 };
  const { session, calls } = fixture(path => path.endsWith('/identity') ? newer : undefined);
  await session.open(); await session.load('identity');
  session.edit(d => { d.model = 'discard'; }); await session.save();
  assert.equal(session.state.definition.model, 'component model');
  assert.equal(session.state.definition.executionPositions.slot.physicalEntity.futurePurpose.value, 1);
  assert.deepEqual(copy(session.state.unsupported), ['definition.executionPositions.slot.physicalEntity.futurePurpose']);
  assert.equal(calls.filter(c => c.method === 'PUT').length, 0);
});
