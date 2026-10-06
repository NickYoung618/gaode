import test from 'node:test';
import assert from 'node:assert/strict';
import { canDisplaySuccess } from '../../src/state/status-projection.ts';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
test('unknown quality/save/handoff cannot be displayed as success', async () => { assert.equal(canDisplaySuccess('Unknown'), false); assert.equal(canDisplaySuccess('HandoffNotReady'), false); const out = resolve(process.cwd(), '..', 'artifacts/frontend/status'); await mkdir(out, { recursive: true }); await writeFile(resolve(out, 'restricted-state.json'), JSON.stringify({ mode: 'Test/Simulated', states: ['Unknown', 'HandoffNotReady'], success: false }, null, 2)); });
