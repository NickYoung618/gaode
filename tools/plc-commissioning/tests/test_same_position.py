"""Core regression via real loopback Modbus. No real devices."""
import asyncio
import unittest
from test_commissioning import Tests as BaseTests
from app import AXES

class SamePositionTests(BaseTests):
    async def test_manual_same_position_sends_no_writes(self):
        self.sim.set(AXES['Y'][4],20)
        self.sim.stale_axes.add('Y')
        before=len(self.sim.requests)
        a=(await self.app.command('axis',dict(axis='Y',target=20)))['action']
        self.assertTrue(a['reusedPosition'])
        self.assertFalse(a['startDispatched'])
        self.assertIsNone(a['plcCompleted'])
        self.assertTrue(a['cleared'])
        self.assertTrue(all(r['function']==3 for r in self.sim.requests[before:]))

    async def test_fresh_read_prevents_cached_position_reuse(self):
        self.sim.set(AXES['Y'][4],20)
        await self.poll()
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
        self.sim.set(AXES['Y'][4],20)
        self.sim.set(AXES['ZCamera'][4],5)
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
        self.sim.stale_axes.update(('X','Y','ZCamera'))
        await self.run_jobs([dict(type='xy',x=0,y=0),dict(type='axis',axis='ZCamera',value=0)])
        self.assertEqual(len(self.app.actions),3)
        self.assertTrue(all(a['reusedPosition'] for a in self.app.actions))
        forbidden={AXES[a][i] for a in ('X','Y','ZCamera') for i in (1,2)}
        self.assertFalse(any(e['kind']=='WRITE_ACCEPTED' and e['mb'] in forbidden for e in self.app.events))

# Use the existing connection fixture without collecting its unrelated broad suite.
for name in dir(BaseTests):
    if name.startswith('test_') and name not in SamePositionTests.__dict__:
        setattr(SamePositionTests,name,None)
del BaseTests
