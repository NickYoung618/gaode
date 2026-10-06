"""Package 0.4 only from fresh matching software evidence; preserve distributed older archives."""
from pathlib import Path
import argparse, hashlib, json, shutil, time, zipfile

ROOT=Path(__file__).resolve().parents[4]
TOOL=Path(__file__).resolve().parents[1]
ART=ROOT/'artifacts/017-confirmed-plc-addresses'
PUB=ROOT/'artifacts/016-plc-field-ui/publish'
parser=argparse.ArgumentParser()
parser.add_argument('--evidence',type=Path,required=True)
parser.add_argument('--output',type=Path,default=ROOT/'packaging/plc-field-probe')
parser.add_argument('--stage-only',action='store_true')
args=parser.parse_args()
evidence=json.loads((args.evidence/'results.json').read_text(encoding='utf-8'))
assert evidence['toolVersion']=='0.4.0' and not evidence['hardwareTested']
assert {c['name'] for c in evidence['checks']}=={'field_layout','primary','codecs','failures','motion_failure','browser'}
assert all(c['passed'] for c in evidence['checks'])
for item in json.loads((args.evidence/'source-hashes.json').read_text(encoding='utf-8')):
    assert hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest()==item['sha256'],item['path']
name='Gaode-PlcProbe-0.4.0'
stage=args.output/('stage-'+time.strftime('%Y%m%d-%H%M%S'))/name
stage.mkdir(parents=True)
shutil.copytree(PUB,stage,dirs_exist_ok=True)
for f in ['Start.cmd','site.template.json','confirmed-points.json','README.md','FIELD-CODEX.md','INTEGRATION.md']:
    shutil.copy2(TOOL/f,stage/f)
for folder in ['wwwroot','sources','source','tests']:
    shutil.copytree(TOOL/folder,stage/folder,dirs_exist_ok=True,ignore=shutil.ignore_patterns('bin','obj','__pycache__'))
for f in ['Float32Codec.cs','BoolByteCodec.cs']:
    shutil.copy2(ROOT/'backend/src/Gaode.Plc.Protocol'/f,stage/'source'/f)
for feature in ['015-plc-field-probe','016-plc-field-ui','017-confirmed-plc-addresses']:
    shutil.copytree(ROOT/'specs'/feature,stage/'specs'/feature)
shutil.copy2(ART/'中控机联调包开发提示词.txt',stage/'中控机联调包开发提示词.txt')
for p in args.evidence.rglob('*'):
    if p.is_file() and p.suffix in ('.json','.jsonl','.png') and not {'exports','source','wwwroot'} & set(p.relative_to(args.evidence).parts):
        dest=stage/'verification'/args.evidence.name/p.relative_to(args.evidence);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
entry=ART/'packaged-entry-check.json'
if entry.exists():
    result=json.loads(entry.read_text(encoding='utf-8'))
    assert result.get('passed') and result['assemblySha256']==hashlib.sha256((PUB/'Gaode.PlcFieldUi.dll').read_bytes()).hexdigest()
    shutil.copy2(entry,stage/'verification/packaged-entry-check.json')
(stage/'verification/acceptance.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf-8')
files=[dict(path=p.relative_to(stage).as_posix(),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(stage.rglob('*')) if p.is_file()]
manifest=dict(toolVersion='0.4.0',layoutId='confirmed-20261006-v2',platform='win-x64',selfContained=True,hardwareTested=False,files=files)
(stage/'package-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
if args.stage_only:
    result=dict(stage=str(stage),files=len(files)+1)
    (ART/'stage-result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
else:
    assert entry.exists(),'Run staged launcher verification before final packaging'
    archive=args.output/(name+'-20261006.zip');assert not archive.exists(),'Published version already exists; do not overwrite it'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for p in stage.rglob('*'):
            if p.is_file():z.write(p,name+'/'+p.relative_to(stage).as_posix())
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        for f in files:assert hashlib.sha256(z.read(name+'/'+f['path'])).hexdigest()==f['sha256']
    digest=hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_suffix('.zip.sha256.txt').write_text(digest+'  '+archive.name+'\n',encoding='utf-8')
    result=dict(package=str(archive),stage=str(stage),bytes=archive.stat().st_size,files=len(files)+1,sha256=digest)
    (ART/'package-result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2),flush=True)
