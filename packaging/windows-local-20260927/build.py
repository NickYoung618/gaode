from pathlib import Path
import json, hashlib, datetime, argparse, zipfile

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description='Build only the monitor update overlay; archive.py assembles the complete runnable package.')
parser.add_argument('--plc-dir', type=Path, required=True, help='Explicit verified current VirtualPlc build directory')
parser.add_argument('--base-package', type=Path, required=True, help='Verified previous full package; unchanged business payload is retained byte-for-byte')
args = parser.parse_args()
plc_dir = args.plc_dir.resolve(); base_path = args.base_package.resolve()
assert (plc_dir/'VirtualPlc.dll').is_file(), plc_dir
stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
work = repo/'artifacts'/('windows-package-'+stamp); pkg=work/'Gaode-008-Windows'
pkg.mkdir(parents=True,exist_ok=False)
with zipfile.ZipFile(base_path) as base:
    manifest=json.loads(base.read('Gaode-008-Windows/package-manifest.json'))
    assert manifest['revision']=='monitor-xyz-history-r8-minimal', 'This monitor update is assessed against r8 only'
    for name in ['Select-Test.ps1','Start-Test.cmd','测试配方清单.md']:
        assert (Path(__file__).parent/name).read_bytes()==base.read('Gaode-008-Windows/'+name), name
files=dict(manifest['files'])
overrides={f'VirtualPlc/wwwroot/{n}':repo/'VirtualPlc/wwwroot'/n for n in ['app.js','index.html','styles.css']}
overrides.update({n:Path(__file__).parent/n for n in ['Start.ps1','README.md']})
for name in ['VirtualPlc.dll','VirtualPlc.deps.json','VirtualPlc.runtimeconfig.json','VirtualPlc.staticwebassets.endpoints.json']:
    overrides['VirtualPlc/bin/Debug/net10.0/'+name]=plc_dir/name
for name,source in overrides.items():
    assert name in files, name
    data=source.read_bytes();target=pkg/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    files[name]=hashlib.sha256(data).hexdigest()
manifest.update({'createdUtc':stamp,'revision':'unlock-monitor-r9','plcBuildDirectory':str(plc_dir),
    'basePackage':str(base_path),'basePackageSha256':hashlib.sha256(base_path.read_bytes()).hexdigest(),
    'sourcePackageSha256':hashlib.sha256(base_path.read_bytes()).hexdigest(),'monitorRevision':'unlock-history-r9',
    'binaries':'r8 unchanged business payload; current VirtualPlc build and monitor assets', 'files':files})
(pkg/'package-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
(work/'build-result.json').write_text(json.dumps({'package':str(pkg),'work':str(work),'kind':'overlay-staging-not-runnable','basePackage':str(base_path)},indent=2),encoding='utf-8')
print(work)
