"""Component assertions for the same function called by persisted-route verification."""
import copy
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from semantic_009_evidence import sorting_commits, recovery_checks, failed_unload_holds


class SemanticSortingTests(unittest.TestCase):
    def fixture(self):
        correlation = dict(runId='run', actionId='action', objectId='member')
        assignment = dict(objectId='member', sourcePoint={'id': 'P03'}, targetPoint={'id': 'P15'})
        source = dict(target=assignment['sourcePoint'], matched=True, correlation=correlation)
        target = dict(target=assignment['targetPoint'], matched=True, correlation=correlation)
        common = dict(correlation=correlation, diagnosticEvidenceReferences=[{'evidenceId': 'component-only'}])
        return [('pick', dict(schemaVersion='sorting-evidence/1', kind='SortingAssignmentInTransit',
                             assignment=assignment, evidence=dict(common, sourcePositionReached=source))),
                ('place', dict(schemaVersion='sorting-evidence/1', kind='SortingAssignmentOccupied',
                              assignment=assignment, evidence=dict(common, meaning='MaterialTransferred', positions=[source, target])))]

    def test_committed_order_and_positions(self):
        result = sorting_commits(self.fixture(), 'run')
        self.assertEqual(result, [dict(actionId='action',pickEventId='pick',placeEventId='place',entityId='member')])

    def test_no_transfer_does_not_invent_a_pick(self):
        self.assertEqual(sorting_commits([], 'run'), [])

    def test_required_evidence_is_not_inferred_from_final(self):
        for case in ['reverse', 'wrong-run', 'no-position', 'wrong-position', 'no-reference', 'duplicate']:
            with self.subTest(case=case):
                rows = copy.deepcopy(self.fixture())
                if case == 'reverse': rows.reverse()
                if case == 'wrong-run': rows[1][1]['evidence']['correlation']['runId'] = 'other'
                if case == 'no-position': rows[1][1]['evidence']['positions'] = []
                if case == 'wrong-position': rows[1][1]['evidence']['positions'][1]['matched'] = False
                if case == 'no-reference': rows[0][1]['evidence']['diagnosticEvidenceReferences'] = []
                if case == 'duplicate': rows.append(copy.deepcopy(rows[1]))
                with self.assertRaises(ValueError): sorting_commits(rows, 'run')


class UnloadFailureTests(unittest.TestCase):
    def fixture(self):
        return [dict(stage='Sorting',eventType='IntentRecorded',error=None,
                     payload=dict(kind='SortingAssignmentsReserved',assignments=[dict(objectId='P03')])),
                dict(stage='UnloadPreparation',eventType='IntentRecorded',error=None,payload={}),
                dict(stage='UnloadPreparation',eventType='Started',error=None,payload={}),
                dict(stage='UnloadPreparation',eventType='UnknownHeld',error='DeviceActionOutcomeUnconfirmed',payload={})]

    def test_pre_unload_reservation_is_retained_not_a_sorting_action(self):
        self.assertTrue(failed_unload_holds(self.fixture()))

    def test_failed_unload_cannot_authorize_sorting_or_hide_missing_hold(self):
        for case in ['no-hold','completed-unload','sorting-started','pick-committed','late-reservation','wrong-reservation','empty-reservation']:
            with self.subTest(case=case):
                events=self.fixture()
                if case=='no-hold':events.pop()
                if case=='completed-unload':events.append(dict(stage='UnloadPreparation',eventType='Completed',payload={}))
                if case=='sorting-started':events.append(dict(stage='Sorting',eventType='Started',payload={}))
                if case=='pick-committed':events.append(dict(stage='Sorting',eventType='Executing',payload=dict(kind='SortingAssignmentInTransit')))
                if case=='late-reservation':events.append(events.pop(0))
                if case=='wrong-reservation':events[0]['payload']['kind']='SortingAssignmentInTransit'
                if case=='empty-reservation':events[0]['payload']['assignments']=[]
                self.assertFalse(failed_unload_holds(events))


class ConservativeRecoveryTests(unittest.TestCase):
    def fixture(self):
        before=dict(runId='run', rows=[dict(eventId='reservation', stage='Sorting',
            eventType='IntentRecorded', payload='{"kind":"SortingAssignmentsReserved"}', digest='original')])
        after=copy.deepcopy(before)
        after['rows'].append(dict(eventId='recovered',stage='Sorting',eventType='UnknownHeld',
            payload='{}',digest='new',error='RecoveryInFlight',source='Fallback'))
        restart=dict(run=dict(runId='run',state=22,finalOutcome=0,allowedActions=[]),
            beforeHostPid=1,afterHostPid=2,afterWorkerPid=3,plcPid=4,
            hostDllSha256='build',startedUtc='2026-10-02T00:00:00Z')
        return before,after,restart

    def test_recovery_requires_prior_reservation_and_real_new_process(self):
        before,after,restart=self.fixture()
        self.assertTrue(all(recovery_checks(before,after,restart,'build').values()))
        restart['afterHostPid']=restart['beforeHostPid']
        self.assertFalse(all(recovery_checks(before,after,restart,'build').values()))

    def test_new_recovery_event_cannot_replace_or_rewrite_old_fact(self):
        before,after,restart=self.fixture()
        after['rows'][0]['payload']='{"kind":"SortingAssignmentInTransit"}'
        self.assertFalse(all(recovery_checks(before,after,restart,'build').values()))

    def test_history_query_without_held_reconciliation_is_not_recovery(self):
        before,after,restart=self.fixture()
        after['rows']=after['rows'][:1]
        self.assertFalse(all(recovery_checks(before,after,restart,'build').values()))


if __name__ == '__main__': unittest.main()
