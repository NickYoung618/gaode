import test from 'node:test';
import assert from 'node:assert/strict';
import { recoverStart } from '../../src/commands/start-run.ts';
test('timeout recovery queries the original command id', async () => { const seen: string[] = []; const state = await recoverStart({ command: async (id: string) => { seen.push(id); return { value: { runId: 'r', terminalDecision: false } }; } } as any, { requestId: 'req', commandId: 'cmd', status: 'Unknown' }); assert.deepEqual(seen, ['cmd']); assert.equal(state.requestId, 'req'); });
