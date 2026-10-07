"""Real Modbus TCP loopback tests. Never contact real equipment."""
import asyncio
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
import zipfile
import io
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'src'))
sys.path.insert(0,str(ROOT/'tests'))
from app import App
from simulator import Simulator
from transport import decode,encode,real_interpretations


class Tests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.root=Path(self.tmp.name)
        for folder in ('sources','config','specs'):
            shutil.copytree(ROOT/folder,self.root/folder)
        self.app=App(self.root)
        self.sim=Simulator(self.root)
        port=await self.sim.start()
        await self.app.command('connect',dict(host='127.0.0.1',port=port))

    async def asyncTearDown(self):
        await self.app.command('disconnect',{})
        await self.sim.close()
        self.tmp.cleanup()

    async def poll(self):
        async with self.app.lock:await self.app.poll()

    async def test_sources_85_points_readonly_connect(self):
        self.assertEqual(len(self.app.values),85)
        self.assertEqual(sum(p['direction']=='PC->PLC' for p in self.app.points),26)
        self.assertTrue(all(r['function']==3 for r in self.sim.requests))
        self.assertFalse(self.app.heartbeat['enabled'])
        self.assertNotIn(6039,self.app.by_mb)
        self.assertEqual(self.app.by_mb[6037]['logicalName'],'PC_Alarm')
        self.assertEqual(self.app.protocol['profile']['sourceFiles'][0]['sha256'],'37095748a61574d9a1c9bf17ad0ac9118d554fa73ac8f5e10003718b6627511d')
        self.assertEqual(self.app.protocol['profile']['sourceFiles'][1]['sha256'],'8e846d0f71351f87b42db1bf40790f40766a392fd0a9e0968201f8c2056eb476')

    async def test_explicit_heartbeat_and_adjacent_byte(self):
        self.sim.set(2010,1)
        await self.app.command('heartbeat',{'enabled':True})
        for _ in range(7):
            await asyncio.sleep(.11);await self.poll()
        self.assertGreater(self.app.heartbeat['changes'],1)
        self.assertGreater(self.app.heartbeat['responses'],1)
        self.assertEqual(self.sim.get(2010),1)
        self.assertEqual(self.sim.get(2011),self.sim.get(6038))
        self.assertTrue(any(r['function']==6 for r in self.sim.requests))

    async def test_byte_merge_preserves_all_adjacent_bits(self):
        self.sim.registers[1005]=0xAB7F
        await self.app.command('write',{'id':self.app.by_mb[2011]['id'],'value':1})
        self.assertEqual(self.sim.registers[1005],0x017F)
        await self.app.command('write',{'id':self.app.by_mb[2010]['id'],'value':0})
        self.assertEqual(self.sim.registers[1005],0x0100)

    async def test_positive_negative_fractional_real_and_int(self):
        for mb,value in [(2024,-12.5),(2028,7.125),(2044,35.25),(2022,-123)]:
            r=await self.app.command('write',{'id':self.app.by_mb[mb]['id'],'value':value})
            self.assertTrue(r['result']['writeResponded'])
            self.assertTrue(r['result']['readbackMatched'])
            self.assertEqual(self.sim.get(mb),value)
        self.assertEqual(self.sim.registers[1012],0)
        self.assertEqual(self.sim.registers[1013],0xC148)
        self.assertEqual(self.sim.get(2001),0)

    async def test_x_and_r_new_motion_completion_and_clear(self):
        for axis,target,start in [('X',-8.25,2001),('R',72.5,2000)]:
            r=await self.app.command('axis',dict(axis=axis,target=target))
            action=r['action']
            for _ in range(30):
                await asyncio.sleep(.05);await self.poll()
                if action['state']=='completed':break
            self.assertEqual(action['state'],'completed')
            self.assertTrue(action['motionEvidence'])
            self.assertTrue(action['plcCompleted'])
            self.assertTrue(action['cleared'])
            self.assertEqual(self.sim.get(start),0)
            self.assertIsNone(action['coordinateAcceptance'])
            related=[e for e in self.app.events if e.get('operationId')==action['operationId']]
            self.assertTrue({'WRITE_RESPONSE','READBACK','START','MOTION_EVIDENCE','ACTION_COMPLETE','CLEAR'} <= {e['kind'] for e in related})
            self.assertTrue(any(e['kind']=='TX' and e.get('actionId')==action['actionId'] for e in related))

    async def test_old_arrived_without_motion_stays_pending(self):
        self.sim.stale_axes.add('R')
        action=(await self.app.command('axis',dict(axis='R',target=20.5)))['action']
        await self.poll()
        self.assertEqual(action['state'],'pending')
        self.assertFalse(action['motionEvidence'])
        self.assertIsNone(action['plcCompleted'])
        action['startEpoch']-=31
        await self.poll()
        self.assertEqual(action['state'],'timeout')
        self.assertFalse(action['plcCompleted'])
        await self.app.command('clear',{'axis':'R'})
        self.assertEqual(self.sim.get(2000),0)

    async def test_serial_heartbeat_manual_actions(self):
        self.sim.set(2010,1)
        await self.app.command('heartbeat',{'enabled':True})
        results=await asyncio.gather(
            self.app.command('write',{'id':self.app.by_mb[2010]['id'],'value':0}),
            self.app.command('write',{'id':self.app.by_mb[2006]['id'],'value':1}),
            self.app.command('axis',{'axis':'X','target':2.5}))
        self.assertEqual(self.sim.get(2010),0)
        self.assertEqual(self.sim.get(2006),1)
        self.assertTrue(results[0]['result']['readbackMatched'])
        self.assertTrue(results[1]['result']['readbackMatched'])

    async def test_decode_errors_do_not_gate_monitor_or_unrelated_send(self):
        self.sim.registers[3000]=0xA5FF
        await self.poll()
        self.assertEqual(len(self.app.values),85)
        self.assertTrue(self.app.fresh())
        self.assertFalse(self.app.snapshot()['interpretationValid'])
        await self.app.command('write',{'id':self.app.by_mb[2006]['id'],'value':1})
        self.assertEqual(self.sim.get(2006),1)

    async def test_real_fault_blocks_motion_not_reading_or_reset(self):
        self.sim.set(6020,1);await self.poll()
        with self.assertRaisesRegex(ValueError,'急停'):
            await self.app.command('axis',dict(axis='X',target=1.25))
        await self.app.command('write',{'id':self.app.by_mb[2009]['id'],'value':1})
        self.assertEqual(self.sim.get(2009),1)
        self.assertTrue(self.app.fresh())
        self.sim.set(6020,0);self.sim.set(6028,1);await self.poll()
        with self.assertRaisesRegex(ValueError,'本轴报警'):
            await self.app.command('axis',dict(axis='X',target=1.25))
        self.assertTrue(self.app.fresh())

    async def test_configurable_bases_fc04_and_byte_order(self):
        await self.app.command('disconnect',{})
        cfg=json.loads((self.root/'config/default.json').read_text(encoding='utf-8'))
        cfg.update(host='127.0.0.1',port=self.app.cfg['port'],pcBase=200,plcBase=800,feedbackFc=4,boolOrder='EvenHigh',realOrder='Dcba')
        self.sim.cfg=cfg
        self.sim.registers={i:0 for i in range(65536)}
        self.sim.set(6015,1);self.sim.set(6020,0);self.sim.set(6035,0)
        await self.app.command('settings',cfg)
        await self.app.command('connect',{})
        await self.app.command('write',{'id':self.app.by_mb[2024]['id'],'value':-1.25})
        await self.app.command('write',{'id':self.app.by_mb[2011]['id'],'value':1})
        self.assertEqual(self.sim.get(2024),-1.25)
        self.assertEqual(self.sim.registers[205]&255,1)
        self.assertTrue(any(r['function']==4 for r in self.sim.requests))
        self.assertEqual(self.app.snapshot()['points'][0]['pdu'],200)

    async def test_fc22_and_failure_no_unsafe_fallback(self):
        self.app.cfg['byteWrite']='FC22';self.sim.cfg['byteWrite']='FC22'
        self.sim.set(2010,1)
        await self.app.command('write',{'id':self.app.by_mb[2011]['id'],'value':1})
        self.assertEqual(self.sim.get(2010),1)
        self.sim.fail_function=22
        with self.assertRaisesRegex(Exception,'0x02'):
            await self.app.command('write',{'id':self.app.by_mb[2011]['id'],'value':0})
        self.assertFalse(any(r['function']==6 for r in self.sim.requests))

    async def test_txrx_exception_transaction_and_stale(self):
        self.sim.fail_function=16
        with self.assertRaisesRegex(Exception,'0x02'):
            await self.app.command('write',{'id':self.app.by_mb[2024]['id'],'value':12.5})
        self.assertFalse(self.app.sent[self.app.by_mb[2024]['id']]['writeResponded'])
        self.sim.fail_function=None;self.sim.bad_tid=True
        with self.assertRaisesRegex(Exception,'头不匹配'):await self.poll()
        self.assertFalse(self.app.fresh())
        self.assertTrue(any(e['kind']=='RX_INVALID' for e in self.app.events))

    async def test_timeout_not_retried_and_readback_unknown(self):
        self.app.cfg['timeout']=.1
        self.sim.silent=True
        before=len(self.sim.requests)
        with self.assertRaisesRegex(Exception,'TIMEOUT'):
            await self.app.command('write',{'id':self.app.by_mb[2024]['id'],'value':2.5})
        self.assertEqual(len(self.sim.requests)-before,1)
        self.assertFalse(self.app.fresh())
        self.assertEqual(self.app.transport.timeouts,1)

    async def test_export_sources_logs_hashes_notes(self):
        await self.app.command('write',{'id':self.app.by_mb[2006]['id'],'value':1})
        await self.app.command('note',{'text':'现场备注测试 <script>不是指令</script>'})
        with zipfile.ZipFile(io.BytesIO(self.app.export())) as z:
            self.assertIn('sources/PC.xls',z.namelist())
            self.assertIn('sources/PLC.xls',z.namelist())
            logs=z.read('logs/'+self.app.session+'/events.jsonl').decode('utf-8')
            self.assertIn('"kind": "TX"',logs)
            self.assertIn('"kind": "RX"',logs)
            self.assertIn('现场备注测试',logs)
            for name,digest in json.loads(z.read('manifest.sha256.json')).items():
                self.assertEqual(hashlib.sha256(z.read(name)).hexdigest(),digest)

    async def test_encode_all_real_orders_and_invalid_inputs(self):
        p=self.app.by_mb[2024]
        for order in ('Abcd','Badc','Cdab','Dcba'):
            cfg=dict(self.app.cfg,realOrder=order)
            self.assertEqual(decode(p,encode(p,-12.5,cfg),cfg),(-12.5,'Good'))
        self.assertEqual(real_interpretations([0x4148,0])['Abcd'],12.5)
        for mb,value in [(2024,float('nan')),(2024,True),(2011,2),(2011,True),(2012,1.5),(2012,32768)]:
            with self.assertRaises(ValueError):encode(self.app.by_mb[mb],value,self.app.cfg)
        with self.assertRaises(ValueError):await self.app.command('write',{'id':self.app.by_mb[6015]['id'],'value':1})


    async def test_cdab_all_pc_real_and_independent_feedback(self):
        vectors = [(11.0, [0x0000, 0x4130]), (12.5, [0x0000, 0x4148]), (-3.25, [0x0000, 0xC050])]
        # Known IEEE754 words are checked directly, independent of simulator decoding.
        before = len(self.sim.requests)
        for point in self.app.points:
            if point['direction'] == 'PC->PLC' and point['type'] == 'REAL':
                for value, expected in vectors:
                    self.assertEqual(encode(point, value, self.app.cfg), expected)
                    response = await self.app.command('write', {'id':point['id'],'value':value})
                    self.assertEqual(response['result']['rawRegisters'], expected)
                    self.assertTrue(response['result']['readbackMatched'])
        writes = [bytes.fromhex(x['tx']) for x in self.sim.requests[before:] if x['function'] == 16]
        self.assertTrue(writes)
        for tx in writes:
            self.assertIn(list(__import__('struct').unpack('>HH', tx[-4:])), [v[1] for v in vectors])
        self.assertTrue(all(x['function'] in (3,16) for x in self.sim.requests[before:]))
        for mb in (2000,2001,2002,2003,2004,2005):self.assertEqual(self.sim.get(mb), 0)
        self.sim.set(6076, 12.5)
        await self.poll()
        self.assertEqual(self.app.values[self.app.by_mb[6076]['id']]['registers'], [0,0x4148])
        self.assertEqual(decode(self.app.by_mb[6076],[0x4148,0],dict(self.app.cfg,realReadOrder='Abcd'))[0],12.5)
        self.assertEqual(self.app.value(6076),12.5)
        feedback=self.app.by_mb[6076]
        cfg=dict(self.app.cfg,realReadOrder='Cdab')
        self.assertEqual(decode(feedback,[0,0xC050],cfg)[0],-3.25)
        legacy=dict(self.app.cfg);legacy.pop('realWriteOrder');legacy.pop('realReadOrder')
        self.assertEqual(encode(self.app.by_mb[2028],11.0,legacy),[0x4130,0])
        self.assertEqual(encode(self.app.by_mb[2022],-123,self.app.cfg),[0xFF85])
        self.assertEqual(encode(self.app.by_mb[2002],1,self.app.cfg),[1])

    async def test_all_feedback_real_cdab_literal_words_and_actual_socket(self):
        for point in self.app.points:
            if point['direction']=='PLC->PC' and point['type']=='REAL':
                for raw,expected in [([0,0x4130],11.0),([0,0x4148],12.5),([0,0xC050],-3.25)]:
                    self.assertEqual(decode(point,raw,self.app.cfg)[0],expected)
        self.assertEqual(self.app.snapshot()['localEndpoint'][0],'127.0.0.1')
        self.assertTrue(any(e['kind']=='NETWORK_PATH' for e in self.app.events))

    async def test_xy_jitter_never_completes_or_auto_clears_old_arrived(self):
        for axis,actual,start in [('X',6064,2001),('Y',6076,2002)]:
            self.sim.stale_axes.add(axis)
            self.sim.set(actual,5.0 if axis=='X' else 11.0)
            await self.poll()
            a=(await self.app.command('axis',dict(axis=axis,target=2.0)))['action']
            write_count=sum(q['function'] in (6,16,22) for q in self.sim.requests)
            for value in (5.00001335144043,4.999994277954102,5.00001,2.0):
                self.sim.set(actual,value);await self.poll()
                self.assertEqual(a['state'],'pending')
                self.assertFalse(a['motionEvidence'])
                self.assertFalse(a['busySeen'])
                self.assertIsNone(a['plcCompleted'])
                self.assertFalse(a['cleared'])
                self.assertEqual(self.sim.get(start),1)
            self.assertEqual(sum(q['function'] in (6,16,22) for q in self.sim.requests),write_count)
            a['startEpoch']-=31;await self.poll()
            self.assertEqual(a['state'],'timeout')
            self.assertEqual(self.sim.get(start),1)
            await self.app.command('clear',{'axis':axis})
            self.assertEqual(self.sim.get(start),0)

    async def test_only_new_plc_busy_then_arrived_completes(self):
        self.sim.stale_axes.add('X')
        a=(await self.app.command('axis',dict(axis='X',target=2.0)))['action']
        await self.poll();self.assertEqual(a['state'],'pending')
        self.sim.set(6040,0);await self.poll()
        self.assertTrue(a['busySeen']);self.assertEqual(a['state'],'observing')
        self.assertEqual(self.sim.get(2001),1)
        self.sim.set(6040,1);await self.poll()
        self.assertTrue(a['plcCompleted']);self.assertTrue(a['cleared'])
        self.assertEqual(a['completionEvidence'],'post-start PLC feedback 1->0->1')
        self.assertIsNone(a['coordinateAcceptance'])
        self.assertEqual(self.sim.get(2001),0)

    async def test_previous_busy_and_invalid_feedback_do_not_complete(self):
        self.sim.stale_axes.add('Y');self.sim.set(6042,0);await self.poll()
        a=(await self.app.command('axis',dict(axis='Y',target=2.0)))['action']
        self.sim.set(6042,1);self.sim.set(6076,2.0);await self.poll()
        self.assertEqual(a['state'],'pending');self.assertFalse(a['busySeen'])
        self.sim.set(6042,99);await self.poll()
        self.assertEqual(a['state'],'failed');self.assertFalse(a['plcCompleted'])
        self.assertFalse(a['cleared']);self.assertEqual(self.sim.get(2002),1)

if __name__=='__main__':unittest.main(verbosity=2)
