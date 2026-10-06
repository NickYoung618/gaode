import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdir, readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { execFileSync } from 'node:child_process';

test('all three approved pages and actual delivery resources match finite authorized changes', async () => {
  const root = resolve(import.meta.dirname, '..');
  const evidence = process.env.GAODE_FRONTEND_EVIDENCE_ROOT || resolve(root, '../artifacts/recipe-authoring-012/prototype');
  await mkdir(evidence, { recursive: true });
  const output = resolve(evidence, 'positive-' + crypto.randomUUID() + '.json');
  execFileSync('pwsh.exe', ['-NoProfile', '-File', resolve(root, 'scripts/verify-prototype.ps1'), '-Output', output], { stdio: 'pipe' });
  const report = JSON.parse((await readFile(output, 'utf8')).replace(/^\uFEFF/, ''));
  assert.equal(report.status, 'Passed');
  assert.deepEqual(report.pages.map((p: any) => p.page), ['a.html', 'data-view.html', 'login.html']);
  assert.equal(report.pages[1].exactReplacements, 0);
  assert.equal(report.pages[2].exactReplacements, 0);
  assert.ok(report.resources > 0);
});
