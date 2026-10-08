"""Core member selection checks, real loopback Modbus only."""
import asyncio
import unittest
from test_recipe import RecipeTests
from simulator import Simulator
from app import AXES

class MemberGripperTests(unittest.IsolatedAsyncioTestCase):
    asyncSetUp=RecipeTests.asyncSetUp
    asyncTearDown=RecipeTests.asyncTearDown
    poll=RecipeTests.poll

    async def configure(self,members):
        import copy
        profile=copy.deepcopy(self.app.recipe.profile)
        profile['gripperMembers']=members
        await self.app.command('recipe-settings',{'profile':profile})

    def writes(self):
        return [e for e in self.app.events if e['kind']=='WRITE_ACCEPTED']

    async def test_member_switch_and_same_gripper_reuse_no_motion(self):
        await self.configure([dict(memberId='A1',material='A',grabId=1),
                              dict(memberId='B1',material='B',grabId=2),dict(memberId='B2',material='B',grabId=2)])
        await self.app.command('recipe-gripper-check',{})
        self.app.recipe.profile['gripperMembers'][0]['grabId']=2 # already queued requirements stay frozen
        for _ in range(40):
            await self.poll()
            if self.app.recipe.run['state'] in ('completed','failed'):break
            await asyncio.sleep(.01)
        self.assertEqual(self.app.recipe.run['state'],'completed',self.app.recipe.run['reason'])
        self.assertEqual([e['value'] for e in self.writes() if e['mb']==2018],[1,2])
        self.assertTrue(all(e['mb'] in (2018,2006,2011) for e in self.writes()))
        evidence=[e for e in self.app.events if e['kind']=='RECIPE_GRAB']
        self.assertEqual([e['memberId'] for e in evidence],['A1','B1','B2'])
        self.assertTrue(evidence[-1]['reused'])
        self.assertEqual(self.app.recipe.run['selectionMembers'][0]['grabId'],1)

    async def test_missing_member_gripper_blocks_before_any_write(self):
        await self.configure([dict(memberId='A1',material='A',grabId=1),dict(memberId='B1',material='B',grabId=None)])
        before=len(self.sim.requests)
        with self.assertRaises(ValueError):await self.app.command('recipe-gripper-check',{})
        self.assertTrue(all(r['function']==3 for r in self.sim.requests[before:]))
        self.assertIsNone(self.app.recipe.run)

    async def test_unmatched_feedback_times_out_without_motion_or_pick(self):
        await self.configure([dict(memberId='A1',material='A',grabId=1)])
        self.sim.update=lambda:Simulator.update(self.sim) # valid TCP, PLC deliberately does not select
        await self.app.command('recipe-gripper-check',{})
        await self.poll()
        self.app.recipe.jobs[0]['started']-=31
        await self.poll()
        self.assertEqual(self.app.recipe.run['state'],'failed')
        self.assertIn('反馈超时',self.app.recipe.run['reason'])
        self.assertTrue(all(e['mb'] in (2018,2006,2011) for e in self.writes()))

# Fixture class is imported for setup only.
del RecipeTests
