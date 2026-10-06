import test from 'node:test';
import assert from 'node:assert/strict';
import { HttpClient } from '../../src/api/http-client.ts';
import { Station01Api } from '../../src/api/station01-api.ts';

test('station API builds public routes and carries requestId', async () => {
  const seen: { url: string; body?: any }[] = [];
  const fetchImpl = async (url: string, init: RequestInit) => { seen.push({ url, body: init.body ? JSON.parse(String(init.body)) : undefined }); return new Response(JSON.stringify({ requestAccepted: true, commandId: 'c' }), { status: 202 }); };
  const api = new Station01Api(new HttpClient({ baseUrl: 'https://host', fetchImpl }));
  await api.start({ requestId: 'r1' });
  assert.equal(seen[0].url, 'https://host/api/v1/station01/runs'); assert.equal(seen[0].body.requestId, 'r1');
});
