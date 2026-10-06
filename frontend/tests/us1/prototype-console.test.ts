import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, cp, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { execFileSync } from 'node:child_process';

test('prototype guard rejects unrelated page changes and source or delivered-script changes', async () => {
  const root = resolve(import.meta.dirname, '../..');
  const evidence = process.env.GAODE_FRONTEND_EVIDENCE_ROOT || resolve(root, '../artifacts/recipe-authoring-012/prototype');
  await mkdir(evidence, { recursive: true });
  const sandbox = await mkdtemp(resolve(evidence, 'negative-'));
  for (const path of ['src', 'dist', 'scripts']) await cp(resolve(root, path), resolve(sandbox, path), { recursive: true });
  const verify = () => execFileSync('pwsh.exe', ['-NoProfile', '-File', resolve(root, 'scripts/verify-prototype.ps1'),
    '-FrontendRoot', sandbox, '-Output', resolve(sandbox, 'unexpected-pass.json')], { encoding: 'utf8', stdio: 'pipe' });
  const cases = [
    ['src/pages/a.html', /Unlisted source difference/],
    ['src/pages/login.html', /Unlisted source difference/],
    ['dist/a.html', /Unlisted built-page difference/],
    ['src/runtime.js', /Unlisted resource difference/],
    ['dist/recipe-authoring.js', /Unlisted resource difference/]
  ] as const;
  const observations: object[] = [];
  for (const [path, reason] of cases) {
    const file = resolve(sandbox, path), original = await readFile(file);
    await writeFile(file, Buffer.concat([original, Buffer.from('\n/* unlisted alteration */')]));
    try {
      assert.throws(verify, (error: any) => {
        assert.notEqual(error.status, 0);
        assert.match(error.stderr, reason); observations.push({ path, rejected: true, stderr: error.stderr }); return true;
      });
    } finally { await writeFile(file, original); }
  }
  await writeFile(resolve(sandbox, 'rejections.json'), JSON.stringify(observations, null, 2));
});
