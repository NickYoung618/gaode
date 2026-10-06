import test from 'node:test';
import assert from 'node:assert/strict';
import { mediaUrl } from '../../src/media/media-client.ts';
test('media client accepts only GUID references and never local paths', () => {
  assert.match(mediaUrl('https://host', '123e4567-e89b-12d3-a456-426614174000'), /\/media\/123e4567/);
  assert.throws(() => mediaUrl('https://host', 'C:\\secret.jpg'));
});
