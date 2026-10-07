import asyncio
import copy
import json
from pathlib import Path
import shutil
import sys
import tempfile
import time
import unittest
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'src'))
sys.path.insert(0,str(ROOT/'tests'))
from app import App
from simulator import Simulator


class RecipeSimulator(Simulator):
    def __init__(self,root):
        super().__init__(root)
        self.motion_offset=.74
        self.command_duration=.06
        self.last_flip=self.last_sort=0
        self.flip_motion=self.sort_motion=None
        self.flipMovesZ=False

    def start_edges(self,before):
        prior=set(self.motion)
        super().start_edges(before)
        for axis in set(self.motion)-prior:
            _,base,target=self.motion[axis]
            self.motion[axis]=(time.monotonic()-self.motion_offset,base,target)

    def update(self):
        super().update()
        self.set(6062,self.get(2018))
        flip=self.get(2014)
        if flip!=self.last_flip:
            self.last_flip=flip
            if flip:
                feedback=6050 if flip==1 else 6052
                self.set(feedback,1);self.flip_motion=(time.monotonic(),feedback)
            else:
                self.set(6050,0);self.set(6052,0);self.flip_motion=None
        if self.flip_motion and time.monotonic()-self.flip_motion[0]>self.command_duration:
            self.set(self.flip_motion[1],2)
            if self.flipMovesZ:self.set(6092,8.0)
            self.flip_motion=None
        sort=self.get(2016)
        if sort!=self.last_sort:
            self.last_sort=sort
            self.set(6054,0)
            self.sort_motion=(time.monotonic(),sort) if sort else None
        if self.sort_motion and time.monotonic()-self.sort_motion[0]>self.command_duration:
            self.set(6054,self.sort_motion[1]);self.sort_motion=None


class RecipeTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        for folder in ('config','sources','specs'):shutil.copytree(ROOT/folder,self.root/folder)
        self.app=App(self.root);self.sim=RecipeSimulator(self.root)
        self.port=await self.sim.start()
        await self.app.command('connect',dict(host='127.0.0.1',port=self.port))
        p=copy.deepcopy(self.app.recipe.profile);p.update(modelCode=1.25,grabId=2)
        await self.app.command('recipe-settings',{'profile':p})

    async def asyncTearDown(self):
        await self.app.command('disconnect',{});await self.sim.close();self.tmp.cleanup()

    async def poll(self):
        async with self.app.lock:await self.app.poll()

    async def finish(self):
        await self.app.command('recipe-start',{})
        for _ in range(2500):
            job=self.app.recipe.jobs[0] if self.app.recipe.jobs else {}
            if job.get('type')=='simulation' and job.get('started'):job['started']-=12
            await self.poll();run=self.app.recipe.run
            if run['state']=='waiting':
                kind=self.app.recipe.wait['kind']
                self.assertIn(kind,('load','finish'),'模拟模式中间步骤不应逐步确认')
                await self.app.command('recipe-confirm',{'outcome':'OK'})
            if run['state'] in ('completed','failed'):
                self.assertEqual(run['state'],'completed',run['reason']);return run
            await asyncio.sleep(.008)
        self.fail('Recipe timed out in test: '+repr(self.app.recipe.snapshot()))

    async def test_complete_ok_auto_recipe_with_real_feedback_no_r(self):
        self.sim.flipMovesZ=True
        run=await self.finish()
        self.assertEqual(run['result'],'OK')
        self.assertEqual([x['step'] for x in run['inspections'] if x['step'] in (4,5,11,12)],[4,5,11,12])
        self.assertEqual(run['barcode'],'SIM-翻面件-001')
        self.assertTrue(all(x['simulated'] for x in run['inspections']))
        self.assertEqual(self.sim.get(2014),0)
        writes=[json.loads(line) for line in (self.app.logdir/'events.jsonl').read_text(encoding='utf-8').splitlines() if json.loads(line)['kind']=='WRITE_ACCEPTED']
        self.assertFalse(any(e['mb'] in (2000,2044) for e in writes))
        self.assertFalse(any(e['mb']==2016 for e in writes))
        self.assertEqual([e['value'] for e in writes if e['mb']==2014],[1,2,0])
        self.assertTrue(any(e['mb']==2006 and e['value']==1 for e in writes))
        self.assertTrue(any(e['mb']==2011 for e in writes))
        self.assertTrue(any(e['mb']==2048 and e.get('recipeStep')==6 for e in writes))
        self.assertEqual([e['mb'] for e in writes if e.get('recipeStep')==8 and e['mb'] in (2001,2002) and e['value']==1],[2001,2002])
        self.assertFalse(any(e['mb'] in (2003,2004,2005,2032,2036,2040) and e.get('recipeStep')==8 for e in writes))
        flip_writes=[e for e in writes if e.get('recipeStep') in (7,9)]
        self.assertFalse(any(e['mb'] in (2003,2004,2005,2032,2036,2040) for e in flip_writes))
        self.assertFalse(any(e['mb'] in (2004,2005,2036,2040) for e in writes))
        self.assertEqual([e['value'] for e in writes if e['mb']==2032],[5,5,5,5])
        self.assertEqual([e['recipeStep'] for e in writes if e['mb']==2032],[4,5,11,12])
        for step in (2,3,4,5,6,8,10,11,12,14):
            stepwrites=[e for e in writes if e.get('recipeStep')==step and e['mb'] in (2024,2028,2001,2002)]
            self.assertEqual([e['mb'] for e in stepwrites[:4]],[2024,2028,2001,2002])
        self.assertFalse(any(e['mb'] in (2003,2004,2005,2032,2036,2040) and e.get('recipeStep') in (3,14) for e in writes))
        self.assertFalse(any(e['mb'] in (2016,2018) for e in writes))
        self.assertTrue(all(x['recordPurpose']=='流程测试' and not x['realInspectionResult'] for x in run['inspections']))
        self.assertTrue(run['trail']);self.assertTrue(run['signals'])
        for a in self.app.actions:
            self.assertTrue(a['busySeen']);self.assertTrue(a['cleared'])
        capture=[e for e in run['milestones'] if e['kind']=='SIMULATION' and e.get('recipeStep')==3]
        self.assertEqual(len(capture),1)

    async def test_ng_sorting_pick_place_and_grab_feedback(self):
        self.app.recipe.profile['flowTestOnly']=False
        self.app.recipe.profile['simulation']['B']='NG'
        run=await self.finish();self.assertEqual(run['result'],'NG')
        writes=[json.loads(line) for line in (self.app.logdir/'events.jsonl').read_text(encoding='utf-8').splitlines() if json.loads(line)['kind']=='WRITE_ACCEPTED']
        self.assertEqual([e['value'] for e in writes if e['mb']==2016],[1,2,0])
        self.assertEqual([e['value'] for e in writes if e['mb']==2018],[2])
        self.assertTrue(any(e['mb']==2024 and e['value']==80 for e in writes))
        self.assertEqual(self.sim.get(6062),2)
        z=[e['value'] for e in writes if e['mb']==2040 and e.get('recipeStep')==13]
        self.assertIn(6,z);self.assertIn(7,z);self.assertIn(2,z)

    async def test_pose_review_pending_skips_second_face_not_rescan(self):
        self.app.recipe.profile['flowTestOnly']=False
        self.app.recipe.profile['simulation']['poseReview']='NG'
        run=await self.finish();self.assertEqual(run['result'],'Pending')
        self.assertEqual(run['steps'][10]['state'],'skipped');self.assertEqual(run['steps'][11]['state'],'skipped')
        self.assertFalse(any(x['step'] in (11,12) for x in run['inspections']))
        writes=[json.loads(line) for line in (self.app.logdir/'events.jsonl').read_text(encoding='utf-8').splitlines() if json.loads(line)['kind']=='WRITE_ACCEPTED']
        self.assertTrue(any(e['mb']==2024 and e['value']==95 for e in writes))
        self.assertEqual(len([e for e in run['milestones'] if e['kind']=='SIMULATION' and e.get('recipeStep')==3]),1)

    async def test_pause_disconnect_and_manual_exclusion(self):
        await self.app.command('recipe-start',{})
        await self.app.command('recipe-pause',{})
        before=len(self.sim.requests);await self.poll()
        self.assertFalse(any(e['kind']=='START' for e in self.app.events))
        with self.assertRaises(ValueError):await self.app.command('axis',dict(axis='R',target=4))
        await self.app.command('disconnect',{})
        self.assertEqual(self.app.recipe.run['state'],'failed')
        self.assertFalse(self.app.recipe.active())

    async def test_missing_site_codes_wait_only_dependent_step(self):
        p=copy.deepcopy(self.app.recipe.profile);p['modelCode']=None
        await self.app.command('recipe-settings',{'profile':p})
        await self.app.command('recipe-start',{})
        self.app.recipe.run['step']=6;self.app.recipe.prepare();await self.poll();await self.poll()
        self.assertEqual(self.app.recipe.wait['kind'],'parameters')
        self.assertTrue(self.app.fresh());self.assertEqual(len(self.app.values),85)
        self.assertFalse(any(e['kind']=='WRITE_ACCEPTED' and e.get('mb')==2014 for e in self.app.events))

    async def test_old_flip_done_does_not_start_or_pass(self):
        await self.app.command('recipe-start',{})
        self.sim.set(6050,2);await self.poll()
        self.app.recipe.run['step']=7;self.app.recipe.prepare()
        for _ in range(5):await self.poll()
        self.assertEqual(self.app.recipe.run['state'],'failed')
        self.assertEqual(self.sim.get(2014),0)

    async def test_camera_waits_full_five_seconds(self):
        await self.app.command('recipe-start',{})
        self.app.recipe.run['step']=2
        job=dict(type='simulation',kind='pose',message='模拟3D',started=time.time()-4)
        self.app.recipe.jobs=[job]
        await self.poll()
        self.assertEqual(len(self.app.recipe.run['inspections']),0)
        job['started']=time.time()-5.1
        await self.poll()
        self.assertEqual(len(self.app.recipe.run['inspections']),1)

    async def test_worker_observes_short_motion_with_adaptive_poll(self):
        self.sim.motion_offset=.65
        worker=asyncio.create_task(self.app.worker())
        try:
            action=await self.app.command('axis',dict(axis='ZCamera',target=5))
            action=action['action']
            deadline=time.monotonic()+2
            while not action['cleared'] and action['state'] in ('observing','pending','completed') and time.monotonic()<deadline:await asyncio.sleep(.02)
            self.assertEqual(action['state'],'completed',action)
            self.assertTrue(action['busySeen']);self.assertTrue(action['cleared'])
        finally:
            worker.cancel()
            try:await worker
            except asyncio.CancelledError:pass

    async def test_arrived_but_position_mismatch_never_clears(self):
        action=await self.app.begin_axis('X',10,'tolerance-test')
        action.update(requirePositionMatch=True,positionTolerance=.1,busySeen=True)
        self.sim.motion.clear();self.sim.set(6040,1);self.sim.set(6064,9.7)
        await self.poll()
        self.assertEqual(action['state'],'pending');self.assertFalse(action['cleared'])
        self.assertEqual(self.sim.get(2001),1)
        self.sim.set(6064,9.95);await self.poll()
        self.assertTrue(action['cleared']);self.assertTrue(action['coordinateAcceptance'])

    async def test_recipe_same_position_arrived_without_busy_completes(self):
        for axis,actual,feedback,start in [('Y',6076,6042,2002),('ZCamera',6084,6044,2003)]:
            self.sim.stale_axes.add(axis);self.sim.set(actual,5);self.sim.set(feedback,1);await self.poll()
            action=await self.app.begin_axis(axis,5,'same-position-'+axis)
            action.update(requirePositionMatch=True,positionTolerance=.1)
            await self.poll()
            self.assertTrue(action['cleared']);self.assertEqual(action['state'],'completed')
            self.assertFalse(action['busySeen']);self.assertFalse(action['motionEvidence'])
            self.assertIn('motion transition not observed',action['completionEvidence'])
            self.assertEqual(self.sim.get(start),0)

    async def test_recipe_arrived_without_busy_but_wrong_position_waits(self):
        self.sim.stale_axes.add('Y');self.sim.set(6076,20);await self.poll()
        action=await self.app.begin_axis('Y',25,'wrong-position')
        action.update(requirePositionMatch=True,positionTolerance=.1)
        await self.poll()
        self.assertFalse(action['cleared']);self.assertEqual(self.sim.get(2002),1)
        self.sim.set(6076,24.95);await self.poll()
        self.assertTrue(action['cleared']);self.assertTrue(action['coordinateAcceptance'])

    async def test_failed_recipe_manual_clear_stays_in_real_signal_timeline(self):
        await self.app.command('recipe-start',{})
        self.app.recipe.run['state']='failed'
        self.sim.set(2003,1);await self.poll()
        await self.app.command('clear',dict(axis='ZCamera'))
        records=[e for e in self.app.snapshot()['signalTimeline'] if e['kind']=='READBACK' and e['mb']==2003]
        self.assertEqual(records[-1]['before'],1)
        self.assertEqual(records[-1]['value'],0)
        self.assertTrue(records[-1]['matched'])
        self.assertFalse(any(e['mb'] in (2011,6038) for e in self.app.snapshot()['signalTimeline']))


if __name__=='__main__':unittest.main(verbosity=2)
