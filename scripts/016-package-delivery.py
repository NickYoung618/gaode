"""Prepare immutable acceptance references, then seal a byte-exact delta without writing the source tree."""
import argparse,difflib,hashlib,json,subprocess,sys,zipfile
from datetime import datetime,timezone
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path('E:/dzk/gaode-1')
FEATURE=ROOT/'specs/016-public-preparation-tray-check-unload'
PACKAGE=ROOT/'delivery-package/integrated-20261006'
ACCEPTANCE=ROOT/'artifacts/016-public-tray-flow/integrated-copy-acceptance-2/continuation-1'

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def save(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def candidates():
    baseline=json.loads((ROOT/'delivery-baseline/manifest.json').read_text(encoding='utf-8-sig'))['files']
    listed=subprocess.check_output(['git','ls-files','-z','--cached','--others','--exclude-standard'],cwd=ROOT).decode('utf-8').split('\0')
    blocked={'artifacts','delivery-baseline','delivery-package','delivery-history','.git','node_modules','bin','obj','dist','__pycache__','TestResults'}
    names=sorted(set(baseline)|set(listed)-{''})
    rows=[]
    for name in names:
        path=Path(name)
        if blocked.intersection(path.parts) or name in ('.specify/feature.json','.gitignore'):continue
        if path.suffix.lower() in ('.db','.sqlite','.sqlite3','.dll','.exe','.pdb','.cache','.pyc'):continue
        current=ROOT/path
        if current.exists() and not current.resolve().is_relative_to(ROOT):raise ValueError('OutsideCopy:'+name)
        old=baseline.get(name,{}).get('sha256')
        copied=sha(current) if current.is_file() else None
        if copied==old:continue
        main=SOURCE/path
        source=sha(main) if main.is_file() else None
        disposition='AlreadyPresent' if source==copied else 'DirectDelta' if source==old else 'ThreeWayRequired'
        rows.append(dict(path=name,baselineSha256=old,copySha256=copied,currentSourceSha256=source,
            disposition=disposition,size=current.stat().st_size if current.is_file() else 0))
    return baseline,rows

def prepare():
    PACKAGE.mkdir(exist_ok=True)
    save(FEATURE/'delivery-manifest.json',dict(schemaVersion='016-delivery/1',featureDir=FEATURE.relative_to(ROOT).as_posix(),
        source=str(SOURCE),copy=str(ROOT),mainMerged=False,
        files='../../delivery-package/integrated-20261006/file-manifest.json',originalRecovery='../../delivery-package/integrated-20261006/original-recovery.zip',
        delta='../../delivery-package/integrated-20261006/delta-files.zip',seal='../../delivery-package/integrated-20261006/seal.json',
        acceptance=ACCEPTANCE.relative_to(ROOT).as_posix()+'/result.json',requiredAcceptance=dict(result='Passed',phase='all'),
        excluded=['.specify/feature.json','.git','.gitignore','runtime databases','build/cache directories','large baseline archive','tokens'],
        sharedRegistrations=['backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json',
            'backend/tests/Gaode.Rules.Tests/Architecture/009-public-shapes.json'],
        installation=['Stop owned Host; prepare new /3 or controlled Test /2→/3 upgrade; preserve archive and old payloads',
            'Current recipe writes use recipe-definition/5; historical /2,/3,/4 remain original values; fill real member mapping before save'],
        proofPolicy='Do not merge until external seal and current acceptance both verify. File hashes live in the sealed package to avoid self-referential manifests.'))
    baseline,rows=candidates()
    changed=[]
    for name,item in baseline.items():
        path=SOURCE/name
        actual=sha(path) if path.is_file() else None
        if actual!=item['sha256']:changed.append(dict(path=name,baselineSha256=item['sha256'],currentSha256=actual))
    save(PACKAGE/'source-current-comparison.json',dict(checkedAtUtc=datetime.now(timezone.utc).isoformat(),
        baselineFiles=len(baseline),sourceChanges=changed,changedDeliveryFiles=rows,
        policy='Read-only source comparison. Recheck baseline/copy/current hashes immediately before each merge; never replace a divergent source file.'))
    print(json.dumps({'deliveryFiles':len(rows),'sourceChanges':len(changed),'threeWayRequired':sum(r['disposition']=='ThreeWayRequired' for r in rows)}))

def seal():
    sys.path.insert(0,str(ROOT/'scripts/workflow'))
    import recipe_execution_010 as gate
    result=json.loads((ACCEPTANCE/'result.json').read_text(encoding='utf-8-sig'))
    if result.get('result')!='Passed' or result.get('phase')!='all':raise ValueError('FinalAcceptanceRequired')
    execution_source=ROOT/result.get('executionSourceBefore',(ACCEPTANCE/'source-before.json').relative_to(ROOT).as_posix())
    if not execution_source.resolve().is_relative_to(ROOT/'artifacts/016-public-tray-flow') or not execution_source.is_file():
        raise ValueError('ActualExecutionSourceFreezeRequired')
    if gate.value_digest(gate.source_files())!=result['sourceDigest']:raise ValueError('AcceptedSourceChanged')
    builds=json.loads((ACCEPTANCE/'build-after.json').read_text(encoding='utf-8-sig'))
    if gate.build_files(light=False)!=builds:raise ValueError('AcceptedBuildChanged')
    refs=json.loads((ACCEPTANCE/'gate-references.json').read_text(encoding='utf-8-sig'))
    verified=gate.validate_bundle(refs['result']['lightweightCredential'],refs['result']['acceptanceParent'],'BoundaryMinimum')
    if refs['result']['result']!='Passed' or verified['result']!='Passed':raise ValueError('Current009010Required')
    baseline,rows=candidates()
    save(PACKAGE/'file-manifest.json',dict(schemaVersion='016-files/1',files=rows,acceptedSourceDigest=result['sourceDigest']))
    originals=ROOT/'delivery-baseline/source-baseline.zip'
    review=[]
    with zipfile.ZipFile(originals) as archive,zipfile.ZipFile(PACKAGE/'original-recovery.zip','w',zipfile.ZIP_DEFLATED) as recovery,\
        zipfile.ZipFile(PACKAGE/'delta-files.zip','w',zipfile.ZIP_DEFLATED) as delta:
        for row in rows:
            name=row['path'];current=ROOT/name
            if row['baselineSha256'] is not None:
                old=archive.read(name)
                if hashlib.sha256(old).hexdigest()!=row['baselineSha256']:raise ValueError('OriginalBytesMismatch:'+name)
                recovery.writestr(name,old)
            else:old=b''
            if row['copySha256'] is not None:
                new=current.read_bytes()
                if hashlib.sha256(new).hexdigest()!=row['copySha256']:raise ValueError('CopyBytesMismatch:'+name)
                delta.writestr(name,new)
            else:new=b''
            try:
                a=old.decode('utf-8-sig').replace('\r\n','\n').splitlines(True)
                b=new.decode('utf-8-sig').replace('\r\n','\n').splitlines(True)
                review.extend(difflib.unified_diff(a,b,fromfile='baseline/'+name,tofile='delivery/'+name))
            except UnicodeDecodeError:review.append('Binary delta: '+name+'\n')
    (PACKAGE/'review.diff').write_text(''.join(review),encoding='utf-8')
    # Verify every ZIP member against the independent file manifest.
    with zipfile.ZipFile(PACKAGE/'delta-files.zip') as delta,zipfile.ZipFile(PACKAGE/'original-recovery.zip') as recovery:
        for row in rows:
            if row['copySha256'] is not None:assert hashlib.sha256(delta.read(row['path'])).hexdigest()==row['copySha256']
            if row['baselineSha256'] is not None:assert hashlib.sha256(recovery.read(row['path'])).hexdigest()==row['baselineSha256']
    # Export finite acceptance evidence, excluding runtime stores and credentials.
    proof_paths=set(ACCEPTANCE.rglob('*.json'))|set(ACCEPTANCE.rglob('*.log'))|set(ACCEPTANCE.rglob('*.trx'))
    boundary=Path(refs['boundary']).resolve()
    if not boundary.is_relative_to(ROOT/'artifacts/recipe-execution-008/009-isolation'):
        raise ValueError('OwnedGateEvidenceRequired')
    for path in boundary.rglob('*'):
        if path.is_file() and path.suffix.lower() in ('.json','.log','.trx','.md'):
            proof_paths.add(path)
    light=Path(refs['result']['lightweightCredential']).resolve()
    if not light.is_relative_to(ROOT/'artifacts/recipe-execution-010'):
        raise ValueError('OwnedLightweightEvidenceRequired')
    proof_paths.update(p for p in light.rglob('*') if p.is_file() and p.suffix.lower() in ('.json','.log','.trx','.md'))
    for name in ('final-acceptance-2/result.json','final-acceptance-2/Gaode.Integration.Tests-0.trx',
                 'final-acceptance-2/Gaode.Integration.Tests-0.log','final-acceptance-2/boundary-minimum.log',
                 'recheck-transaction-diagnostic.trx','recheck-transaction-diagnostic.log'):
        failed=ROOT/'artifacts/016-public-tray-flow'/name
        if failed.is_file():proof_paths.add(failed)
    # Only current-run facts and screenshots, never browser connection tokens,
    # device databases, configuration credentials or old artifact trees.
    for path in (ROOT/'artifacts/016-public-tray-flow').glob('*/final-016.json'):
        if path.stat().st_mtime < execution_source.stat().st_mtime:continue
        run_root=path.parent
        for name in ('final-016.json','restart-016.json','before-confirmation.json','stage-events-016.json','browser-ready.json','browser-choice.json','browser-result.json'):
            fact=run_root/name
            if fact.is_file():proof_paths.add(fact)
        proof_paths.update((run_root/'browser').glob('*.png'))
        for name in ('browser-timeline.json','host-process.json','host-reread-process.json','host.out.log','host-reread.out.log'):
            fact=run_root/name
            if fact.is_file():proof_paths.add(fact)
    migration_roots=[]
    for pattern in ('migration-*/restart-readback.json','received-014-special-*/special-restart-and-events.json'):
        for path in (ROOT/'artifacts/016-public-tray-flow').glob(pattern):
            if path.stat().st_mtime < execution_source.stat().st_mtime:continue
            run_root=path.parent;migration_roots.append(run_root.relative_to(ROOT).as_posix())
            for name in ('input.json','before-confirmation.json','actual-sqlite.json','restart-readback.json',
                         'authoring-browser-result.json','authoring-restart-sqlite.json','special-restart-and-events.json',
                         'special-origin-stage-proof.json','obligations.json','evidence-final.json',
                         'page-completion-reference.json','page-ready-reference.json',
                         'host-process.json','host-reread-process.json','host.out.log','host-reread.out.log','authoring-browser.log'):
                fact=run_root/name
                if fact.is_file():proof_paths.add(fact)
            proof_paths.update((run_root/'authoring-browser').glob('*.png'))
            page_reference=run_root/'page-completion-reference.json'
            if page_reference.is_file():
                page=json.loads(page_reference.read_text(encoding='utf-8-sig'))
                page_file=Path(page['path']).resolve()
                if not page.get('matched') or not page_file.is_relative_to(ROOT/'artifacts/016-public-tray-flow/pages'):
                    raise ValueError('ActualReceived014PageProofRequired')
                proof_paths.update(p for p in page_file.parent.glob('*') if p.is_file() and p.suffix.lower() in ('.json','.png'))
    if len(migration_roots)!=8:raise ValueError('RequiredCurrentMigrationAndReceived014RootsMissingOrDuplicate')
    save(PACKAGE/'current-migration-evidence-index.json',dict(actualRunRoots=migration_roots,
        scope='Seven registered automatic migration rows plus received014 actual two-unit process, current entry only; no FixtureOnly product evidence'))
    proof_manifest=[]
    with zipfile.ZipFile(PACKAGE/'verification-evidence.zip','w',zipfile.ZIP_DEFLATED) as proof:
        for path in sorted(proof_paths):
            if not path.resolve().is_relative_to(ROOT/'artifacts'):raise ValueError('OutsideOwnedEvidence')
            name=path.relative_to(ROOT).as_posix()
            proof.write(path,name)
            proof_manifest.append(dict(path=name,sha256=sha(path),size=path.stat().st_size))
    save(PACKAGE/'verification-evidence-manifest.json',dict(files=proof_manifest))
    hashes={p.relative_to(PACKAGE).as_posix():sha(p) for p in PACKAGE.rglob('*') if p.is_file() and p.name!='seal.json'}
    save(PACKAGE/'seal.json',dict(result='Passed',mainMerged=False,acceptedSourceDigest=result['sourceDigest'],
        acceptance=ACCEPTANCE.relative_to(ROOT).as_posix(),fileCount=len(rows),artifactSha256=hashes,
        boundaryRequired=refs['result']['requiredCount'],lightweightRequired=len(gate.manifest(gate.ROOT/gate.LIGHT)['cases'])))
    print(json.dumps({'result':'Passed','fileCount':len(rows),'artifacts':hashes}))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--mode',choices=['prepare','seal'],required=True)
    args=parser.parse_args()
    if ROOT.resolve()!=Path('C:/dzk-work/gaode-016-public-tray-flow').resolve():raise ValueError('OwnedCopyRequired')
    prepare() if args.mode=='prepare' else seal()
