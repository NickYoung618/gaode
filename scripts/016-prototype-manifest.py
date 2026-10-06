"""Extend exact archive patches only for the authorized 016 regions; preserve existing 012 replacements."""
from pathlib import Path
import json,hashlib,zipfile
repo=Path(__file__).resolve().parents[1];front=repo/'frontend'
manifest_path=front/'scripts/recipe-authoring-012-differences.json'
m=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
hashtext=lambda value:hashlib.sha256(value.encode('utf-8')).hexdigest()
with zipfile.ZipFile('E:/dzk/gaode/原型.zip') as archive:
    raw=archive.read(next(n for n in archive.namelist() if n=='a.html' or n.endswith('/a.html'))).decode('utf-8-sig')
page=next(p for p in m['pages'] if p['page']=='a.html')
if not any(p['id'].startswith('a.html-016') for p in page['patches']):
    edits=[('positions-button','      <button id="btnRecipe"',
        '      <button id="btnPositions" class="btn px-3 py-1.5 rounded-md bg-cyan-500/20 text-cyan-100 border border-cyan-500/50 text-sm">示教／公共位置</button>\r\n'),
        ('end-state','  <main class=', '  <p id="trayEndState" class="text-xs px-3 text-slate-300"></p>\r\n'),
        ('dialogs','</body>', '<dialog id="publicPositionsDialog" style="background:#0f172a;color:#e2e8f0;padding:24px;border:1px solid #64748b;border-radius:10px"></dialog>\r\n<dialog id="trayAnomalyDialog" style="background:#0f172a;color:#e2e8f0;padding:24px;border:1px solid #64748b;border-radius:10px"></dialog>\r\n')]
    for name,anchor,insert in edits:
        applied=False
        for patch in page['patches']:
            if anchor in patch['after']:
                patch['after']=patch['after'].replace(anchor,insert+anchor,1)
                patch['afterSha256']=hashtext(patch['after'])
                patch['requirement']+='; 新016 FR-011/006独立增量（2026-10-06用户授权）'
                applied=True;break
        if not applied:
            offset=raw.index(anchor)
            assert not any(p['offset']<offset<p['offset']+len(p['before']) for p in page['patches'])
            page['patches'].append(dict(id='a.html-016-'+name,requirement='新016 FR-011；006/contracts/public-tray-flow-016.md；2026-10-06用户授权',
                region=name,archiveStartLine=raw[:offset].count('\n')+1,offset=offset,before='',beforeSha256=hashtext(''),after=insert,afterSha256=hashtext(insert)))
    page['patches'].sort(key=lambda p:p['offset'])
    expected='';position=0
    for p in page['patches']:
        assert p['offset']>=position and raw[p['offset']:p['offset']+len(p['before'])]==p['before']
        expected+=raw[position:p['offset']]+p['after'];position=p['offset']+len(p['before'])
    expected+=raw[position:]
    # Ensure the only new semantic text is precisely what was already implemented.
    current=(front/'src/pages/a.html').read_text(encoding='utf-8-sig')
    assert expected.replace('\r\n','\n')==current
    (front/'src/pages/a.html').write_bytes(expected.encode('utf-8'))
    m['authority']+='; 016 authorized independent public-position/anomaly dialogs and end reason, original navigation preserved.'
    manifest_path.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
else:
    # Update only resources actually changed by this feature or its offline build.
    changed={'src/runtime.js','src/recipe-authoring.js','dist/runtime.js','dist/recipe-authoring.js','dist/vendor/tailwind.css','scripts/build.mjs','scripts/verify-prototype.ps1'}
    for resource in m['resources']:
        if resource['path'] in changed:resource['sha256']=hashlib.sha256((front/resource['path']).read_bytes()).hexdigest()
    for name in ['src/public-tray-flow.js','dist/public-tray-flow.js']:
        resource=next((r for r in m['resources'] if r['path']==name),None)
        if resource is None:resource={'path':name};m['resources'].append(resource)
        resource['sha256']=hashlib.sha256((front/name).read_bytes()).hexdigest()
    manifest_path.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('016 exact prototype regions registered')
