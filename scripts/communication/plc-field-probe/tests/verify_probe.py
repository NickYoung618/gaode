"""Bounded developer checks; only loopback sockets, never the field PLC."""
import json
import hashlib
import os
from pathlib import Path
import socket
import struct
import subprocess
import threading
import time
import urllib.request
import shutil
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[4]
TOOL = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts' / '015-plc-field-probe' / time.strftime('%Y%m%d-%H%M%S')
OUT.mkdir(parents=True)
FLAGS = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
RESULTS = []
save_sources = {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (
    TOOL/'Probe.ps1',TOOL/'Export.ps1',Path(__file__),ROOT/'VirtualPlc/bin/Release/net10.0/VirtualPlc.dll')}
(OUT/'source-hashes.json').write_text(json.dumps(save_sources,indent=2),encoding='utf-8')


def save(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')


def exact(s, count):
    result = b''
    while len(result) < count:
        chunk = s.recv(count - len(result))
        if not chunk:
            raise EOFError()
        result += chunk
    return result


def probe(name, config, *args, duration=4):
    directory = OUT / name
    directory.mkdir()
    cfg = directory / 'config.json'
    save(cfg, config)
    command = ['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(TOOL / 'Probe.ps1'),
               '-Config', str(cfg), '-OutputDirectory', str(directory / 'runs'),
               '-NoDashboard', '-DurationSeconds', str(duration), *args]
    return directory, command


def collect(directory, process):
    stdout, _ = process.communicate(timeout=45)
    (directory / 'console.log').write_bytes(stdout or b'')
    files = list((directory / 'runs').glob('*/summary.json'))
    summary = json.loads(files[0].read_text(encoding='utf-8-sig')) if files else None
    return process.returncode, summary, files[0].parent if files else None


def run_probe(name, config, *args, duration=4):
    directory, command = probe(name, config, *args, duration=duration)
    p = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, creationflags=FLAGS)
    return collect(directory, p)


def rows(folder, name):
    return [json.loads(line) for line in (folder / name).read_text(encoding='utf-8-sig').splitlines()]


def check(name, function):
    try:
        detail = function()
        RESULTS.append(dict(name=name, passed=True, detail=detail))
        print('PASS', name, flush=True)
    except Exception as exc:
        RESULTS.append(dict(name=name, passed=False, error=repr(exc)))
        print('FAIL', name, repr(exc), flush=True)
    save(OUT / 'results.json', dict(hardwareTested=False, checks=RESULTS))


class ReferenceServer:
    def __init__(self, fault=None):
        self.socket = socket.socket()
        self.socket.bind(('127.0.0.1', 0))
        self.port = self.socket.getsockname()[1]
        self.socket.listen()
        self.socket.settimeout(0.2)
        self.stop = threading.Event()
        self.requests = []
        self.fault = fault
        self.thread = threading.Thread(target=self.serve, daemon=True)
        self.thread.start()

    def serve(self):
        while not self.stop.is_set():
            try:
                client, _ = self.socket.accept()
            except socket.timeout:
                continue
            except OSError:
                return
            with client:
                # Connection can be idle while PowerShell JIT/logging starts. Only the client I/O deadline is under test.
                client.settimeout(30)
                try:
                    while not self.stop.is_set():
                        header = exact(client, 7)
                        tid, protocol, length, unit = struct.unpack('>HHHB', header)
                        pdu = exact(client, length-1)
                        fc, offset, count = struct.unpack('>BHH', pdu)
                        self.requests.append((fc, offset, count))
                        if self.fault == 'timeout':
                            self.stop.wait(2)
                            continue
                        if self.fault == 'exception':
                            reply = bytes([fc | 0x80, 2])
                        elif fc in (1, 2):
                            reply = bytes([fc, (count+7)//8, 1])
                        elif fc in (3, 4):
                            # Independent literal oracle: float 12.5 in all four wire orders; signed -2.
                            registers = {0:0x4148,1:0,2:0,3:0x4148,4:0x4841,5:0,6:0,7:0x4841,8:65534}
                            reply = bytes([fc,count*2])+b''.join(struct.pack('>H',registers[x]) for x in range(offset,offset+count))
                        else:
                            raise AssertionError('Unexpected write in read-only fixture')
                        if self.fault == 'transaction':
                            tid = (tid+1) % 65536
                        data = struct.pack('>HHHB', tid,protocol,len(reply)+1,unit)+reply
                        # Fragment MBAP and PDU to exercise real TCP stream assembly.
                        for part in (data[:3],data[3:8],data[8:]):
                            if part:
                                client.sendall(part)
                except (EOFError,OSError):
                    pass

    def close(self):
        self.stop.set()
        self.socket.close()
        self.thread.join(timeout=3)


def fixture_config(port):
    return dict(schemaVersion=1,purpose='Virtual',sourceReference='Independent TCP test fixture, not field data',
                host='127.0.0.1',port=port,unitId=1,addressBase=0,intervalMs=300,timeoutMs=500,
                signals=[dict(enabled=True,name=f'Float{i}',label=order,direction='PLC->PC',area='HoldingRegister',
                              type='Float32',byteOrder=order,address=i*2) for i,order in enumerate(('ABCD','CDAB','BADC','DCBA'))]
                + [dict(enabled=True,name='Signed',label='signed',direction='PLC->PC',area='HoldingRegister',type='Int16',address=8),
                   dict(enabled=True,name='Input',label='input',direction='PLC->PC',area='DiscreteInput',type='Bool',address=0)])


def fixture_case(fault):
    server = ReferenceServer(fault)
    try:
        code, summary, folder = run_probe('wire-'+(fault or 'decode'), fixture_config(server.port), duration=1)
        assert summary, 'missing summary; inspect console.log'
        if fault:
            assert code == 1 and summary['status'] == 'Failed', summary
            assert any(r['direction']=='ERROR' and r['error'] for r in rows(folder,'wire.jsonl'))
            if fault=='exception':
                assert '0x02' in summary['error'], summary
        else:
            assert code == 0, summary
            values={r['data']['signal']:r['data']['value'] for r in rows(folder,'events.jsonl') if r['category']=='Signal'}
            assert all(values[f'Float{i}']==12.5 for i in range(4)), values
            assert values['Signed']==-2 and values['Input']==1, values
        assert all(fc in (1,2,3,4) for fc,_,_ in server.requests), server.requests
        return dict(requests=server.requests,summary=str(folder/'summary.json'))
    finally:
        server.close()


def admission():
    config=json.loads((TOOL/'virtual.example.json').read_text(encoding='utf-8'))
    config['heartbeat']['confirmed']=False
    code,summary,_=run_probe('write-not-confirmed',config,'-Heartbeat',duration=1)
    assert code==1 and summary is None, (code,summary)
    assert '心跳写入未确认' in (OUT/'write-not-confirmed/console.log').read_text(encoding='utf-8-sig')
    config['host']='192.0.2.1'
    code,summary,_=run_probe('virtual-not-loopback',config,duration=1)
    assert code==1 and summary is None, (code,summary)
    assert 'Virtual 测试点表仅允许连接本机回环地址' in (OUT/'virtual-not-loopback/console.log').read_text(encoding='utf-8-sig')
    return 'Both rejected before a TCP session was created.'


def free_port():
    with socket.socket() as s:
        s.bind(('127.0.0.1',0))
        return s.getsockname()[1]


def request_json(url):
    with urllib.request.urlopen(url,timeout=2) as response:
        return json.load(response)


def write_virtual_ready(port):
    # Test setup via actual protocol, not HTTP state forcing. Not part of the delivered probe.
    with socket.create_connection(('127.0.0.1',port),timeout=2) as s:
        pdu=struct.pack('>BHH',5,2,65280)
        request=struct.pack('>HHHB',46001,0,len(pdu)+1,1)+pdu
        s.sendall(request)
        assert exact(s,12)==request


def virtual_cases():
    plc_port,http_port=free_port(),free_port()
    log=(OUT/'virtual-plc.log').open('wb')
    process=subprocess.Popen(['dotnet',str(ROOT/'VirtualPlc/bin/Release/net10.0/VirtualPlc.dll'),
        '--contentRoot',str(ROOT/'VirtualPlc'),'--urls',f'http://127.0.0.1:{http_port}',
        '--Modbus:Port',str(plc_port),'--Dashboard:OpenBrowserOnStart','false'],
        cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,creationflags=FLAGS)
    base=f'http://127.0.0.1:{http_port}'
    try:
        deadline=time.monotonic()+30
        while time.monotonic()<deadline:
            try:
                request_json(base+'/health')
                break
            except (OSError,ValueError):
                if process.poll() is not None:
                    raise AssertionError('VirtualPlc exited; inspect virtual-plc.log')
                time.sleep(.2)
        else:
            raise AssertionError('VirtualPlc did not become ready')
        config=json.loads((TOOL/'virtual.example.json').read_text(encoding='utf-8'))
        config['port']=plc_port
        code,summary,folder=run_probe('virtual-readonly',config)
        assert code==0 and summary['statistics']['writeAttempts']==0 and summary['statistics']['heartbeatEdges']>=1, summary
        audit=request_json(base+'/api/simulator/audit')
        save(OUT/'readonly-audit.json',audit)
        assert not audit['writes'], audit
        code,summary,folder=run_probe('virtual-heartbeat',config,'-Heartbeat')
        assert code==0 and summary['statistics']['heartbeatReadbacks']>=2,summary
        audit=request_json(base+'/api/simulator/audit')
        save(OUT/'heartbeat-audit.json',audit)
        # Field names are checked from the server's returned audit, rather than assuming a UI value.
        assert audit['writes'], audit
        code,summary,folder=run_probe('virtual-motion-blocked',config,'-Heartbeat','-AllowMotion','-ActionName','X','-Target','12.5')
        assert code==1 and 'PLC_Ready_State' in summary['error'],summary
        sent=[bytes.fromhex(r['hex']) for r in rows(folder,'wire.jsonl') if r['direction']=='TX']
        assert not any(p[7]==16 or (p[7]==5 and p[8:10]!=b'\x00\x01') for p in sent)
        directory,command=probe('virtual-motion',config,'-Heartbeat','-AllowMotion','-ActionName','X','-Target','12.5',duration=9)
        test_process=subprocess.Popen(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=FLAGS)
        deadline=time.monotonic()+20
        ready_sent=False
        while time.monotonic()<deadline and test_process.poll() is None:
            event_files=list((directory/'runs').glob('*/events.jsonl'))
            if event_files and 'Readback' in event_files[0].read_text(encoding='utf-8-sig'):
                write_virtual_ready(plc_port)
                ready_sent=True
                break
            time.sleep(.03)
        code,summary,folder=collect(directory,test_process)
        assert ready_sent and code==0 and summary['statistics']['actionsCompleted']==1,summary
        categories=[r['category'] for r in rows(folder,'events.jsonl')]
        for name in ('ActionAccepted','TargetWritten','ActionStarted','ActionMoving','ActionCompleted'):
            assert name in categories,categories
        wire=rows(folder,'wire.jsonl')
        transmissions=[bytes.fromhex(r['hex']) for r in wire if r['direction']=='TX']
        assert sum(p[7]==16 for p in transmissions)==1
        assert [(struct.unpack('>HH',p[8:12])) for p in transmissions if p[7]==5 and p[8:10]==b'\x00\x21']==[(33,65280),(33,0)]
        audit=request_json(base+'/api/simulator/audit')
        save(OUT/'motion-audit.json',audit)
        return dict(transport='real TCP to separately built VirtualPlc',fieldHardwareTested=False,evidence=str(OUT))
    finally:
        process.terminate()
        process.wait(timeout=10)
        log.close()


def export_case():
    tool=OUT/'export-tool'
    tool.mkdir()
    shutil.copy2(TOOL/'Export.ps1',tool/'Export.ps1')
    shutil.copy2(TOOL/'Probe.ps1',tool/'Probe.ps1')
    shutil.copy2(TOOL/'site.template.json',tool/'site.json')
    shutil.copy2(TOOL/'field-notes.template.json',tool/'field-notes.json')
    # A real failed transport session must survive export unchanged.
    previous=sorted((ROOT/'artifacts/015-plc-field-probe').glob('*/wire-timeout/runs/*/summary.json'))
    assert previous,'timeout evidence missing'
    session=previous[-1].parent
    result=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(tool/'Export.ps1'),
                           '-SessionDirectory',str(session)],capture_output=True,creationflags=FLAGS,timeout=45)
    (tool/'export-console.log').write_bytes(result.stdout+result.stderr)
    assert result.returncode==0,result.stdout+result.stderr
    archive=list(tool.glob('PLC-return-*.zip'))[0]
    with zipfile.ZipFile(archive) as z:
        names=z.namelist()
        assert {'site.json','field-notes.json','return-manifest.json','Probe.ps1','Export.ps1'}<=set(names),names
        for name in ('summary.json','wire.jsonl','events.jsonl','config.snapshot.json'):
            assert z.read(session.name+'/'+name)==(session/name).read_bytes(),name
        manifest=json.loads(z.read('return-manifest.json'))
        assert manifest['sessions'] and all(f['sha256'] for f in manifest['files']),manifest
    return dict(archive=str(archive),failedSessionPreserved=True)


cases={'decode':('read-decode-and-fragmented-tcp',lambda:fixture_case(None)),
       'exception':('modbus-exception-evidence',lambda:fixture_case('exception')),
       'transaction':('wrong-transaction-rejected',lambda:fixture_case('transaction')),
       'timeout':('timeout-evidence',lambda:fixture_case('timeout')),
       'admission':('write-and-virtual-admission',admission),
       'virtual':('independent-virtual-plc-read-heartbeat-motion',virtual_cases),
       'export':('return-package-preserves-failed-evidence',export_case)}
for key in (sys.argv[1:] or cases):
    check(*cases[key])
print('EVIDENCE',OUT,flush=True)
raise SystemExit(0 if all(r['passed'] for r in RESULTS) else 1)
