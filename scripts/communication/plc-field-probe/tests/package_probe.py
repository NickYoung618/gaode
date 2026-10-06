"""Package this bounded tool and its observed evidence; no field connection or deployment."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT=Path(__file__).resolve().parents[4]
TOOL=Path(__file__).resolve().parents[1]
SPEC=ROOT/'specs/015-plc-field-probe'
OUTPUT=ROOT/'packaging/plc-field-probe'
OUTPUT.mkdir(parents=True,exist_ok=True)
version='0.2.0'
files={}
for name in ('Probe.ps1','Export.ps1','Start.cmd','Export.cmd','site.template.json','virtual.example.json',
             'field-notes.template.json','README.md','FIELD-CODEX.md'):
    files[name]=(TOOL/name).read_bytes()
for name in ('spec.md','plan.md','contracts/probe-config.md','validation.md'):
    files['specs/015-plc-field-probe/'+name]=(SPEC/name).read_bytes()

evidence=ROOT/'artifacts/015-plc-field-probe'
selections={
    'decode':('20261005-174137','wire-decode'),
    'exception':('20261005-173824','wire-exception'),
    'transaction':('20261005-173824','wire-transaction'),
    'timeout':('20261005-173824','wire-timeout'),
    'write-refused':('20261005-174137','write-not-confirmed'),
    'virtual-endpoint-refused':('20261005-174137','virtual-not-loopback'),
    'readonly':('20261005-174350','virtual-readonly'),
    'heartbeat':('20261005-174350','virtual-heartbeat'),
    'motion-blocked':('20261005-174350','virtual-motion-blocked'),
    'motion':('20261005-174350','virtual-motion'),
}
for name,(run,case) in selections.items():
    source=evidence/run/case
    for p in source.rglob('*'):
        if p.is_file():
            files['verification/cases/'+name+'/'+p.relative_to(source).as_posix()]=p.read_bytes()
for run in ('20261005-173824','20261005-174137','20261005-174350'):
    for name in ('results.json','source-hashes.json'):
        p=evidence/run/name
        if p.exists():
            files['verification/development-runs/'+run+'/'+name]=p.read_bytes()
for name in ('readonly-audit.json','heartbeat-audit.json','motion-audit.json'):
    files['verification/'+name]=(evidence/'20261005-174350'/name).read_bytes()

accepted={}
for run in ('20261005-173824','20261005-174137','20261005-174350'):
    data=json.loads((evidence/run/'results.json').read_text(encoding='utf-8'))
    assert data['hardwareTested'] is False
    for check in data['checks']:
        accepted[check['name']]=dict(check,sourceRun=run)
assert len(accepted)==7 and all(x['passed'] for x in accepted.values()),accepted
files['verification/acceptance.json']=json.dumps(dict(
    toolVersion=version,hardwareTested=False,softwareChecks=list(accepted.values()),
    limitation='Only software/TCP fixtures and local VirtualPlc. Field parameters and real machinery remain unverified.',
    historicalFailures='Original run results are preserved. Later targeted fixes supersede corresponding failures; see validation.md.'
),ensure_ascii=False,indent=2).encode('utf-8')

manifest=dict(toolVersion=version,createdDate='2026-10-05',hardwareTested=False,
              files=[dict(path=name,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()) for name,data in sorted(files.items())])
manifest_bytes=json.dumps(manifest,ensure_ascii=False,indent=2).encode('utf-8')
(TOOL/'package-manifest.json').write_bytes(manifest_bytes)
files['package-manifest.json']=manifest_bytes
destination=OUTPUT/f'Gaode-PlcProbe-{version}-20261005.zip'
with zipfile.ZipFile(destination,'w',compression=zipfile.ZIP_DEFLATED) as z:
    for name,data in sorted(files.items()):
        z.writestr('Gaode-PlcProbe-'+version+'/'+name,data)
with zipfile.ZipFile(destination) as z:
    assert z.testzip() is None
    for f in manifest['files']:
        assert hashlib.sha256(z.read('Gaode-PlcProbe-'+version+'/'+f['path'])).hexdigest()==f['sha256']
digest=hashlib.sha256(destination.read_bytes()).hexdigest()
destination.with_suffix('.zip.sha256.txt').write_text(digest+'  '+destination.name+'\n',encoding='utf-8')
print(json.dumps(dict(package=str(destination),bytes=destination.stat().st_size,sha256=digest,files=len(files)),indent=2))
