"""Core regression via real loopback Modbus. No real devices."""
import asyncio
import unittest
from test_commissioning import Tests as BaseTests
from app import AXES

class SamePositionTests(BaseTests):
    async def establish(self, targets):
        for axis,target in targets.items():
            action=await self.app.begin_axis(axis,target,'establish-'+axis,position_tolerance=.1)
            for _ in range(80):
                await self.poll()
                if action['state']=='completed':break
                await asyncio.sleep(.025)
            self.assertEqual(action['state'],'completed',action)
            self.assertEqual(self.sim.get(AXES[axis][3]),0)

    async def test_manual_same_position_sends_no_writes(self):
        await self.establish({'Y':20})
        self.sim.stale_axes.add('Y')
        before=len(self.sim.requests)
        a=(await self.app.command('axis',dict(axis='Y',target=20)))['action']
        self.assertTrue(a['reusedPosition'])
        self.assertFalse(a['startDispatched'])
        self.assertIsNone(a['plcCompleted'])
        self.assertTrue(a['cleared'])
        self.assertTrue(all(r['function']==3 for r in self.sim.requests[before:]))

    async def test_fresh_read_prevents_cached_position_reuse(self):
        await self.establish({'Y':20})
        self.sim.set(AXES['Y'][4],21)
        self.sim.stale_axes.add('Y')
        a=(await self.app.command('axis',dict(axis='Y',target=20)))['action']
        self.assertFalse(a['reusedPosition'])
        self.assertTrue(a['startDispatched'])
        self.assertNotEqual(a['state'],'completed')
        await self.app.command('clear',dict(axis='Y'))

    async def run_jobs(self,jobs):
        await self.app.command('recipe-start',{})
        self.app.recipe.jobs=jobs
        for _ in range(80):
            await self.poll()
            self.assertEqual(self.app.recipe.run['state'],'running',self.app.recipe.run['reason'])
            if not self.app.recipe.jobs:return
            await asyncio.sleep(.025)
        self.fail('Core recipe jobs did not complete')

    async def test_recipe_x_moves_y_and_detection_z_reused(self):
        await self.establish({'Y':20,'ZCamera':5})
        self.app.events.clear()
        self.sim.stale_axes.update(('Y','ZCamera'))
        await self.poll()
        await self.run_jobs([dict(type='xy',x=10,y=20),dict(type='axis',axis='ZCamera',value=5)])
        actions={a['axis']:a for a in self.app.actions}
        self.assertTrue(actions['X']['busySeen'])
        self.assertTrue(actions['X']['plcCompleted'])
        self.assertTrue(actions['X']['cleared'])
        for axis in ('Y','ZCamera'):
            self.assertTrue(actions[axis]['reusedPosition'])
            self.assertFalse(actions[axis]['startDispatched'])
        forbidden={AXES[a][i] for a in ('Y','ZCamera') for i in (1,2)}
        self.assertFalse(any(e['kind']=='WRITE_ACCEPTED' and e['mb'] in forbidden for e in self.app.events))

    async def test_recipe_all_axes_reused_sends_no_axis_writes(self):
        await self.establish({'X':0,'Y':0,'ZCamera':0})
        self.app.actions.clear();self.app.events.clear()
        self.sim.stale_axes.update(('X','Y','ZCamera'))
        await self.run_jobs([dict(type='xy',x=0,y=0),dict(type='axis',axis='ZCamera',value=0)])
        self.assertEqual(len(self.app.actions),3)
        self.assertTrue(all(a['reusedPosition'] for a in self.app.actions))
        forbidden={AXES[a][i] for a in ('X','Y','ZCamera') for i in (1,2)}
        self.assertFalse(any(e['kind']=='WRITE_ACCEPTED' and e['mb'] in forbidden for e in self.app.events))

    async def test_initial_same_coordinate_requires_normal_motion(self):
        a=(await self.app.command('axis',dict(axis='Y',target=0)))['action']
        self.assertFalse(a['reusedPosition']);self.assertTrue(a['startDispatched'])
        await self.until_action(a,lambda a:a['cleared'])
        self.assertTrue(a['busySeen'])

    async def test_reconnect_and_observed_reset_discard_closure(self):
        await self.establish({'Y':20})
        await self.app.command('disconnect',{});await self.app.command('connect',{})
        a=(await self.app.command('axis',dict(axis='Y',target=20)))['action']
        self.assertFalse(a['reusedPosition'])
        await self.until_action(a,lambda a:a['cleared'])
        self.sim.set(2009,1);await self.poll();self.sim.set(2009,0)
        a=(await self.app.command('axis',dict(axis='Y',target=20)))['action']
        self.assertFalse(a['reusedPosition']);await self.until_action(a,lambda a:a['cleared'])

    async def test_raw_target_write_invalidates_closure(self):
        await self.establish({'Y':20})
        await self.app.command('write',dict(id=self.app.by_mb[2028]['id'],value=20))
        a=(await self.app.command('axis',dict(axis='Y',target=20)))['action']
        self.assertFalse(a['reusedPosition']);await self.until_action(a,lambda a:a['cleared'])

    async def test_manual_coordinate_mismatch_clears_without_position_eligibility(self):
        self.app.cfg['axes']['Y']['tolerance']=.1
        a=(await self.app.command('axis',dict(axis='Y',target=10)))['action']
        started,baseline,_=self.sim.motion['Y'];self.sim.motion['Y']=(started,baseline,11)
        await self.until_action(a,lambda a:a['cleared'])
        self.assertTrue(a['plcCompleted']);self.assertFalse(a['coordinateAcceptance']);self.assertNotIn('Y',self.app.axis_closures)
        b=(await self.app.command('axis',dict(axis='Y',target=11)))['action']
        self.assertFalse(b['reusedPosition']);await self.until_action(b,lambda a:a['cleared'])

    async def test_stale_request_block_cannot_authorize_position_reuse(self):
        await self.establish({'Y':20})
        self.assertTrue(self.app.position_satisfied('Y',20,.1))
        self.app.values[self.app.by_mb[2002]['id']]['readStartedAt']-=10
        self.assertFalse(self.app.position_satisfied('Y',20,.1))
        await self.app.capture()
        self.assertTrue(self.app.position_satisfied('Y',20,.1))

# Use the existing connection fixture without collecting its unrelated broad suite.
for name in dir(BaseTests):
    if name.startswith('test_') and name not in SamePositionTests.__dict__:
        setattr(SamePositionTests,name,None)
del BaseTests
