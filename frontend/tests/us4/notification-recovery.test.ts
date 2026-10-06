import test from 'node:test';
import assert from 'node:assert/strict';
import { reconnect } from '../../src/state/recovery-coordinator.ts';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
test('reconnect is bounded and refreshes only after connection', async () => { let attempts = 0; let refreshed = 0; const ok = await reconnect({ maxAttempts: 2, delayMs: 1, connect: async () => { if (++attempts < 2) throw new Error('offline'); }, refresh: async () => { refreshed++; }, onOffline: () => {} }); assert.equal(ok, true); assert.equal(refreshed, 1); const out = resolve(process.cwd(), '..', 'artifacts/frontend/notifications'); await mkdir(out, { recursive: true }); await writeFile(resolve(out, 'recovery.json'), JSON.stringify({ attempts, refreshed, connected: ok }, null, 2)); });
