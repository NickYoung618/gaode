"""Communication counterpart of the persisted operation-route business checks.

Independent oracle plus actual SQLite raw batches, VirtualPlc write receipts and
changes. This script cannot declare a business result or grant action permission.
"""
import argparse
import hashlib
import json
import sqlite3
import struct
from pathlib import Path


def prop(value, name, default=None):
    return next((v for k, v in value.items() if k.lower() == name.lower()), default) if isinstance(value, dict) else default


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def require(ok, reason):
    if not ok: raise ValueError(reason)


def sorting_packets(batch, oracle):
    signals = {s['id']: s for s in oracle['signals']}
    def value(name, meaning):
        matches = [int(k) for k, v in signals[name]['values'].items() if v == meaning]
        require(len(matches) == 1, 'IndependentValueMissing:'+name+'/'+meaning)
        return matches[0]
    events = []
    for exchange in prop(batch, 'exchanges', []):
        if not prop(exchange, 'requestHex') or not prop(exchange, 'responseHex'): continue
        request, response = (bytes.fromhex(prop(exchange, field)) for field in ('requestHex', 'responseHex'))
        require(len(request) >= 12 and len(response) >= 9, 'TruncatedPacket')
        require(request[:4] == response[:4] and request[6:8] == response[6:8] and
                int.from_bytes(request[4:6], 'big') == len(request)-6 and
                int.from_bytes(response[4:6], 'big') == len(response)-6, 'PacketIdentityOrLength')
        fn, offset, field_value = request[7], int.from_bytes(request[8:10], 'big'), int.from_bytes(request[10:12], 'big')
        for name in ['Sorting_Cmd', 'Sorting_Exec_Status', 'Sorting_OK']:
            at = signals[name]['pduOffset']
            if fn == 3 and offset <= at < offset + field_value:
                require(response[8] == field_value*2 and len(response) == 9+field_value*2, 'ReadPayloadLength')
                result = struct.unpack_from('>H', response, 9+(at-offset)*2)[0]
                events.append(('read', name, result))
            if fn in (6, 16) and offset == at:
                require(response[8:12] == request[8:12], 'WriteReceiptMismatch')
                if fn == 16:
                    require(field_value == 1 and request[12] == 2 and len(request) == 15, 'SortFieldWriteWidth')
                events.append(('write', name, field_value if fn == 6 else int.from_bytes(request[13:15], 'big')))
    expected = [('write','Sorting_Cmd',value('Sorting_Cmd','Pick')),
                ('read','Sorting_Exec_Status',value('Sorting_Exec_Status','Executing')),
                ('read','Sorting_Exec_Status',value('Sorting_Exec_Status','Picked')),
                ('write','Sorting_Cmd',value('Sorting_Cmd','Idle')),
                ('write','Sorting_Cmd',value('Sorting_Cmd','Place')),
                ('read','Sorting_Exec_Status',value('Sorting_Exec_Status','Executing')),
                ('read','Sorting_Exec_Status',value('Sorting_Exec_Status','Placed')),
                ('write','Sorting_Cmd',value('Sorting_Cmd','Idle')),
                ('write','Sorting_OK',value('Sorting_OK','Acknowledged')),
                ('read','Sorting_Exec_Status',value('Sorting_Exec_Status','Idle')),
                ('write','Sorting_OK',value('Sorting_OK','Idle'))]
    cursor = 0
    for event in events:
        if cursor < len(expected) and event == expected[cursor]: cursor += 1
    require(cursor == len(expected), 'SortingWireSequenceIncomplete:'+str(cursor))
    return len(events)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence_root', type=Path)
    parser.add_argument('fixture', type=Path)
    args = parser.parse_args()
    root, fixture = args.evidence_root.resolve(), load(args.fixture)
    oracle_path = Path(__file__).resolve().parents[2]/'backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json'
    oracle = load(oracle_path)
    facts = load(root/'page-api-device-facts.json')
    run_id = facts['run']['runId'].lower()
    recipe = next(r for r in load(Path(fixture['recipeCatalogPath']))['recipes'] if r['recipeId'] == fixture['recipeRef']['recipeId'])
    positions = [p for p in recipe['positions'] if p['slotId'] in fixture['occupiedSlots']]
    stages = recipe['execution']['stages']
    audit = facts.get('plcWriteAudit') or {}
    actions, writes = prop(audit, 'actions', []), prop(audit, 'writes', [])
    moves = [a for a in actions if prop(a,'kind') == 'Move' and prop(a,'phase') == 'completed']
    checks = {
        'complete_device_xyz_and_axis': bool(moves) and prop(audit,'actionGap') is False and all(
            prop(a,'axisRole') == ('ScanZ' if prop(a,'command') == 5 else 'GrabZ' if prop(a,'command') in (3,4) else 'DetectionZ') and
            all(prop(prop(a,'target'),axis) is not None and prop(prop(a,'target'),axis) == prop(prop(a,'actual'),axis)
                for axis in ('x','y','z')) for a in moves),
        'actual_modbus_write_receipts': bool(writes) and prop(audit,'gap') is False and all(
            prop(w,'requestHex') and prop(w,'responseHex') and prop(w,'connectionId') and
            prop(w,'transactionId') is not None and prop(w,'rawWords') is not None
            for w in writes if prop(w,'accepted')),
    }
    # Read real captured bytes; never reconstruct old protocol samples from the
    # newly semantic StageEvents. Raw rows stay in the communication boundary.
    store=Path(load(root/'storage-location.json')['testRoot']) if (root/'storage-location.json').is_file() else root
    with sqlite3.connect((store/'station01.test.db').resolve().as_uri()+'?mode=ro', uri=True) as db:
        batches=[]
        for payload,digest in db.execute('SELECT RawPayloadJson,PayloadDigest FROM PlcCommunicationEvidence WHERE lower(RunId)=?',(run_id,)):
            require(hashlib.sha256(payload.encode()).hexdigest() == digest.lower(), 'RawPayloadDigestMismatch')
            batch=json.loads(payload)
            if prop(batch,'interpretation') == 'MaterialTransferred' and not prop(batch,'httpExchanges'):
                require(prop(batch,'gap') is False, 'TransferCaptureGap')
                sorting_packets(batch, oracle)
                batches.append(batch)
    places=[a for a in actions if prop(a,'kind')=='Sort' and prop(a,'command')==2 and prop(a,'phase')=='completed']
    checks['actual_sorting_packet_sequence'] = len(batches)==len(places)
    if fixture['caseId']=='Q02-PENDING-P03':
        slot = next(s for s in oracle['signals'] if s['id']=='Sorting_Part_Index')
        slot_writes=[w for w in writes if prop(w,'accepted') and prop(w,'documentNumber')==int(slot['documentHex'],16) and prop(w,'rawWords')!=[0]]
        checks['actual_source_slot_three_not_action_sequence_one']=len(slot_writes)==len(places)==1 and all(
            prop(w,'rawWords')==[3] and prop(w,'pduOffset')==slot['pduOffset'] and
            prop(w,'responseHex')==prop(w,'requestHex') and prop(w,'sequence') in prop(places[0],'writeSequenceRefs',[]) for w in slot_writes)
    if not recipe['execution']['route'].startswith('specialType1'):
        manual=any(p.get('resolvedFlipPosition',{}).get('mode')=='manual' for p in positions)
        flips=sum(len(s['targets'])*len(positions) for s in stages[1:]) if recipe['unitKind']!='assembledEntity' else (len(stages)-1)*len(positions)
        batch=facts['plcChanges']; changes=batch['changes']
        checks['complete_plc_change_export']=batch['gap'] is False and len(changes)==batch['latestSequence']-batch['oldestSequence']+1
        ack='Manual_Flip_Complete' if manual else 'Flip_OK'
        checks['actual_flip_ack_set_and_clear']=all(sum(c.get('name')==ack and str(c.get('current'))==value for c in changes)==flips for value in ('1','0'))
    else:
        auxiliary=facts.get('specialActions',[])
        exits=[a for a in auxiliary if prop(prop(a,'request',{}),'kind')=='Exit']
        checks['special_http_actions_completed']=len(auxiliary)==len(positions)*(2+len(stages)) and all(
            prop(a,'status')=='Completed' for a in auxiliary) and len(exits)==len(positions) and all(
            not prop(a,'occupiedEntityId') for a in exits)
    result=dict(scope='CommunicationOnly',runId=run_id,checks=checks,passed=all(checks.values()),
                oracleSha256=hashlib.sha256(oracle_path.read_bytes()).hexdigest())
    with (root/'operation-wire-validation.json').open('x',encoding='utf-8') as output: json.dump(result,output,ensure_ascii=False,indent=2)
    print(json.dumps(result,ensure_ascii=True))
    return 0 if result['passed'] else 1


if __name__=='__main__': raise SystemExit(main())
