import test from 'node:test';
import assert from 'node:assert/strict';
import { mediaUrl } from '../src/media/media-client.ts';
import { readFile, readdir } from 'node:fs/promises';
import { resolve } from 'node:path';
test('frontend boundary rejects local media paths and external page resource markers', async () => {
  assert.throws(() => mediaUrl('https://host', 'C:\\data\\x.png'));
  const files = await readdir(resolve(process.cwd(), 'dist'), { recursive: true, withFileTypes: true });
  for (const entry of files.filter(item => item.isFile())) {
    const content = await readFile(resolve(entry.parentPath || resolve(process.cwd(), 'dist'), entry.name), 'utf8');
    assert.doesNotMatch(content, /https:\/\/cdn\.tailwindcss\.com|https:\/\/unpkg\.com\/lucide|file:\/\//);
    assert.doesNotMatch(content, /console\.log\([^)]*(accessToken|token)/i);
  }
});
