"""Serial, finite 016 verification. Real chains and current 009/010 gates remain distinct."""
import argparse,collections,json,os,subprocess,sys,uuid
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts/workflow'))
import recipe_execution_010 as gate010
import runner

def save(path,value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--phase',choices=['core','gates','all'],default='all')
    parser.add_argument('--evidence',required=True)
    args=parser.parse_args()
    folder=(ROOT/args.evidence).resolve()
    if not folder.is_relative_to(ROOT/'artifacts/016-public-tray-flow') or folder.exists():
        raise ValueError('New016OwnedEvidenceRootRequired')
    folder.mkdir(parents=True)
    env=dict(os.environ,PYTHONUTF8='1',PYTHONDONTWRITEBYTECODE='1',
        GAODE_016_TEST_ROOT=str(ROOT/'artifacts/016-public-tray-flow'),GAODE_011_PYTHON=sys.executable,
        GAODE_009_EVIDENCE_ROOT=str(folder/'native'))
    if env.get('GAODE_013_ATTEMPT_ROOT') or env.get('GAODE_014_JOINT_ROOT'):
        raise ValueError('MixedFeatureEvidenceEnvironmentRejected')
    commands=[];failures=[]
    def execute(label,argv,cwd=ROOT,continue_on_failure=False):
        with (folder/(label+'.log')).open('wb') as log:
            code=subprocess.run(argv,cwd=cwd,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=3600).returncode
        commands.append({'label':label,'command':argv,'exitCode':code})
        print(label+': '+str(code),flush=True)
        if code:
            message=label+'Failed; see '+str(folder/(label+'.log'))
            if continue_on_failure:failures.append(message)
            else:raise ValueError(message)
    before=None
    try:
        execute('backend-build',['dotnet','build','backend/Gaode.slnx','-p:RestoreLockedMode=true','-m:1','-v','minimal'])
        if args.phase in ('core','all'):
            execute('frontend-install',['npm.cmd','ci'],ROOT/'frontend')
            execute('frontend-build',['npm.cmd','run','build'],ROOT/'frontend')
            execute('frontend-typecheck',['npm.cmd','run','typecheck'],ROOT/'frontend')
            execute('prototype',['pwsh','-NoProfile','-File','frontend/scripts/verify-prototype.ps1','-Output',str(folder/'prototype.json')])
        before=gate010.source_files();save(folder/'source-before.json',before)
        build_before=gate010.build_files(light=False);save(folder/'build-before.json',build_before)
        if args.phase in ('core','all'):
            fixed=json.loads((ROOT/'specs/016-public-preparation-tray-check-unload/verification-cases.json').read_text(encoding='utf-8-sig'))
            supplement=json.loads((ROOT/'specs/016-public-preparation-tray-check-unload/supplemental-verification.json').read_text(encoding='utf-8-sig'))
            for suite_index,suite in enumerate(fixed['suites']+supplement['suites']):
                name=suite['suite'];project='backend/tests/'+name+'/'+name+'.csproj'
                actual=[]
                for index,selector in enumerate(suite['selectors']):
                    label=name+('-supplement-' if suite_index>=len(fixed['suites']) else '-')+str(index)
                    execute(label,['dotnet','test',project,'--no-build','--no-restore','--filter','FullyQualifiedName~'+selector,
                        '--logger','trx;LogFileName='+label+'.trx','--results-directory',str(folder)],continue_on_failure=True)
                    trx=folder/(label+'.trx')
                    actual.extend(e.attrib['testName'] for e in ET.parse(trx).findall('.//{*}UnitTestResult'))
                    if not runner.read_trx(trx):failures.append(label+'FailedIncompleteOrSkipped')
                if collections.Counter(actual)!=collections.Counter(suite['cases']):
                    raise ValueError(name+'MissingDuplicateSkippedOrDifferentCases')
            execute('frontend-members',['node','--test','--test-reporter=tap','frontend/tests/us1/public-tray-members-016.test.ts'])
            tap=(folder/'frontend-members.log').read_text(encoding='utf-8-sig')
            if '# tests 1' not in tap or '# pass 1' not in tap or '# skipped 0' not in tap:
                raise ValueError('FrontendRequiredCaseMissingOrSkipped')
            execute('frontend-navigation',['node','--test','--test-reporter=tap','frontend/tests/us1/recipe-authoring.test.ts','frontend/tests/us1/recipe-layout-review.test.ts'])
            navigation=(folder/'frontend-navigation.log').read_text(encoding='utf-8-sig')
            if '# tests 17' not in navigation or '# pass 17' not in navigation or '# skipped 0' not in navigation:
                raise ValueError('ReceivedNavigationCasesMissingOrSkipped')
        if args.phase in ('gates','all'):
            boundary=ROOT/'artifacts/recipe-execution-008/009-isolation'/('016-'+uuid.uuid4().hex)
            execute('boundary-minimum',[sys.executable,'-B','scripts/workflow/boundary_minimum.py','--evidence',str(boundary)])
            result=json.loads((boundary/'result.json').read_text(encoding='utf-8-sig'))
            credential=result['lightweightCredential']
            checked=gate010.validate_bundle(credential,result['acceptanceParent'],'BoundaryMinimum')
            if result['result']!='Passed' or checked['result']!='Passed':raise ValueError('Current009010GateRejected')
            save(folder/'gate-references.json',{'boundary':str(boundary),'result':result,'lightweight':checked})
            if args.phase == 'all':
                execute('migration-affected-audit',[sys.executable,'-B','scripts/016-migration-impact.py',str(folder)])
        after=gate010.source_files();save(folder/'source-after.json',after)
        if before!=after:raise ValueError('SourceOrInputsChangedDuringVerification')
        build_after=gate010.build_files(light=False);save(folder/'build-after.json',build_after)
        if build_before!=build_after:raise ValueError('BuildChangedDuringVerification')
        accepted=not failures
        save(folder/'result.json',{'result':'Passed' if accepted else 'Rejected','phase':args.phase,'sourceDigest':gate010.value_digest(after),
            'commands':commands,'failures':failures,'scope':'016 main software flow; Test substitutes declared; no Real hardware acceptance'})
        return 0 if accepted else 1
    except (ValueError,OSError,subprocess.TimeoutExpired,ET.ParseError) as error:
        save(folder/'result.json',{'result':'Rejected','phase':args.phase,'error':str(error),'commands':commands})
        print(str(error),file=sys.stderr);return 1

if __name__=='__main__':sys.exit(main())
