import test from 'node:test';
import assert from 'node:assert/strict';
import '../../src/recipe-authoring.js';
const { createSession, editCapture } = globalThis.GaodeRecipeAuthoring;
test('full hidden body and original read ETag survive editing; conflicts do not resubmit', async () => {
  const original = { recipeId: 'offline-r', model: 'OFFLINE', approval: { evidenceReference: 'OFFLINE-hidden-source' }, composition: [], captureProfiles: {}, executionPositions: {} };
  let writes = 0;
  const session = createSession({ requestId: () => 'OFFLINE-edit', requestTimeoutMs: 1000, send: async (path, init) => {
    if (path.endsWith('/catalog')) return { value: { items: [], authoringAccess: { canSave: true, canValidate: true } } };
    if (init?.method === 'PUT') {
      writes++; assert.equal(init.headers['If-Match'], 'original-etag');
      assert.equal(JSON.parse(init.body).definition.approval.evidenceReference, 'OFFLINE-hidden-source');
      throw Object.assign(new Error('version conflict'), { status: 412 });
    }
    return { value: { definition: original }, etag: 'original-etag' };
  } });
  await session.open(); await session.load('offline-r');
  session.edit(d => { d.model = 'edited'; }); await session.save();
  assert.equal(writes, 1); assert.notEqual(session.state.phase, 'Saved'); assert.equal(original.model, 'OFFLINE');
});
test('one photograph acquires its own exposure/brightness profile without changing the other photograph', () => {
  const shared = { id: 'shared', version: '1', settings: { profileId: 'shared', exposureUs: 1000, gain: 0, brightnessPercent: 30, lightChannel: 'OFFLINE-channel', settleMs: 0, roiPixels: [0,0,1,1] } };
  const first = { captureProfile: 'shared', stageId: 'stage:1' }, second = { captureProfile: 'shared', stageId: 'stage:1' };
  const body = { captureProfiles: { shared }, executionPositions: { s1: { physicalEntity: { coordinates: [first, second] }, members: {} } } };
  const card = { item: { slotId: 's1', member: 'OFFLINE-part' }, point: first, reference: 'point1', camera: 'A' };
  assert.equal(editCapture(body, card, 'exposureUs', 1100), true);
  assert.equal(editCapture(body, card, 'brightnessPercent', 40), true);
  assert.equal(body.captureProfiles[first.captureProfile].settings.exposureUs, 1100);
  assert.equal(body.captureProfiles[first.captureProfile].settings.brightnessPercent, 40);
  assert.equal(body.captureProfiles[second.captureProfile].settings.exposureUs, 1000);
  assert.equal(shared.settings.brightnessPercent, 30);
});
