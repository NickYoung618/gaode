from pathlib import Path
import json, hashlib, zipfile, sys
work=Path(sys.argv[1]).resolve()
pkg=work/'Gaode-008-Windows'
candidate='--candidate' in sys.argv
result=None if candidate else json.loads((work/'package-validation.json').read_text(encoding='utf-8-sig'))
if not candidate: assert result['passed'], result
manifest=json.loads((pkg/'package-manifest.json').read_text(encoding='utf-8-sig'))
# Do not ship prior manual/validation databases, logs or browser profiles.
files=list(manifest['files'])
base=zipfile.ZipFile(manifest['basePackage']) if manifest.get('basePackage') else None
if base:
    assert hashlib.file_digest(open(manifest['basePackage'],'rb'),'sha256').hexdigest()==manifest['basePackageSha256']
def payload(name):
    return (pkg/name).read_bytes() if (pkg/name).is_file() else base.read('Gaode-008-Windows/'+name)
for name in files:
    assert hashlib.sha256(payload(name)).hexdigest().upper()==manifest['files'][name].upper(), name
if not candidate: (pkg/'package-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
zip_path=work.parent/(sys.argv[2] if len(sys.argv)>2 else 'Gaode-008-Windows-x64-20260927.zip')
assert not zip_path.exists(), 'Preserve previous package; choose a new output name'
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for name in files+['package-manifest.json']+([] if candidate else ['package-validation.json']):
        archive.writestr('Gaode-008-Windows/'+name,payload(name))
if base: base.close()
with zipfile.ZipFile(zip_path) as archive:
    assert archive.testzip() is None
    for name,sha in manifest['files'].items():
        assert hashlib.sha256(archive.read('Gaode-008-Windows/'+name)).hexdigest().upper()==sha.upper(),name
sha=hashlib.sha256(zip_path.read_bytes()).hexdigest()
zip_path.with_suffix('.zip.sha256').write_text(sha+'  '+zip_path.name+'\n',encoding='ascii')
(work/'archive-verification.json').write_text(json.dumps({'zip':str(zip_path),'bytes':zip_path.stat().st_size,'sha256':sha,'crcAndAllPayloadDigestsPassed':True,'runtimeDataIncluded':False},indent=2),encoding='utf-8')
print(zip_path)
print(zip_path.stat().st_size)
