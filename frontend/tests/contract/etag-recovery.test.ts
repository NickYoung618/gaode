import test from 'node:test';
import assert from 'node:assert/strict';
import { EtagCache } from '../../src/state/etag-cache.ts';

test('ETag cache rejects lower observed revisions and accepts 304', () => {
  const cache = new EtagCache<any>();
  assert.equal(cache.accept({ revision: 3, value: 'new' }, 'e3'), true);
  assert.equal(cache.accept({ revision: 2, value: 'old' }, 'e2'), false);
  assert.equal(cache.current.value, 'new'); assert.equal(cache.accept(undefined, 'e3', 304), true);
});
