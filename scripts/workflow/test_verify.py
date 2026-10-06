"""Verify contract tests with controlled fixture commands; no product execution."""
import io
import argparse
import copy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import uuid
from datetime import datetime, timedelta, timezone

import runner

AREA = runner.ROOT / "artifacts" / "workflow-selfcheck" / uuid.uuid4().hex


class Checks(unittest.TestCase):
    def test_independent_platform_test_root_admission(self):
        # Load only the actual path function: never start a product process here.
        command = r'''
$ErrorActionPreference='Stop'
$errors=$null; $tokens=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($env:PLATFORM_SOURCE,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw 'PlatformParseFailed' }
$function=$ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Resolve-TestStorageRoot'},$true)
if (-not $function) { throw 'ProductionPathFunctionMissing' }
. ([scriptblock]::Create($function.Extent.Text))
$inputCase=$env:PLATFORM_PATH_CASE | ConvertFrom-Json
try {
 $location=Resolve-TestStorageRoot -Root $inputCase.root -Repository $inputCase.repo -HasFixture $inputCase.fixture -ExternalParent $inputCase.parent
 @{ accepted=$true; root=$location.Root; allowed=$location.Allowed } | ConvertTo-Json -Compress
} catch { @{ accepted=$false; reason=$_.Exception.Message } | ConvertTo-Json -Compress }
'''
        with tempfile.TemporaryDirectory(prefix='gaode-009-platform-') as temporary:
            parent=Path(temporary); existing=parent/'existing';existing.mkdir()
            link=parent/'linked'
            made=subprocess.run(['pwsh','-NoProfile','-Command',
                'New-Item -ItemType Junction -Path $env:TEST_LINK -Target $env:TEST_TARGET | Out-Null'],
                env=dict(os.environ,TEST_LINK=str(link),TEST_TARGET=str(existing)),capture_output=True,text=True)
            self.assertEqual(made.returncode,0,made.stderr)
            cases=[('allowed',parent/'new',parent,True,True),
                   ('relative',Path('relative-root'),parent,True,False),
                   ('outside',self.project/'outside',parent,True,False),
                   ('existing',existing,parent,True,False),
                   ('linked-ancestor',link/'new',parent,True,False),
                   ('nonfixture',parent/'new',parent,False,False),
                   ('parent-outside-temp',self.project/'new',self.project,True,False)]
            for label,path,allowed,fixture,expected in cases:
                with self.subTest(case=label):
                    result=subprocess.run(['pwsh','-NoProfile','-Command',command],capture_output=True,text=True,
                        env=dict(os.environ,PLATFORM_SOURCE=str(runner.ROOT/'scripts/start-station01-virtual-loop.ps1'),
                                 PLATFORM_PATH_CASE=json.dumps(dict(root=str(path),parent=str(allowed),repo=str(runner.ROOT),fixture=fixture))))
                    self.assertEqual(result.returncode,0,result.stderr)
                    actual=json.loads(result.stdout)
                    self.assertEqual(actual['accepted'],expected,actual)
                    if expected:
                        self.assertEqual(Path(actual['root']),path)
                        self.assertEqual(Path(actual['allowed']),allowed)
                        self.assertFalse(path.exists(), 'Admission must not create or prepare a database.')

    def setUp(self):
        self.project = AREA / self._testMethodName
        self.folder = self.project / "request"
        self.folder.mkdir(parents=True)
        self.request = {"request_id": uuid.uuid4().hex}
        self.control = {}

    def verify(self, failing_command=None, missing_trx=False, bad_counters=False, digests=None):
        observed = []
        for relative in runner.required_build_binaries():
            dll=self.project/relative
            dll.parent.mkdir(parents=True,exist_ok=True)
            dll.write_bytes(b'controlled orchestration fixture; not a product assembly')
        required=self.project/'scripts/workflow/009-required-cases.json'
        required.parent.mkdir(parents=True,exist_ok=True)
        required.write_text('{"schemaVersion":"009-required-cases/1","cases":[{"caseId":"fixture","scope":"verify"}]}')
        def fake(command, log, env, timeout=1200):
            observed.append((command, env))
            log.write_text("fixture only", encoding="utf-8")
            if len(observed) == failing_command:
                return 1
            if command[1] == "test" and '--list-tests' not in command and not missing_trx:
                name = command[command.index("--logger") + 1].split("LogFileName=")[1]
                extra = ' failed="1"' if bad_counters else ' failed="0"'
                (log.parent / name).write_text('<TestRun><ResultSummary><Counters total="2" '
                    'executed="2" passed="2"' + extra + '/></ResultSummary></TestRun>', encoding="utf-8")
            return 0
        # Upper orchestration fixture only. Real L calling/rejection is exercised
        # by the C01-C03 samples using the production credential parser.
        def isolated_l(parent, profile, work):
            own = work('synthetic-L-not-acceptance')
            return dict(passed=own['passed'], ownResult=own, credential='synthetic-L-not-acceptance')
        if digests:
            digests = [*digests, digests[-1]]
        with patch.object(runner.recipe010, 'run_with_lightweight', side_effect=isolated_l), \
             patch.object(runner.recipe010, 'final_gate', side_effect=lambda own, *args: dict(passed=own)), \
             patch.object(runner, "ROOT", self.project), \
             patch.dict(os.environ, {"GAODE_VERIFY_TEST_PARENT": ""}), \
             patch.object(runner, "run_logged", side_effect=fake), \
             patch.object(runner, 'collect_required_ledger', return_value={'result':'Passed','fixtureOnly':True}), \
             patch.object(runner, "source_digest", side_effect=digests, return_value="fixture-digest"):
            result = runner.run_verify(self.request, self.folder, self.control)
        return result, observed

    def test_all_commands_pass_with_isolated_test_root_and_trx(self):
        result, observed = self.verify()
        self.assertTrue(result["passed"])
        self.assertEqual(len(observed), 13)
        self.assertTrue(any('--ledger-selfcheck' in c for c,_ in observed))
        self.assertTrue(any('scripts/check-009-script-boundary.py' in c for c,_ in observed))
        self.assertTrue(any('scripts/workflow/validator_components.py' in c for c,_ in observed))
        self.assertEqual(sum('--list-tests' in c for c,_ in observed),4)
        for _, env in observed:
            self.assertEqual(env["GAODE_ENVIRONMENT"], "Test")
            self.assertTrue(Path(env["GAODE_TEST_ROOT"]).is_relative_to(self.project))
        summary = runner.load(self.project / result["evidence"])
        self.assertEqual(summary['coordination_root'], str(self.project))
        self.assertEqual(sum(x.get("test_counts", {}).get("passed", 0) for x in summary["commands"]), 8)

    def test_failed_build_stops_tests(self):
        result, observed = self.verify(failing_command=4)
        self.assertFalse(result["passed"])
        self.assertEqual(len(observed), 4)

    def test_controlled_external_test_store_is_new_and_explicit(self):
        parent = Path(tempfile.gettempdir()) / ('gaode-009-root-fixture-' + uuid.uuid4().hex)
        request_id = 'standalone-' + self.request['request_id']
        root = runner.create_test_root(self.folder, request_id, 1, str(parent))
        self.assertEqual(root, parent / request_id / 'verify-01' / 'test-data')
        self.assertEqual(runner.load(root / 'purpose.json')['purpose'], 'Test')
        self.assertEqual(runner.load(root / 'purpose.json')['request_id'], request_id)
        with self.assertRaises(FileExistsError):
            runner.create_test_root(self.folder, request_id, 1, str(parent))

    def test_controlled_external_test_store_rejects_relative_outside_and_link(self):
        for value in ('relative-test-root', str(Path(tempfile.gettempdir()).anchor)):
            with self.subTest(root=value), self.assertRaises(ValueError):
                runner.create_test_root(self.folder, self.request['request_id'], 1, value)
        with patch.object(Path, 'is_symlink', return_value=True), self.assertRaises(ValueError):
            runner.create_test_root(self.folder, self.request['request_id'], 1,
                str(Path(tempfile.gettempdir()) / ('gaode-009-link-fixture-' + uuid.uuid4().hex)))

    def test_missing_trx_is_not_pass(self):
        result, _ = self.verify(missing_trx=True)
        self.assertFalse(result["passed"])

    def test_conflicting_failed_counters_is_not_pass(self):
        result, _ = self.verify(bad_counters=True)
        self.assertFalse(result["passed"])

    def test_verify_step_returns_failure_data_for_fix_instead_of_raising(self):
        with patch.object(runner, "context", return_value=(self.request, self.folder, self.control)), \
             patch.object(runner, "verify_protected"), \
             patch.object(runner, "run_verify", return_value={"passed": False, "evidence": "fixture"}):
            self.assertFalse(runner.step("verify", "review")["passed"])

    def test_fourth_verify_is_rejected_before_commands(self):
        self.control["verification_count"] = 3
        # This component is also run while the real outer verifier owns the
        # workspace lock. It must exercise the fixture's lock and exact round
        # limit, not wait for or mistake WorkspaceBusy for the expected rejection.
        with patch.object(runner, "ROOT", self.project), \
             patch.object(runner, "run_logged", side_effect=AssertionError("no commands")):
            with self.assertRaisesRegex(ValueError, "三轮总验证已用尽"):
                runner.run_verify(self.request, self.folder, self.control, wait_seconds=0)

    def test_source_changed_during_verify_is_not_pass(self):
        result, commands = self.verify(digests=["before", "before", "changed"])
        self.assertEqual(len(commands), 13)
        self.assertFalse(result["passed"])

    def test_logged_command_timeout_is_nonzero(self):
        log = self.folder / "timeout.log"
        code = runner.run_logged([sys.executable, "-B", "-c", "import time; time.sleep(30)"],
                                 log, os.environ.copy(), timeout=1)
        self.assertEqual(code, 124)

    def test_integration_harness_window_does_not_change_other_commands(self):
        self.assertEqual(runner.verification_timeout('test:Gaode.Integration.Tests'), 10800)
        for kind in ('build', 'scripts', 'test:Gaode.Communication.Tests', 'discover:Gaode.Integration.Tests'):
            self.assertEqual(runner.verification_timeout(kind), 1200)

    def test_powershell_entry_maps_zero_and_nonzero_to_zero_and_one(self):
        scripts = self.project / "scripts"
        (scripts / "workflow").mkdir(parents=True)
        shutil.copyfile(runner.ROOT / "scripts/verify.ps1", scripts / "verify.ps1")
        for code, expected in ((0, 0), (7, 1)):
            (scripts / "workflow/verify_entry.py").write_text(
                f"import sys; print('编码测试'); sys.exit({code})\n", encoding="utf-8")
            result = subprocess.run(["pwsh", "-NoProfile", "-File", str(scripts / "verify.ps1")],
                                    env=dict(os.environ, GAODE_WORKFLOW_PYTHON=sys.executable),
                                    capture_output=True, text=True, encoding="utf-8", timeout=30)
            self.assertEqual(result.returncode, expected, result.stderr)
            self.assertIn("编码测试", result.stdout)


def check_validator_report_admission(assertions, valid_only=False):
    """Controlled reports test the production collector, not product behavior."""
    modes=['valid'] if valid_only else ['missing-row','skipped','old-source','old-time','not-run','missing-native','bad-native',
                                      'missing-engine','wrong-engine-version','changed-engine']
    for mode in modes:
        with assertions.subTest(componentReport=mode):
            folder=AREA/('validator-'+mode+'-'+uuid.uuid4().hex);folder.mkdir(parents=True)
            context=dict(runId='fixture',sourceDigest='source',manifestDigest='manifest',buildDigest='build',startedAt='2026-10-02T00:00:00+00:00')
            reports={}
            for name in ['validator-python.log','validator-node.log','validator-node.xml']:
                (folder/name).write_text('Controlled ledger fixture; not real validation',encoding='utf-8')
                reports[name]=runner.digest(folder/name)
            engine=folder/'engine-fixture.py'
            engine.write_text('Controlled tool provenance fixture only',encoding='utf-8')
            report=dict(context,schemaVersion='009-validator-components/1',scope='VerifierComponentsOnly',result='Passed',
                workflowEngine=dict(distribution='specify-cli',version='1.0.5.dev0',path=str(engine),sha256=runner.digest(engine)),
                reports=reports,cases=[dict(caseId='fixture-row',discovered=True,executed=True,outcome='Passed')])
            commands=[dict(kind='script-components',exit_code=0)]
            if mode=='missing-row':report['cases']=[]
            if mode=='skipped':report['cases'][0]['outcome']='Skipped'
            if mode=='old-source':report['sourceDigest']='old'
            if mode=='old-time':report['startedAt']='2026-10-01T00:00:00+00:00'
            if mode=='not-run':commands=[]
            if mode=='missing-native':del report['reports']['validator-node.xml']
            if mode=='bad-native':(folder/'validator-node.xml').write_text('changed',encoding='utf-8')
            if mode=='missing-engine':del report['workflowEngine']
            if mode=='wrong-engine-version':report['workflowEngine']['version']='unreviewed'
            if mode=='changed-engine':engine.write_text('changed',encoding='utf-8')
            runner.save(folder/'validator-components.json',report)
            manifest=dict(requiredWorkflowEngineVersion='1.0.5.dev0',cases=[dict(caseId='fixture-row',scope='verify',kind='script-component',requiredEvidence=['script-component'])])
            with patch.object(runner,'SUITES',()):
                result=runner.collect_required_ledger(folder,manifest,context,folder/'absent-script.json',folder/'absent-selfcheck.json',commands)
            assertions.assertEqual('Passed' if valid_only else 'Rejected',result['result'])


def check_migration_admission(assertions, valid_only=False):
    import hashlib
    from migration_audit import audit, initial_registration
    # Reproducible evaluator input only. No product source, registry or execution
    # is substituted: runner's real migration audit remains a separate duty.
    context = dict(runId='migration-validator-FixtureOnly', sourceDigest='FixtureOnly-source',
                   manifestDigest='FixtureOnly-manifest', buildDigest='FixtureOnly-build', fixtureOnly=True)
    with tempfile.TemporaryDirectory(prefix='gaode-migration-FixtureOnly-') as temporary:
        root = Path(temporary)
        text = '// FixtureOnly: source correspondence, not executable product evidence.\nAssert.Equal(expected, actual);\n'
        (root/'FixtureOnly.cs').write_text(text, encoding='utf-8')
        digest = hashlib.sha256((root/'FixtureOnly.cs').read_bytes()).hexdigest()
        groups, cases, index = [], [], 0
        # The actual evaluator protects 53 groups / 193 methods / 245 original
        # rows. Construct that shape independently rather than cloning old data.
        for group_index in range(53):
            methods = []
            for _ in range(4 if group_index < 34 else 3):
                index += 1
                ids = [f'FixtureOnly-{index}-{r}' for r in range(2 if index <= 52 else 1)]
                rows = [dict(row=r, literal=f'FixtureOnly({index},{r})') for r in range(len(ids))]
                executable = [dict(caseId=cid, suite='FixtureOnly', method=f'FixtureOnly.M{index}',
                                   dataRow=str(r)) for r, cid in enumerate(ids)]
                cases.extend(dict(row, scope='verify', requiredEvidence=['FixtureOnly']) for row in executable)
                methods.append(dict(path='FixtureOnly.cs', name=f'M{index}', originalFileSha256=digest,
                    source=text, dataRows=rows, assertionLines=['Assert.Equal(expected, actual);'],
                    plannedCaseIds=ids, historicalSkip=False, migrationReview='FixtureOnly controlled mapping',
                    replacementMethods=[dict(path='FixtureOnly.cs', reviewedSourceSha256=digest,
                        assertionSources=[dict(path='FixtureOnly.cs', sha256=digest,
                                               assertions=['Assert.Equal(expected, actual);'])], executableRows=executable)],
                    rowCoverage=[dict(plannedCaseId=cid, oldRow=row['row'], oldLiteral=row['literal'],
                                      requiredCaseIds=[cid]) for cid, row in zip(ids, rows, strict=True)]))
            groups.append(dict(migrationId=f'T{group_index+1:02}', oldMethods=methods, scriptAssertions=[]))
        original = dict(groups=groups, fixtureOnly=True, initialRegistrationIntegrity=hashlib.sha256(
            json.dumps(initial_registration(groups), sort_keys=True, ensure_ascii=False).encode()).hexdigest())
        manifest = dict(cases=cases, fixtureOnly=True)
        complete = [dict(context, caseId=c['caseId'], discovered=True, executed=True,
                         outcome='Passed', evidenceKinds=['FixtureOnly']) for c in cases]
        positive = audit(root, original, manifest, complete, context)
        assertions.assertEqual('Passed', positive['result'], positive['failures'][:3])
        results = [dict(mode='valid', result=positive['result'], failures=positive['failures'])]
        modes = [] if valid_only else ['old-row-missing', 'initial-assertion-changed',
            'case-filtered', 'case-skipped', 'old-execution', 'replacement-source-changed']
        for mode in modes:
            with assertions.subTest(migrationAdmission=mode):
                registry, observed = copy.deepcopy(original), copy.deepcopy(complete)
                method = registry['groups'][0]['oldMethods'][0]
                case_id = method['rowCoverage'][0]['requiredCaseIds'][0]
                row = next(r for r in observed if r['caseId'] == case_id)
                expected = {'old-row-missing': ('T01/M1', 'OriginalDataRowUnmapped'),
                    'initial-assertion-changed': ('Registry', 'OriginalObligationsChanged'),
                    'case-filtered': (case_id, 'ExecutionNotPassed:'+case_id),
                    'case-skipped': (case_id, 'ExecutionNotPassed:'+case_id),
                    'old-execution': (case_id, 'StaleExecution:'+case_id),
                    'replacement-source-changed': ('T01/M1', 'ReviewedSourceChanged:FixtureOnly.cs')}[mode]
                if mode == 'old-row-missing': method['rowCoverage'] = []
                if mode == 'initial-assertion-changed': method['assertionLines'] = []
                if mode == 'case-filtered': row['executed'] = False
                if mode == 'case-skipped': row['outcome'] = 'Skipped'
                if mode == 'old-execution': row['runId'] = 'old'
                if mode == 'replacement-source-changed':
                    (root/'FixtureOnly.cs').write_text(text+'// changed source\n', encoding='utf-8')
                result = audit(root, registry, manifest, observed, context)
                (root/'FixtureOnly.cs').write_text(text, encoding='utf-8')
                assertions.assertEqual('Rejected', result['result'])
                assertions.assertIn(dict(obligation=expected[0], reason=expected[1]), result['failures'])
                results.append(dict(mode=mode, result=result['result'], expectedReason=expected[1],
                                    failures=result['failures']))
        AREA.mkdir(parents=True, exist_ok=True)
        runner.save(AREA/('migration-admission-positive.json' if valid_only else 'migration-admission-negative.json'),
                    dict(fixtureOnly=True, evidenceLevel='ControlledEvaluatorSelfcheckOnly', cases=results))


def check_native_admission(assertions, valid_only=False):
    from native_evidence import collect, index
    modes = ['valid', 'valid-type-budget'] if valid_only else ['missing', 'duplicate', 'old-file', 'failed-case',
                                         'wrong-entry', 'missing-channel', 'no-observation', 'invalid-type-budget']
    for mode in modes:
        with assertions.subTest(nativeAdmission=mode):
            folder = AREA/('native-'+mode+'-'+uuid.uuid4().hex)
            incoming, evidence = folder/'incoming', folder/'evidence'
            incoming.mkdir(parents=True); evidence.mkdir()
            now = datetime.now(timezone.utc)
            context = dict(runId='native-fixture', sourceDigest='fixture', manifestDigest='fixture',
                           buildDigest='fixture', startedAt=(now-timedelta(seconds=3)).isoformat())
            case = dict(caseId='PD-N01-overlap/same-area', requiredEvidence=['trx', 'formal-entry', 'both-channel-dispatch'])
            payload = dict(caseId=case['caseId'], entry='LatestProtocolPlcDevice.StartAsync -> same instance RequestStartAsync',
                validator='PlcDefinitionAdmission.Prepare/RequireAdmitted', startAt=now.isoformat(), requestAt=now.isoformat(),
                fixtureDigest='fixture', oracleDigest='independent-fixture', gap=False,
                dispatchScope=['business', 'heartbeat'], count=0, writes=[])
            if mode in ('valid-type-budget', 'invalid-type-budget'):
                case = dict(caseId='BA07-config/nonfinite-internal', requiredEvidence=['trx', 'budget-window'])
                payload = dict(caseId=case['caseId'], scope='ProductionModelTypeBoundaryAndSemanticValidator;NoTcpClaim',
                    memberType='System.Nullable<System.Int32>', rejected=['NaN', 'Infinity', '-Infinity'], legal=10000)
                if mode == 'invalid-type-budget': payload['memberType'] = 'System.Double'
            if mode == 'wrong-entry': payload['entry'] = 'NeverCalledProductionEntry'
            if mode == 'missing-channel': payload['dispatchScope'] = ['business']
            if mode == 'no-observation': del payload['writes']
            if mode != 'missing': runner.save(incoming/'proof.json', payload)
            if mode == 'duplicate': runner.save(incoming/'duplicate.json', payload)
            if mode == 'old-file': os.utime(incoming/'proof.json', (0, 0))
            result = dict(outcome='Failed' if mode == 'failed-case' else 'Passed',
                          startTime=(now-timedelta(seconds=1)).isoformat(),
                          endTime=(datetime.now(timezone.utc)+timedelta(seconds=1)).isoformat())
            actual = collect(case, result, index(incoming), evidence, context)
            expected = ['budget-window'] if mode == 'valid-type-budget' else ['both-channel-dispatch', 'formal-entry']
            assertions.assertEqual(expected if valid_only else [], actual)


class LedgerChecks(unittest.TestCase):
    """G cases call the same validation function as formal verify; fixtures never prove product behavior."""
    def setUp(self):
        self.context=dict(runId='current',sourceDigest='source',manifestDigest='manifest',buildDigest='build')
        self.manifest={'cases':[
            dict(caseId=cid,scope=scope,requiredEvidence=evidence) for cid,scope,evidence in [
                ('CS-SOURCE','verify',['trx']),('SCRIPT-SOURCE-JS','verify',['ast-source']),
                ('N12-python-sorting/pick','verify',['ast-case']),
                ('PD-N01-overlap/same-area','verify',['formal-entry','both-channel-dispatch']),
                ('BA04-boundary/late-bound','verify',['sqlite-commit-receipt']),
                ('SU02-after-commit/before-receipt','integration',['sqlite-state','process-interruption'])]]}
        self.rows=[dict(self.context,caseId=c['caseId'],discovered=True,executed=True,outcome='Passed',
                        evidenceKinds=c['requiredEvidence'][:]) for c in self.manifest['cases']]

    def rejected(self,rows,scope='verify'):
        self.assertEqual(runner.validate_required_ledger(self.manifest,rows,self.context,scope)['result'],'Rejected')

    def test_G01_missing_manifest_scanner(self):
        path=AREA/'G01'
        path.mkdir(parents=True,exist_ok=True)
        with patch.object(runner,'ROOT',path):
            with self.assertRaises(OSError): runner.required_manifest()
            manifest=path/'scripts/workflow/009-required-cases.json'
            manifest.parent.mkdir(parents=True,exist_ok=True)
            manifest.write_text(json.dumps(dict(self.manifest,schemaVersion='009-required-cases/1',requiredFiles=['missing-checker.py'])))
            with self.assertRaisesRegex(ValueError,'RequiredScannerMissing'): runner.required_manifest()

    def test_G02_missing_case(self):
        check_migration_admission(self)
        for row in self.rows:
            with self.subTest(case=row['caseId']):
                scope=next(c['scope'] for c in self.manifest['cases'] if c['caseId']==row['caseId'])
                self.rejected([r for r in self.rows if r is not row],scope)

    def test_G03_filtered(self):
        for index in range(len(self.rows)):
            rows=copy.deepcopy(self.rows); rows[index]['executed']=False
            self.rejected(rows,self.manifest['cases'][index]['scope'])

    def test_G04_skipped(self):
        for outcome in ['Skipped','NotExecuted','Failed','Error']:
            for index in range(len(self.rows)):
                rows=copy.deepcopy(self.rows); rows[index]['outcome']=outcome
                self.rejected(rows,self.manifest['cases'][index]['scope'])

    def test_G05_zero_discovery(self):
        self.rejected([])
        for row in self.rows: row['discovered']=False
        self.rejected(self.rows)
        path=AREA/'empty.trx';path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text('<TestRun><ResultSummary><Counters total="0" executed="0" passed="0"/></ResultSummary></TestRun>')
        self.assertFalse(runner.read_trx(path))

    def test_G06_stale_report(self):
        check_native_admission(self)
        check_validator_report_admission(self)
        for field in self.context:
            for index in range(len(self.rows)):
                rows=copy.deepcopy(self.rows); rows[index][field]='old'
                self.rejected(rows,self.manifest['cases'][index]['scope'])
        for index in range(len(self.rows)):
            rows=copy.deepcopy(self.rows); rows[index]['evidenceKinds']=[]
            self.rejected(rows,self.manifest['cases'][index]['scope'])

    def test_G07_complete(self):
        check_native_admission(self, valid_only=True)
        check_migration_admission(self, valid_only=True)
        check_validator_report_admission(self, valid_only=True)
        for scope in ['verify','integration']:
            self.assertEqual(runner.validate_required_ledger(self.manifest,self.rows,self.context,scope)['result'],'Passed')


def run_ledger_selfcheck():
    parser=argparse.ArgumentParser()
    parser.add_argument('--ledger-selfcheck',action='store_true')
    parser.add_argument('--output',type=Path,required=True)
    for name in ('run-id','source-digest','manifest-digest'): parser.add_argument('--'+name,required=True)
    args=parser.parse_args()
    labels=['G01-missing-manifest-scanner','G02-missing-case','G03-filtered','G04-skipped',
            'G05-zero-discovery','G06-stale-report','G07-complete']
    cases=[]
    for name,label in zip(unittest.defaultTestLoader.getTestCaseNames(LedgerChecks),labels,strict=True):
        result=unittest.TextTestRunner(verbosity=1).run(LedgerChecks(name))
        cases.append(dict(caseId=label,outcome='Passed' if result.wasSuccessful() else 'Failed'))
    report=dict(schemaVersion='009-ledger-selfcheck/1',runId=args.run_id,sourceDigest=args.source_digest,
                manifestDigest=args.manifest_digest,cases=cases,fixtureOnly=True,
                result='Passed' if all(c['outcome']=='Passed' for c in cases) else 'Rejected')
    output=runner.inside(args.output,runner.ROOT/'artifacts')
    output.parent.mkdir(parents=True,exist_ok=True)
    with output.open('x',encoding='utf-8') as stream: json.dump(report,stream,indent=2)
    return 0 if report['result']=='Passed' else 1


if __name__ == "__main__" and '--ledger-selfcheck' in sys.argv:
    sys.exit(run_ledger_selfcheck())
elif __name__ == "__main__":
    AREA.mkdir(parents=True)
    stream = io.StringIO()
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Checks))
    (AREA / "results.txt").write_text(stream.getvalue(), encoding="utf-8")
    print(stream.getvalue())
    print("Evidence:", AREA.relative_to(runner.ROOT))
    sys.exit(0 if result.wasSuccessful() else 1)
