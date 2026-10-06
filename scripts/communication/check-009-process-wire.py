"""Independent raw/PLC audit checks for 009 process evidence; no business decisions."""
import argparse
from datetime import datetime
import hashlib
import importlib.util
import json
from pathlib import Path
import sqlite3

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('wire009',HERE/'check-009-wire-evidence.py')
wire=importlib.util.module_from_spec(spec);spec.loader.exec_module(wire)
spec=importlib.util.spec_from_file_location('operation_wire009',HERE/'check-009-operation-wire.py')
operation=importlib.util.module_from_spec(spec);spec.loader.exec_module(operation)


def load(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def prop(value,name,default=None):return next((v for k,v in value.items() if k.lower()==name.lower()),default)
def utc(value):return datetime.fromisoformat(value.replace('Z','+00:00'))


def special_http(batch,observed):
    exchanges=prop(batch,'httpExchanges',[])
    posts=[e for e in exchanges if e['method']=='POST']
    reads=[e for e in exchanges if e['method']=='GET']
    if prop(batch,'gap') or len(posts)!=1 or not reads:raise ValueError('SpecialHttpCaptureIncomplete')
    if any(e['error'] or not e['responseBodyHex'] or not 200<=e['statusCode']<300 for e in exchanges):
        raise ValueError('SpecialHttpResponseUnsuccessful')
    request=json.loads(bytes.fromhex(posts[0]['requestBodyHex']))
    terminal=json.loads(bytes.fromhex(reads[-1]['responseBodyHex']))
    if request!=observed['request'] or terminal!=observed:raise ValueError('SpecialHttpDiffersFromIndependentProcess')
    if any(request[k]!=batch[k] for k in ('runId','operationId','actionId')):raise ValueError('SpecialHttpActionMismatch')
    if not all(e['endpoint'].endswith('/'+request['actionId']) for e in reads):raise ValueError('SpecialHttpPollIdentityMismatch')
    if terminal['status']!='Completed' or terminal['source']!='Virtual' or terminal['errorCode']:
        raise ValueError('SpecialHttpNoReliableCompletion')
    if request['kind']=='Exit' and terminal['occupiedEntityId'] is not None:raise ValueError('SpecialExitStillOccupied')
    return len(exchanges)


def inspect(root):
    collected=load(root/'collection.json');store=Path(collected['testRoot'])
    facts=load(root/'process-api-facts.json');case=collected['caseId'];run_id=collected['runId'].lower()
    oracle_path=HERE.parents[1]/'backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json'
    oracle=load(oracle_path);signals={s['id']:s for s in oracle['signals']}
    audit=facts['plcWriteAudit'];writes=audit['writes'];actions=audit['actions']
    checks={'complete_write_and_action_audit':audit['gap'] is False and audit['actionGap'] is False and bool(writes)}
    batches=[];raw_rows=[]
    with sqlite3.connect((store/'station01.test.db').resolve().as_uri()+'?mode=ro',uri=True) as db:
        for identity,payload,digest,persisted in db.execute('SELECT EvidenceId,RawPayloadJson,PayloadDigest,PersistedUtc FROM PlcCommunicationEvidence WHERE lower(RunId)=?',(run_id,)):
            if hashlib.sha256(payload.encode()).hexdigest()!=digest.lower():raise ValueError('ActualRawDigestMismatch')
            batch=json.loads(payload);batches.append(batch);raw_rows.append(dict(evidenceId=identity,persistedUtc=persisted,payloadDigest=digest))
    checks['real_run_raw']=bool(batches)
    total=0
    for batch in batches:
        exchanges=prop(batch,'exchanges',[])
        if not exchanges:continue  # HTTP-special evidence is independently inspected below.
        # Sequence here is explicitly the captured batch ordinal, not a invented
        # historical transport sequence or reconstructed response value.
        items=[dict(connectionId=x['connectionId'],channel=x['channel'],sequence=i+1,
                    request=x['requestHex'],response=x['responseHex'],error=x['error'],
                    **{k:x[k] for k in ('transactionId','unit','function','offset','count','startedUtc','endedUtc')})
               for i,x in enumerate(exchanges)]
        parsed=wire.check(dict(schemaVersion='009-wire-evidence/1',runId=run_id,caseId=case,gap=prop(batch,'gap'),exchanges=items),oracle)
        total+=parsed['successfulExchanges']
    checks['actual_packets_match_independent_definition']=total>0
    if case=='L09':
        auxiliary=facts['specialActions']
        http_batches=[b for b in batches if prop(b,'httpExchanges')]
        checks['each_special_action_has_persistent_http']=bool(auxiliary) and len(http_batches)==len(auxiliary)
        for batch in http_batches:
            actual=[a for a in auxiliary if a['request']['actionId']==batch['actionId']]
            if len(actual)!=1:raise ValueError('SpecialProcessActionMissingOrDuplicate')
            special_http(batch,actual[0])
        checks['special_exit_and_no_ordinary_transfer']=bool([a for a in auxiliary if a['request']['kind']=='Exit']) and not any(prop(a,'kind')=='Sort' for a in actions)
    transfer_batches=[b for b in batches if prop(b,'interpretation')=='MaterialTransferred' and not prop(b,'httpExchanges')]
    for b in transfer_batches:operation.sorting_packets(b,oracle)
    places=[a for a in actions if prop(a,'kind')=='Sort' and prop(a,'command')==2 and prop(a,'phase')=='completed']
    checks['independent_pick_place_sequence']=len(transfer_batches)==len(places)
    if case=='L08':
        changes=facts['plcChanges']
        checks['manual_area_and_ack_recorded']=changes['gap'] is False and all(
            any(c['name']==name and str(c['current'])==value for c in changes['changes'])
            for name in ('Manual_Zone_Occupied','Manual_Flip_Complete') for value in ('1','0'))
    def matches(item,signal):
        return prop(item,'area')==1 and prop(item,'pduOffset')==signals[signal]['pduOffset']
    def value(signal,meaning):return int(next(k for k,v in signals[signal]['values'].items() if v==meaning))
    if case.startswith(('F01-','F02-','F03-','F04-')):
        raw={item['name']:item['rawValue'] for item in facts['plcState']['holdingRegisters']}
        unload=[w for w in writes if matches(w,'XY_Move_Cmd') and prop(w,'rawWords')==[value('XY_Move_Cmd','Unload')]]
        after_unload=[w for w in writes if unload and prop(w,'sequence')>prop(unload[0],'sequence')]
        if case.startswith('F01-') or case=='F03-uncertain':
            checks['one_actual_unload_dispatch_no_following_motion']=len(unload)==1 and not any(
                any(matches(w,s) for s in ('XY_Move_Cmd','Sorting_Cmd','Camera_Target_X','Camera_Target_Y','Grab_Target_Z'))
                and any(prop(w,'rawWords')) for w in after_unload)
        if case in ('F01-missing','F01-old-action'):
            phase='testPreviousFeedbackHeld' if case=='F01-old-action' else 'testFeedbackMissing'
            held=[a for a in actions if prop(a,'phase')==phase and prop(a,'command')==value('XY_Move_Cmd','Unload')]
            checks['actual_feedback_hold_not_completed']=len(held)==1 and not any(prop(a,'actionSequence')==prop(held[0],'actionSequence') and prop(a,'phase')=='completed' for a in actions)
            if case=='F01-old-action':
                checks['prior_completed_feedback_retained']=len(held)==1 and prop(prop(held[0],'handshake'),'xy')==value('XY_Pos_Confirmed','Arrived') and any(prop(a,'kind')=='Move' and prop(a,'phase')=='completed' and prop(a,'sequence')<prop(held[0],'sequence') for a in actions)
        if case in ('F01-stale','F03-uncertain'):
            faults=audit['feedbackFaults'];expected='Delay' if case=='F01-stale' else 'Close'
            checks['real_processed_request_then_transport_fault']=len(faults)==1 and faults[0]['disposition']==expected and bool(faults[0]['processedResponseHex'])
            if len(faults)==1:
                failed=[x for b in batches for x in prop(b,'exchanges',[]) if x.get('transactionId')==faults[0]['transactionId'] and x.get('requestHex','')[14:]==faults[0]['requestPduHex'] and x.get('error') and not x.get('responseHex')]
                checks['current_failed_exchange_persisted']=bool(failed) and any(utc(x['startedUtc'])<=utc(faults[0]['receivedAtUtc'])<=utc(x['endedUtc']) for x in failed)
                if case=='F01-stale':
                    checks['actual_late_response_interval']=faults[0]['delayElapsedAtUtc'] is not None and (utc(faults[0]['delayElapsedAtUtc'])-utc(faults[0]['appliedAtUtc'])).total_seconds()>=1.5 and any(utc(x['endedUtc'])<utc(faults[0]['delayElapsedAtUtc']) for x in failed)
            if case=='F03-uncertain':checks['actual_accepted_write_without_response']=len(unload)==1 and prop(unload[0],'accepted') is True and not prop(unload[0],'responseHex') and prop(unload[0],'responseSentAtUtc') is None
        if case=='F02-position':
            wrong=[a for a in actions if prop(a,'phase')=='testWrongPosition']
            checks['actual_wrong_position_not_target_substitution']=len(wrong)==1 and prop(wrong[0],'actual')!=prop(wrong[0],'target')
            checks['picked_feedback_cannot_approve_place']=raw['Sorting_Exec_Status']==value('Sorting_Exec_Status','Picked') and not any(matches(w,'Sorting_Cmd') and prop(w,'rawWords')==[value('Sorting_Cmd','Place')] for w in writes)
        if case in ('F02-face','F04-handshake'):
            flips=[a for a in actions if prop(a,'kind')=='Flip' and prop(a,'phase')=='completed']
            checks['one_actual_flip']=len(flips)==1
            if case=='F02-face':checks['actual_face_differs_from_request']=len(flips)==1 and raw['Flip_Current_Face']!=prop(flips[0],'command')
            else:checks['actual_unclosed_flip_handshake']=raw['Flip_Status']==value('Flip_Status','Completed') and raw['Flip_OK']==value('Flip_OK','Acknowledged')
        if case=='F04-reset':
            checks['actual_reset_stays_unconfirmed']=any(prop(a,'phase')=='testResetFeedbackHeld' for a in actions) and raw['Z_Reset_Status']==value('Z_Reset_Status','Resetting') and raw['Inspection_Status']!=value('Inspection_Status','Waiting')
    if case=='L10':
        coil=lambda name:[w for w in writes if prop(w,'area')==0 and prop(w,'pduOffset')==signals[name]['pduOffset'] and any(prop(w,'rawWords')) and prop(w,'requestHex')==prop(w,'responseHex')]
        starts=coil('PC_Start_Cmd');resets=coil('System_Reset_Cmd')
        checks['actual_two_starts_separated_by_reset']=len(starts)==2 and len(resets)==1 and prop(starts[0],'sequence')<prop(resets[0],'sequence')<prop(starts[1],'sequence')
        changes=facts['plcChanges']
        checks['original_motion_failure_observed']=changes['gap'] is False and any(c['name']=='XY_Pos_Confirmed' and str(c['current'])==str(value('XY_Pos_Confirmed','Timeout')) for c in changes['changes'])
    if case=='L05':
        source_slot=[w for w in writes if matches(w,'Sorting_Part_Index') and prop(w,'rawWords')!=[0]]
        checks['independent_source_slot_three']=len(source_slot)==len(places)==1 and all(prop(w,'rawWords')==[3] and
            prop(w,'responseHex')==prop(w,'requestHex') and prop(w,'sequence') in prop(places[0],'writeSequenceRefs',[]) for w in source_slot)
    if case.startswith(('F05-','F06-')):
        fault=[json.loads(x) for x in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
        hit=[x for x in fault if x['caseId']==case and x['runId'].lower()==run_id]
        if not hit:raise ValueError('ActualFaultNotHit')
        first=min(utc(x['atUtc']) for x in hit)
        pick=[w for w in writes if matches(w,'Sorting_Cmd') and prop(w,'rawWords')==[value('Sorting_Cmd','Pick')]]
        place=[w for w in writes if matches(w,'Sorting_Cmd') and prop(w,'rawWords')==[value('Sorting_Cmd','Place')]]
        checks['actual_pick_dispatched_once_no_place']=len(pick)==1 and not place
        changed=[c for c in facts['plcChanges']['changes'] if c['name']=='Sorting_Exec_Status' and str(c['current'])==str(value('Sorting_Exec_Status','Picked'))]
        checks['actual_pick_observed']=bool(changed) and facts['plcChanges']['gap'] is False
        forbidden=['Sorting_Cmd','Camera_Target_X','Camera_Target_Y','Grab_Target_Z','Sorting_Part_Index']
        # Include refused requests: zero accepted writes alone is insufficient.
        after=[w for w in writes if utc(prop(w,'receivedAtUtc'))>=first and any(matches(w,s) for s in forbidden)]
        checks['no_new_target_or_action_after_save_fault']=not after
        identity={x['identity'].lower() for x in hit}
        matching=[b for b in batches if prop(b,'evidenceId','').lower() in identity]
        if case=='F06-A':checks['raw_transaction_not_committed']=not matching and any(x['phase']=='SqliteRejectionInstalledBeforeInsert' for x in hit)
        if case=='F06-B':checks['raw_actually_committed_without_place']=len(matching)==1 and any(x['phase']=='ActualCommitCompleted' for x in hit)
        if case=='F06-C':checks['raw_result_not_assumed']=any(x['phase']=='ActualExclusiveLockAcquired' for x in hit)
        if case=='F06-C':
            restart=load(root/'restart-facts.json');after=restart['actionAudit']
            checks['recovery_audit_complete']=after['gap'] is False and after['actionGap'] is False
            # Startup may initialise communication. It must never resend the old
            # physical action or begin a new move/transfer after read-only recovery.
            start=utc(restart['startedUtc'])
            control=['Sorting_Cmd','Camera_Target_X','Camera_Target_Y','Camera_Target_Z','Scan_Target_Z','Grab_Target_Z','Sorting_Part_Index','XY_Move_Cmd','Flip_Target_Face','Pallet_Lock_Cmd','Retry_Cmd']
            checks['restart_no_physical_action_or_target']=not any(utc(prop(w,'receivedAtUtc'))>=start and any(matches(w,s) for s in control) and any(prop(w,'rawWords')) for w in after['writes'])
            checks['restart_no_new_start_command']=not any(utc(prop(w,'receivedAtUtc'))>=start and prop(w,'area')==0 and prop(w,'pduOffset')==signals['PC_Start_Cmd']['pduOffset'] and any(prop(w,'rawWords')) for w in after['writes'])
            checks['restart_retains_single_original_pick']=sum(matches(w,'Sorting_Cmd') and prop(w,'rawWords')==[value('Sorting_Cmd','Pick')] for w in after['writes'])==1
    if case.startswith(('BA03-','BA05-cancel/')):
        events=[]
        for line in (store/'logs/host.out.log').open(encoding='utf-8-sig',errors='replace'):
            if 'RuntimeFlow {' not in line:continue
            event=json.loads(line.split('RuntimeFlow ',1)[1])
            if str(event.get('runId','')).lower()==run_id and event['step']=='RecipeApplication' and event['outcome']=='WindowRegistered':events.append(event)
        if len(events)!=1:raise ValueError('BindingWindowEvidenceMissing')
        window=events[0]['facts']['window'];start=utc(prop(window,'startedUtc'));end=utc(prop(window,'deadlineUtc'))
        observed=[x for b in batches for x in prop(b,'exchanges',[]) if start<=utc(x['startedUtc'])<end]
        if case.startswith('BA03-'):
            checks['each_io_successful_during_healthy_wait']=bool(observed) and all(x['error'] is None and x['responseHex'] for x in observed)
        heart=signals['PC_Heartbeat_Resp']
        heartbeat_start=start if case.startswith('BA03-') else utc(load(root/'cancel-receipt.json')['atUtc'])
        checks['heartbeat_writes_continue']=sum(prop(w,'area')==0 and prop(w,'pduOffset')==heart['pduOffset'] and heartbeat_start<=utc(prop(w,'receivedAtUtc'))<end for w in writes)>= (3 if case.startswith('BA03-') else 1)
        forbidden=['Recipe_ID','NG_Zone_Count','Pending_Zone_Count','Zone_Config_Ready']
        binding_writes=[w for w in writes if utc(prop(w,'receivedAtUtc'))>=start and any(matches(w,s) for s in forbidden)]
        if case=='BA05-cancel/inflight':
            seam=[json.loads(line) for line in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
            entered=[x for x in seam if x['phase']=='BindingFirstWriteResponded' and x['runId'].lower()==run_id]
            closed=[x for x in seam if x['phase']=='BindingFenceClosed' and x['runId'].lower()==run_id]
            first=[w for w in binding_writes if matches(w,'NG_Zone_Count')]
            checks['real_first_capacity_write_before_cancel']=len(entered)==len(first)==1 and prop(first[0],'rawWords')==[9] and prop(first[0],'requestHex')==prop(first[0],'responseHex') and start<=utc(prop(first[0],'receivedAtUtc'))<=utc(entered[0]['atUtc'])<heartbeat_start
            checks['fence_closed_by_original_request']=len(closed)==len(entered)==1 and closed[0]['reason']=='OriginalRequestCancelled' and closed[0]['identity']==entered[0]['identity']
            checks['no_following_binding_write']=not any(matches(w,'Pending_Zone_Count') or matches(w,'Recipe_ID') or matches(w,'Zone_Config_Ready') and prop(w,'rawWords')!=[0] for w in binding_writes)
        else:
            checks['no_binding_writes_while_unready_or_after_close']=not binding_writes
    if case.startswith('BA06-') or case=='BA02-success/api-existing-handoff':
        journal=[]
        for line in (store/'logs/host.out.log').open(encoding='utf-8-sig',errors='replace'):
            if 'RuntimeFlow {' in line:
                event=json.loads(line.split('RuntimeFlow ',1)[1])
                if str(event.get('runId','')).lower()==run_id:journal.append(event)
        windows=[e['facts'] for e in journal if e['step']=='RecipeApplication' and e['outcome']=='WindowRegistered']
        binding_signals=['Recipe_ID','NG_Zone_Count','Pending_Zone_Count','Zone_Config_Ready']
        if 'strict-before-port' in case:
            seam=[json.loads(line) for line in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
            released=[e for e in seam if e['phase']=='BeforeBindingReleased']
            checks['expired_original_deadline_zero_binding_dispatch']=len(released)==1 and not windows and not any(utc(prop(w,'receivedAtUtc'))>=utc(released[0]['atUtc']) and any(matches(w,s) for s in binding_signals) for w in writes)
        else:
            api=case=='BA02-success/api-existing-handoff' or '/api-' in case
            checks['actual_entry_count']=len(windows)==(2 if api else 1)
            for index,registered in enumerate(windows):
                window=registered['window'];start=utc(prop(window,'startedUtc'));end=utc(prop(window,'deadlineUtc'))
                following=utc(prop(windows[index+1]['window'],'startedUtc')) if index+1<len(windows) else None
                applied=[b for b in batches if prop(b,'interpretation')=='DeviceRecipeApplied' and str(prop(b,'bindingId')).lower()==str(registered['bindingId']).lower()]
                dispatch=[w for w in writes if start<=utc(prop(w,'receivedAtUtc'))<end and (following is None or utc(prop(w,'receivedAtUtc'))<following) and matches(w,'Recipe_ID')]
                checks['entry_'+str(index)+'_actual_device_application']=len(applied)==1 and len(dispatch)==1 and prop(dispatch[0],'requestHex')==prop(dispatch[0],'responseHex')
                # Include post-deadline dispatch observation. A later separately
                # admitted API binding is not a continuation of the prior request.
                following=utc(prop(windows[index+1]['window'],'startedUtc')) if index+1<len(windows) else None
                checks['entry_'+str(index)+'_no_late_binding_dispatch']=not any(utc(prop(w,'receivedAtUtc'))>=end and (following is None or utc(prop(w,'receivedAtUtc'))<following) and any(matches(w,s) for s in binding_signals) for w in writes)
            if case!='BA06-downstream/legacy-PROCESS':
                last=utc(prop(windows[-1]['window'],'startedUtc')) if windows else utc(facts['observedUtc'])
                product=['XY_Move_Cmd','Sorting_Cmd','Flip_Target_Face','Camera_Target_X','Camera_Target_Y','Camera_Target_Z','Scan_Target_Z','Grab_Target_Z','Sorting_Part_Index']
                checks['no_product_action_after_binding_boundary']=not any(utc(prop(w,'receivedAtUtc'))>=last and any(matches(w,s) for s in product) and any(prop(w,'rawWords')) for w in writes)
    checks['both_channels_real']=len({prop(w,'connectionId') for w in writes})>=2
    evidence=['host-tcp','independent-plc','dispatch-timeline'] if all(checks.values()) else []
    return dict(scope='CommunicationOnly',caseId=case,runId=run_id,checks=checks,passed=all(checks.values()),
                evidenceKinds=evidence,oracleSha256=hashlib.sha256(oracle_path.read_bytes()).hexdigest(),
                successfulExchanges=total,rawRows=raw_rows,capturedSequenceMeaning='Batch ordinal only')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('root',type=Path);args=parser.parse_args()
    result=inspect(args.root.resolve())
    with (args.root/'wire-validation.json').open('x',encoding='utf-8') as output:json.dump(result,output,indent=2)
    print(json.dumps(result));return 0 if result['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
