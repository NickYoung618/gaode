"""Independent packet fixtures for the actual communication probe, no PLC code imports."""
import copy
import importlib.util
import json
import struct
import unittest
from pathlib import Path
here=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('operation_wire',here/'check-009-operation-wire.py')
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)
oracle=json.loads((here.parents[1]/'backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json').read_text(encoding='utf-8'))
spec=importlib.util.spec_from_file_location('wire_probe',here/'check-009-wire-evidence.py')
wire=importlib.util.module_from_spec(spec);spec.loader.exec_module(wire)


class PreDispatchFailureTests(unittest.TestCase):
    def fixture(self):
        # Independent literal heartbeat read, plus a recorded failed attempt
        # which never formed a request. It is not successful packet coverage.
        signals=[dict(id='heartbeat',area='Coil',pduOffset=0,width=1,writer='PLC',direction='PLCToPC')]
        request=struct.pack('>HHHBBHH',1,0,6,1,1,0,1).hex()
        response=struct.pack('>HHHBBBB',1,0,4,1,1,1,1).hex()
        failed=dict(connectionId='same',channel='business',sequence=2,request=None,response=None,
                    error='InvalidOperationException: Connection requires reconciliation; automatic reconnect is disabled.',
                    transactionId=None,unit=None,function=None,offset=None,count=None,
                    startedUtc='2026-10-02T01:00:00+00:00',endedUtc='2026-10-02T01:00:01+00:00')
        evidence=dict(schemaVersion='009-wire-evidence/1',runId='r',caseId='component',gap=False,exchanges=[
            dict(connectionId='same',channel='business',sequence=1,request=request,response=response,error=None),failed])
        return evidence,dict(signals=signals)

    def test_null_request_is_retained_as_failure_without_success_coverage(self):
        evidence,expected=self.fixture()
        result=wire.check(evidence,expected)
        self.assertEqual(result['successfulExchanges'],1)
        self.assertEqual(result['failedExchanges'],1)
        self.assertEqual(result['preDispatchFailures'],1)

    def test_null_request_cannot_hide_packets_metadata_or_missing_evidence(self):
        for case in ['response-present','transaction-present','no-error','missing-metadata','reversed-time','no-success']:
            with self.subTest(case=case):
                evidence,expected=self.fixture();row=evidence['exchanges'][1]
                if case=='response-present':row['response']=evidence['exchanges'][0]['response']
                if case=='transaction-present':row['transactionId']=2
                if case=='no-error':row['error']=None
                if case=='missing-metadata':del row['offset']
                if case=='reversed-time':row['endedUtc']='2026-10-01T01:00:00+00:00'
                if case=='no-success':evidence['exchanges']=evidence['exchanges'][1:]
                with self.assertRaises(ValueError):wire.check(evidence,expected)


class SortingPacketTests(unittest.TestCase):
    def packets(self):
        # Literal source §2.5: document0021 cmd,0022 status,0054 ack, offset=doc-1.
        steps=[(6,32,1),(3,33,1),(3,33,2),(6,32,0),(6,32,2),(3,33,1),
               (3,33,3),(6,32,0),(6,83,1),(3,33,0),(6,83,0)]
        values=[]
        for tx,(fn,offset,value) in enumerate(steps,1):
            request=struct.pack('>HHHBBHH',tx,0,6,1,fn,offset,1 if fn==3 else value)
            response=struct.pack('>HHHBBBH',tx,0,5,1,3,2,value) if fn==3 else request
            values.append(dict(requestHex=request.hex(),responseHex=response.hex()))
        return dict(exchanges=values)

    def test_complete_real_packet_forms(self):
        self.assertEqual(probe.sorting_packets(self.packets(),oracle),11)

    def test_missing_and_wrong_steps_are_rejected(self):
        for case in ['missing-pick','missing-place','missing-clear','wrong-code','wrong-transaction']:
            with self.subTest(case=case):
                batch=self.packets()
                if case=='missing-pick':del batch['exchanges'][2]
                if case=='missing-place':del batch['exchanges'][6]
                if case=='missing-clear':del batch['exchanges'][-1]
                if case=='wrong-code':batch['exchanges'][6]['responseHex']=batch['exchanges'][6]['responseHex'][:-4]+'0002'
                if case=='wrong-transaction':batch['exchanges'][6]['responseHex']='ffff'+batch['exchanges'][6]['responseHex'][4:]
                with self.assertRaises(ValueError):probe.sorting_packets(batch,oracle)

    def test_shared_endpoint_wrong_code_does_not_change_independent_expectation(self):
        batch=self.packets()
        batch['exchanges'][6]['responseHex']=batch['exchanges'][6]['responseHex'][:-4]+'0021'
        with self.assertRaisesRegex(ValueError,'SortingWireSequenceIncomplete'):
            probe.sorting_packets(batch,oracle)


if __name__=='__main__':unittest.main()
