"""Finite 013 comparison of actual transport/SQLite evidence; no replay or threshold tuning."""
import collections
import datetime as dt
import json
import ipaddress
import math
from pathlib import Path
import sqlite3

ACCEPTANCE_VERSION = "013-acceptance/2"
FIRST_STATE_GROUPS = {"X", "F:first", "U:first"}
ORDINARY_GROUPS = {"B", "P", "F", "U", "H", "T"}


def performance_only(finding):
    """Finite stakeholder-approved classification; unknown findings stay blocking."""
    if finding in {"SinglePdu25ms", "CommandEligibilityToSend50ms",
                   "HostObservationToEcho50ms", "DeviceEdgeToEcho400ms"}:
        return True
    prefix, _, group = finding.partition(":")
    if prefix in {"CompletePublication", "DueToFirstSend", "DemandComplete"}:
        return group in ORDINARY_GROUPS | FIRST_STATE_GROUPS
    if prefix in {"ActualInterval", "ConservativeAge"}:
        return group in ORDINARY_GROUPS
    if prefix == "SharedBlockInterval":
        return group in {str(k) for k in ((1,3,2),(1,7,1),(1,15,1),(1,33,5),
                                        (3,79,2),(3,127,5),(3,12,6),(3,132,4))}
    return False


def metric(values, limits):
    values = list(values)
    limits = [limits] * len(values) if isinstance(limits, (int, float)) else list(limits)
    if len(values) != len(limits):
        raise ValueError("MetricSampleThresholdCountMismatch")
    return dict(stats(values), limitsMs=sorted(set(limits)),
                exceeded=sum(v > limit for v, limit in zip(values, limits)),
                measurement="Measured" if values else "NotMeasured")


def target_classification(targets):
    # Called once after all after-only measurements, including device heartbeat receipts.
    findings = sorted(set(targets["errors"]))
    observations = [e for e in findings if performance_only(e)]
    targets.update(acceptanceVersion=ACCEPTANCE_VERSION, observedFindings=findings,
        errors=[e for e in findings if not performance_only(e)],
        performanceObservations=dict(result="TargetsNotMet" if observations else "NoMeasuredDeviation",
            findings=observations, metrics=targets.get("performanceMetrics", {}),
            notMeasured=["FailureToDiagnosticRecord25ms"] ))
    targets["result"] = "Failed" if targets["errors"] else "Passed"
    return targets


def closure_decision(comparison, components, lightweight, n3, evidence_errors=()):
    """Final profile decision after real ledger parsing and L, also used by offline review.

    This does not issue an L credential or replace identity/ledger validation.
    Callers must supply the actual gate results, retaining original execution identities.
    """
    evidence = list(evidence_errors) + comparison.get("inputErrors", [])
    for label, checked in (("Components", components), ("Lightweight", lightweight)):
        if (checked.get("result") != "Passed" or checked.get("errors")
                or not checked.get("requiredCount")
                or checked.get("observedCount") != checked.get("requiredCount")):
            evidence.append(label + "EvidenceRejected")
    if lightweight.get("requiredCount") != 70:
        evidence.append("CompleteLightweight70Required")
    removed = n3.get("removedCase")
    if (not removed or n3.get("positive") != components or
            n3.get("rejected", {}).get("result") != "Rejected" or
            n3.get("rejected", {}).get("errors") != ["MissingOrDuplicateExecution:" + removed]):
        evidence.append("N3RealPositiveThenMissingRowRequired")
    passed = comparison.get("passed") is True and not evidence
    observations = comparison.get("after", {}).get("afterTargets", {}).get("performanceObservations", {})
    correctness = dict(comparison.get("correctness", {"result":"NotEvaluated"}),
        protectionComponents=components.get("result", "NotRun"))
    if components.get("result") != "Passed": correctness["result"] = "NotQualified"
    return dict(acceptanceVersion=ACCEPTANCE_VERSION, passed=passed,
        correctness=correctness,
        loadReduction=comparison.get("loadReduction", {"result":"NotEvaluated"}),
        performanceObservations=observations,
        evidenceIntegrity=dict(result="Failed" if evidence else "Passed", errors=evidence),
        softwareClosure="ClosedWithPerformanceAndFieldLimitations" if passed else "Rejected",
        hardErrors=comparison.get("errors", []) + evidence,
        fieldLimitations=comparison.get("formalPlc"))


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def stage_trace_path(directory, pid, component):
    current = Path(directory) / f"stages-{pid}-{component}.json"
    historical = Path(directory) / f"stages-{pid}.json"
    if current.exists() and historical.exists():
        raise ValueError("StageTraceIdentityAmbiguous")
    if current.exists():
        payload = read(current)
        if payload.get("pid") != pid or payload.get("component") != component:
            raise ValueError("StageTraceIdentityMismatch")
        return current
    return historical  # Read-only historical evidence; no product runtime fallback.


def instant(value):
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))


def transport_endpoint(value):
    address, port = value.rsplit(":", 1)
    address = ipaddress.ip_address(address.strip("[]"))
    return str(getattr(address, "ipv4_mapped", None) or address), int(port)


def device_exchange_key(record, host=False):
    local, remote = transport_endpoint(record["localEndpoint"]), transport_endpoint(record["remoteEndpoint"])
    return ((local, remote) if host else (remote, local)) + tuple(record[k] for k in ("transaction", "function", "offset", "count"))


def match_device_exchange(host, index, bindings=None):
    candidates = [d for d in index.get(device_exchange_key(host, host=True), [])
        if host["requestWriteStartedTick"] <= d["receivedTick"] <= d["responseWriteStartedTick"] <= host["responseEnded"]
        and d["responseWriteStartedTick"] <= d["responseSentTick"]]
    if len(candidates) != 1:
        raise ValueError("DeviceExchangeCandidateCount:" + str(len(candidates)))
    device = candidates[0]
    if bindings is not None:
        expected = bindings.setdefault(host["connectionId"], device["connectionId"])
        if expected != device["connectionId"]:
            raise ValueError("DeviceConnectionIdentityChanged")
    return device


def heartbeat_edge_receipts(edges, echoes, uncertainty_ms):
    """Both boundaries use the device's actual UTC records; retain the measured clock residual.

    Never use a converted edge QPC to discard a real response near an anchor boundary,
    or borrow the next same-valued response after another device transition.
    """
    ordered = sorted(edges, key=lambda e: instant(e["occurredAtUtc"]))
    delays, associations, errors = [], [], []
    for i, edge in enumerate(ordered):
        start = instant(edge["occurredAtUtc"])
        stop = instant(ordered[i + 1]["occurredAtUtc"]) if i + 1 < len(ordered) else None
        matches = [e for e in echoes if e["value"] == (65280 if edge["current"] == "1" else 0)
            and instant(e["receivedAtUtc"]) >= start
            and (stop is None or instant(e["receivedAtUtc"]) < stop)]
        if not matches:
            errors.append("DeviceEdgeResponseMissing:" + str(edge["sequence"]))
            continue
        first = min(matches, key=lambda e: instant(e["receivedAtUtc"]))
        delay = (instant(first["receivedAtUtc"]) - start).total_seconds() * 1000 + uncertainty_ms
        delays.append(delay)
        associations.append(dict(edgeSequence=edge["sequence"], edgeUtc=edge["occurredAtUtc"],
            connectionId=first["connectionId"], transaction=first["transaction"], receivedAtUtc=first["receivedAtUtc"],
            delayMs=delay, uncertaintyMs=uncertainty_ms))
    return delays, associations, errors


def stats(values):
    values = sorted(values)
    return dict(count=len(values), maximum=max(values, default=0),
                mean=sum(values) / len(values) if values else 0,
                p95=values[min(len(values)-1, math.ceil(len(values)*.95)-1)] if values else 0)


def continuous_owner(group, previous, current, policies, old_tick, tick):
    # A policy change in an unrelated group cannot erase this group's gap.
    if group in ("B", "H"):
        return previous.get("epoch") == current.get("epoch")
    if "generation" in current and "generation" in previous:
        return (previous["generation"], previous["epoch"]) == (current["generation"], current["epoch"])
    stages = [p for p in policies if old_tick < p["tick"] <= tick]
    key = {"X":"axis", "F":"flip", "F:first":"flipFast", "U":"putBack", "U:first":"putBackFast", "T":"transfer"}.get(group)
    return not any(p["transfer"] for p in stages) if group == "P" else not any(not p[key] for p in stages)


def side(root, name):
    folder = Path(root) / name / "full-run"
    pid = read(folder / "host-process.json")["hostPid"]
    measured = read(folder / "measurement" / f"transport-{pid}.json")
    idle = read(folder / "idle-window.json")
    activity = read(folder / "activity-window.json")
    frequency = measured["frequency"]
    ms = lambda ticks: ticks * 1000 / frequency
    tick = lambda utc: activity["startTick"] + round((instant(utc)-instant(activity["startUtc"])).total_seconds()*frequency)
    errors = []
    if measured["dropped"]: errors.append("MeasurementOverflow")
    if abs((idle["endTick"]-idle["startTick"])/frequency - 60) > .000001: errors.append("IdleWindowNot60Seconds")
    database = folder / "store/station01.test.db"
    with sqlite3.connect(database.resolve().as_uri() + "?mode=ro", uri=True) as db:
        run = db.execute("select RunId,State,Terminal,CreatedUtc from Runs").fetchall()
        final = db.execute("select PersistedUtc from StageEvents where EventType='FinalUnloadCompleted'").fetchall()
        if len(run) != 1 or run[0][1:3] != ("Completed", "Completed") or len(final) != 1:
            raise ValueError("CommonBusinessCompletionMissing:"+name)
        start, end = tick(run[0][3]), tick(final[0][0])
        packets = db.execute("select ActionId,RawPayloadJson from PlcCommunicationEvidence where RunId=?", (run[0][0],)).fetchall()
        docs = [json.loads(payload) for _, payload in packets]
        if any(doc["gap"] for doc in docs): errors.append("RawEvidenceGap")
        if not packets: errors.append("RawEvidenceMissing")
        evidence = dict(rows=len(packets), bytes=sum(len(payload.encode()) for _, payload in packets),
                        rawExchanges=sum(len(d["exchanges"]) for d in docs))
        by_action = collections.defaultdict(lambda: dict(completed=0, evidenceBytes=0, rawExchanges=0))
        for (_, payload), doc in zip(packets, docs):
            item = by_action[doc["interpretation"]]
            item["completed"] += 1; item["evidenceBytes"] += len(payload.encode()); item["rawExchanges"] += len(doc["exchanges"])
        for item in by_action.values(): item["transactionsPerCompletedUnit"] = item["rawExchanges"]/item["completed"]
    obligations = read(folder / "obligations.json")
    if obligations.get("softwareObligationsComplete") is not True or obligations.get("algorithmFacts") != 9 or obligations.get("mediaCount") != 7:
        errors.append("FrozenRun2CompletionObligationsMissing")
    points = [p for p in measured["points"] if p["Sent"] > 0]
    stage_path = stage_trace_path(folder / "measurement", pid, "Gaode.Infrastructure")
    stages = read(stage_path) if stage_path.exists() else None
    cycles=[]
    if stages:
        if stages["dropped"]: errors.append("StageTimingOverflow")
        actual = {e["facts"]["requestWriteStartedTick"]:e["facts"] for e in stages["entries"] if e["kind"]=="host-exchange"}
        for p in points:
            if p["Sent"] in actual:
                p["ResponseCollected"] = actual[p["Sent"]]["responseEnded"]
        cycles=[e["facts"] for e in stages["entries"] if e["kind"]=="heartbeat-cycle"]
        plc_pid=read(folder / "processes.json")["plcPid"]
        plc_path=stage_trace_path(folder / "measurement", plc_pid, "VirtualPlc")
        if plc_path.exists():
            plc=read(plc_path)
            if plc["dropped"]:errors.append("DeviceStageTimingOverflow")
            device = collections.defaultdict(list)
            for entry in plc["entries"]:
                if entry["kind"] == "plc-exchange":
                    device[device_exchange_key(entry["facts"])].append(entry["facts"])
            bindings = {}
            for p in points:
                h=actual.get(p["Sent"])
                if h:
                    try:
                        d = match_device_exchange(h, device, bindings)
                        p["DeviceReceived"] = d["receivedTick"]
                        p["DeviceReceipt"] = dict(value=d["count"], receivedAtUtc=d["receivedAtUtc"],
                            connectionId=d["connectionId"], transaction=d["transaction"])
                    except ValueError as error:
                        if idle["startTick"] <= p["Sent"] < end:
                            errors.append(str(error))
    def window(a, b):
        selected = [p for p in points if a <= p["Sent"] < b]
        return dict(requests=len(selected), rate=len(selected)/((b-a)/frequency),
                    failed=sum(not p["Success"] for p in selected),
                    byChannel=dict(collections.Counter(p["Channel"] for p in selected)),
                    bySource=dict(collections.Counter(p["Source"] for p in selected)),
                    queueMs=stats(ms(p["Sent"]-p["Enqueued"]) for p in selected),
                    exchangeMs=stats(ms(p["Ended"]-p["Sent"]) for p in selected),
                    responseBytes=sum(p["ResponseBytes"] for p in selected),
                    crossingResponses=sum(p["Ended"] >= b for p in selected))
    result = dict(side=name, commonErrors=errors, durationMs=ms(end-start),
                  boundaries=dict(startUtc=run[0][3], finalPersistedUtc=final[0][0], startTick=start, endTick=end,
                      reconciliation="Actual Host persisted run admission through SQLite Final commit; UTC mapped to the same-machine monotonic anchor"),
                  idle=window(idle["startTick"], idle["endTick"]), activity=window(start,end),
                  evidence=evidence, actions=dict(by_action), counters=measured["counters"],
                  actionAccounting="Per-action raw windows overlap shared collection; never add them to the unique whole-flow count.")
    if result["idle"]["failed"] or result["activity"]["failed"]: errors.append("ActualFailedExchange")
    io_limit = read(Path(root) / "inputs" / name / "config/budget.json")["businessMs"]["plcIo"]
    result["originalIoIncludingQueueMs"] = metric(
        (ms(p["Ended"]-p["Enqueued"]) for p in points if idle["startTick"] <= p["Sent"] < end), io_limit)
    if result["originalIoIncludingQueueMs"]["exceeded"]:
        errors.append("OriginalIoDeadlineExceeded")
    if name == "after":
        result["afterTargets"] = after_targets(measured, points, idle, start, end, frequency, cycles)
        if stages:
            result["stageTiming"] = dict(schema=stages["schema"], dropped=stages["dropped"], records=len(stages["entries"]),
                recordingMs=ms(stages["recordingTicks"]), maxRecordingMs=ms(stages["maxRecordingTicks"]),
                responseCollectionMs=stats(ms(p["ResponseCollected"]-p["Sent"]) for p in points if "ResponseCollected" in p),
                note="Additive after-only internal timing. Original neutral Ended/service-end and all common count/SQLite boundaries retained.")
        changes=read(folder / "plc-changes.json")
        targets=result["afterTargets"]
        end_error=abs(ms(activity["endTick"]-activity["startTick"])-(instant(activity["endUtc"])-instant(activity["startUtc"])).total_seconds()*1000)
        edges=[c for c in changes["changes"] if c["name"]=="PLC_Heartbeat_Req" and idle["startTick"]<=tick(c["occurredAtUtc"])<end]
        echoes = [p for p in points if p["Channel"] == "heartbeat" and p["Function"] == 5]
        if any("DeviceReceipt" not in p for p in echoes):
            targets["errors"].append("DeviceEchoReceiptMissing")
        delays, associations, missing = heartbeat_edge_receipts(edges,
            [p["DeviceReceipt"] for p in echoes if "DeviceReceipt" in p], end_error)
        targets["errors"].extend(missing)
        targets["deviceHeartbeatAssociations"] = associations
        if changes["gap"] or not edges:targets["errors"].append("DeviceHeartbeatTimelineUnavailable")
        if max(delays,default=0)>400:targets["errors"].append("DeviceEdgeToEcho400ms")
        targets["deviceEdgeToEchoMs"]=stats(delays)
        targets["performanceMetrics"]["DeviceEdgeToEcho400ms"] = metric(delays, 400)
        targets["sameMachineUtcAnchorResidualMs"]=end_error
        target_classification(targets)
    return result


def after_targets(measured, points, idle, start, end, frequency, cycles=()):
    errors=[]; ms=lambda ticks:ticks*1000/frequency
    samples = collections.defaultdict(list)
    def observe(name, value, limit):
        samples[name].append((value, limit))
        if value > limit: errors.append(name)
    selected=[p for p in points if idle["startTick"] <= p["Sent"] < idle["endTick"]]
    budgets={"B":726,"P":122,"H":201,"H:echo":61}
    counts=collections.Counter(p["Source"] for p in selected)
    for source,limit in budgets.items():
        if counts[source]>limit:errors.append(f"IdleBudget:{source}:{counts[source]}>{limit}")
    if set(counts)-set(budgets):errors.append("IdleContainsDemandOrUnfinishedWork:"+str(dict(counts)))
    if len(selected)>1110:errors.append("IdleTotalBudgetExceeded")
    counters=measured["counters"]
    if counters.get("AdmissionPreparations") != 1:errors.append("MappingPreparationCount")
    for counter in ("LoopPlanPreparations","RepeatedCacheWakeups"):
        if counters.get(counter,0)!=0:errors.append(counter)
    normal=[p for p in points if idle["startTick"] <= p["Sent"] < end]
    for p in normal:
        observe("SinglePdu25ms", ms(p.get("ResponseCollected",p["Ended"])-p["Sent"]), 25)
        if p["Channel"] == "business" and p["Function"] in (5,6,16):
            observe("CommandEligibilityToSend50ms", ms(p["Sent"]-p["Enqueued"]), 50)
    policies=measured.get("policies",[])
    if not policies:errors.append("PolicyOwnershipTimelineMissing")
    def policy(tick):
        choices=[p for p in policies if p["tick"]<=tick]
        return choices[-1] if choices else dict(basic=500,position=1000,axis=False,flip=False,flipFast=False,putBack=False,putBackFast=False,transfer=False)
    timing=collections.defaultdict(list); previous={}
    rounds=[r for r in measured.get("rounds",[]) if idle["startTick"]<=r["published"]<end]
    for r in rounds:
        group=r["group"]; all_blocks=r["blocks"]
        blocks=sorted((b for b in all_blocks if b["Queued"]>=r["queued"]),key=lambda b:b["Sent"])
        if not blocks:errors.append("RoundActualBlocksMissing");continue
        first=blocks[0]["Sent"]; last=blocks[-1]["Ended"]; p=policy(first)
        full=ms(r["published"]-first); lateness=ms(first-r["due"])
        waits=sum(ms(b["Sent"]-a["Ended"]) for a,b in zip(blocks,blocks[1:]))
        timing[group].append(dict(dueToQueueMs=ms(r["queued"]-r["due"]),queueToSendMs=ms(first-r["queued"]),
            firstToPublishMs=full,interBlockWaitMs=waits,publicationMs=ms(r["published"]-last),
            oldestDependencyAgeMs=ms(r["published"]-min(b["Sent"] for b in all_blocks))))
        limit=150 if group=="B" else 50 if group=="P" else 75 if group=="T" else 25
        observe(f"CompletePublication:{group}", full, limit)
        # A demand round is accounted separately by its actual PDU source.
        demand=r["source"].startswith("D:") if "source" in r else any(q["Source"].startswith("D:") and q["Sent"]==first for q in points)
        if not demand: observe(f"DueToFirstSend:{group}", lateness, 25)
        if demand:
            goal=200 if group=="B" else 100 if group=="P" else 75
            observe(f"DemandComplete:{group}", ms(r["published"]-r["queued"]), goal)
        period=p["basic"] if group=="B" else p["position"] if group=="P" else 300 if group=="H" else 50 if group=="X" or group.endswith(":first") else 200
        if group in previous:
            old_first,old_publish,old_policy,old_period,old_round=previous[group]
            continuous=continuous_owner(group,old_round,r,policies,old_first,first)
            if continuous: observe(f"ActualInterval:{group}", ms(first-old_first), max(period,old_period)+25)
            if continuous and group in FIRST_STATE_GROUPS:
                observe(f"FirstStateInterval75:{group}", ms(r["published"]-old_first), 75)
            age=375 if group=="B" and period==200 else 675 if group=="B" else 575 if group=="P" and period==500 else 1075 if group=="P" else 350 if group=="H" else 300 if group=="T" else 250
            if continuous and group not in FIRST_STATE_GROUPS:
                observe(f"ConservativeAge:{group}", ms(r["published"]-old_first), age)
        previous[group]=(first,r["published"],p,period,r)
    for group in ("B","P","H"):
        if not timing[group]:errors.append("MissingGroupTiming:"+group)
    # Shared fields remain continuously sampled across B/X and P/T ownership.
    # These are the frozen Test layout's complete readable blocks, not business constants.
    shared={}; block_rows={}
    for q in sorted(normal,key=lambda q:q["Sent"]):
        if q["Channel"]!="business":continue
        key=(q["Function"],q["Offset"],q["CountOrValue"])
        if key not in ((1,3,2),(1,7,1),(1,15,1),(1,33,5),(3,79,2),(3,127,5),(3,12,6),(3,132,4)):continue
        p=policy(q["Sent"])
        period=(200 if p["transfer"] else p["position"]) if key[0:2] in ((3,12),(3,132)) else 50 if key==(3,127,5) and p["axis"] else p["basic"]
        if key in shared:
            old,old_period=shared[key]; interval=ms(q["Sent"]-old["Sent"])
            block_rows.setdefault(str(key),[]).append(interval)
            observe("SharedBlockInterval:"+str(key), interval, max(period,old_period)+25)
        shared[key]=(q,period)
    # Actual fixed-route command obligations stay outside product/business constants.
    active=[p for p in points if start<=p["Sent"]<end]
    writes=[p for p in active if p["Channel"]=="business" and p["Function"] in (5,6,16)]
    if len(writes)!=108:errors.append(f"Run2CoreWrites:{len(writes)}!=108")
    starts=[p for p in writes if p["Function"]==5 and p["Offset"] in range(33,38) and p["CountOrValue"]==65280]
    if len(starts)!=33:errors.append("Run2AxisStarts33")
    demand=[p for p in active if p["Channel"]=="business" and p["Function"] in (1,3) and (p["Source"].startswith("D:") or p["Source"]=="K")]
    if len(demand)>283:errors.append("DemandReadBudget283")
    periodic_budget=0; budget_segments=[]
    for i,p in enumerate(policies):
        a=max(start,p["tick"]); b=min(end,policies[i+1]["tick"] if i+1<len(policies) else end)
        if b<=a:continue
        seconds=(b-a)/frequency
        sources={"B":(5 if p["axis"] else 6,p["basic"])}
        if not p["transfer"]:sources["P"]=(2,p["position"])
        if p["axis"]:sources["X"]=(1,50)
        if p["flip"]:sources["F:first" if p["flipFast"] else "F"]=(1,50 if p["flipFast"] else 200)
        if p["putBack"]:sources["U:first" if p["putBackFast"] else "U"]=(1,50 if p["putBackFast"] else 200)
        if p["transfer"]:sources["T"]=(3,200)
        allowance=sum(n*(math.ceil(seconds*1000/period)+1) for n,period in sources.values())
        actual=sum(a<=q["Sent"]<b and q["Source"] in sources for q in active)
        if actual>allowance:errors.append("OwnershipSegmentPeriodicBudget")
        periodic_budget+=allowance
        budget_segments.append(dict(start=a,end=b,sources=sources,budget=allowance,actual=actual))
    hb_limit=math.ceil((end-start)/frequency/.3)+1
    periodic_budget+=hb_limit
    periodic=sum(p["Source"] in ("B","P","X","F","U","T","F:first","U:first","H") for p in active)
    if periodic>periodic_budget:errors.append("ActivePeriodicBudget")
    echo_count=sum(p["Channel"]=="heartbeat" and p["Function"]==5 for p in active)
    failure_count=sum(p["Source"]=="Stop" for p in active)
    buckets=dict(periodic=periodic,demand=len(demand),businessWrite=len(writes)-failure_count,
                 heartbeatEcho=echo_count,failureExtra=failure_count)
    if sum(buckets.values())!=len(active):errors.append("UnclassifiedOrDoubleCountedActualPdu")
    first_state=[]
    for i,p in enumerate(policies):
        old=policies[i-1] if i else {}
        for key,group in (("axis","X"),("flipFast","F:first"),("putBackFast","U:first")):
            if not p[key] or old.get(key):continue
            if not start<=p["tick"]<end:continue
            observed=[r for r in rounds if r["group"]==group and r["published"]>=p["tick"]]
            if not observed:errors.append("FirstStateObservationMissing:"+group);continue
            commands=[q for q in writes if q["Ended"]<=p["tick"] and
                (q["Function"]==5 and q["Offset"] in range(33,38) and q["CountOrValue"]==65280 if group=="X" else
                 q["Function"]==6 and q["Offset"]==95 and q["CountOrValue"]==(1 if group=="F:first" else 2))]
            if not commands:errors.append("FirstStateCommandCorrelationMissing:"+group);continue
            elapsed=ms(observed[0]["published"]-commands[-1]["Ended"]);first_state.append(elapsed)
            observe("ActivationToFirstPublication75ms:"+group, elapsed, 75)
    echoes=[p for p in normal if p["Source"]=="H:echo"]
    echo_latencies=[]
    for p in echoes:
        reads=[q for q in normal if q["Source"]=="H" and q["Ended"]<=p["Sent"]]
        if not reads:errors.append("HeartbeatResponseObservationMissing");continue
        echo_latencies.append(ms(p["Ended"]-reads[-1]["Ended"]))
    if cycles:
        echo_latencies=[ms(c["echoedAt"]-c["observedAt"]) for c in cycles
                        if c["echoed"] and idle["startTick"]<=c["readStarted"]<end]
    for elapsed in echo_latencies: observe("HostObservationToEcho50ms", elapsed, 50)
    return dict(result="Failed" if errors else "Passed",errors=sorted(set(errors)),idleCounts=dict(counts),
        timing={g:{key:stats(v[key] for v in rows) for key in rows[0]} for g,rows in timing.items() if rows},
        commandWrites=len(writes),axisStarts=len(starts),demandReads=len(demand),periodic=periodic,
        periodicBudget=periodic_budget,segments=budget_segments,buckets=buckets,
        activationToFirstPublicationMs=stats(first_state),hostObservationToEchoMs=stats(echo_latencies),
        performanceMetrics={k:metric((v for v,_ in rows),(limit for _,limit in rows))
                            for k,rows in samples.items() if performance_only(k)},
        hardTimingMetrics={k:metric((v for v,_ in rows),(limit for _,limit in rows))
                           for k,rows in samples.items() if not performance_only(k)},
        sharedBlockIntervalsMs={k:stats(v) for k,v in block_rows.items()})


def compare(root):
    before=side(root,"before"); after=side(root,"after")
    errors=[]
    unchanged=[]
    before_input=Path(root)/"inputs/before";after_input=Path(root)/"inputs/after"
    for p in before_input.rglob('*'):
        if not p.is_file():continue
        relative=p.relative_to(before_input);other=after_input/relative
        if not other.is_file():errors.append("AfterInputMissing:"+str(relative));continue
        if p.read_bytes()==other.read_bytes():unchanged.append(str(relative));continue
        if relative.as_posix()=="input-manifest.json":continue
        left=read(p);right=read(other)
        if relative.as_posix()=="config/budget.json":
            if left['schemaVersion']!='1.1' or right['schemaVersion']!='2.0' or left['version']!='1' or right['version']!='2':errors.append('BudgetVersionMapping')
            left['businessMs'].pop('plcPoll');left.pop('schemaVersion');right.pop('schemaVersion');left.pop('version');right.pop('version')
        elif relative.as_posix()=="config/simulation.json":
            if left['schemaVersion']!='1.0' or right['schemaVersion']!='1.0' or left['version']!='1' or right['version']!='2':errors.append('SimulationVersionMapping')
            left.pop('version');right.pop('version');left['budgetRef'].pop('version');right['budgetRef'].pop('version')
        elif relative.name in ('run-1.json','run-2.json'):
            for field in ('budgetRef','simulationRef'):left[field].pop('version');right[field].pop('version')
        else:errors.append('UnapprovedInputDifference:'+str(relative))
        if left!=right:errors.append('InputSemanticDifference:'+str(relative))
    return compare_sides(before, after, errors, unchanged)


def compare_sides(before, after, input_errors=(), unchanged=()):
    """Same final comparison for measured sides and focused decision regression cases."""
    errors = list(input_errors)
    if before["commonErrors"] or after["commonErrors"]:errors.append("CommonQualificationFailed")
    if after["afterTargets"]["result"]!="Passed":errors.append("AfterHardRequirementsNotMet")
    qualification_errors = errors.copy()
    if after["idle"]["requests"]>=before["idle"]["requests"]:errors.append("IdleTransactionsDidNotDecrease")
    if after["activity"]["requests"]>=before["activity"]["requests"]:errors.append("CompletedFlowTransactionsDidNotDecrease")
    if after["durationMs"]>before["durationMs"]+4950:errors.append("FlowDurationRegression4950ms")
    for key in ("rawExchanges","bytes"):
        if after["evidence"][key]>=before["evidence"][key]:errors.append("EvidenceBurdenDidNotDecrease:"+key)
    return dict(acceptanceVersion=ACCEPTANCE_VERSION, passed=not errors,scope="013SoftwareComparison",errors=errors,
        inputErrors=list(input_errors),
        correctness=dict(result="Failed" if qualification_errors else "Passed", errors=qualification_errors),
        loadReduction=dict(result="Failed" if errors[len(qualification_errors):] else "Passed", errors=errors[len(qualification_errors):]),
        before=before,after=after,unchangedInputs=unchanged,
        formalPlc="NotMeasurable: formal addresses, heartbeat period, minimum state holds and device timebase not delivered")
