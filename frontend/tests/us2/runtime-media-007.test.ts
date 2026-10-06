import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const runtime = readFileSync(new URL('../../src/runtime.js', import.meta.url), 'utf8');
const runId = '11111111-1111-4111-8111-111111111111';
const media = (suffix: string, role: string, camera: string, revision: number, readiness = 'Ready') => ({
  mediaId: `22222222-2222-4222-8222-2222222222${suffix}`,
  captureId: `33333333-3333-4333-8333-3333333333${suffix}`,
  role, businessCamera: camera, stepSequence: role === 'Detection' ? revision : null,
  committedRevision: revision, committedAtUtc: '2026-09-23T00:00:00Z',
  readiness, objectId: suffix === '01' ? 'object-old' : 'object-current', localFace: 1, source: 'Test/FixedImage'
});

test('actual run media bytes bind to distinct temporary Test slots without demo images or guessed order', async () => {
  const items = [media('01', 'Detection', 'A', 4), media('02', 'ThreeD', 'ThreeD', 1),
    media('03', 'Detection', 'A', 9), media('04', 'F', 'F', 2, 'NotReady'),
    media('05', 'Detection', 'B', 6)];
  const names = ['密封面环面检测1', '密封面环面检测2', '密封面孔底检测1', '密封面孔底检测2',
    '来料状态2D检测', '来料状态3D检测', '读码参数'];
  const tiles = names.map(name => {
    const image: any = { alt: name, dataset: {}, src: 'assets/demo.jpg',
      removeAttribute: (key: string) => { if (key === 'src') image.src = ''; } };
    const marker = { textContent: '' };
    const tile: any = { image, marker, click: null, addEventListener: (_event: string, callback: () => void) => { tile.click = callback; },
      querySelector: (selector: string) => selector === 'img' ? image :
      selector === '.flex.items-center.gap-1.text-white' ? marker : null };
    return tile;
  });
  const summary = { addEventListener: (_event: string, _callback: () => void) => {},
    querySelector: (selector: string) => selector === 'img' ? null : { textContent: '' } };
  const ids = new Map<string, any>();
  const node = (id: string) => ids.get(id) ?? ids.set(id, { textContent: '', style: {},
    querySelectorAll: () => [], closest: () => null,
    get innerHTML() { return this.html || ''; },
    set innerHTML(value: string) { this.html = value; this.textContent = value.replace(/<[^>]*>/g, '').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&'); }
  }).get(id);
  const events: any[] = [], fetched: string[] = [], created: string[] = [], timers: Array<() => void> = [];
  const document = { getElementById: (id: string) => node(id),
    querySelectorAll: (selector: string) => selector === '#camGrid > div' ? [...tiles, summary] : [] };
  const fetch = async (url: string, init: any) => {
    fetched.push(url);
    if (url.endsWith('/status')) return new Response(JSON.stringify({ host: 'Ready', stage: 'Detection',
      currentRun: { runId }, algorithm: { state: 'Ready' } }), { status: 200 });
    if (url.endsWith('/evidence')) return new Response(JSON.stringify({ stages: [], finalResult: 'NotCompleted' }), { status: 200 });
    if (url.endsWith('/media')) return new Response(JSON.stringify({ runId, items }), { status: 200 });
    if (/\/media\/[0-9a-f-]+$/.test(url)) {
      assert.equal(init.headers.Authorization, 'Bearer controlled-test-token');
      return new Response(new Uint8Array([137, 80, 78, 71]),
        { status: 200, headers: { 'Content-Type': 'image/png' } });
    }
    return new Response(JSON.stringify({ events: ['Detection'], resultContext: { kind: 'Single', id: 'object-current', localFace: 1 },
      results: [{ kind: 'Single', id: 'object-current', availability: 'Committed', disposition: 'Pending' },
        { kind: 'Single', id: 'object-old', availability: 'Committed', disposition: 'NG' }] }), { status: 200 });
  };
  const window: any = { __GAODE_HOST_CONFIG__: { mode: 'Test', apiBaseUrl: 'http://127.0.0.1:5001',
    testToken: 'controlled-test-token' }, dispatchEvent: (event: any) => { events.push(event); } };
  const context = { window, document, fetch, Headers, Response, CustomEvent: class {
    type: string; options: any;
    constructor(type: string, options: any) { this.type = type; this.options = options; }
  }, URL: { createObjectURL: (_: Blob) => { const url = `blob:test-${created.length}`; created.push(url); return url; },
    revokeObjectURL: (_: string) => {} }, setInterval: (callback: () => void) => { timers.push(callback); } };
  vm.runInNewContext(runtime, context);
  await new Promise(resolve => setTimeout(resolve, 30));
  const displayed = tiles.filter(tile => tile.image.src.startsWith('blob:'));
  assert.equal(displayed.length, 3);
  assert.equal(new Set(displayed.map(tile => tile.image.dataset.mediaId)).size, 3);
  assert.ok(displayed.every(tile => tile.image.dataset.runId === runId &&
    tile.marker.textContent.includes('临时格位')));
  assert.ok(tiles.every((tile, index) => tile.image.alt === names[index]));
  assert.ok(tiles.filter(tile => !tile.image.src).every(tile =>
    tile.marker.textContent.includes('未就绪') || tile.marker.textContent.includes('未参与') ||
    tile.marker.textContent.includes('未采集')));
  assert.ok(fetched.some(url => url.endsWith(`/media/${items[2].mediaId}`)));
  assert.ok(!fetched.some(url => url.endsWith(`/media/${items[0].mediaId}`)));
  assert.ok(!fetched.some(url => url.endsWith(`/media/${items[3].mediaId}`)));
  const bindings = events.filter(event => event.type === 'station01:media-rendered').at(-1).options.detail.bindings;
  assert.equal(bindings.filter((binding: any) => binding.displayed).length, 3);
  assert.ok(bindings.every((binding: any) => binding.source === 'Test/FixedImage'));
  assert.ok(tiles.filter(tile => tile.image.src).every(tile => tile.marker.textContent.includes('Test/FixedImage')));
  assert.ok(tiles.every(tile => !tile.marker.textContent.includes('Test/Simulated')));
  timers[0]();
  await new Promise(resolve => setTimeout(resolve, 30));
  assert.equal(tiles.filter(tile => tile.image.src.startsWith('blob:')).length, 3);
  assert.equal(created.length, 3);
  assert.equal(window.__GAODE_HOST_CONFIG__, undefined);
  assert.equal(node('verdictBig').textContent, 'Pending');
  const aTile = tiles.find(tile => tile.image.dataset.businessCamera === 'A')!;
  await aTile.click();
  assert.equal(node('verdictBig').textContent, 'NG');
  assert.ok(node('itemList').textContent.includes('object-old'));
  timers[0](); await new Promise(resolve => setTimeout(resolve, 30));
  assert.equal(node('verdictBig').textContent, 'NG');
  await aTile.click();
  assert.equal(node('verdictBig').textContent, 'Pending');
  assert.ok(node('itemList').textContent.includes('object-current'));

});
