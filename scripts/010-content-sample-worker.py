"""ContentSampleWorker/1: controlled PNG sample reader, independent of the fixture worker.

Reads explicit Test media samples for tray observations, codes and AB/CD inspection.
No target-result lookup, fixture worker import, subprocess delegation or pseudo-random result.
"""
import hashlib
import json
import math
import os
from pathlib import Path
import struct
import sys
import time
import zlib
import uuid
from datetime import datetime, timezone

IMPLEMENTATION = "ContentSampleWorker/1"
CONTRACT = "station01-worker/2.0"
KEYWORD = b"gaode010.sample.v1"


def decode_sample(data):
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("SamplePngRequired")
    pos, samples, saw_header, saw_pixels, saw_end = 8, [], False, False, False
    while pos + 12 <= len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        kind, payload = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + length]
        end = pos + 8 + length
        if end + 4 > len(data) or zlib.crc32(kind + payload) & 0xffffffff != struct.unpack(">I", data[end:end + 4])[0]:
            raise ValueError("SamplePngChunkInvalid")
        if kind == b"IHDR":
            saw_header = len(payload) == 13 and struct.unpack(">II", payload[:8])[0] > 0 and struct.unpack(">II", payload[:8])[1] > 0
        if kind == b"IDAT":
            saw_pixels = bool(zlib.decompress(payload))
        if kind == b"tEXt" and payload.startswith(KEYWORD + b"\x00"):
            samples.append(json.loads(payload[len(KEYWORD) + 1:].decode("ascii")))
        pos = end + 4
        if kind == b"IEND":
            saw_end = pos == len(data)
            break
    if not (saw_header and saw_pixels and saw_end) or len(samples) != 1:
        raise ValueError("OneReadableSampleRequired")
    return samples[0]


def compute(role, inputs, samples, threshold, observation_context=None, normal_tilt_limit=None):
    """The same content-processing function is used by process execution and self checks."""
    if role == "TrayPose" and len(samples) == 1:
        context = observation_context
        if not isinstance(context, dict) or not isinstance(normal_tilt_limit, (int, float)) or not math.isfinite(normal_tilt_limit) or normal_tilt_limit <= 0:
            raise ValueError("TrayPoseConfigurationMissing")
        purpose, check_round = context["purpose"], context["checkRound"]
        transition = context["relatedTransitionId"]
        if purpose == "InitialPreparation":
            if check_round != 1 or transition is not None:
                raise ValueError("InitialObservationContextInvalid")
        elif purpose == "PostPlacementCheck":
            if check_round <= 1 or not transition:
                raise ValueError("PostPlacementContextInvalid")
            uuid.UUID(transition)
        else:
            raise ValueError("TrayObservationPurposeInvalid")
        slots = []
        for value in samples[0]["traySlots"]:
            index, present, tilt = value["physicalSlotIndex"], value["present"], value["tiltDegrees"]
            if not isinstance(index, int) or isinstance(index, bool) or index < 1 or index in [s["physicalSlotIndex"] for s in slots]:
                raise ValueError("PhysicalSlotIdentityInvalid")
            presence, pose, reason = "Unknown", "Unknown", "PresenceUnknown"
            if present is False:
                presence, reason = "Absent", "NoMaterialObserved"
            elif present is True:
                presence, reason = "Present", "PoseUnknown"
                if isinstance(tilt, (int, float)) and not isinstance(tilt, bool) and math.isfinite(tilt):
                    pose = "Normal" if abs(tilt) <= normal_tilt_limit else "Abnormal"
                    reason = "MeasuredTiltWithinTestLimit" if pose == "Normal" else "MeasuredTiltOutsideTestLimit"
            elif present is not None:
                raise ValueError("PresenceInputInvalid")
            identity = {key: value[key] for key in ("cellId", "region", "row", "column")}
            if not identity["cellId"] or identity["region"] not in ("OK", "NG", "Pending") or not all(
                    isinstance(identity[key], int) and not isinstance(identity[key], bool) and 1 <= identity[key] <= 10
                    for key in ("row", "column")):
                raise ValueError("ExplicitTrayCellIdentityRequired")
            slots.append(dict(physicalSlotIndex=index, presence=presence, pose=pose, reason=reason, **identity))
        if not slots:
            raise ValueError("TraySlotsMissing")
        location = None
        if purpose == "InitialPreparation" and samples[0].get("fMarker") is not None:
            marker = samples[0]["fMarker"]
            x, y, frame = marker["xMicrometres"], marker["yMicrometres"], marker["frame"]
            if not all(isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v) for v in (x, y)) or not isinstance(frame, str) or not frame:
                raise ValueError("FMarkerInvalid")
            location = dict(x=x / 1000, y=y / 1000, unit="mm", frame=frame, sourceReference="TestMedia:fMarkerMicrometres")
        expected = samples[0]["expectedPhysicalSlotIndices"]
        mapping = samples[0]["mappingSourceReference"]
        if not expected or len(set(expected)) != len(expected) or sorted(expected) != sorted(s["physicalSlotIndex"] for s in slots) or not mapping:
            raise ValueError("TrayCoverageIncomplete")
        return {"trayObservation": dict(schemaVersion="tray-observation/2", mappingSourceReference=mapping,
                expectedPhysicalSlotIndices=expected, observationId=str(uuid.uuid4()), observedAtUtc=datetime.now(timezone.utc).isoformat(),
                purpose=purpose, checkRound=check_round, relatedTransitionId=transition, slots=slots, fLocation=location)}
    if role in ("FDecode", "EDecode") and len(samples) == 1:
        code = samples[0]["trayCode" if role == "FDecode" else "entityCode"]
        if not isinstance(code, str) or not code.strip():
            raise ValueError("TrayCodeMissing")
        return {"rawCodes": [code]}
    if role != "Detection" or len(samples) not in (1, 2):
        raise ValueError("UnsupportedSampleCapability")
    identities = {(item["objectId"], item["localFace"], item["heightRound"], item.get("stageId")) for item in inputs}
    if len(identities) != 1 or any(not item["objectId"] or item["localFace"] < 1 or item["heightRound"] < 1 for item in inputs):
        raise ValueError("SampleTargetIdentityMismatch")
    cameras = [item["camera"] for item in inputs]
    if any(c not in ("A", "B", "C", "D") for c in cameras) or len(samples) == 2 and set(cameras) not in ({"A", "B"}, {"C", "D"}):
        raise ValueError("SampleCameraPairUnsupported")
    scores = []
    valid = True
    reliable_ng = False
    for item, sample in zip(inputs, samples):
        if sample["camera"] != item["camera"] or not isinstance(sample["valid"], bool):
            raise ValueError("SampleCameraIdentityMismatch")
        score = sample["score"]
        if not isinstance(score, (int, float)) or not math.isfinite(score):
            raise ValueError("SampleScoreInvalid")
        valid = valid and sample["valid"]
        scores.append(score)
        reliable_ng = reliable_ng or sample["valid"] and score >= threshold
    disposition = "NG" if reliable_ng else "Pending" if not valid else "OK"
    return {"classification": "Fused" if len(samples) == 2 else "Detected", "disposition": disposition,
            "inputMediaIds": [item["mediaId"] for item in inputs], "score": max(scores), "threshold": threshold}


def main(config_path):
    config_data = Path(config_path).read_bytes()
    config = json.loads(config_data.decode("utf-8-sig"))
    if config["purpose"] != "Test" or config["implementation"] != IMPLEMENTATION:
        raise ValueError("ApprovedSampleConfigurationRequired")
    if not isinstance(config["delayMs"], int) or isinstance(config["delayMs"], bool) or config["delayMs"] < 0:
        raise ValueError("ExplicitSampleDelayRequired")
    threshold = config["threshold"]
    if not isinstance(threshold, (int, float)) or not math.isfinite(threshold):
        raise ValueError("SampleThresholdInvalid")
    root = Path.cwd().resolve()
    identity = {"implementation": IMPLEMENTATION, "pid": os.getpid(),
                "scriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper(),
                "configSha256": hashlib.sha256(config_data).hexdigest().upper()}

    def audit(event, **fields):
        with (root / "worker-protocol.jsonl").open("a", encoding="utf-8") as stream:
            stream.write(json.dumps({"event": event, "atUtc": datetime.now(timezone.utc).isoformat(), **identity, **fields}) + "\n")

    audit("Started")
    for line in sys.stdin:
        message = json.loads(line)
        if message.get("contractVersion") != CONTRACT:
            raise ValueError("WorkerContractMismatch")
        if message["type"] == "Shutdown":
            break
        if message["type"] == "Hello":
            print(json.dumps({"type": "Ready", "contractVersion": CONTRACT,
                "workerSessionId": message["workerSessionId"], "callId": message["callId"], "attempt": 1, "reason": json.dumps(identity)}), flush=True)
            audit("Ready", workerSessionId=message["workerSessionId"])
            continue
        if message["type"] != "Execute":
            continue
        base = {k: message[k] for k in ("contractVersion", "workerSessionId", "callId", "attempt", "role", "leaseId")}

        def send(kind, **extra):
            print(json.dumps({"type": kind, **base, **extra}, separators=(",", ":")), flush=True)
            audit(kind, **base, **extra)

        inputs, samples, digests = message["inputs"], [], []
        try:
            if len(inputs) not in (1, 2) or len({x["mediaId"] for x in inputs}) != len(inputs):
                raise ValueError("SampleInputCountOrIdentityInvalid")
            for item in inputs:
                path = (root / item["inputKey"]).resolve()
                if root not in path.parents:
                    raise ValueError("InputMediaOutsideRoot")
                data = path.read_bytes()
                digest = hashlib.sha256(data).hexdigest().upper()
                if len(data) != item["byteLength"] or digest != item["sha256"].upper():
                    raise ValueError("InputMediaLengthOrDigestMismatch")
                digests.append(digest)
                samples.append(decode_sample(data))
                audit("MediaRead", **base, mediaId=item["mediaId"], captureId=item["captureId"],
                      inputKey=item["inputKey"], sha256=digest, byteLength=len(data), sample=samples[-1])
            send("Accepted", reason=":".join(digests))
            start = time.monotonic()
            time.sleep(config["delayMs"] / 1000)
            result = compute(message["role"], inputs, samples, threshold,
                             message.get("observationContext"), config.get("normalTiltLimitDegrees"))
            result["inputDigests"] = digests
            result["elapsedMs"] = round((time.monotonic() - start) * 1000)
            send("Result", resultJson=json.dumps(result, separators=(",", ":")), reason=":".join(digests))
        except (ValueError, KeyError, OSError, TypeError, zlib.error) as error:
            send("Result", errorCode=str(error), resultJson="{}")
        finally:
            for index, item in enumerate(inputs):
                send("InputReleased", inputIndex=index, reason=item["sha256"])


if __name__ == "__main__":
    main(sys.argv[1])
