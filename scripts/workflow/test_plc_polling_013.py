"""Small offline regressions for actual 013 heartbeat association boundaries. No business launch."""
import copy
import unittest
import json
import tempfile
from pathlib import Path

from plc_polling_013_compare import device_exchange_key, heartbeat_edge_receipts, match_device_exchange, stage_trace_path
from plc_polling_013_compare import target_classification, compare_sides, closure_decision, metric
import recipe_execution_010 as gate


class HeartbeatAssociationTests(unittest.TestCase):
    def test_same_pid_component_files_stay_distinct_and_wrong_identity_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for component in ("Gaode.Infrastructure", "VirtualPlc"):
                p = root / f"stages-42-{component}.json"
                p.write_text(json.dumps(dict(pid=42, component=component)), encoding="utf-8")
                self.assertEqual(p, stage_trace_path(root, 42, component))
            host = root / "stages-42-Gaode.Infrastructure.json"
            host.write_text(json.dumps(dict(pid=43, component="Gaode.Infrastructure")), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "IdentityMismatch"):
                stage_trace_path(root, 42, "Gaode.Infrastructure")
            legacy = root / "stages-42.json"
            legacy.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "IdentityAmbiguous"):
                stage_trace_path(root, 42, "VirtualPlc")
            host.unlink()
            self.assertEqual(legacy, stage_trace_path(root, 42, "Gaode.Infrastructure"))

    def setUp(self):
        # Actual attempt12 edge 855 / response 524, plus the next transitions.
        self.edges = [
            dict(sequence=855, current="0", occurredAtUtc="2026-10-04T15:22:29.4614121+00:00"),
            dict(sequence=857, current="1", occurredAtUtc="2026-10-04T15:22:30.4614565+00:00"),
            dict(sequence=862, current="0", occurredAtUtc="2026-10-04T15:22:31.4661744+00:00")]
        self.echoes = [
            dict(value=0, connectionId="actual-peer", transaction=524, receivedAtUtc="2026-10-04T15:22:29.4618334+00:00"),
            dict(value=65280, connectionId="actual-peer", transaction=529, receivedAtUtc="2026-10-04T15:22:30.7123308+00:00"),
            dict(value=0, connectionId="actual-peer", transaction=533, receivedAtUtc="2026-10-04T15:22:31.6379771+00:00")]
        self.host = dict(connectionId="host", localEndpoint="[::ffff:127.0.0.1]:62017",
            remoteEndpoint="127.0.0.1:61987", transaction=524, function=5, offset=1, count=0,
            requestWriteStartedTick=100, responseEnded=200)
        self.device = dict(connectionId="peer", localEndpoint="127.0.0.1:61987",
            remoteEndpoint="127.0.0.1:62017", transaction=524, function=5, offset=1, count=0,
            receivedTick=110, responseWriteStartedTick=120, responseSentTick=210)

    def test_near_anchor_response_is_not_replaced_by_next_same_value(self):
        # Real Host send 46260709273146 precedes the converted edge 46260709273351;
        # same-device UTC proves receipt follows the actual edge. No tolerance filter.
        self.assertLess(46260709273146, 46260709273351)
        delays, rows, errors = heartbeat_edge_receipts(self.edges, self.echoes, .4284)
        self.assertEqual([], errors)
        self.assertEqual([524, 529, 533], [r["transaction"] for r in rows])
        self.assertGreater(delays[0], .4284)
        self.assertLess(delays[0], 1)

    def test_missing_or_wrong_value_cannot_borrow_later_same_value(self):
        for echoes in (self.echoes[1:], [dict(self.echoes[0], value=65280), *self.echoes[1:]]):
            with self.subTest(echoes=echoes):
                _, rows, errors = heartbeat_edge_receipts(self.edges, echoes, 0)
                self.assertIn("DeviceEdgeResponseMissing:855", errors)
                self.assertNotIn(855, [r["edgeSequence"] for r in rows])

    def test_transport_identity_lifecycle_missing_and_ambiguity(self):
        key = device_exchange_key(self.device)
        self.assertIs(self.device, match_device_exchange(self.host, {key: [self.device]}, {}))
        changed = copy.deepcopy(self.device)
        changed["remoteEndpoint"] = "127.0.0.1:62018"
        for index in ({}, {device_exchange_key(changed): [changed]}, {key: [self.device, self.device]}):
            with self.subTest(index=index), self.assertRaises(ValueError):
                match_device_exchange(self.host, index, {})
        with self.assertRaisesRegex(ValueError, "DeviceConnectionIdentityChanged"):
            match_device_exchange(self.host, {key: [self.device]}, {"host": "another-generation"})
        changed = dict(self.device, responseWriteStartedTick=300)
        with self.assertRaises(ValueError):
            match_device_exchange(self.host, {key: [changed]}, {})


class AcceptanceClassificationTests(unittest.TestCase):
    """Controlled decision inputs, never reported as actual component/PLC execution."""
    def setUp(self):
        self.before = dict(commonErrors=[], idle=dict(requests=8000), activity=dict(requests=7500),
            durationMs=59000, evidence=dict(bytes=1500000,rawExchanges=4500))
        self.after = dict(commonErrors=[], idle=dict(requests=1080), activity=dict(requests=2500),
            durationMs=58900, evidence=dict(bytes=670000,rawExchanges=1900))
        self.context = {k:"controlled-decision-fixture" for k in gate.IDENTITY}
        self.context["startedAt"] = "2026-10-05T00:00:00+00:00"
        self.expected = [dict(caseId="required-protection", method="RealProtection", dataRowId="row1",
            dataRow="independent-input", requiredEvidence=["RawTrace", "Persistence"])]
        self.rows = [gate.row(self.expected[0], self.context, "Passed", self.context["startedAt"],
            "2026-10-05T00:00:01+00:00")]
        self.light = dict(result="Passed",requiredCount=70,observedCount=70,errors=[])

    def decide(self, findings=(), input_errors=(), rows=None, common_errors=()):
        measured = metric([1, 617.2716], 25)
        self.after["afterTargets"] = target_classification(dict(errors=list(findings),
            performanceMetrics={"SinglePdu25ms":measured}))
        self.after["commonErrors"] = list(common_errors)
        comparison = compare_sides(self.before, self.after, input_errors)
        qualified = gate.validate_rows(self.expected, self.rows if rows is None else rows, self.context)
        positive = gate.validate_rows(self.expected, self.rows, self.context)
        n3 = dict(positive=positive,removedCase="required-protection",
            rejected=gate.validate_rows(self.expected, [], self.context))
        return closure_decision(comparison, qualified, self.light, n3)

    def test_only_approved_performance_deviations_allow_qualified_closure(self):
        findings = ["SinglePdu25ms", "DueToFirstSend:B", "CompletePublication:P", "ActualInterval:H",
            "SharedBlockInterval:(3, 127, 5)", "ConservativeAge:B", "CommandEligibilityToSend50ms",
            "DemandComplete:B", "HostObservationToEcho50ms", "DeviceEdgeToEcho400ms"]
        result = self.decide(findings)
        self.assertTrue(result["passed"])
        self.assertEqual("ClosedWithPerformanceAndFieldLimitations", result["softwareClosure"])
        self.assertEqual("TargetsNotMet", result["performanceObservations"]["result"])
        self.assertCountEqual(findings, result["performanceObservations"]["findings"])
        measured = result["performanceObservations"]["metrics"]["SinglePdu25ms"]
        self.assertEqual((2,617.2716,1), (measured["count"],measured["maximum"],measured["exceeded"]))

    def test_hard_failures_and_unknown_findings_cannot_be_offset_by_load_savings(self):
        for failure in ("FirstStateInterval75:X", "FirstStateInterval75:F:first", "ActualInterval:U:first",
                "ActivationToFirstPublication75ms:X", "IdleTotalBudgetExceeded", "LoopPlanPreparations",
                "RepeatedCacheWakeups", "Run2AxisStarts33", "DeviceEchoReceiptMissing",
                "DeviceEdgeResponseMissing:1", "MissingGroupTiming:B", "NewUnclassifiedFailure"):
            with self.subTest(failure=failure):
                result = self.decide(["SinglePdu25ms", failure])
                self.assertFalse(result["passed"])
                self.assertEqual("Failed",result["correctness"]["result"])
                self.assertEqual("Passed",result["loadReduction"]["result"])
        for failure in ("OriginalIoDeadlineExceeded", "ActualFailedExchange", "RawEvidenceGap"):
            with self.subTest(failure=failure):
                self.assertFalse(self.decide(["SinglePdu25ms"],common_errors=[failure])["passed"])
        self.after["durationMs"] = 63951
        self.assertFalse(self.decide()["passed"])

    def test_missing_skip_identity_incomparability_and_n3_positive_remain_blocking(self):
        variants = [[], [dict(self.rows[0],outcome="Skipped",executed=False)],
            [dict(self.rows[0],sourceDigest="wrong-source")], [dict(self.rows[0],evidenceKinds=["RawTrace"])]]
        for rows in variants:
            with self.subTest(rows=rows):
                result=self.decide(["SinglePdu25ms"],rows=rows)
                self.assertFalse(result["passed"])
                self.assertEqual("Failed",result["evidenceIntegrity"]["result"])
        self.assertFalse(self.decide(input_errors=["InputSemanticDifference:budget.json"])["passed"])
        self.light["observedCount"]=69
        self.assertFalse(self.decide()["passed"])


if __name__ == "__main__":
    unittest.main()
