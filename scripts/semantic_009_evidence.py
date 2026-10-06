"""Finite business evidence checks; never imports protocol definitions or probes."""
def prop(value, name, default=None):
    return next((v for k, v in value.items() if k.lower() == name.lower()), default) if isinstance(value, dict) else default


def failed_unload_holds(events):
    """Allow the actual pre-unload reservation; reject any sorting execution.

    ThreeStageWorkflowExecutor reserves assignments before requesting unload.
    A persisted reservation is required conservative occupancy, not a pick.
    """
    started=next((i for i,e in enumerate(events) if e['stage']=='UnloadPreparation' and e['eventType']=='IntentRecorded'),None)
    held=[e for e in events if e['stage']=='UnloadPreparation' and e['eventType']=='UnknownHeld' and e.get('error')]
    sorting=[(i,e) for i,e in enumerate(events) if e['stage']=='Sorting']
    return (started is not None and bool(held) and
            not any(e['stage']=='UnloadPreparation' and e['eventType']=='Completed' for e in events) and
            all(i<started and e['eventType']=='IntentRecorded' and not e.get('error') and
                prop(e['payload'],'kind')=='SortingAssignmentsReserved' and bool(prop(e['payload'],'assignments'))
                for i,e in sorting))


def recovery_checks(before, after, restart, build_digest):
    """Actual SQLite rows before/after a Host restart; no physical fact inferred."""
    previous={row['eventId']:row for row in before['rows']}
    current={row['eventId']:row for row in after['rows']}
    run=restart['run']
    return {
        'recovery_same_run_and_build':before['runId']==after['runId']==run['runId'].lower() and restart['hostDllSha256']==build_digest,
        'actual_new_host_and_worker':len({restart['beforeHostPid'],restart['afterHostPid'],restart['afterWorkerPid'],restart['plcPid']})==4 and bool(restart['startedUtc']),
        'prior_committed_intent_retained':bool(previous) and any(row['stage']=='Sorting' and row['eventType']=='IntentRecorded' for row in previous.values()) and all(current.get(key)==row for key,row in previous.items()),
        'real_conservative_reconciliation':any(row['eventId'] not in previous and row['stage']=='Sorting' and row['eventType']=='UnknownHeld' and row.get('error')=='RecoveryInFlight' and row.get('source')=='Fallback' for row in current.values()),
        # Existing public RunState.RecoveryRequired; not a device encoding.
        'recovered_run_has_no_continuation':run['state']==22 and run['finalOutcome']==0 and not run['allowedActions'],
    }


def sorting_commits(events, run_id):
    """Rows are committed SQLite events in Sequence order, not callback claims."""
    picked, completed = {}, []
    for event_id, payload in events:
        if prop(payload, 'schemaVersion') != 'sorting-evidence/1':
            continue
        kind = prop(payload, 'kind')
        if kind not in {'SortingAssignmentInTransit', 'SortingAssignmentOccupied'}:
            continue
        evidence, assignment = prop(payload, 'evidence', {}), prop(payload, 'assignment', {})
        correlation = prop(evidence, 'correlation', {})
        action = prop(correlation, 'actionId')
        if not action or str(prop(correlation, 'runId')).lower() != run_id.lower():
            raise ValueError('SortingCommitIdentity')
        if prop(correlation, 'objectId') != prop(assignment, 'objectId') or not prop(evidence, 'diagnosticEvidenceReferences'):
            raise ValueError('SortingCommitObjectOrEvidence')
        if kind == 'SortingAssignmentInTransit':
            reached = prop(evidence, 'sourcePositionReached', {})
            if prop(reached, 'matched') is not True or prop(reached, 'target') != prop(assignment, 'sourcePoint'):
                raise ValueError('PickSourceNotProven')
            if prop(reached, 'correlation') != correlation or action in picked:
                raise ValueError('PickIdentityOrDuplicate')
            picked[action] = (correlation, assignment, event_id)
        else:
            prior = picked.get(action)
            if not prior or prior[:2] != (correlation, assignment) or prop(evidence, 'meaning') != 'MaterialTransferred':
                raise ValueError('TransferWithoutCurrentCommittedPick')
            positions = prop(evidence, 'positions', [])
            for point in [prop(assignment, 'sourcePoint'), prop(assignment, 'targetPoint')]:
                if not any(prop(p, 'target') == point and prop(p, 'matched') is True and
                           prop(p, 'correlation') == correlation for p in positions):
                    raise ValueError('TransferPositionNotProven')
            completed.append(dict(actionId=action, pickEventId=prior[2], placeEventId=event_id,
                                  entityId=prop(assignment, 'objectId')))
    if len(picked) != len(completed) or len({c['actionId'] for c in completed}) != len(completed):
        raise ValueError('IncompleteOrDuplicateTransfer')
    return completed
