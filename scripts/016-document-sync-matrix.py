"""Recover the actual directed synchronization matrix from the immutable byte baseline."""
import json,zipfile,difflib,re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
prefixes=('001-','003-','006-','008-','009-','010-','011-','012-','014-')
rows=[];checkbox_errors=[]
with zipfile.ZipFile(root/'delivery-baseline/source-baseline.zip') as baseline:
    for name in baseline.namelist():
        path=Path(name)
        if len(path.parts)<3 or path.parts[0]!='specs' or not path.parts[1].startswith(prefixes):continue
        if path.name not in ('spec.md','plan.md','tasks.md','data-model.md','research.md','quickstart.md') and path.parent.name!='contracts':continue
        target=root/name
        if not target.is_file():continue
        old=baseline.read(name).decode('utf-8-sig').replace('\r\n','\n');new=target.read_text(encoding='utf-8-sig')
        if old==new:continue
        if path.name=='tasks.md':
            pattern=r'^- \[([ xX])\] (T[0-9]+)(?=\s)'
            before={i:s.lower() for s,i in re.findall(pattern,old,re.M)}
            after={i:s.lower() for s,i in re.findall(pattern,new,re.M)}
            checkbox_errors.extend(name+':'+i for i,s in before.items() if after.get(i)!=s)
        a=old.splitlines();b=new.splitlines()
        for tag,i,j,k,l in difflib.SequenceMatcher(None,a,b,autojunk=False).get_opcodes():
            if tag=='equal':continue
            rows.append({'file':name,'baselineLine':i+1,'currentLine':k+1,'operation':tag,
                         'old':'\n'.join(a[i:j]),'new':'\n'.join(b[k:l]),
                         'authority':'2026-10-06新016直接相关活动文档同步授权；历史证据/来源/原型不改'})
assert not checkbox_errors,checkbox_errors
target=root/'specs/016-public-preparation-tray-check-unload/document-sync-changes.json'
target.write_text(json.dumps({'historicalCheckboxesPreserved':True,'files':len({r['file'] for r in rows}),'changes':rows},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'changedFiles':len({r['file'] for r in rows}),'changedBlocks':len(rows),'historicalCheckboxesPreserved':True}))
