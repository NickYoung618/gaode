"""Finite 009 verification orchestration. Never imports product protocol types.

The checked-in manifest is the expectation. A selected route is a subset, and
missing process, component, page or maintenance evidence cannot become a pass.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sqlite3
import sys
import tempfile
import uuid
import xml.etree.ElementTree as ET
import runner
import recipe_execution_010 as recipe010

ROOT=Path(__file__).resolve().parents[2]


def load(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def save(path,value):
    with Path(path).open('x',encoding='utf-8') as output:json.dump(value,output,ensure_ascii=False,indent=2)


def process_cases():
    value=load(ROOT/'scripts/architecture/009-process-cases.json')
    ids=[c['caseId'] for c in value['cases']]
    if value['schemaVersion']!='009-process-cases/1' or len(ids)!=len(set(ids)):
        raise ValueError('ProcessCaseManifestInvalid')
    return value


def select_cases(manifest,selected):
    known={x['caseId']:x for x in manifest['cases']}
    if not selected:return list(known.values())
    if len(selected)!=len(set(selected)) or not set(selected).issubset(known):
        raise ValueError('UnknownOrDuplicateProcessCase')
    return [known[k] for k in selected]


def validate_mode(mode,variant,freeze):
    if mode!='Mutation':
        if variant or freeze:raise ValueError('VariantOnlyForMutation')
        return
    if variant not in ('M01','M02','M03','M04','M05'):raise ValueError('MutationVariantRequired')
    if freeze is None:raise ValueError('FreezeManifestRequired')
    if not Path(freeze).is_file():raise ValueError('FreezeManifestMissing')
    value=load(freeze)
    if value.get('schemaVersion')!='009-freeze/1' or value.get('baselinePassed') is not True:
        raise ValueError('AcceptedFrozenBaselineRequired')
    baseline=Path(value['baselineAcceptance'])
    if not baseline.is_file() or digest(baseline)!=value['baselineAcceptanceSha256']:
        raise ValueError('AcceptedBaselineEvidenceChanged')
    if load(baseline).get('overall009BaselinePassed') is not True:
        raise ValueError('BaselineNotAccepted')
    for item in value['files']:
        actual=(ROOT/item['path']).resolve()
        if not actual.is_relative_to(ROOT) or not actual.is_file() or digest(actual)!=item['sha256']:
            raise ValueError('FrozenFileChanged:'+item['path'])


def assess(cases,observed,context):
    return runner.validate_required_ledger(dict(cases=cases),observed,context,scope='integration')


def assess_component(ledger,verification,manifest,context):
    """Recheck fixed expectations; a successful child exit is insufficient."""
    required=['selfcheck','scripts','restore','build','script-components']
    required += [kind+':'+suite for suite in runner.SUITES for kind in ('discover','test')]
    commands=verification.get('commands',[])
    actual=runner.validate_required_ledger(manifest,ledger.get('cases',[]),context)
    checks=dict(
        fixed_rows_complete=actual['result']=='Passed',
        current_context=all(ledger.get(k)==context[k] for k in ('runId','sourceDigest','manifestDigest','buildDigest')),
        current_interval=datetime.fromisoformat(ledger['startedAt'])>=datetime.fromisoformat(context['startedAt']),
        reported_success=ledger.get('result')=='Passed' and verification.get('passed') is True,
        source_unchanged=verification.get('source_at_build')==verification.get('source_digest')==context['sourceDigest'],
        all_commands=[c.get('kind') for c in commands]==required and all(c.get('exit_code')==0 for c in commands),
        all_trx=all(c.get('tests_all_passed') is True for c in commands if c.get('kind','').startswith('test:')))
    return dict(passed=all(checks.values()),scope='CurrentComponentEvidenceOnly',
                checks=checks,failures=actual['failures'])


def inspect_maintenance(path,test,context):
    if not path.is_file():raise ValueError('MaintenanceNativeEvidenceMissing')
    if test.get('outcome')!='Passed':raise ValueError('MaintenancePassingCurrentTestRequired')
    instant=lambda value:datetime.fromisoformat(value.replace('Z','+00:00'))
    value=load(path);case=value['caseId'];source=value['source'];actual=value['actual']
    start=instant(test['startTime']);end=instant(test['endTime'])
    written=datetime.fromtimestamp(path.stat().st_mtime,timezone.utc)
    tool=ROOT/'backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll'
    checks={
        'current_passing_case':case in test['testName'] and instant(context['startedAt'])<=start<=written<=end,
        'actual_production_tool':value['sourceLinkedComponentTool'] is False and Path(value['tool']).resolve()==tool.resolve() and digest(tool)==value['toolSha256'],
        'actual_interruption':value['actualProcess']['kind']=='ActualProcessKilledAtRealTransactionBoundary' and value['actualProcess']['pid']>0 and start<=instant(value['actualProcess']['interruptedAt'])<=end,
        'actual_source_and_target':source['State']=='U0' and actual['State']=='U2' and source['Code']==actual['Code']=='Verified' and source['StoreId']==actual['StoreId'] and source['Profile']==actual['Profile']=='Test' and bool(source['StructureDigest']) and bool(actual['StructureDigest']),
        'payload_media_unchanged':all(source[k] and source[k]==actual[k] for k in ('BusinessDigest','MediaDigest')),
        'no_repeated_ddl':value['repeated']['State']=='U2' and value['repeated']['DdlExecuted']==0,
        'formal_admission':value['admission']['Compatible'] is True and value['admission']['StoreId']==actual['StoreId'],
    }
    if case.startswith(('SU02','SU03')):checks['commit_unknown_resolved_as_target']=value['result']['State']=='U2' and value['result']['DdlExecuted']==0
    if case.startswith('SU03'):
        held=value['heldObservation']
        checks['unclassified_reentry_rejected']=held['state']=='U1' and held['reentry']['ExitCode']!=0 and held['ddlBefore']==held['ddlAfter']
    if case.startswith('SU04'):
        held=value['heldObservation']
        checks['inconsistent_rejected_before_controlled_restore']=held['rejected']['ExitCode']!=0 and held['restored']['State']=='U0'
    database=Path(value['root'])/'station01.test.db'
    with sqlite3.connect(database.resolve().as_uri()+'?mode=ro',uri=True) as db:
        manifests=list(db.execute('SELECT StoreId,SchemaVersion,Profile FROM Manifests'))
        checks['same_database_manifest']=len(manifests)==1 and manifests[0][0].lower()==actual['StoreId'].lower() and manifests[0][1:]==('s01-store/2','Test')
        checks['actual_target_structure']=db.execute("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='PlcCommunicationEvidence'").fetchone()[0]==1
        checks['actual_payload_digests']=all(hashlib.sha256(p.encode()).hexdigest()==h.lower() for p,h in db.execute('SELECT PayloadJson,PayloadDigest FROM Writes'))
        checks['actual_media_files']=all((Path(value['root'])/'media-root'/key).is_file() and (Path(value['root'])/'media-root'/key).stat().st_size==length for key,length in db.execute('SELECT RelativeKey,ByteLength FROM Media'))
    return dict(caseId=case,checks=checks,passed=all(checks.values()),nativeArtifact=str(path),
        nativeSha256=digest(path),database=str(database),scope='ControlledSQLiteMaintenance;NoHostWorkerClaim',
        evidenceKinds=['process-interruption','sqlite-state','formal-admission','payload-media-integrity'] if all(checks.values()) else [])


def run_maintenance(root,env,context):
    folder=root/'maintenance';folder.mkdir()
    native=folder/'native';native.mkdir()
    child=env.copy();child.pop('GAODE_009_STOREPREP_COMPONENT',None)
    child['GAODE_009_EVIDENCE_ROOT']=str(native)
    child['GAODE_TEST_ROOT']=str(Path(child['GAODE_VERIFY_TEST_PARENT'])/context['runId']/'maintenance')
    Path(child['GAODE_TEST_ROOT']).mkdir(parents=True)
    project='backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj'
    args=['dotnet','test',project,'--no-build','--no-restore','--filter','FullyQualifiedName~StorePreparationTests.RequiredUpgradeCase']
    discovered=runner.run_logged(args+['--list-tests'],folder/'discovery.log',child,timeout=120)
    code=runner.run_logged(args+['--logger','trx;LogFileName=maintenance.trx','--results-directory',str(folder)],folder/'execution.log',child,timeout=600)
    seen=(folder/'discovery.log').read_text(encoding='utf-8-sig')
    results=[]
    if (folder/'maintenance.trx').is_file():results=[r.attrib for r in ET.parse(folder/'maintenance.trx').findall('.//{*}UnitTestResult')]
    rows={}
    for case in process_cases()['cases']:
        cid=case['caseId']
        if not cid.startswith('SU'):continue
        row=dict(context,caseId=cid,discovered=discovered==0 and cid in seen,executed=code!=124,outcome='Failed',evidenceKinds=[])
        reports=[p for p in native.glob('*.json') if load(p).get('caseId')==cid]
        matches=[r for r in results if cid in r.get('testName','')]
        if code==0 and len(matches)==len(reports)==1:
            try:
                proof=inspect_maintenance(reports[0],matches[0],context)
                save(folder/(cid.replace('/','--')+'-validation.json'),proof)
                if proof['passed']:row.update(outcome='Passed',evidenceKinds=proof['evidenceKinds'])
            except (OSError,ValueError,KeyError,sqlite3.Error) as error:row['reason']=str(error)
        rows[cid]=row
    return rows


def _main(credential, parent_attempt, profile):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode',required=True,choices=['Gates','Baseline','Mutation'])
    parser.add_argument('--variant');parser.add_argument('--freeze',type=Path)
    parser.add_argument('--fixture',type=Path);parser.add_argument('--case',action='append',default=[])
    parser.add_argument('--host',required=True,type=Path);parser.add_argument('--plc',dest='plc_binary',required=True,type=Path)
    parser.add_argument('--evidence',required=True,type=Path);parser.add_argument('--windows-native-thread-pool',action='store_true')
    args=parser.parse_args();validate_mode(args.mode,args.variant,args.freeze)
    root=args.evidence.resolve();allowed=ROOT/'artifacts/recipe-execution-008/009-isolation'
    if not root.is_relative_to(allowed) or root.exists():raise ValueError('New009EvidenceRootRequired')
    if any(not p.is_file() for p in (args.host,args.plc_binary)):raise ValueError('ActualHostAndPlcBuildRequired')
    root.mkdir(parents=True)
    manifest_path=ROOT/'scripts/workflow/009-required-cases.json'
    manifest=runner.required_manifest();known=process_cases();chosen=select_cases(known,args.case)
    if args.fixture:
        relative=args.fixture.resolve().relative_to(ROOT).as_posix()
        chosen=[c for c in chosen if relative in c.get('fixtures',[])]
        if not chosen:raise ValueError('FixtureNotInSelectedFixedCases')
    context=dict(runId='standalone-'+uuid.uuid4().hex,sourceDigest=runner.source_digest(),
        manifestDigest=digest(manifest_path),buildDigest=runner.build_identity()['buildDigest'],
        startedAt=datetime.now(timezone.utc).isoformat())
    env=os.environ.copy();results=[];component=None
    parent=Path(env.get('GAODE_VERIFY_TEST_PARENT',Path(tempfile.gettempdir())/'gaode-009-independent')).resolve()
    if not parent.is_relative_to(Path(tempfile.gettempdir()).resolve()):raise ValueError('ExternalTestParentOutsideUserTemporaryDirectory')
    env['GAODE_VERIFY_TEST_PARENT']=str(parent)
    if args.mode=='Gates' or not (args.case or args.fixture):
        with (root/'verify-only.log').open('x',encoding='utf-8') as log:
            code=subprocess.call([sys.executable,'-B',str(ROOT/'scripts/workflow/verify_entry.py'),
                                  '--request-id',context['runId']],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
        results.append(dict(kind='LocalVerifyOnly',exitCode=code))
        folder=ROOT/'artifacts/workflow'/context['runId']/'verify-01'
        try:
            # Restore/build is allowed to produce the single current build.
            # Do not copy a previous run's context or relabel its evidence.
            context.update(sourceDigest=runner.source_digest(),buildDigest=runner.build_identity()['buildDigest'])
            ledger=load(folder/'009-execution-ledger.json');verification=load(folder/'verification.json')
            component=assess_component(ledger,verification,manifest,context)
            component.update(executed=True,exitCode=code,overall009Passed=False,
                files=[dict(path=str(p),sha256=digest(p)) for p in
                    (folder/'009-execution-ledger.json',folder/'verification.json',folder/'009-build-files.json')])
            component['passed'] &= code==0
            component['passed'] = recipe010.final_gate(component['passed'], credential, parent_attempt, profile, 'protocol_isolation:component')['passed']
        except (OSError,ValueError,KeyError,TypeError) as error:
            component=dict(executed=True,exitCode=code,passed=False,overall009Passed=False,reason=str(error))
    save(root/'context.json',dict(context,mode=args.mode,variant=args.variant,fixture=str(args.fixture),cases=[x['caseId'] for x in chosen]))
    save(root/'build-files.json',runner.build_identity())
    observed=[]
    if args.mode!='Gates':
        parent=Path(env.get('GAODE_VERIFY_TEST_PARENT',Path(tempfile.gettempdir())/'gaode-009-independent')).resolve()
        if not parent.is_relative_to(Path(tempfile.gettempdir()).resolve()):raise ValueError('ExternalTestParentOutsideUserTemporaryDirectory')
        env['GAODE_VERIFY_TEST_PARENT']=str(parent)
        maintenance=None
        for case in chosen:
            if case['caseId'].startswith('SU'):
                if maintenance is None:maintenance=run_maintenance(root,env,context)
                observed.append(maintenance[case['caseId']]);continue
            row=dict(context,caseId=case['caseId'],discovered=True,executed=False,outcome='NotRun',evidenceKinds=[],attempts=[])
            # This first delivery implements the actual route/persistence seam.
            # Other fixed cases remain rejected until their concrete drivers land.
            supported=case['kind'] in ('main-flow','necessary-failure') or case['caseId'].startswith(('BA02-','BA03-','BA05-','BA06-')) or case['caseId'] in ('BA04-boundary/late-bound-PROCESS','BA04-boundary/late-handoff-PROCESS')
            if not supported:
                row['reason']='RequiredCaseDriverNotImplemented';observed.append(row);continue
            for index,relative in enumerate(case['fixtures']):
                if args.fixture and (ROOT/relative).resolve()!=args.fixture.resolve():continue
                label=case['caseId'].replace('/','--')+'-'+str(index)
                folder=root/label;store=parent/context['runId']/label
                actual_fixture=ROOT/relative
                if case['caseId'].endswith('/capacity') or case['caseId']=='BA05-cancel/inflight':
                    # Existing accepted Test capacity input, not a budget change.
                    # Preserve every motion/worker/media input and version the copy.
                    fixture=load(actual_fixture);catalog=load(fixture['recipeCatalogPath'])
                    recipe=next(r for r in catalog['recipes'] if r['recipeId']==fixture['recipeRef']['recipeId'])
                    recipe['zoneCapacity']=dict(ng=9,pending=8);recipe['version']+='.009-capacity'
                    catalog_path=root/(label+'-catalog.json');save(catalog_path,catalog)
                    fixture['recipeCatalogPath']=str(catalog_path);fixture['recipeRef']['catalogDigest']=digest(catalog_path).upper()
                    fixture['recipeRef']['version']=recipe['version']
                    actual_fixture=root/(label+'-fixture.json');save(actual_fixture,fixture)
                cmd=['pwsh','-NoProfile','-File',str(ROOT/'scripts/run-009-process-case.ps1'),'-CaseId',case['caseId'],
                    '-FixtureManifest',str(actual_fixture),'-EvidenceRoot',str(folder),'-TestRoot',str(store),
                    '-HostDll',str(args.host.resolve()),'-PlcDll',str(args.plc_binary.resolve())]
                if args.windows_native_thread_pool:cmd+=['-WindowsNativeThreadPool']
                if case['caseId'].startswith(('F05-','F06-')):cmd+=['-PersistenceFault',case['caseId']]
                if case['caseId'].startswith('BA03-') or case['caseId']=='BA05-cancel/pending':cmd+=['-PersistenceFault','BA03-healthy-wait']
                if case['caseId']=='BA05-cancel/inflight':cmd+=['-PersistenceFault','BA05-inflight']
                if case['caseId'].startswith('BA04-'):cmd+=['-PersistenceFault','BA04-late-handoff' if 'handoff' in case['caseId'] else 'BA04-late-bound']
                if case['caseId'].startswith('BA06-downstream/strict-'):
                    cmd+=['-PersistenceFault','BA06-'+case['caseId'].split('/strict-')[1].removesuffix('-PROCESS')]
                if case['caseId']=='BA02-success/api-existing-handoff' or case['caseId'].startswith(('BA06-downstream/api-existing','BA06-downstream/api-none')):
                    cmd+=['-PersistenceFault','BA02-api-input']
                with (root/(label+'.log')).open('x',encoding='utf-8') as log:
                    code=subprocess.call(cmd,cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
                row['executed']=True;attempt=dict(fixture=relative,root=str(folder),testRoot=str(store),collectionExit=code)
                if code==0:
                    for kind,script in [('business','scripts/validate-009-process-business.py'),('wire','scripts/communication/check-009-process-wire.py')]:
                        with (root/(label+'-'+kind+'.log')).open('x',encoding='utf-8') as log:
                            attempt[kind+'Exit']=subprocess.call([sys.executable,'-B',str(ROOT/script),str(folder)],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
                    if all(attempt.get(k)==0 for k in ('collectionExit','businessExit','wireExit')):
                        proofs=[load(folder/(name+'-validation.json')) for name in ('business','wire')]
                        attempt['evidenceKinds']=sorted(set().union(*(p['evidenceKinds'] for p in proofs)))
                row['attempts'].append(attempt)
                if code!=0:
                    # A failed collector may have unconfirmed owned resources.
                    # Never start another route on the same ports until its
                    # failure/cleanup has been inspected and reconciled.
                    row['collectionStopped']='NextCaseBlockedUntilResourceReconciliation'
                    break
            required_attempts=len(case['fixtures'])
            if len(row['attempts'])==required_attempts and all(a.get('businessExit')==a.get('wireExit')==a.get('collectionExit')==0 for a in row['attempts']):
                row['outcome']='Passed';row['evidenceKinds']=sorted(set.intersection(*(set(a['evidenceKinds']) for a in row['attempts'])))
            else:row['outcome']='Failed'
            observed.append(row);save(root/(case['caseId'].replace('/','--')+'-result.json'),row)
            if row.get('collectionStopped'):break
    full=assess([c for c in manifest['cases'] if c['scope']=='integration'],observed,context)
    unchanged=runner.source_digest()==context['sourceDigest'] and runner.build_identity()['buildDigest']==context['buildDigest']
    if not unchanged:full['result']='Rejected';full['failures'].append(dict(code='SourceChangedDuringRun'))
    light = recipe010.final_gate(True, credential, parent_attempt, profile, 'protocol_isolation')
    if not light['passed']:
        full['result']='Rejected';full['failures'].append(dict(code='LightweightCredentialRejected', errors=light['lightweight'].get('errors', [])))
        if component:component['passed']=False
    save(root/'process-execution-ledger.json',dict(full,lightweightCredential=credential,acceptanceParent=parent_attempt))
    subset=bool(args.case or args.fixture)
    subset_ok=light['passed'] and unchanged and bool(observed) and all(r['outcome']=='Passed' for r in observed)
    result=dict(context,lightweightCredential=credential,acceptanceParent=parent_attempt,scope='SelectedCasesOnly' if subset else args.mode,component=component,
        subsetPassed=subset_ok if subset else None,overall009Passed=False,processLedger=full['result'],
        reason='T061RequiresCompleteComponentsProcessesConsumersAndCurrentBuildEvidence')
    save(root/'result.json',result);print(json.dumps(result))
    # Never promote a component gate or route subset into complete acceptance.
    return 0 if subset and subset_ok or args.mode=='Gates' and component and component['passed'] and unchanged else 1


def main():
    parent = 'protocol-' + uuid.uuid4().hex
    profile = 'SelectedCasesOnly' if '--case' in sys.argv or '--fixture' in sys.argv else 'Default009'
    checked = recipe010.run_with_lightweight(parent, profile,
        lambda credential: dict(passed=_main(credential, parent, profile) == 0), point='protocol_isolation')
    return 0 if checked['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
