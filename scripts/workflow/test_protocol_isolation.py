"""Finite 009 orchestration checks; these are not device acceptance results."""
import json
from pathlib import Path
import tempfile
import unittest
import copy
import protocol_isolation as subject


class Checks(unittest.TestCase):
    def test_component_merge_requires_same_run_build_and_actual_commands(self):
        context=dict(runId='current',sourceDigest='source',manifestDigest='manifest',buildDigest='build',
                     startedAt='2026-10-02T00:00:00+00:00')
        manifest=dict(cases=[dict(caseId='component',scope='verify',requiredEvidence=['trx'])])
        ledger=dict(context,result='Passed',cases=[dict(context,caseId='component',discovered=True,
                    executed=True,outcome='Passed',evidenceKinds=['trx'])])
        kinds=['selfcheck','scripts','restore','build','script-components']
        kinds += [kind+':'+suite for suite in subject.runner.SUITES for kind in ('discover','test')]
        verification=dict(passed=True,source_at_build='source',source_digest='source',
                          commands=[dict(kind=k,exit_code=0,tests_all_passed=True) for k in kinds])
        for case in ('valid','other-run','old-build','missing-case','skip','missing-command',
                     'failed-command','failed-trx','source-changed','old-time'):
            with self.subTest(case=case):
                a,b=copy.deepcopy(ledger),copy.deepcopy(verification)
                if case=='other-run':a['runId']='other'
                if case=='old-build':a['buildDigest']='old'
                if case=='missing-case':a['cases']=[]
                if case=='skip':a['cases'][0]['outcome']='Skipped'
                if case=='missing-command':b['commands']=b['commands'][1:]
                if case=='failed-command':b['commands'][0]['exit_code']=1
                if case=='failed-trx':b['commands'][-1]['tests_all_passed']=False
                if case=='source-changed':b['source_at_build']='changed'
                if case=='old-time':a['startedAt']='2026-10-01T00:00:00+00:00'
                answer=subject.assess_component(a,b,manifest,context)
                self.assertEqual(answer['passed'],case=='valid',answer)

    def test_dependency_binary_change_or_missing_output_invalidates_build(self):
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for relative in subject.runner.required_build_binaries():
                p=root/relative;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b'controlled fixture')
            dependency=root/'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Infrastructure.dll'
            dependency.write_bytes(b'first')
            with patch.object(subject.runner,'ROOT',root):
                before=subject.runner.build_identity()
                dependency.write_bytes(b'second')
                self.assertNotEqual(before['buildDigest'],subject.runner.build_identity()['buildDigest'])
                (root/subject.runner.required_build_binaries()[0]).unlink()
                with self.assertRaisesRegex(ValueError,'RequiredBuildOutputMissing'):
                    subject.runner.build_identity()

    def test_all_representative_fixtures_use_reviewed_configuration(self):
        relatives={relative for case in subject.process_cases()['cases'] for relative in case.get('fixtures',[])}
        for relative in sorted(relatives):
            value=subject.load(subject.ROOT/relative)
            self.assertEqual(value['purpose'],'Test',relative)
            self.assertEqual(value['budgetRef'],dict(id='s01-budget-virtual-loop',version='3.0.0'),relative)
            self.assertEqual(value['simulationRef'],dict(id='s01-sim-virtual-loop',version='3.0.0'),relative)

    def test_mutation_requires_valid_frozen_baseline(self):
        with tempfile.TemporaryDirectory() as directory:
            missing=Path(directory)/'missing.json'
            with self.assertRaisesRegex(ValueError,'FreezeManifestRequired'):
                subject.validate_mode('Mutation','M01',None)
            with self.assertRaisesRegex(ValueError,'FreezeManifestMissing'):
                subject.validate_mode('Mutation','M01',missing)

    def test_other_modes_cannot_smuggle_variant(self):
        with self.assertRaisesRegex(ValueError,'VariantOnlyForMutation'):
            subject.validate_mode('Baseline','M01',None)

    def test_case_selection_rejects_unknown_or_duplicate(self):
        cases=dict(schemaVersion='009-process-cases/1',cases=[dict(caseId='L01'),dict(caseId='L05')])
        self.assertEqual([x['caseId'] for x in subject.select_cases(cases,['L05'])],['L05'])
        for chosen in (['unknown'],['L01','L01']):
            with self.assertRaises(ValueError):subject.select_cases(cases,chosen)

    def test_pass_requires_all_native_evidence_and_current_context(self):
        context=dict(runId='r',sourceDigest='s',manifestDigest='m',buildDigest='b')
        case=dict(caseId='L01',scope='integration',requiredEvidence=['sqlite','host-tcp'])
        row=dict(context,caseId='L01',discovered=True,executed=True,outcome='Passed',evidenceKinds=['sqlite'])
        self.assertEqual(subject.assess([case],[row],context)['result'],'Rejected')
        row['evidenceKinds'].append('host-tcp')
        self.assertEqual(subject.assess([case],[row],context)['result'],'Passed')
        row['runId']='old'
        self.assertEqual(subject.assess([case],[row],context)['result'],'Rejected')

    def test_maintenance_native_evidence_cannot_be_inferred_from_exit_code(self):
        with tempfile.TemporaryDirectory() as directory:
            report=Path(directory)/'missing.json'
            with self.assertRaisesRegex(ValueError,'MaintenanceNativeEvidenceMissing'):
                subject.inspect_maintenance(report,{},dict(startedAt='2026-10-01T00:00:00+00:00'))
            report.write_text(json.dumps(dict(caseId='SU02-after-commit/before-receipt',
                actualProcess=dict(exitCode=-1))),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'MaintenancePassingCurrentTestRequired'):
                subject.inspect_maintenance(report,{},dict(startedAt='2026-10-01T00:00:00+00:00'))


if __name__=='__main__':unittest.main()
