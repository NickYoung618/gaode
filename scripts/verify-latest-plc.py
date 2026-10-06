"""Five-scenario first-station E2E driver using only Host APIs for workflow control."""
import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import socket
import sys
import sqlite3
import subprocess
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
OPERATOR_TOKEN = 'station01-explicit-local-test-token'
ADMIN_TOKEN = 'station01-system-administrator-token'
SCENARIOS = ('normal', 'algorithm-pending', 'unlock-gates', 'plc-unknown', 'host-restart')
STATE_BLOCKED = 20
STATE_RECOVERY_REQUIRED = 22
STATE_AWAITING_MANUAL = 16
STATE_COMPLETED = 26


def free_port():
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        return listener.getsockname()[1]


def json_value(value):
    return value.hex() if isinstance(value, bytes) else value


def instant(value):
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


class ScenarioRun:
    def __init__(self, scenario, source_store, evidence_root):
        self.scenario = scenario
        self.folder = evidence_root / 'whole-tray' / scenario
        self.store = self.folder / 'store'
        self.store.mkdir(parents=True)
        with sqlite3.connect(source_store / 'station01.test.db') as source:
            if source.execute('select count(*) from Runs').fetchone()[0] != 0:
                raise ValueError('The supplied store is not empty')
            with sqlite3.connect(self.store / 'station01.test.db') as target:
                source.backup(target)
        (self.store / 'media-root').mkdir()
        self.host_port, self.plc_http_port, self.modbus_port = free_port(), free_port(), free_port()
        self.processes, self.logs, self.transcript, self.samples = [], [], [], []
        self.run_id = None
        self.tray_id = str(uuid.uuid4())
        self.worker_manifest = self.folder / 'worker-input.json'
        self.worker_script = ROOT / ('backend/tests/Gaode.Integration.Tests/Fixtures/detection-late-worker.py'
                                    if scenario == 'algorithm-pending' else 'scripts/virtual-station01-algorithm.py')
        config = json.loads((ROOT / 'specs/007-station01-integrated-loop/examples/virtual-algorithm.json').read_text(encoding='utf-8-sig'))
        config['fCode'] = 'TEST-TRAY-0999'
        if scenario == 'unlock-gates': config['detectionDisposition'] = ['UNMAPPED']
        self.worker_manifest.write_text(json.dumps(config), encoding='utf-8')
        self.evidence = None
        self.plc_audit = None
        self.result = {'scenario': scenario, 'status': 'Failed',
                       'softwareScope': 'Virtual/Simulated/Test'}

    def launch(self, name, command, env=None):
        log_path = self.folder / f'{name}.log'
        log = log_path.open('w', encoding='utf-8')
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=log, stderr=log,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        self.logs.append(log)
        self.processes.append({'name': name, 'process': process, 'command': command,
                               'pid': process.pid, 'log': log_path.name})
        return process

    def api(self, port, path, body=None, token=OPERATOR_TOKEN, expected=(200, 202)):
        headers = {'Authorization': 'Bearer ' + token}
        if body is not None:
            headers['Content-Type'] = 'application/json'
        data = json.dumps(body).encode() if body is not None else None
        web_request = urllib.request.Request(f'http://127.0.0.1:{port}' + path,
                                             data=data, headers=headers)
        try:
            with urllib.request.urlopen(web_request, timeout=20) as response:
                raw = response.read().decode()
                value, status = json.loads(raw) if raw else None, response.status
        except urllib.error.HTTPError as error:
            raw = error.read().decode()
            try:
                value = json.loads(raw)
            except json.JSONDecodeError:
                value = raw
            status = error.code
        self.transcript.append({'method': 'POST' if body is not None else 'GET', 'path': path,
                                'request': body, 'status': status, 'response': value,
                                'observedAtUtc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())})
        if status not in expected:
            raise AssertionError(f'{path}: expected {expected}, got {status}: {value}')
        return status, value

    @staticmethod
    def raw_get(port, path):
        with urllib.request.urlopen(f'http://127.0.0.1:{port}' + path, timeout=10) as response:
            return json.load(response)

    def wait_http(self, port, path, seconds=20, token=None):
        deadline, last = time.monotonic() + seconds, None
        while time.monotonic() < deadline:
            try:
                if token is None:
                    return self.raw_get(port, path)
                web_request = urllib.request.Request(f'http://127.0.0.1:{port}' + path,
                                                     headers={'Authorization': 'Bearer ' + token})
                with urllib.request.urlopen(web_request, timeout=10) as response:
                    return json.load(response)
            except (OSError, urllib.error.URLError) as error:
                last = error
                time.sleep(.05)
        raise TimeoutError(f'HTTP readiness failed for {port}{path}: {last}')

    def start_plc(self):
        return self.launch('plc', [
            'dotnet', str(ROOT / 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll'),
            '--contentRoot', str(ROOT / 'VirtualPlc'),
            '--urls', f'http://127.0.0.1:{self.plc_http_port}',
            f'--Modbus:Port={self.modbus_port}', '--Dashboard:OpenBrowserOnStart=false',
            '--Simulation:ScanPeriodMs=3', '--Simulation:HeartbeatPeriodMs=100',
            '--Simulation:MotionDurationMs=80', '--Simulation:ZResetDurationMs=50',
            '--Simulation:FlipDurationMs=60', '--Simulation:SortingDurationMs=60',
            '--Simulation:PalletLockDurationMs=80', '--Simulation:ZoneConfigDurationMs=40',
            '--Simulation:ActionDurationJitterMs=0', '--Simulation:InterActionGapMs=10'])

    def host_command(self):
        return [
            'dotnet', str(ROOT / 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll'),
            '--contentRoot', str(ROOT / 'backend/src/Gaode.Host'),
            '--urls', f'http://127.0.0.1:{self.host_port}',
            '--Gaode:Mode=VirtualPlcIntegration', '--Gaode:TestRoot=' + str(self.store),
            '--Gaode:AllowedTestRoot=' + str(self.folder),
            '--Gaode:ConfigRoot=' + str(ROOT / 'specs/007-station01-integrated-loop/examples'),
            '--Gaode:SchemaRoot=' + str(ROOT / 'specs/001-station01-public-preparation/contracts'),
            '--Gaode:PublicId=s01-public-virtual-loop', '--Gaode:PublicVersion=1.2.0',
            '--Gaode:BudgetId=s01-budget-virtual-loop', '--Gaode:BudgetVersion=3.0.0',
            '--Gaode:SimulationId=s01-sim-virtual-loop', '--Gaode:SimulationVersion=3.0.0', '--Gaode:PlcHost=127.0.0.1',
            f'--Gaode:PlcPort={self.modbus_port}', '--Gaode:PlcProvider=Virtual',
            '--Gaode:PlcIoTimeoutMs=1000',
            '--Gaode:WorkerExecutablePath=' + sys.executable,
            '--Gaode:WorkerScriptPath=' + str(self.worker_script),
            '--Gaode:WorkerManifestPath=' + str(self.worker_manifest),
            '--Gaode:ImageManifestPath=' + str(ROOT / 'specs/008-recipe-driven-inspection/fixtures/media-manifest.json'),
            '--Recipes:CatalogPath=' + str(ROOT / 'specs/008-recipe-driven-inspection/fixtures/recipes-singleface-gates.json'),
            '--Logging:LogLevel:Default=Information']

    def start_host(self, name='host'):
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT='VirtualPlc',
                   Gaode__Tokens__Operator=OPERATOR_TOKEN,
                   Gaode__Tokens__SystemAdministrator=ADMIN_TOKEN)
        return self.launch(name, self.host_command(), env)

    def start_run(self):
        context = json.dumps({
            'schemaVersion': 'station01-start-run-context/1.0', 'trayId': self.tray_id,
            'stationId': '10000000-0000-0000-0000-000000000001',
            'lineId': '20000000-0000-0000-0000-000000000001', 'scenarioId': 'S1',
            'occupiedSlots': ['P01'], 'purpose': 'Test'
        }, separators=(',', ':'))
        _, receipt = self.api(self.host_port, '/api/v1/station01/runs', {
            'requestId': uuid.uuid4().hex, 'contextJson': context,
            'publicConfigRef': {'id': 's01-public-virtual-loop', 'version': '1.2.0'},
            'budgetRef': {'id': 's01-budget-virtual-loop', 'version': '3.0.0'},
            'simulationRef': {'id': 's01-sim-virtual-loop', 'version': '3.0.0'}})
        self.run_id = receipt['runId']
        return receipt

    def run_snapshot(self):
        _, snapshot = self.api(self.host_port, f'/api/v1/station01/runs/{self.run_id}')
        try:
            simulator = self.raw_get(self.plc_http_port, '/api/simulator/state')
        except Exception:
            simulator = {'unavailable': True}
        self.samples.append({'run': snapshot, 'virtualPlc': simulator})
        return snapshot

    def wait_run(self, predicate, seconds=180):
        deadline, last = time.monotonic() + seconds, None
        while time.monotonic() < deadline:
            last = self.run_snapshot()
            if predicate(last):
                return last
            time.sleep(.05)
        raise TimeoutError(f'Run did not reach expected state: {last}')

    def evidence_api(self, allow_missing=False):
        expected = (200, 404) if allow_missing else (200,)
        status, value = self.api(self.host_port,
                                 f'/api/v1/station01/runs/{self.run_id}/evidence',
                                 expected=expected)
        if status == 404:
            return None
        self.evidence = value
        return self.evidence

    def confirm_manual(self, snapshot, request_id='manual-e2e-1'):
        return self.api(self.host_port,
                        f'/api/v1/station01/runs/{self.run_id}/manual-removal-confirmations', {
                            'requestId': request_id,
                            'expectedRevision': snapshot['observedRevision'],
                            'reason': '联调客户端确认操作员已移除托盘'})

    def wait_stage_started(self, stage, seconds=180):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            evidence = self.evidence_api(allow_missing=True)
            if evidence is None:
                time.sleep(.02)
                continue
            started = any(item['stage'] == stage and item['eventType'] == 'Started'
                          for item in evidence['stages'])
            state = self.raw_get(self.plc_http_port, '/api/simulator/state')
            if started and state.get('activeAction'):
                self.plc_audit = self.raw_get(self.plc_http_port, '/api/simulator/audit')
                return evidence, state
            time.sleep(.02)
        raise TimeoutError(f'Did not observe dispatched {stage} action')

    def execute(self):
        plc = self.start_plc()
        self.wait_http(self.plc_http_port, '/health')
        host = self.start_host()
        self.wait_http(self.host_port, '/api/v1/station01/status', token=OPERATOR_TOKEN)
        self.start_run()

        if self.scenario == 'unlock-gates':
            early = self.run_snapshot()
            status, _ = self.api(self.host_port,
                f'/api/v1/station01/runs/{self.run_id}/manual-removal-confirmations', {
                    'requestId': 'manual-before-unlock',
                    'expectedRevision': early['observedRevision'],
                    'reason': '门禁验证：尚未观察到解锁'}, expected=(409,))
            assert status == 409
            final = self.wait_run(lambda value: value['state'] in
                                  (STATE_BLOCKED, STATE_RECOVERY_REQUIRED))
            evidence = self.evidence_api()
            self.plc_audit = self.raw_get(self.plc_http_port, '/api/simulator/audit')
            writes = self.plc_audit['writes']
            assert final['state'] == STATE_BLOCKED, final
            assert evidence['wholeTrayCompletionId'] is None, evidence
            assert not any(w['area'] == 1 and w['documentNumber'] == 35 and w['value'] == 0
                           for w in writes), writes
            self.result.update(status='Passed', reason='Missing algorithm quality blocked aggregation; manual confirmation returned 409')
            return

        if self.scenario in ('plc-unknown', 'host-restart'):
            self.wait_stage_started('UnloadPreparation')
            if self.scenario == 'plc-unknown':
                plc.terminate()
                plc.wait(timeout=10)
                final = self.wait_run(lambda value: value['state'] in
                                      (STATE_RECOVERY_REQUIRED, STATE_BLOCKED), 20)
                evidence = self.evidence_api()
                unknown = [item for item in evidence['stages'] if item['eventType'] == 'UnknownHeld']
                assert final['state'] == STATE_RECOVERY_REQUIRED and unknown, (final, evidence)
                unload_dispatches = [w for w in self.plc_audit['writes'] if
                                     w['area'] == 1 and w['documentNumber'] == 1 and
                                     w['value'] == 4 and w['accepted']]
                assert len(unload_dispatches) == 1, unload_dispatches
                self.result.update(status='Passed', reason='Post-dispatch PLC disconnect entered UnknownHeld without resend')
                return
            host.terminate()
            host.wait(timeout=10)
            self.start_host('host-restart')
            self.wait_http(self.host_port, '/api/v1/station01/status', token=OPERATOR_TOKEN)
            self.wait_run(lambda value: value['state'] == STATE_RECOVERY_REQUIRED, 20)
            evidence = self.evidence_api()
            unknown = [item for item in evidence['stages'] if item['eventType'] == 'UnknownHeld']
            assert unknown and not any(item['eventType'] == 'FinalUnloadCompleted'
                                       for item in evidence['stages']), evidence
            self.plc_audit = self.raw_get(self.plc_http_port, '/api/simulator/audit')
            unload_dispatches = [w for w in self.plc_audit['writes'] if
                                 w['area'] == 1 and w['documentNumber'] == 1 and
                                 w['value'] == 4 and w['accepted']]
            assert len(unload_dispatches) == 1, unload_dispatches
            unload_events = [item for item in evidence['stages'] if
                             item['stage'] == 'UnloadPreparation' and
                             item['eventType'] in ('Started', 'UnknownHeld')]
            assert len(unload_events) >= 2
            assert len({item['stageDeadlineAtUtc'] for item in unload_events}) == 1, unload_events
            self.result.update(status='Passed', reason='Restart rebuilt committed facts and held in-flight PLC action')
            return

        awaiting = self.wait_run(lambda value: value['state'] in
                                 (STATE_AWAITING_MANUAL, STATE_BLOCKED, STATE_RECOVERY_REQUIRED))
        assert awaiting['state'] == STATE_AWAITING_MANUAL, awaiting
        self.confirm_manual(awaiting)
        self.wait_run(lambda value: value['state'] == STATE_COMPLETED, 15)
        evidence = self.evidence_api()
        self.plc_audit = self.raw_get(self.plc_http_port, '/api/simulator/audit')
        assert evidence['finalResult'] == 'FinalUnloadCompletion', evidence
        assert evidence['finalSourceMatrix']['scope'] == 'SoftwareLoopOnly', evidence
        writes = self.plc_audit['writes']
        assert not any(w['area'] == 1 and w['documentNumber'] == 35 and w['value'] == 1
                       for w in writes), writes
        assert any(w['area'] == 1 and w['documentNumber'] == 35 and w['value'] == 0
                   for w in writes), writes
        whole_event = next(item for item in evidence['stages'] if
                           item['eventType'] == 'WholeTrayCompleted')
        unlock_write = next(w for w in writes if w['area'] == 1 and
                            w['documentNumber'] == 35 and w['value'] == 0)
        assert instant(unlock_write['occurredAtUtc']) >= instant(whole_event['persistedAtUtc'])
        if self.scenario == 'algorithm-pending':
            detection = [item for item in evidence['stages'] if item['stage'] == 'Detection']
            failures = [item for item in detection if item['eventType'] == 'AttemptFailed']
            retries = [item for item in detection if item['eventType'] == 'RetryScheduled']
            assert len(failures) == 3
            assert len(retries) == 2
            assert any(item['eventType'] == 'PendingRecorded' for item in detection)
            assert len({item['stageDeadlineAtUtc'] for item in detection
                        if item['stageDeadlineAtUtc']}) == 1, detection
            delay_one = (instant(retries[0]['persistedAtUtc']) -
                         instant(failures[0]['persistedAtUtc'])).total_seconds()
            delay_two = (instant(retries[1]['persistedAtUtc']) -
                         instant(failures[1]['persistedAtUtc'])).total_seconds()
            assert delay_one >= 1.8 and delay_two >= 4.8, (delay_one, delay_two)
        self.result.update(status='Passed', reason='Same run/tray reached authenticated FinalUnloadCompletion')

    @staticmethod
    def export_table(database, table):
        with sqlite3.connect(database) as connection:
            connection.row_factory = sqlite3.Row
            return [{key: json_value(row[key]) for key in row.keys()}
                    for row in connection.execute(f'SELECT * FROM "{table}"')]

    def finish(self):
        if self.plc_audit is None:
            try:
                self.plc_audit = self.raw_get(self.plc_http_port, '/api/simulator/audit')
            except Exception:
                self.plc_audit = {'schemaVersion': 'virtual-plc-write-audit/1.0',
                                  'source': 'Virtual', 'writes': [],
                                  'capture': 'UnavailableAfterDisconnect'}
        for entry in reversed(self.processes):
            process = entry['process']
            if process.poll() is None:
                if os.name == 'nt':
                    subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], check=False,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
                else: process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
            entry['exitCode'] = process.returncode
        for log in self.logs:
            log.close()
        process_json = [{key: value for key, value in entry.items() if key != 'process'}
                        for entry in self.processes]
        (self.folder / 'process.json').write_text(json.dumps(process_json, ensure_ascii=False, indent=2), encoding='utf-8')
        (self.folder / 'api-transcript.json').write_text(json.dumps(self.transcript, ensure_ascii=False, indent=2), encoding='utf-8')
        (self.folder / 'run-snapshot.json').write_text(json.dumps(self.samples[-1] if self.samples else {}, ensure_ascii=False, indent=2), encoding='utf-8')
        (self.folder / 'modbus-audit.json').write_text(json.dumps(self.plc_audit, ensure_ascii=False, indent=2), encoding='utf-8')
        database = self.store / 'station01.test.db'
        tables = ('StageEvents', 'StageProjections', 'ComponentEvidenceMatrices',
                  'WholeTrayCompletions', 'ControlledRecoveryDecisions', 'Runs')
        exported = {table: self.export_table(database, table) for table in tables}
        (self.folder / 'sqlite-events.json').write_text(json.dumps(exported['StageEvents'], ensure_ascii=False, indent=2), encoding='utf-8')
        (self.folder / 'sqlite-projections.json').write_text(json.dumps(exported['StageProjections'], ensure_ascii=False, indent=2), encoding='utf-8')
        source_matrix = self.evidence or {'matrices': exported['ComponentEvidenceMatrices']}
        (self.folder / 'source-matrix.json').write_text(json.dumps(source_matrix, ensure_ascii=False, indent=2), encoding='utf-8')
        (self.folder / 'whole-tray-completion.json').write_text(json.dumps(exported['WholeTrayCompletions'], ensure_ascii=False, indent=2), encoding='utf-8')
        unlock = [row for row in exported['StageEvents'] if row['EventType'] == 'ObservedUnlocked']
        manual = [row for row in exported['StageEvents'] if row['EventType'] in
                  ('ManualTrayRemovalConfirmed', 'FinalUnloadCompleted')]
        (self.folder / 'unlock-evidence.json').write_text(json.dumps(unlock, ensure_ascii=False, indent=2), encoding='utf-8')
        (self.folder / 'manual-final.json').write_text(json.dumps(manual, ensure_ascii=False, indent=2), encoding='utf-8')
        self.result.update(runId=self.run_id, trayId=self.tray_id,
                           evidenceFiles=['process.json', 'api-transcript.json', 'modbus-audit.json',
                                          'sqlite-events.json', 'sqlite-projections.json',
                                          'source-matrix.json', 'whole-tray-completion.json',
                                          'unlock-evidence.json', 'manual-final.json',
                                          'run-snapshot.json', 'store/station01.test.db'])
        (self.folder / 'final-result.json').write_text(json.dumps(self.result, ensure_ascii=False, indent=2), encoding='utf-8')
        hashes = {}
        for path in sorted(self.folder.rglob('*')):
            if path.is_file() and path.name != 'manifest.json':
                hashes[str(path.relative_to(self.folder))] = hashlib.sha256(path.read_bytes()).hexdigest()
        manifest = {'schemaVersion': 'station01-e2e-evidence/1.0', 'scenario': self.scenario,
                    'runId': self.run_id, 'trayId': self.tray_id,
                    'ports': {'host': self.host_port, 'virtualPlcHttp': self.plc_http_port,
                              'modbusTcp': self.modbus_port},
                    'sources': None if self.evidence is None else self.evidence.get('finalSourceMatrix') or self.evidence.get('readyForUnlockSourceMatrix'),
                    'scope': 'StandaloneDebug_Not010Acceptance',
                    'productionAcceptance': False, 'files': hashes}
        (self.folder / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--empty-store', required=True)
    parser.add_argument('--scenario', choices=(*SCENARIOS, 'all'), default='all')
    parser.add_argument('--evidence-run-id')
    args = parser.parse_args()
    source = Path(args.empty_store).resolve()
    if not source.is_relative_to(ROOT) or not (source / 'station01.test.db').is_file():
        raise ValueError('Empty store must be an existing directory inside this project')
    evidence_id = args.evidence_run_id or uuid.uuid4().hex
    evidence_root = ROOT / 'artifacts' / 'plc-latest' / evidence_id
    evidence_root.mkdir(parents=True, exist_ok=False)
    selected = SCENARIOS if args.scenario == 'all' else (args.scenario,)
    summary = {'evidenceRunId': evidence_id, 'root': str(evidence_root), 'scenarios': {}}
    failed = False
    for scenario in selected:
        run = ScenarioRun(scenario, source, evidence_root)
        try:
            run.execute()
        except Exception as error:
            failed = True
            run.result.update(status='Failed', reason=f'{type(error).__name__}: {error}')
        finally:
            run.finish()
        summary['scenarios'][scenario] = run.result
        print(json.dumps(run.result, ensure_ascii=False))
    summary['passed'] = not failed and all(value['status'] == 'Passed'
                                           for value in summary['scenarios'].values())
    (evidence_root / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'passed': summary['passed'], 'root': str(evidence_root)}, ensure_ascii=False))
    raise SystemExit(0 if summary['passed'] else 1)


if __name__ == '__main__':
    main()
