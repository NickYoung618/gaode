import test from 'node:test';
import assert from 'node:assert/strict';
import { transition } from '../../src/state/ui-state.ts';
import { HttpClient } from '../../src/api/http-client.ts';
test('authorization failures remain distinct from offline and generic errors', () => { assert.equal(transition('Ready', 'unauthorized'), 'Unauthorized'); assert.equal(transition('Ready', 'forbidden'), 'Forbidden'); assert.equal(transition('Ready', 'offline'), 'Offline'); });
test('all contract failure statuses remain errors', async () => {
  for (const status of [401, 403, 404, 409, 429, 503]) {
    const client = new HttpClient({ baseUrl: 'https://host', fetchImpl: async () => new Response(JSON.stringify({ code: `E${status}`, message: '受限' }), { status }) });
    await assert.rejects(() => client.get('/status'), (error: any) => error.contract.httpStatus === status && error.contract.code === `E${status}`);
  }
});
