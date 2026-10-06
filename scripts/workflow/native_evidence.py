"""Bind fixed component artifacts to the actual passing TRX case that wrote them.

This copies existing evidence, never reconstructs device or database facts. The
product tests assert their native content; the collector enforces provenance,
time, uniqueness and required artifact forms before admitting evidence kinds.
"""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import sqlite3


def prop(value, name, default=None):
    return next((v for k, v in value.items() if k.lower() == name.lower()), default)


def instant(value):
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


def index(root):
    records = {}
    for path in Path(root).glob('*.json'):
        try:
            value = json.loads(path.read_text(encoding='utf-8-sig'))
            case_id = value.get('caseId') if isinstance(value, dict) else None
            if case_id:
                records.setdefault(case_id, []).append((path, value))
        except (OSError, UnicodeError, ValueError):
            # A malformed required artifact is absent from the admitted index.
            # Its required case therefore fails; it is never counted as passed.
            continue
    return records


def collect(case, result, records, evidence, context):
    required = set(case.get('requiredEvidence', [])) - {'trx'}
    if not required:
        return []
    selected = records.get(case['caseId'], [])
    if result.get('outcome') != 'Passed' or len(selected) != 1:
        return []
    path, value = selected[0]
    try:
        started, ended = instant(result['startTime']), instant(result['endTime'])
        written = datetime.fromtimestamp(path.stat().st_mtime, timezone.utc)
        if started < instant(context['startedAt']) or not started <= written <= ended:
            return []
    except (OSError, KeyError, ValueError):
        return []
    kinds = set()
    cid = case['caseId']
    if cid.startswith('PD-'):
        if (value.get('entry') == 'LatestProtocolPlcDevice.StartAsync -> same instance RequestStartAsync'
                and value.get('validator') == 'PlcDefinitionAdmission.Prepare/RequireAdmitted'
                and value.get('startAt') and value.get('requestAt') and value.get('fixtureDigest')
                and value.get('oracleDigest') and value.get('gap') is False
                and set(value.get('dispatchScope', [])) == {'business', 'heartbeat'}
                and isinstance(value.get('writes'), list) and value.get('count') == len(value['writes'])):
            kinds.update(('formal-entry', 'both-channel-dispatch'))
    elif cid.startswith(('BA01-', 'BA04-', 'BA06-', 'BA07-')):
        if prop(value, 'scope') and any(prop(value, k) is not None for k in
                ('receipt', 'request', 'requests', 'snapshot', 'applicationWindow', 'originalDigest')):
            kinds.add('budget-window')
        if (cid == 'BA07-config/nonfinite-internal' and
                prop(value, 'scope') == 'ProductionModelTypeBoundaryAndSemanticValidator;NoTcpClaim' and
                prop(value, 'memberType') == 'System.Nullable<System.Int32>' and
                prop(value, 'rejected') == ['NaN', 'Infinity', '-Infinity'] and prop(value, 'legal') == 10000):
            # This fixed BA07 row proves model/configuration admission, before a
            # window may exist. Do not invent a request/window for rejected input.
            kinds.add('budget-window')
        if cid in ('BA04-boundary/late-bound', 'BA04-boundary/late-handoff'):
            actual = prop(value, 'actual', {})
            if (prop(actual, 'Batch') and prop(actual, 'Receipt') and prop(value, 'reconciled')
                    and prop(value, 'denied') and prop(value, 'after')
                    and Path(prop(value, 'actualDatabaseEvidence', '')).is_file()):
                kinds.add('sqlite-commit-receipt')
    elif cid.startswith('DIAG-COMPONENT/'):
        if value.get('componentOnly') is True and value.get('independentProcess') is False:
            if value.get('actualWire') and value.get('documents') and value.get('events'):
                kinds.add('tcp')
    database = prop(value, 'evidenceStorePath')
    store = prop(value, 'storeRoot')
    if database is None and store:
        database = str(Path(store)/'station01.test.db')
    if database and Path(database).is_file():
        kinds.add('sqlite')
    if not required.issubset(kinds):
        return []
    target_dir = Path(evidence)/'native-component'
    target_dir.mkdir(exist_ok=True)
    data = path.read_bytes()
    target = target_dir/path.name
    with target.open('xb') as output:
        output.write(data)
    files = [dict(path=str(target.relative_to(evidence)), sha256=hashlib.sha256(data).hexdigest(), kind=k)
             for k in sorted(required)]
    if required.intersection({'sqlite', 'sqlite-commit-receipt'}):
        if not database:
            return []
        # Preserve a consistent, read-only backup of the actual test database.
        # No schema or source-store mutation occurs and no fact is synthesized.
        snapshot = target.with_suffix('.db')
        with snapshot.open('xb'):
            pass
        with sqlite3.connect(Path(database).resolve().as_uri()+'?mode=ro', uri=True) as source_db:
            with sqlite3.connect(snapshot) as destination_db:
                source_db.backup(destination_db)
        files.append(dict(path=str(snapshot.relative_to(evidence)),
                          sha256=hashlib.sha256(snapshot.read_bytes()).hexdigest(), kind='sqlite'))
    attachment = dict(context, caseId=cid, files=files, originalArtifact=str(path),
                      actualTestInterval=dict(start=result['startTime'], end=result['endTime']),
                      scope='ComponentOnly;NotIndependentHostPlcAcceptance')
    with (Path(evidence)/(cid.replace('/', '--')+'.evidence.json')).open('x', encoding='utf-8') as output:
        json.dump(attachment, output, indent=2)
    return sorted(required)
