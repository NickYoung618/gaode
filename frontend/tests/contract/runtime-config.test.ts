import test from 'node:test';
import assert from 'node:assert/strict';
import { createRuntimeConfig, PROTOTYPE_SHA256 } from '../../src/config/runtime-config.ts';

test('runtime config accepts only approved prototype and HTTP(S) endpoints', () => {
  const cfg = createRuntimeConfig({ apiBaseUrl: 'https://host.example', signalrUrl: 'https://host.example/hubs/station01', mode: 'Test', prototypeSha256: PROTOTYPE_SHA256 });
  assert.equal(cfg.prototypeSha256, PROTOTYPE_SHA256);
  assert.throws(() => createRuntimeConfig({ apiBaseUrl: 'file:///db', signalrUrl: 'https://x', prototypeSha256: PROTOTYPE_SHA256 }));
});
