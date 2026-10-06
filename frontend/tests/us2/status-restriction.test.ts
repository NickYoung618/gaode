import test from 'node:test';
import assert from 'node:assert/strict';
import { canDisplaySuccess, displayState } from '../../src/state/status-projection.ts';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
test('restricted device and algorithm states never become success', async () => {
  for (const state of ['NotIntegrated', 'NotReady', 'Unknown', 'Pending']) { assert.equal(canDisplaySuccess(state), false); assert.equal(displayState(state), state); }
  const fixture = JSON.parse(await readFile(new URL('../fixtures/full-simulation.json', import.meta.url), 'utf8'));
  for (const state of Object.values(fixture.capabilities)) assert.equal(canDisplaySuccess(state), false);
  assert.equal(fixture.mustNotDisplaySuccess, true);
  const out = resolve(process.cwd(), '..', 'artifacts/frontend/status'); await mkdir(out, { recursive: true }); await writeFile(resolve(out, 'full-simulation.json'), JSON.stringify(fixture, null, 2));
});
