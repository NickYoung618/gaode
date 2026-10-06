"""Finite unit fixtures of the same communication-only route checker."""
import importlib.util
import unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('route_wire',Path(__file__).with_name('check-009-route-wire.py'))
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)


class RouteProbeTests(unittest.TestCase):
    def test_authorization_requires_complete_zero_action_audit(self):
        self.assertTrue(probe.check('authorization',dict(plcWriteAudit=dict(actions=[],actionGap=False)),{})['passed'])
        for audit in [dict(actions=[],actionGap=True),dict(actions=[{'kind':'Move'}],actionGap=False),dict(actions=[])]:
            self.assertFalse(probe.check('authorization',dict(plcWriteAudit=audit),{})['passed'])

    def test_route_ack_count_does_not_accept_missing_clear(self):
        changes=[dict(name='Flip_OK',current='1'),dict(name='Flip_OK',current='0')]
        self.assertTrue(probe.check('route',dict(plcChanges=dict(gap=False,changes=changes)),dict(flips=1))['passed'])
        self.assertFalse(probe.check('route',dict(plcChanges=dict(gap=False,changes=changes[:1])),dict(flips=1))['passed'])

    def test_timeout_requires_the_actual_controlled_hold(self):
        changes=[dict(name=n,current=v) for n,v in [('Flip_Status','2'),('Flip_Current_Face','2'),('Flip_OK','1')]]
        self.assertTrue(probe.check('flip-timeout',dict(plcChanges=dict(gap=False,changes=changes)),{})['passed'])
        changes.append(dict(name='Flip_OK',current='0'))
        self.assertFalse(probe.check('flip-timeout',dict(plcChanges=dict(gap=False,changes=changes)),{})['passed'])

    def test_scene_counts_both_pick_and_place(self):
        actions=[dict(kind='Sort',phase='completed',command=v) for v in [1,2]]
        facts=dict(plcWriteAudit=dict(actions=actions,actionGap=False),plcChanges=dict(changes=[],gap=False))
        self.assertTrue(probe.check('scene',facts,dict(sorts=1))['passed'])
        actions.pop()
        self.assertFalse(probe.check('scene',facts,dict(sorts=1))['passed'])

    def test_summary_retains_motion_and_reset_obligations(self):
        changes=[dict(address='4x0001',current=v) for v in ['2','2','4']]+[dict(name='Z_Reset_Status',current='2')]*2
        self.assertTrue(probe.check('summary',dict(plcChanges=dict(changes=changes,gap=False)),dict(captures=2))['passed'])
        changes.pop()
        self.assertFalse(probe.check('summary',dict(plcChanges=dict(changes=changes,gap=False)),dict(captures=2))['passed'])


if __name__=='__main__':unittest.main()
