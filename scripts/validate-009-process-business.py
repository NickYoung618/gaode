"""Read actual independent-run business facts. No wire decoding or page claims."""
import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import sqlite3
from semantic_009_evidence import prop, sorting_commits, recovery_checks, failed_unload_holds


def load(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def read_runtime(path,run_id=None):
    result=[]
    for line in path.open(encoding='utf-8-sig',errors='replace'):
        if 'RuntimeFlow {' not in line:continue
        value=json.loads(line.split('RuntimeFlow ',1)[1])
        if run_id is None or str(value.get('runId','')).lower()==run_id:result.append(value)
    return result


def recovery_input(root,store,run_id):
    with sqlite3.connect((store/'station01.test.db').resolve().as_uri()+'?mode=ro',uri=True) as db:
        rows=[dict(eventId=identity,stage=stage,eventType=kind,payload=payload,recordedDigest=digest,
                   payloadSha256=hashlib.sha256(payload.encode()).hexdigest(),error=error,source=source)
              for identity,stage,kind,payload,digest,error,source in db.execute(
                  'SELECT EventId,Stage,EventType,PayloadJson,PayloadDigest,ErrorCode,Source FROM StageEvents WHERE lower(RunId)=? ORDER BY Sequence',(run_id.lower(),))]
    # StageEvent.PayloadDigest is the caller's idempotency value; existing
    # recovery records use "recovery-in-flight". Preserve it as recorded and
    # compute a separate actual payload checksum, rather than invent its format.
    if not rows or any(not row['recordedDigest'] for row in rows):
        raise ValueError('RecoveryActualCommittedInputInvalid')
    return dict(runId=run_id.lower(),rows=rows)


def binding_path_checks(case,root,store,run,writes,events,logs):
    """Actual three-entry evidence; original deadlines are never reconstructed."""
    instant=lambda value:datetime.fromisoformat(value.replace('Z','+00:00'))
    frozen=[p['routeDeadlines'] for _,p in writes if prop(p,'kind')=='RecipeExecutionDeadlinesFrozen']
    windows=[x['facts'] for x in logs if x['step']=='RecipeApplication' and x['outcome']=='WindowRegistered']
    intents=[p for _,p in writes if prop(p,'kind')=='RecipePlanAndBindingIntent']
    receipts=[p['receipt'] for _,p in writes if prop(p,'kind')=='RecipeApplicationReceiptObserved']
    bound=[p for _,p in writes if prop(p,'kind')=='RecipePlanBound']
    downstream=[x for x in logs if x['step']=='DetectionHandoff' and x['outcome']=='RequestPrepared']
    checks={}
    api=case=='BA02-success/api-existing-handoff' or '/api-' in case
    legacy=case=='BA06-downstream/legacy-PROCESS' or '/api-none-' in case
    checks['original_deadlines_not_recreated']=len(frozen)==(0 if legacy else 1)
    if api:
        before=load(root/'before-independent-binding.json');after=load(root/'after-independent-binding.json')
        response=load(root/'independent-binding-response.json')['receipt'];view=response['bindingResult']
        api_rows=[e for e in events if e['stage']=='RecipeApplication']
        api_intents=[e['payload'] for e in api_rows if prop(e['payload'],'kind')=='RecipePlanAndBindingIntent']
        api_receipts=[e['payload']['receipt'] for e in api_rows if prop(e['payload'],'kind')=='RecipeApplicationReceiptObserved']
        checks['separate_real_api_save_stream']=len(api_intents)==len(api_receipts)==1 and len(api_rows)==3 and len(windows)==2 and len(receipts)==1
        if len(api_intents)!=1 or len(api_receipts)!=1:return dict(checks,api_receipt_missing=False)
        selected=api_receipts[0];details=next(x for x in windows if str(x['bindingId']).lower()==selected['bindingId'].lower())
        checks['api_preserves_existing_handoff_and_run_revision']=all(before['run'].get(k)==after['run'].get(k) for k in ('handoffId','handoff','observedRevision','persistedRevision','state')) and bool(before['run'].get('handoffId'))
        checks['api_does_not_authorize_product_replay']=response['productContinuationAuthorized'] is False and not downstream and not any(e['stage']=='Detection' for e in events)
        checks['api_receipt_actual_projection']=view['bindingId']==selected['bindingId'] and view['outcome']=='Completed' and view['diagnosticEvidenceReferences']==selected['deviceEvidence']['diagnosticEvidenceReferences']
        checks['api_current_frozen_source']=selected['budgetSource']==receipts[0]['budgetSource']
        wanted={'RecipePlanBound'}
    else:
        checks['one_continuous_window_or_expired_admission']=len(windows)==(0 if 'strict-before-port' in case else 1)
        if 'strict-before-port' in case:
            seam=[json.loads(line) for line in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
            released=[x for x in seam if x['phase']=='BeforeBindingReleased']
            checks['expired_before_port_no_intent_or_success']=len(released)==1 and len(frozen)==1 and instant(released[0]['atUtc'])>=instant(frozen[0]['detectionDeadlineUtc']) and not intents and not receipts and not bound and not downstream and bool(run.get('errorCode'))
            return checks
        if len(windows)!=1:return dict(checks,window_missing=False)
        details=windows[0];selected=receipts[0] if len(receipts)==1 else None;wanted={'RecipePlanBound','Handoff'}
    # Runtime structured facts preserve CLR property casing; committed payloads
    # use the web JSON contract. Read those actual fields without inventing values.
    window={k:prop(details['window'],k) for k in ('startTick','dueTick','startedUtc','deadlineUtc')}
    source={k:prop(details['source'],k) for k in ('budgetMs','schemaVersion','version','purpose','configurationId','snapshotId','source','digest')}
    prior=details['existingDeadlines']
    checks['versioned_frozen_ten_seconds']=source['budgetMs']==10000 and source['schemaVersion']=='1.1' and source['version']=='2.0.0' and source['purpose']=='Test' and all(source[k] for k in ('configurationId','snapshotId','source','digest'))
    original=[] if legacy else [frozen[0][k] for k in ('detectionDeadlineUtc','unloadDeadlineUtc','sortingDeadlineUtc')] if len(frozen)==1 else []
    checks['only_actual_original_deadlines_used']={instant(x) for x in prior}=={instant(x) for x in original}
    start=instant(window['startedUtc']);end=instant(window['deadlineUtc'])
    from datetime import timedelta
    checks['effective_window_is_original_minimum']=abs((end-min([start+timedelta(milliseconds=10000)]+[instant(x) for x in original])).total_seconds())<0.00001
    if 'strict-during-save' in case:
        seam=[json.loads(line) for line in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
        committed=[x for x in seam if x['phase']=='ActualCommitCompleted']
        closed=[x for x in logs if x['step']=='RecipeApplication' and x['outcome']=='ClosedWithoutAuthorization']
        checks['real_commit_but_expired_receipt_never_continues']=len(committed)==len(bound)==len(closed)==1 and not receipts and not downstream and bool(run.get('errorCode')) and closed[0]['tick']>=window['dueTick'] and closed[0]['facts']['newDispatchAllowed'] is False
        checks['original_deadline_wins_before_ten_seconds']=end-start<timedelta(milliseconds=10000) and len(committed)==1 and instant(committed[0]['atUtc'])<end
        return checks
    checks['one_valid_complete_receipt']=selected is not None and selected['wasCompletedInWindow'] is True and selected['validity']=='ValidCurrent'
    if selected is None:return checks
    commits=selected['requiredCommits'];intent=selected['intentCommit'];raw=selected['requiredEvidenceCommit']
    checks['actual_intent_precedes_window']=intent['actualCommit']=='Committed' and intent['validity']=='ValidCurrent' and intent['receivedTick']<=window['startTick']
    checks['all_device_and_business_receipts_in_window']={c['savePurpose'] for c in commits}==wanted and all(c['actualCommit']=='Committed' and c['validity']=='ValidCurrent' and c['correlation']==selected['correlation'] and window['startTick']<=c['receivedTick']<window['dueTick'] for c in commits+[raw])
    if 'strict-before-next' in case:
        seam=[json.loads(line) for line in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
        released=[x for x in seam if x['phase']=='BeforeContinuationReleased']
        checks['completed_binding_does_not_refresh_expired_detection']=len(released)==1 and instant(released[0]['atUtc'])>=instant(frozen[0]['detectionDeadlineUtc']) and not downstream and bool(run.get('errorCode')) and not any(e['stage']=='Detection' for e in events)
    if case=='BA06-downstream/legacy-PROCESS':
        checks['legacy_detection_starts_after_handoff']=len(downstream)==1 and downstream[0]['facts']['strict'] is False and downstream[0]['tick']>=selected['receivedTick']
        if len(downstream)==1:
            detail=downstream[0]['facts']
            checks['legacy_original_duration_not_backdated']=detail['stageStartedAtUtc'] is not None and 119.9<=(instant(detail['stageDeadlineAtUtc'])-instant(detail['stageStartedAtUtc'])).total_seconds()<=120
        checks['legacy_actual_route_completed']=run['state']==26 and run['finalOutcome']==1 and not run.get('errorCode')
    return checks


def inspect(root):
    collected=load(root/'collection.json');store=Path(collected['testRoot'])
    fixture=load(collected['fixture']);facts=load(root/'process-api-facts.json');run=facts['run']
    run_id=run['runId'].lower();case=collected['caseId']
    process=load(store/'process.json');owned=load(root/'owned-processes.json')
    recipe=next(r for r in load(fixture['recipeCatalogPath'])['recipes'] if r['recipeId']==fixture['recipeRef']['recipeId'])
    database=store/'station01.test.db'
    with sqlite3.connect(database.resolve().as_uri()+'?mode=ro',uri=True) as db:
        writes=[(kind,json.loads(payload)) for kind,payload in db.execute('SELECT Kind,PayloadJson FROM Writes WHERE lower(RunId)=? ORDER BY Revision',(run_id,))]
        events=[dict(eventId=identity,stage=stage,eventType=kind,error=error,payload=json.loads(payload)) for identity,stage,kind,error,payload in db.execute(
            'SELECT EventId,Stage,EventType,ErrorCode,PayloadJson FROM StageEvents WHERE lower(RunId)=? ORDER BY Sequence',(run_id,))]
        media=list(db.execute('SELECT MediaId,CaptureId,RelativeKey,ByteLength,State FROM Media WHERE lower(RunId)=?',(run_id,)))
    all_logs=read_runtime(store/'logs/host.out.log')
    logs=[x for x in all_logs if str(x.get('runId','')).lower()==run_id]
    checks={
        'same_run_actual_collection':run_id==collected['runId'].lower() and not collected['error'] and not collected['cleanupErrors'],
        'independent_processes':len({process.get(k) for k in ('hostPid','plcPid','workerPid')})==3 and all(process.get(k) for k in ('hostPid','plcPid','workerPid')) and len(owned)==3,
        'actual_config':fixture['budgetRef']['version']=='2.0.0' and fixture['simulationRef']['version']=='2.0.0',
        'actual_worker_origin':bool(process.get('workerSession')) and process.get('workerOwner')=='Host/WorkerProcessSupervisor',
        'actual_loaded_build':process['configuration']['hostDllSha256'].lower()==hashlib.sha256(Path(collected['hostDll']).read_bytes()).hexdigest() and process['configuration']['plcDllSha256'].lower()==hashlib.sha256(Path(collected['plcDll']).read_bytes()).hexdigest(),
        'actual_worker_started_and_ready':all(any(x['step']=='WorkerProcess' and x['outcome']==phase and x['processId']==process['hostPid'] and prop(x['facts'],'processId')==process['workerPid'] for x in all_logs) for phase in ('Started','Ready')),
        'actual_worker_run_io':any(x['step']=='WorkerDispatch' and x['outcome']=='Sending' and x['processId']==process['hostPid'] for x in logs) and any(x['step']=='WorkerFeedback' and x['outcome']=='Received' and x['processId']==process['hostPid'] for x in logs),
        # Binding intent/approval facts use Writes. A rejected binding has not
        # entered a downstream stage and must not fabricate a StageEvent.
        'actual_run_database':bool(writes) and (case.startswith('BA') or case=='F04-reset' or bool(events)),
        'persistent_structured_log':bool(logs),
    }
    media_proof=[]
    for media_id,capture_id,key,length,state in media:
        path=store/'media-root'/key
        # The controlled per-case root plus two real media identities can exceed
        # Win32 MAX_PATH. Use the native absolute path, never treat that API limit
        # as proof the file is absent or synthesize a media digest.
        if os.name=='nt':path=Path('//?/'+str(path.absolute()))
        saved=[p for kind,p in writes if kind=='Media' and prop(p,'relativeKey')==key]
        hashes=[prop(e['payload'],'sha256') for e in events if prop(e['payload'],'relativeKey')==key and prop(e['payload'],'sha256')]
        inputs=[item for event in logs if event['step']=='WorkerDispatch' and event['outcome']=='Sending'
            for item in prop(event['facts'],'inputs',[]) if str(prop(item,'mediaId')).lower()==media_id.lower()]
        actual=hashlib.sha256(path.read_bytes()).hexdigest().upper() if path.is_file() else None
        media_proof.append(dict(mediaId=media_id,captureId=capture_id,relativeKey=key,sha256=actual,byteLength=length,
            digestSource='ActualFile;PersistedWorkerDispatchInput;ApplicableStageCaptureFact',
            valid=path.is_file() and path.stat().st_size==length and state=='FileCompleted' and len(saved)==1 and
                  str(prop(saved[0],'mediaId')).lower()==media_id.lower() and str(prop(saved[0],'captureId')).lower()==capture_id.lower() and
                  prop(saved[0],'byteLength')==length and bool(inputs) and bool(actual) and
                  all(str(prop(i,'captureId')).lower()==capture_id.lower() and prop(i,'sha256','').upper()==actual for i in inputs) and
                  all(x.upper()==actual for x in hashes)))
    checks['media_real_files_and_commits']=bool(media_proof) and all(x['valid'] for x in media_proof)
    if case.startswith('BA06-') or case=='BA02-success/api-existing-handoff':
        checks.update(binding_path_checks(case,root,store,run,writes,events,logs))
    elif case.startswith('BA'):
        registrations=[x for x in logs if x['step']=='RecipeApplication' and x['outcome']=='WindowRegistered']
        checks['one_total_window']=len(registrations)==1
        if len(registrations)!=1:raise ValueError('UniqueApplicationWindowMissing')
        details=registrations[0]['facts']
        window={key:prop(details['window'],key) for key in ('startTick','dueTick','startedUtc','deadlineUtc')}
        source={key:prop(details['source'],key) for key in ('budgetMs','configurationId','schemaVersion','version','purpose','source','snapshotId','digest')}
        intents=[p for _,p in writes if prop(p,'kind')=='RecipePlanAndBindingIntent']
        checks['real_frozen_budget']=source['budgetMs']==10000 and source['schemaVersion']=='1.1' and source['version']=='2.0.0' and source['purpose']=='Test' and all(source[k] for k in ('configurationId','source','snapshotId','digest'))
        capacity=case.endswith('/capacity') or case=='BA05-cancel/inflight'
        checks['one_real_intent_and_input']=len(intents)==1 and (prop(intents[0],'ngCapacity') is not None)==capacity and (prop(intents[0],'pendingCapacity') is not None)==capacity
        observed=[p['receipt'] for _,p in writes if prop(p,'kind')=='RecipeApplicationReceiptObserved']
        bound=[p for _,p in writes if prop(p,'kind')=='RecipePlanBound']
        public_views=[m['facts']['recipeApplication'] for m in facts['runEvidence'].get('motionEvidence',[]) if (m.get('facts') or {}).get('recipeApplication')]
        if case.startswith('BA02-'):
            checks['actual_completed_receipt']=len(observed)==1 and observed[0]['wasCompletedInWindow'] is True and observed[0]['validity']=='ValidCurrent'
            if len(observed)!=1:raise ValueError('CurrentBindingReceiptNotPersisted')
            receipt=observed[0];commits=receipt['requiredCommits']
            checks['intent_before_window']=receipt['intentCommit']['actualCommit']=='Committed' and receipt['intentCommit']['validity']=='ValidCurrent' and receipt['intentCommit']['receivedTick']<=window['startTick']
            checks['all_required_commits_within_same_window']=all(c['actualCommit']=='Committed' and c['validity']=='ValidCurrent' and c['correlation']==receipt['correlation'] and window['startTick']<=c['receivedTick']<window['dueTick'] for c in commits) and {c['savePurpose'] for c in commits}=={'RecipePlanBound','Handoff'}
            checks['device_confirmation_and_raw_within_window']=receipt['requiredEvidenceCommit']['actualCommit']=='Committed' and receipt['requiredEvidenceCommit']['validity']=='ValidCurrent' and window['startTick']<=receipt['requiredEvidenceCommit']['receivedTick']<window['dueTick']
            checks['current_api_correlated_binding']=len(public_views)==1 and public_views[0]['bindingId']==receipt['bindingId']
        else:
            checks['no_current_success_or_product_continuation']=run.get('recipeState')!='Bound' and run.get('handoff') not in (2,3) and not observed and not any(x['step']=='DetectionMove' and x['outcome']=='Dispatching' for x in logs)
            closed=[x for x in logs if x['step']=='RecipeApplication' and x['outcome']=='ClosedWithoutAuthorization']
            checks['qualification_closed']=len(closed)==1 and closed[0]['facts']['newDispatchAllowed'] is False
            if case.startswith('BA03-'):
                duration=(datetime.fromisoformat(window['deadlineUtc'])-datetime.fromisoformat(window['startedUtc'])).total_seconds()
                checks['healthy_wait_uses_real_ten_seconds']=duration==10 and closed[0]['tick']>=window['dueTick'] and not bound
            elif case.startswith('BA04-'):
                held=[json.loads(line) for line in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
                commit=[x for x in held if x['phase']=='ActualCommitCompleted']
                checks['real_committed_record_not_rollback']=len(commit)==1 and len(bound)==1
                with sqlite3.connect(database.resolve().as_uri()+'?mode=ro',uri=True) as db:
                    checks['exact_commit_identity_found']=len(commit)==1 and db.execute('SELECT count(*) FROM Writes WHERE lower(WriteId)=?',(commit[0]['identity'].lower(),)).fetchone()[0]==1
                checks['late_lookup_did_not_restore_authorization']=len(public_views)==1 and all(
                    view.get('outcome')=='AwaitingRequiredBusinessCommits' and view.get('hostValidatedTick') is None and
                    any(c['actualCommit']=='Committed' and c['receiptValidity'] is None for c in view['requiredCommits'] if c['kind']=='RecipePlanBound')
                    for view in public_views)
            elif case.startswith('BA05-cancel/'):
                cancelled=load(root/'cancel-receipt.json')
                checks['actual_cancel_accepted_before_total_deadline']=cancelled['receipt']['requestAccepted'] is True and cancelled['receipt']['admissionClosed'] is True and datetime.fromisoformat(cancelled['atUtc'].replace('Z','+00:00'))<datetime.fromisoformat(window['deadlineUtc'])
                checks['no_bound_or_late_authorization']=not bound and not public_views and run['cancelRequested'] is True
            else:raise ValueError('BindingBusinessCaseNotImplemented:'+case)
    elif case.startswith('L'):
        catalog_digest=hashlib.sha256(Path(fixture['recipeCatalogPath']).read_bytes()).hexdigest().upper()
        frozen=[p for _,p in writes if prop(p,'kind')=='RecipeExecutionDeadlinesFrozen']
        checks['catalog_and_frozen_version']=catalog_digest==fixture['recipeRef']['catalogDigest'] and len(frozen)==1 and prop(frozen[0],'catalogDigest')==catalog_digest and prop(frozen[0],'recipeVersion')==fixture['recipeRef']['version']
        positions=[p for p in recipe['positions'] if p['slotId'] in fixture['occupiedSlots']]
        stages=recipe['execution']['stages'];special=recipe['execution']['route'].startswith('specialType1')
        def expected_stage(stage,batch):
            result=[]
            for pair in dict.fromkeys(t['cameraPair'] for t in stage['targets']):
                for camera in pair:
                    for target in [t for t in stage['targets'] if t['cameraPair']==pair]:
                        for position in batch:
                            for member in position['members']:
                                if member['material']!=target['material']:continue
                                unit=position['unitIdPattern'].replace('{TrayRunId}','RUN')
                                identity=member['memberIdPattern'].replace('{UnitId}',unit).split(':',1)[1]
                                result.append((camera,target['localFace'],identity))
            return result
        expected=[v for p in positions for s in stages for v in expected_stage(s,[p])] if special else [v for s in stages for v in expected_stage(s,positions)]
        captures=[e['payload'] for e in events if prop(e['payload'],'relativeKey') and prop(e['payload'],'stepSequence') and prop(e['payload'],'camera') in ('A','B','C','D')]
        actual=[(prop(p,'camera'),prop(p,'localFace'),prop(p,'objectId').split(':',1)[1]) for p in captures]
        checks['complete_camera_entity_face_sequence']=actual==expected
        kinds=[prop(p,'kind') for _,p in writes]
        checks['one_initial_scan_and_decode']=kinds.count('Capture3D')==kinds.count('CaptureF')==1 and 'RescanWholeTray' not in kinds
        e_count=len(positions) if recipe.get('eCode',{}).get('enabled') else 0
        face_count=len(set((face,identity) for _,face,identity in expected))
        checks['complete_media_and_worker_intents']=len(media)==2+len(expected)+e_count and sum(k=='AlgorithmIntent' for k,_ in writes)==2+len(expected)+face_count+e_count
        checks['final_and_all_saves']=run['state']==26 and run['finalOutcome']==1 and not run.get('errorCode') and all(
            any(e['stage']==s and e['eventType']==k and not e['error'] for e in events) for s,k in (
                ('Detection','Completed'),('UnloadPreparation','Completed'),('Sorting','Completed'),
                ('UnlockObservation','ObservedUnlocked'),('ManualTrayRemovalConfirmation','FinalUnloadCompleted')))
        transfers=sorting_commits([(e['eventId'],e['payload']) for e in events if e['stage']=='Sorting'],run_id)
        checks['current_pick_save_before_transfer']=all(any(m.get('entityId')==t['entityId'] and m.get('state')=='Completed' and
            str(m.get('committedEventId','')).lower()==t['placeEventId'].lower() for m in run.get('movements',[])) for t in transfers)
        checks['no_unknown_held']=not any(e['eventType']=='UnknownHeld' for e in events)
        context=run.get('resultContext') or {}
        current=next((r for r in run.get('results',[]) if r.get('kind')==context.get('kind') and r.get('id')==context.get('id')),None)
        media_ids={str(prop(e['payload'],'mediaId')).lower() for e in events if prop(e['payload'],'mediaId')}
        checks['real_inspection_media_identity']=bool(current and current.get('inspections') and all(
            inspection.get('mediaIds') and all(str(identity).lower() in media_ids for identity in inspection['mediaIds']) for inspection in current['inspections']))
        checks['missing_detail_not_fabricated']=bool(current and all(
            i.get('confidence') is None and i.get('defects') is None for i in current.get('inspections',[])
            if i.get('detailAvailability',{}).get('confidence')=='NotProvided' and i.get('detailAvailability',{}).get('defects')=='NotProvided'))
        if case=='L05':
            physical=[r for r in run['results'] if r['kind']=='Single']
            p03=[r for r in physical if r['id'].endswith(':P:P03:M01')]
            p01=[r for r in physical if r['id'].endswith(':P:P01:M01')]
            movements=run.get('movements',[])
            occupied={e['eventId'].lower() for e in events if e['stage']=='Sorting' and e['eventType']=='Completed' and prop(e['payload'],'kind')=='SortingAssignmentOccupied'}
            checks['required_real_disposition']=len(transfers)==1 and len(p03)==1 and p03[0]['disposition']=='Pending' and p03[0].get('dispositionState')=='Completed' and len(movements)==1 and movements[0]['entityId']==p03[0]['id'] and movements[0].get('physicalSlotIndex')==3 and movements[0].get('sourcePointRef')=='P03' and movements[0].get('targetPointRef')=='P15' and movements[0].get('state')=='Completed' and movements[0].get('committedEventId','').lower() in occupied
            checks['ok_retained_without_face_movement']=len(p01)==1 and p01[0]['disposition']=='OK' and p01[0].get('dispositionState')=='NoMoveRequired' and all(r.get('dispositionState') is None for r in run['results'] if r['kind']=='Face')
        if case=='L10':
            old=facts['faultRun'];old_id=old['runId'].lower();next_receipt=load(root/'fault-restart-receipt.json')['receipt']
            with sqlite3.connect(database.resolve().as_uri()+'?mode=ro',uri=True) as db:
                old_writes=[(kind,json.loads(payload)) for kind,payload in db.execute('SELECT Kind,PayloadJson FROM Writes WHERE lower(RunId)=? ORDER BY Revision',(old_id,))]
            old_kinds=[prop(payload,'kind') for _,payload in old_writes]
            checks['explicit_complete_new_identity']=old_id!=run_id and next_receipt['runId'].lower()==run_id and load(root/'receipt.json')['runId'].lower()==old_id and old['faultRestart']['newRunId'].lower()==run_id and run['faultRestart']['faultRunId'].lower()==old_id
            checks['old_fault_retained_not_resumed']=old['state']==22 and old['finalOutcome']==0 and all(kind in old_kinds for kind in ('FailedMoveRecoveryRequired','RecoveryOldExecutionClosed','RecoveryResetObserved','RecoveryInitialCheckAccepted','RecoveryNewRunLinked')) and 'RecoverySingleResendAuthorized' not in old_kinds and not any(kind=='AlgorithmIntent' for kind,_ in old_writes)
            checks['new_run_retains_original_load']=load(root/'explicit-new-run-request.json')['contextJson']==load(next(root.glob('load-*.json')))['request']['contextJson'] and 'RecoveryFromFaultRun' in kinds
        if special:
            actions=[prop(e['payload'],'result',{}) for e in events if prop(e['payload'],'kind') in ('SpecialActionConfirmed','SpecialExitCompleted')]
            exits=[a for a in actions if prop(prop(a,'request',{}),'kind')=='Exit']
            checks['actual_special_actions_and_free_exit']=len(actions)==len(positions)*(2+len(stages)) and len(exits)==len(positions) and all(
                prop(prop(a,'evidence',{}),'meaning')=='MaterialTransferred' and prop(prop(a,'evidence',{}),'isCorrelated') is True and
                prop(prop(a,'evidence',{}),'correlation')==prop(prop(a,'request',{}),'correlation') and prop(prop(a,'evidence',{}),'diagnosticEvidenceReferences') for a in actions) and all(not prop(a,'occupiedEntityId') for a in exits)
        if case=='L08':
            manual=[e['payload'] for e in events if prop(e['payload'],'kind')=='ManualFaceEstablished']
            confirmation=load(root/'manual-confirmation.json');prompt=load(root/'manual-prompt.json')['pending']
            checks['manual_confirmation_current_face_and_source']=len(manual)==1 and confirmation['adoptedFace']==prompt['targetFace'] and confirmation['faceSource']=='ManualConfirmed/CommandDefault' and manual[0]['sensorMeasuredFace'] is False and manual[0]['evidence']['face']['source']=='CommandDefaultManualConfirmed'
            checks['manual_area_cleared_before_final']=facts['status']['plc']['manualArea']=='Clear' and not any(prop(e['payload'],'kind')=='FaceEstablished' for e in events)
    elif case.startswith(('F01-','F02-','F03-','F04-')):
        close=load(root/'fault-run-at-close.json')
        checks['current_failure_retained_after_late_observation']=bool(run.get('errorCode')) and run['errorCode']==close['errorCode'] and run['state']==close['state'] and run['finalOutcome']==close['finalOutcome']==0
        checks['no_final_or_unknown_transfer_success']=not any(e['eventType'] in ('FinalUnloadCompleted','ObservedUnlocked') or prop(e['payload'],'kind')=='SortingAssignmentOccupied' for e in events)
        if case.startswith('F01-') or case=='F03-uncertain':
            checks['failed_unload_holds_without_sorting']=failed_unload_holds(events)
        if case=='F02-position':
            checks['wrong_position_never_approves_pick_commit']=any(e['stage']=='Sorting' and e['eventType']=='UnknownHeld' for e in events) and not any(prop(e['payload'],'kind')=='SortingAssignmentInTransit' for e in events)
        if case in ('F02-face','F04-handshake'):
            captures=[e['payload'] for e in events if prop(e['payload'],'relativeKey') and prop(e['payload'],'stepSequence')]
            checks['no_dependent_second_face_capture']=bool(captures) and not any(prop(p,'localFace')==2 for p in captures)
            checks['detection_not_complete']=not any(e['stage']=='Detection' and e['eventType']=='Completed' for e in events)
        if case=='F04-reset':
            kinds=[prop(p,'kind') for _,p in writes]
            checks['public_3d_saved_but_no_f_or_binding']=kinds.count('Capture3D')==1 and 'CaptureF' not in kinds and 'RecipePlanAndBindingIntent' not in kinds and not events
    elif case.startswith(('F05-','F06-')):
        seam=[json.loads(line) for line in (store/'009-fault-events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
        hit=[x for x in seam if x['runId'].lower()==run_id and x['caseId']==case]
        identities={x['identity'] for x in hit}
        checks['actual_fault_hit_current_run_once']=len(identities)==1 and len({x['nonce'] for x in hit})==1
        checks['failed_without_final_or_transfer']=bool(run.get('errorCode')) and run['finalOutcome']==0 and not any(prop(e['payload'],'kind')=='SortingAssignmentOccupied' for e in events)
        if case!='F06-C':checks['unknown_held_persisted']=any(e['eventType']=='UnknownHeld' for e in events)
        transit=[e for e in events if prop(e['payload'],'kind')=='SortingAssignmentInTransit']
        if case=='F05-A':
            checks['actual_precommit_rejection']=any(x['phase']=='SqliteRejectionInstalledBeforeInsert' for x in hit) and not transit and any(x['step']=='StageEventSave' and x['outcome']=='RollbackConfirmed' and prop(x['facts'],'actualCommit')=='ConfirmedRolledBack' and prop(x['facts'],'eventId','').lower() in {i.lower() for i in identities} for x in logs)
        elif case in ('F05-B','F05-C'):
            checks['actual_commit_not_rollback']=len(transit)==1 and transit[0]['eventId'].lower() in {i.lower() for i in identities} and any(x['phase']=='ActualCommitCompleted' for x in hit)
        else:
            checks['raw_failure_not_successful_intransit']=not transit
            checks['observed_pick_not_disclaimed']=any(prop(e['payload'],'kind')=='PickEvidencePersistenceUnconfirmed' for e in events) or case=='F06-C' and any(prop(e['payload'],'kind')=='SortingAssignmentsReserved' for e in events)
        if case.endswith('-C'):checks['actual_store_unavailable_interval']=any(x['phase']=='ActualExclusiveLockAcquired' for x in hit)
        if case=='F06-C':
            before=load(root/'recovery-input.json');restart=load(root/'restart-facts.json')
            checks.update(recovery_checks(before,recovery_input(root,store,run_id),restart,hashlib.sha256(Path(collected['hostDll']).read_bytes()).hexdigest()))
            checks['no_new_pick_fact_claimed_during_outage']=not any(prop(json.loads(row['payload']),'kind') in ('PickEvidencePersistenceUnconfirmed','SortingAssignmentInTransit') for row in before['rows'])
            restart_logs=read_runtime(store/'logs/host.restart.out.log')
            checks['actual_restarted_worker_ready']=all(any(x['step']=='WorkerProcess' and x['outcome']==phase and x['processId']==restart['afterHostPid'] and prop(x['facts'],'processId')==restart['afterWorkerPid'] for x in restart_logs) for phase in ('Started','Ready'))
    else:raise ValueError('BusinessCaseNotImplemented:'+case)
    evidence=['sqlite','current-api','worker','media','persistent-diagnostics'] if all(checks.values()) else []
    return dict(scope='BusinessOnly;IndependentBackend;NoPageClaim',caseId=case,runId=run_id,checks=checks,
        passed=all(checks.values()),evidenceKinds=evidence,database=str(database),media=media_proof)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('root',type=Path)
    parser.add_argument('--snapshot-recovery-input',action='store_true');parser.add_argument('--store',type=Path);parser.add_argument('--run-id')
    args=parser.parse_args()
    if args.snapshot_recovery_input:
        if args.store is None or not args.run_id:raise ValueError('ActualRecoveryStoreAndRunRequired')
        with (args.root/'recovery-input.json').open('x',encoding='utf-8') as output:
            json.dump(recovery_input(args.root,args.store,args.run_id),output,ensure_ascii=False,indent=2)
        return 0
    result=inspect(args.root.resolve())
    with (args.root/'business-validation.json').open('x',encoding='utf-8') as output:json.dump(result,output,ensure_ascii=False,indent=2)
    print(json.dumps(result));return 0 if result['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
