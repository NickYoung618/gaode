import { cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { execFile } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = join(root, 'src');
const dist = join(root, 'dist');
await rm(dist, { recursive: true, force: true });
await mkdir(dist, { recursive: true });
await cp(join(source, 'pages'), dist, { recursive: true });
await cp(join(source, 'assets'), join(dist, 'assets'), { recursive: true });
await cp(join(source, 'vendor'), join(dist, 'vendor'), { recursive: true });
await cp(join(source, 'runtime.js'), join(dist, 'runtime.js'));
await cp(join(source, 'commissioning-console.js'), join(dist, 'commissioning-console.js'));
await cp(join(source, 'recipe-authoring.js'), join(dist, 'recipe-authoring.js'));
await cp(join(source, 'public-tray-flow.js'), join(dist, 'public-tray-flow.js'));
await promisify(execFile)(process.execPath, [
  join(root, 'node_modules', 'tailwindcss', 'lib', 'cli.js'),
  '-i', join(source, 'tailwind.css'),
  '-o', join(dist, 'vendor', 'tailwind.css'),
  '--content', join(source, 'pages', '*.html')
], { cwd: root });
await cp(join(root, 'node_modules', 'lucide', 'dist', 'umd', 'lucide.min.js'),
  join(dist, 'vendor', 'lucide.min.js'));
await cp(join(root, 'node_modules', '@microsoft', 'signalr', 'dist', 'browser', 'signalr.min.js'),
  join(dist, 'vendor', 'signalr.min.js'));
for (const page of ['login.html', 'a.html', 'data-view.html']) {
  const file = join(dist, page);
  let html = await readFile(file, 'utf8');
  html = html.replace('<script src="https://cdn.tailwindcss.com"></script>',
    '<link rel="stylesheet" href="./vendor/tailwind.css" />')
    .replaceAll('https://unpkg.com/lucide@latest', './vendor/lucide.min.js')
    .replace(/<link rel="preconnect" href="https:\/\/fonts\.[^"]*"[^>]*\/>\r?\n?/g, '')
    .replace(/<link[^>]+href="https:\/\/fonts\.googleapis\.com[^"]*"[^>]*\/>/g,
      '<link rel="stylesheet" href="./vendor/fonts.css" />');
  const authoringScript = page === 'a.html' ? '  <script src="./recipe-authoring.js"></script>\n  <script src="./public-tray-flow.js"></script>\n' : '';
  html = html.replace('</body>', '  <script src="./vendor/signalr.min.js"></script>\n' + authoringScript + '  <script src="./commissioning-console.js"></script>\n  <script src="./runtime.js"></script>\n</body>');
  await writeFile(file, html, 'utf8');
}
// The approved pages refer to the historical prototype.html filename. Keep that
// navigation intact by shipping an immutable copy of the approved a.html page.
await cp(join(dist, 'a.html'), join(dist, 'prototype.html'));
for (const file of ['data-view-bindings.ts', 'error-bindings.ts', 'login-bindings.ts', 'notification-bindings.ts', 'run-console-bindings.ts', 'status-bindings.ts']) await rm(join(dist, file), { force: true });
await writeFile(join(dist, 'prototype-hash.txt'), '3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0\n', 'utf8');
console.log(`Built ${dist}`);
