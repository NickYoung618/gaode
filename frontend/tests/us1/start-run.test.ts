import test from 'node:test';
import assert from 'node:assert/strict';
import { startRun } from '../../src/commands/start-run.ts';

test('start uses one request id and distinguishes receipt from physical completion', async () => {
  const calls: any[] = []; const api: any = { start: async (body: any) => { calls.push(body); return { value: { requestAccepted: true, commandId: 'c1', runId: 'run1' } }; } };
  const state = await startRun(api, {}, 'request-1');
  assert.equal(calls.length, 1); assert.equal(calls[0].requestId, 'request-1'); assert.equal(state.status, 'Accepted'); assert.equal(state.runId, 'run1');
});
