"""Assemble independent Windows x64 complete and source-only incremental packages."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import zipfile

ROOT=Path(__file__).resolve().parents[1]
NAME='Gaode-PlcCommissioning-1.1.6-win-x64'
FILES=['Rollback-1.1.5.ps1','配方启动.cmd','虚拟上位机联调.cmd','停止服务.cmd','Start-PLC.ps1','Stop-PLC.ps1','中文快速说明.txt','requirements-dev.txt']
FOLDERS=['src','sources','config','specs','tests','tools','evidence']


def files(folder):
    return [p for p in folder.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc'
            and p.relative_to(folder).parts[0]!='logs' and p.name not in ('local.json','local.tmp','recipe.local.json','startup-error.log')]


def manifest(folder):
    return {p.relative_to(folder).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files(folder)
            if p.name not in ('文件SHA256.json','文件SHA256-增量.json')}


def zip_folder(folder,target):
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for p in files(folder):z.write(p,folder.name+'/'+p.relative_to(folder).as_posix())
    with zipfile.ZipFile(target) as z:
        if z.testzip() is not None:raise ValueError('ZIP integrity failure')


def copy_changed(source,target):
    source,target=Path(source),Path(target)
    if target.exists() and hashlib.sha256(source.read_bytes()).digest()==hashlib.sha256(target.read_bytes()).digest():
        return str(target)
    return shutil.copy2(source,target)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--prepare-only',action='store_true');args=parser.parse_args()
    published=ROOT/'release'/NAME;published.mkdir(parents=True,exist_ok=True)
    for name in FILES:shutil.copy2(ROOT/name,published/name)
    for name in FOLDERS+['runtime']:
        shutil.copytree(ROOT/name,published/name,dirs_exist_ok=True,copy_function=copy_changed,ignore=shutil.ignore_patterns('__pycache__','*.pyc','local.json','local.tmp','recipe.local.json'))
    exe=(published/'runtime/python.exe').read_bytes();offset=struct.unpack_from('<I',exe,60)[0]
    assert struct.unpack_from('<H',exe,offset+4)[0]==0x8664
    base=dict(schema='IndependentCommissioningBaseline/1',version='1.1.6',sharedProjectFilesChanged=[],
        sourceOrigin='Independent implementation; final PC.xls/PLC.xls only for addresses',
        baselineSources=json.loads((ROOT/'sources/manifest.json').read_text(encoding='utf-8')),
        runtime='Bundled isolated CPython 3.12 Windows x64; no system Python/.NET SDK required',
        tested='Loopback only; see evidence/member-gripper-core.json',fieldConfig='config/default.json = software initial values, not calibrated')
    (published/'基线与交接.json').write_text(json.dumps(base,ensure_ascii=False,indent=2),encoding='utf-8')
    (published/'文件SHA256.json').write_text(json.dumps(manifest(published),ensure_ascii=False,indent=2),encoding='utf-8')
    if args.prepare_only:print(published);return
    (published/'release.ready.json').write_text(json.dumps(dict(version='1.1.6',ready=True),indent=2),encoding='utf-8')
    (published/'文件SHA256.json').write_text(json.dumps(manifest(published),ensure_ascii=False,indent=2),encoding='utf-8')
    complete=ROOT/'release'/f'{NAME}-完整包.zip';zip_folder(published,complete)
    increment=ROOT/'release'/'Gaode-PlcCommissioning-1.1.6-独立增量包';increment.mkdir(parents=True,exist_ok=True)
    for name in FILES+['基线与交接.json']:shutil.copy2(published/name,increment/name)
    for name in FOLDERS:shutil.copytree(published/name,increment/name,dirs_exist_ok=True,ignore=shutil.ignore_patterns('__pycache__','*.pyc'))
    (increment/'增量使用说明.txt').write_text('本包是新增独立工具源码、规格与证据，非整目录主工程补丁。共享主工程文件修改0项。按specs中的文件对应关系选择性合入，不能整目录覆盖正在并行开发的主工程。运行时请使用完整包；本增量包未包含runtime。\n',encoding='utf-8')
    (increment/'文件SHA256-增量.json').write_text(json.dumps(manifest(increment),ensure_ascii=False,indent=2),encoding='utf-8')
    delta=ROOT/'release'/'Gaode-PlcCommissioning-1.1.6-独立增量包.zip';zip_folder(increment,delta)
    (ROOT/'release/ZIP-SHA256.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (complete,delta)},indent=2),encoding='utf-8')
    print(json.dumps(dict(complete=str(complete),bytes=complete.stat().st_size,increment=str(delta),incrementBytes=delta.stat().st_size),ensure_ascii=False))


if __name__=='__main__':main()
