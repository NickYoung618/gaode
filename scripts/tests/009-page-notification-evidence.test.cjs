// Collector component checks. These fixtures never substitute for actual page traffic.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { notificationsFromResponse } = require('../page-notification-evidence.cjs');
const envelope = { schemaVersion: 's01/notification/2.0', runId: 'component-run',
  summary: { executionState: 'Running', wholeTaskState: 'NotCompleted', errorCode: null } };
const frame = JSON.stringify({ type: 1, target: 'StateChanged', arguments: [envelope] }) + '\x1e';
for (const binary of [false, true]) test(`PAGE-NOTIFY/${binary ? 'base64' : 'text'}-long-poll`, () => {
  const body = binary ? Buffer.from(frame).toString('base64') : frame;
  assert.deepEqual(notificationsFromResponse({ body, base64Encoded: binary }),
    [{ event: 'StateChanged', runId: 'component-run', envelope }]);
});
test('PAGE-NOTIFY/keepalive-and-final', () => {
  const body = '{}\x1e' + JSON.stringify({ type: 6 }) + '\x1e' +
    JSON.stringify({ type: 1, target: 'FinalUnloadCompleted', arguments: [envelope] }) + '\x1e';
  assert.equal(notificationsFromResponse({ body, base64Encoded: false })[0].event, 'FinalUnloadCompleted');
});
test('PAGE-NOTIFY/malformed-not-success', () => {
  assert.throws(() => notificationsFromResponse({ body: '{bad}\x1e', base64Encoded: false }));
  const invalid = { ...envelope, summary: 'old summary' };
  const body = JSON.stringify({ type: 1, target: 'HandoffReady', arguments: [invalid] }) + '\x1e';
  assert.equal(notificationsFromResponse({ body, base64Encoded: false })[0].envelope.summary, 'old summary');
});
