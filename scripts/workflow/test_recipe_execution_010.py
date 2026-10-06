"""G/C samples use the production parser/ledger/final gate. No devices or databases."""
from __future__ import annotations
import argparse
import copy
import json
from pathlib import Path
import tempfile
import sys
from types import SimpleNamespace
from unittest.mock import patch
import recipe_execution_010 as gate


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding='utf-8')


class Sample:
    """Explicit synthetic report input; never passed off as product execution."""
    def __init__(self, root, profile='OtherFeature', parent='parent'):
        self.root = root
        self.parent = parent
        for name in gate.CHECKER_FILES:
            p=root/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('# controlled parser sample',encoding='utf-8')
        p=root/'backend/tests/Gaode.Rules.Tests/bin/Debug/net10.0/Gaode.Rules.Tests.dll'
        p.parent.mkdir(parents=True);p.write_bytes(b'synthetic build identity for verifier unit sample')
        p=root/'backend/src/sample.cs';p.parent.mkdir(parents=True,exist_ok=True);p.write_text('class Sample {}',encoding='utf-8')
        self.cases=[dict(caseId='sample',kind='dotnet',set='B',method='Sample.Protected.UnknownCamera',dataRowId='0',dataRow=None,
                         evidenceLevel='Component',requiredEvidence=['trx','discovery']),
                    dict(caseId='SCRIPT-Repository',kind='scripts',set='L',method=None,dataRowId=None,dataRow=None,
                         evidenceLevel='StaticComponent',requiredEvidence=['raw-report'])]
        save(root/gate.LIGHT,dict(schemaVersion='010-lightweight-cases/1',requiredFiles=[gate.CHECKER_FILES[0]],cases=self.cases))
        self.folder=root/'artifacts/recipe-execution-010'/parent/'L';self.folder.mkdir(parents=True)
        self.context=gate.make_context(parent,profile,root/gate.LIGHT)
        save(self.folder/'context.json',self.context)
        self.time=gate.now()
        self.trx=f'<TestRun><Results><UnitTestResult testName="Sample.Protected.UnknownCamera" executionId="real-sample-execution" testId="sample-test" outcome="Passed" startTime="{self.time}" endTime="{self.time}" /></Results><ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0" /></ResultSummary></TestRun>'
        (self.folder/'sample.trx').write_text(self.trx,encoding='utf-8')
        (self.folder/'discovery.log').write_text('Sample.Protected.UnknownCamera\n',encoding='utf-8')
        (self.folder/'command.log').write_text('explicit synthetic native report\n',encoding='utf-8')
        save(self.folder/'script.json',dict(result='Passed',files=[dict(path='sample.py',parsed=True)],errors=[],violations=[],cases=[]))
        save(self.folder/'native/recipe-boundary.json',dict(runId=self.context['verificationAttemptId'],result='Passed',files=['backend/src/sample.cs'],closure=['Sample.Protected'],violations=[]))
        self.commands=[dict(kind='build',context=gate.identity(self.context),caseIds=[],exitCode=0,
                       startedAt=self.time,endedAt=self.time,report='command.log',log='command.log'),
                       dict(kind='dotnet',context=gate.identity(self.context),caseIds=['sample'],exitCode=0,
                       startedAt=self.time,endedAt=self.time,report='sample.trx',discovery='discovery.log',log='command.log'),
                       dict(kind='scripts',context=gate.identity(self.context),caseIds=['SCRIPT-Repository'],exitCode=0,
                       startedAt=self.time,endedAt=self.time,report='script.json')]
        result=gate.seal_bundle(self.folder,self.context,self.commands,[c['caseId'] for c in self.cases])
        assert result['result']=='Passed',result

    def reseal_hash(self, name):
        bundle=gate.read_json(self.folder/'bundle.json');bundle['reports'][name]=gate.sha(self.folder/name)
        save(self.folder/'bundle.json',bundle)

    def change_context(self, field, value):
        self.context[field]=value;save(self.folder/'context.json',self.context)
        bundle=gate.read_json(self.folder/'bundle.json');bundle['contextDigest']=gate.value_digest(self.context)
        save(self.folder/'bundle.json',bundle)

    def checked(self):
        return gate.validate_bundle(self.folder,self.parent,self.context['profile'])


def exercise_entry(profile, sample):
    # Invoke the real public entry/wrapper; only its external build/process leaf
    # and unrelated profile payload are replaced by synthetic verifier input.
    import runner
    with patch.object(gate, 'run_lightweight', return_value=str(sample.folder)) as called:
        if profile in ('Default009', 'OtherFeature'):
            request={'request_id':'sample','profile':profile};control={};folder=sample.root/'control';folder.mkdir()
            with patch.object(runner,'ROOT',sample.root), patch.object(runner,'source_digest',return_value=sample.context['sourceDigest']), \
                    patch.object(runner,'_run_default_verify',return_value=dict(passed=True,scope='SyntheticOwnProfile')):
                try:
                    result=runner._run_verify(request,folder,control,'synthetic','synthetic')
                except ValueError as error:
                    assert profile == 'OtherFeature' and str(error) == 'UnknownVerificationProfile:OtherFeature'
                    called.assert_not_called()
                    return False
            passed=result['passed']
        elif profile=='BoundaryMinimum':
            import boundary_minimum
            with patch.object(boundary_minimum.uuid,'uuid4',return_value=SimpleNamespace(hex='fixed')), \
                    patch.object(boundary_minimum,'_main',return_value=0):
                passed=boundary_minimum.main()==0
        else:
            import protocol_isolation
            with patch.object(protocol_isolation.uuid,'uuid4',return_value=SimpleNamespace(hex='fixed')), \
                    patch.object(protocol_isolation,'_main',return_value=0), patch.object(sys,'argv',['probe','--case','selected']):
                passed=protocol_isolation.main()==0
    called.assert_called_once_with(sample.parent,profile)
    return passed


def workflow_final(sample, action, credential):
    import runner
    folder=sample.root/'workflow-control';folder.mkdir(exist_ok=True)
    verification=dict(passed=True,source_digest=sample.context['sourceDigest'],lightweight_credential=credential,
                      acceptance_parent=sample.parent,profile=sample.context['profile'],evidence='synthetic')
    control=dict(verification=verification,selected=[],assessment=dict(passed=True,report='synthetic-review'),
                 current=dict(report='synthetic-review'))
    request=dict(request_id='synthetic',feature='synthetic',milestone='synthetic')
    with patch.object(runner,'context',return_value=(request,folder,control)), patch.object(runner,'verify_protected'), \
         patch.object(runner,'check_report',return_value=dict(status='pass')), patch.object(runner,'task_rows',return_value={}), patch.object(runner,'feature_path',return_value=sample.root/'synthetic-feature'), \
         patch.object(runner,'source_digest',return_value=sample.context['sourceDigest']):
        try:
            result=runner.step(action,'review')
            return not result['needs_fix'] if action=='assess' else result['status']=='software-scope-complete'
        except ValueError:return False


def phase_order_sample(root):
    # Real orchestrator; only external execution leaves are synthetic. No phase
    # result becomes product evidence. B failures and changed freezes stop E/S.
    env=gate.phase_environment(root,dict(verificationAttemptId='sample'),{},'S')
    assert Path(env['GAODE_009_INTEGRATION_ROOT']).is_relative_to(Path(env['GAODE_TEST_ROOT']))
    for mode in ('current', 'B-failed', 'E-failed', 'freeze-changed'):
        order=[]
        def phase(parent, name, fixed, frozen=None):
            order.append(name)
            return dict(result='Rejected' if mode == name + '-failed' else 'Passed', errors=['synthetic failure'])
        def freeze(*args): order.append('freeze'); return dict(synthetic=True)
        def unchanged(*args):
            if mode == 'freeze-changed': raise ValueError('FreezeInvalidated')
        with patch.object(gate,'manifest',return_value={}), patch.object(gate,'review_prepared_inputs',return_value={}), \
             patch.object(gate,'run_phase',side_effect=phase), patch.object(gate,'freeze_baseline',side_effect=freeze), \
             patch.object(gate,'require_frozen',side_effect=unchanged), \
             patch.object(gate,'close_profile',side_effect=lambda *args: order.append('V07') or dict(passed=True)):
            result=gate.run_profile('phase-sample-'+mode,'synthetic-current-L')
        assert result['passed'] is (mode == 'current'), (mode,result)
        assert order == {'current':['B','freeze','E','S','V07'], 'B-failed':['B'],
                         'E-failed':['B','freeze','E'], 'freeze-changed':['B','freeze']}[mode], order


def run_sample(cid, root):
    profile=cid.split('/',1)[1] if cid.startswith('C01/') else 'Default009' if cid=='C03/current' else 'OtherFeature'
    actual_entry=cid.startswith('C01/') or cid=='C03/current'
    parent=({'BoundaryMinimum':'boundary-fixed','SelectedCasesOnly':'protocol-fixed'}.get(profile,'sample-verify-1') if actual_entry else 'parent')
    sample=Sample(root,profile,parent);folder=sample.folder
    group,key=cid.split('/',1)
    if group=='G01':
        if key=='manifest':(root/gate.LIGHT).unlink()
        elif key=='scanner':(root/gate.CHECKER_FILES[0]).unlink()
        else:
            bundle=gate.read_json(folder/'bundle.json');bundle['caseIds'].remove('sample');save(folder/'bundle.json',bundle)
    elif group=='G02':
        if key=='discovery':(folder/'discovery.log').write_text('',encoding='utf-8');sample.reseal_hash('discovery.log')
        else:
            bundle=gate.read_json(folder/'bundle.json');bundle['commands']=[c for c in bundle['commands'] if c['kind']!='dotnet'];save(folder/'bundle.json',bundle)
    elif group=='G03':
        if key=='skip':(folder/'sample.trx').write_text(sample.trx.replace('outcome="Passed"','outcome="Skipped"'),encoding='utf-8');sample.reseal_hash('sample.trx')
        else:
            bundle=gate.read_json(folder/'bundle.json');next(c for c in bundle['commands'] if c['kind']=='dotnet')['caseIds']=[];save(folder/'bundle.json',bundle)
    elif group=='G04':
        if key=='extra-parse':
            row=gate.row(sample.cases[0],sample.context,'Passed',sample.time,sample.time)
            actual=gate.validate_rows([sample.cases[0]],[row,dict(row,caseId='TRX-PARSE',outcome='Error',parseError=True)],sample.context)
            assert actual['result']=='Rejected';return
        name={'trx':'sample.trx','script':'script.json','source':'native/recipe-boundary.json','manifest':None}[key]
        if name:
            (folder/name).write_text('{broken',encoding='utf-8');sample.reseal_hash(name)
        else:(root/gate.LIGHT).write_text('{broken',encoding='utf-8')
    elif group=='G05':
        if key=='time':
            (folder/'sample.trx').write_text(sample.trx.replace(sample.time,'2001-01-01T00:00:00Z'),encoding='utf-8');sample.reseal_hash('sample.trx')
        elif key=='report':(folder/'sample.trx').write_text(sample.trx+' ',encoding='utf-8')
        else:sample.change_context({'attempt':'verificationAttemptId','source':'sourceDigest','build':'buildDigest','input':'inputDigest','manifest':'manifestDigest'}[key],'old-value')
    elif group=='G06':
        if key=='data-row':
            (folder/'sample.trx').write_text(sample.trx.replace('Sample.Protected.UnknownCamera','Sample.Protected.UnknownAlgorithm'),encoding='utf-8');sample.reseal_hash('sample.trx')
        elif key=='moved-role':
            target=root/'backend/src/moved/RoleAdapter.cs';target.parent.mkdir(parents=True);target.write_text('class HiddenBusiness {}',encoding='utf-8')
        elif key=='evidence':(folder/'sample.trx').unlink()
        else:
            r=gate.row(sample.cases[0],sample.context,'Passed',sample.time,sample.time)
            assert gate.validate_rows([sample.cases[0]],[r,r],sample.context)['result']=='Rejected';return
    elif group=='G07':
        assert sample.checked()['result']=='Passed'
        phase_order_sample(root)
        return
    elif group=='C01':
        save(folder/'native/recipe-boundary.json',dict(runId=sample.context['verificationAttemptId'],result='Passed',
            files=['backend/src/sample.cs'],closure=['Sample.Protected'],violations=[dict(ruleId=r,line=1,column=1) for r in ('B02','B04')]))
        sample.reseal_hash('native/recipe-boundary.json')
        assert exercise_entry(profile,sample) is False
        return
    elif group=='C02':
        if key in ('assess','finish'): assert not workflow_final(sample,key,None)
        assert not gate.final_gate(True,None,'parent',profile,key)['passed']
        assert not gate.final_gate(True,folder,'old-parent',profile,key)['passed']
        # Forged passed summary with missing native execution cannot authorize any final consumer.
        bundle=gate.read_json(folder/'bundle.json');bundle['commands']=[];bundle['passed']=True;save(folder/'bundle.json',bundle)
        save(folder/'ledger.json',dict(passed=True,result='Passed'))
        assert not gate.final_gate(True,folder,'parent',profile,key)['passed']
        if key in ('assess','finish'): assert not workflow_final(sample,key,str(folder))
        return
    elif group=='C03':
        if key=='unchanged-reference':
            original=sample.context;current=copy.deepcopy(original);current['verificationAttemptId']='current-attempt'
            c=sample.cases[0];r=gate.row(c,original,'Passed',sample.time,sample.time)
            proof=dict(independentImpactReview='Synthetic: same entire dependency snapshot and atomic report validated',affected=False,
                       originalAttempt=original['verificationAttemptId'],caseId=c['caseId'],sourceFiles=original['sourceFiles'])
            assert gate.validate_reference(proof,original,current,c,r)
            # Relabeling the row as the new attempt is not a legitimate reference.
            assert not gate.validate_reference(proof,original,current,c,dict(r,verificationAttemptId='current-attempt'))
        else:
            if key=='current':
                assert exercise_entry(profile,sample) is True
                assert workflow_final(sample,'assess',str(folder))
                assert workflow_final(sample,'finish',str(folder))
            result=gate.final_gate(True,folder,sample.parent,profile,key);assert result['passed'],result
            if key=='shared':
                rows=result['lightweight']['rows'];by_id={r['caseId']:r for r in rows+rows}
                assert gate.validate_rows(sample.cases,list(by_id.values()),sample.context)['result']=='Passed'
        return
    else:raise ValueError(cid)
    result=sample.checked();assert result['result']=='Rejected',(cid,result)


def selfcheck(context, output):
    fixed=gate.manifest(gate.ROOT/gate.LIGHT)
    rows=[]
    # Keep the finite report fixtures with this attempt's evidence. They contain
    # no product data/process/database; directory cleanup is not a verifier duty.
    # In particular, Windows delayed directory removal must not lose the report.
    for case in fixed['cases']:
        if case['kind']!='selfcheck':continue
        fixture=Path(output).resolve().parent/'selfcheck-fixtures'/case['caseId'].replace('/','-')
        fixture.mkdir(parents=True,exist_ok=False)
        try:
            with patch.object(gate,'ROOT',fixture):run_sample(case['caseId'],fixture)
            rows.append(dict(caseId=case['caseId'],outcome='Passed',evidenceLevel='SyntheticVerifierComponent',fixture=str(fixture)))
        except Exception as error:rows.append(dict(caseId=case['caseId'],outcome='Failed',error=repr(error),fixture=str(fixture)))
    gate.write_json(output,dict(schemaVersion='010-verifier-selfcheck/1',context=gate.identity(context),cases=rows))
    print(json.dumps(rows,ensure_ascii=False))
    return 0 if rows and all(r['outcome']=='Passed' for r in rows) else 1


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--selfcheck',action='store_true',required=True)
    parser.add_argument('--context',required=True,type=Path);parser.add_argument('--output',required=True,type=Path)
    args=parser.parse_args();raise SystemExit(selfcheck(gate.read_json(args.context),args.output))
