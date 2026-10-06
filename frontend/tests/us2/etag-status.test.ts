import test from 'node:test';
import assert from 'node:assert/strict';
import { EtagCache } from '../../src/state/etag-cache.ts';
test('status snapshots keep the newest revision', () => { const cache = new EtagCache<any>(); cache.accept({ observedRevision: 5 }); assert.equal(cache.accept({ observedRevision: 4 }), false); });

import { statusViewModel } from '../../src/pages/status-bindings.ts';
test('status binding reads the v2 semantic connection and preserves absent historical fields', () => {
  assert.deepEqual(statusViewModel({ schemaVersion: 's01-status/2.0', host: 'Ready',
    plc: { connection: 'Connected' }, camera: { state: 'Ready' }, algorithm: { state: 'NotIntegrated' }, quality: 'Pending' }),
    { host: 'Ready', plc: 'Connected', camera: 'Ready', algorithm: 'NotIntegrated', quality: 'Pending' });
  assert.deepEqual(statusViewModel({}), { host: 'Unknown', plc: 'Unknown', camera: 'Unknown', algorithm: 'Unknown', quality: 'Unknown' });
});
