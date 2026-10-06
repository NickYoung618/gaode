"""Run finite verifier components and preserve every executed method/data row.

This produces component evidence only. It never starts Host or VirtualPlc and
cannot establish independent-process/product acceptance.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]


class Cases(unittest.TextTestResult):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs);self.rows=[]
    def record(self,test,outcome,suffix=''):
        self.rows.append(dict(caseId='SCRIPT-COMP/'+test.id()+suffix,discovered=True,executed=True,outcome=outcome))
    def addSuccess(self,test):super().addSuccess(test);self.record(test,'Passed')
    def addFailure(self,test,err):super().addFailure(test,err);self.record(test,'Failed')
    def addError(self,test,err):super().addError(test,err);self.record(test,'Error')
    def addSkip(self,test,reason):super().addSkip(test,reason);self.record(test,'Skipped')
    def addSubTest(self,test,subtest,err):
        super().addSubTest(test,subtest,err)
        suffix='/'+str(subtest.params['case']) if 'case' in subtest.params else '/'+subtest._subDescription()
        self.record(test,'Passed' if err is None else 'Failed',suffix)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',required=True,type=Path)
    args=parser.parse_args();output=args.output.resolve()
    if not output.is_relative_to(ROOT/'artifacts/recipe-execution-008/009-isolation'):
        raise ValueError('EvidenceOutsideControlledRoot')
    if output.exists():raise ValueError('EvidenceAlreadyExists')
    output.parent.mkdir(parents=True,exist_ok=True)
    context={key:os.environ[env] for key,env in [
        ('runId','GAODE_009_RUN_ID'),('sourceDigest','GAODE_009_SOURCE_DIGEST'),
        ('manifestDigest','GAODE_009_MANIFEST_DIGEST'),('buildDigest','GAODE_009_BUILD_DIGEST')]}
    started=datetime.now(timezone.utc).isoformat()
    engine=importlib.metadata.distribution('specify-cli')
    if engine.version != '1.0.5.dev0':
        raise ValueError('WorkflowEngineVersionNotReviewed')
    engine_source=Path(engine.locate_file('specify_cli/workflows/engine.py'))
    engine_evidence=dict(distribution='specify-cli',version=engine.version,
        path=str(engine_source),sha256=hashlib.sha256(engine_source.read_bytes()).hexdigest(),
        installation=engine.read_text('direct_url.json'))
    suite=unittest.TestSuite()
    for directory,pattern in [('scripts/tests','test_009_semantic_evidence.py'),('scripts/communication','test_009_*wire.py')]:
        suite.addTests(unittest.TestLoader().discover(str(ROOT/directory),pattern=pattern))
    # Original T49 obligations remain real engine/adaptor checks. Their shells
    # are controlled unit fixtures; they never run auto-dev or product stages.
    sys.path.insert(0,str(ROOT/'scripts/workflow'))
    for name in ('test_runner.Checks','test_verify.Checks','test_protocol_isolation.Checks'):
        suite.addTests(unittest.defaultTestLoader.loadTestsFromName(name))
    with (output.parent/'validator-python.log').open('x',encoding='utf-8') as log:
        result=unittest.TextTestRunner(stream=log,resultclass=Cases,verbosity=2).run(suite)
    rows=result.rows
    junit=output.parent/'validator-node.xml'
    if junit.exists():raise ValueError('NodeReportAlreadyExists')
    command=['node','--test','--test-reporter=junit','--test-reporter-destination='+str(junit),
             'scripts/tests/009-diagnostic-business.test.cjs','scripts/communication/009-diagnostic-wire.test.cjs',
             'scripts/tests/virtual-plc-monitor.test.cjs','scripts/tests/009-page-notification-evidence.test.cjs']
    with (output.parent/'validator-node.log').open('x',encoding='utf-8') as log:
        node=subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=120)
    parsed=False
    try:
        tree=ET.parse(junit);cases=tree.findall('.//testcase');parsed=bool(cases)
        for case in cases:
            rows.append(dict(caseId=case.attrib['name'],discovered=True,executed=True,
                outcome='Skipped' if case.find('skipped') is not None else 'Failed' if
                case.find('failure') is not None or case.find('error') is not None else 'Passed'))
    except (ET.ParseError,OSError,KeyError):parsed=False
    proof=dict(context,schemaVersion='009-validator-components/1',startedAt=started,scope='VerifierComponentsOnly',cases=rows,
        workflowEngine=engine_evidence,
        result='Passed' if result.wasSuccessful() and result.testsRun>0 and node.returncode==0 and parsed and
        all(r['outcome']=='Passed' for r in rows) else 'Rejected',
        reports={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [output.parent/'validator-python.log',output.parent/'validator-node.log',junit] if p.is_file()})
    with output.open('x',encoding='utf-8') as stream:json.dump(proof,stream,indent=2)
    print(json.dumps(dict(result=proof['result'],scope=proof['scope'],caseRows=len(rows))))
    return 0 if proof['result']=='Passed' else 1


if __name__=='__main__':raise SystemExit(main())
