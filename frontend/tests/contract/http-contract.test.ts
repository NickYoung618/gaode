import test from 'node:test';
import assert from 'node:assert/strict';
import { HttpClient } from '../../src/api/http-client.ts';
import { ApiError } from '../../src/api/error-contract.ts';

test('HTTP client maps structured and anonymous errors without success inference', async () => {
  const fetchImpl = async () => new Response(JSON.stringify({ error: 'NotReady', message: '未就绪' }), { status: 503, headers: { 'content-type': 'application/json' } });
  await assert.rejects(() => new HttpClient({ baseUrl: 'https://host', fetchImpl }).get('/api/v1/station01/status'), (error: any) => error instanceof ApiError && error.contract.code === 'NotReady' && error.contract.httpStatus === 503);
});

test('HTTP client preserves ETag and 304 as cache signal', async () => {
  const fetchImpl = async (_url: string, init: RequestInit) => { assert.equal(new Headers(init.headers).get('If-None-Match'), 'abc'); return new Response(null, { status: 304, headers: { ETag: 'abc' } }); };
  const result = await new HttpClient({ baseUrl: 'https://host', fetchImpl }).get('/status', 'abc');
  assert.equal(result.status, 304); assert.equal(result.etag, 'abc');
});
