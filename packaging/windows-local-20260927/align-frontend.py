from pathlib import Path
import json, shutil, hashlib, sys
repo=Path(__file__).resolve().parents[2]
work=Path(sys.argv[1]).resolve()
pkg=work/'Gaode-008-Windows'
target=pkg/'desktop/bin/Release/net10.0-windows10.0.17763.0/frontend/dist'
shutil.copytree(repo/'frontend/dist',target,dirs_exist_ok=True)
manifest=json.loads((pkg/'package-manifest.json').read_text(encoding='utf-8-sig'))
# Omit an unused nested publish output; preserve all files on disk.
manifest['files']={k:v for k,v in manifest['files'].items() if not k.startswith('desktop/bin/Release/net10.0-windows10.0.17763.0/win-x64/')}
proof={}
for source in (repo/'frontend/dist').rglob('*'):
    if not source.is_file(): continue
    dest=target/source.relative_to(repo/'frontend/dist')
    sha=hashlib.sha256(source.read_bytes()).hexdigest()
    assert hashlib.sha256(dest.read_bytes()).hexdigest()==sha
    manifest['files'][dest.relative_to(pkg).as_posix()]=sha
    proof[source.relative_to(repo/'frontend/dist').as_posix()]=sha
manifest['frontend']='Exact current frontend/dist used by accepted formal Q01; replaces stale desktop output copy'
(pkg/'package-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
report=json.loads((work/'package-validation.json').read_text(encoding='utf-8-sig'))
report['frontendAlignment']={'allCurrentDistFilesMatched':True,'files':proof,'desktopExeSha256':hashlib.sha256((pkg/'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe').read_bytes()).hexdigest(),'newDesktopUiRun':False,'formalUiEvidence':'t065-formal-q01-20260927T050046932Z/Q01'}
(work/'package-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Current frontend aligned and all files matched; no backend/configuration changed.')
