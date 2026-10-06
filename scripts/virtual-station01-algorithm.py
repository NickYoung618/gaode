"""Controlled Test worker. Host owns this process and passes media keys under its workdir."""
import hashlib
import os
import json
import random
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

config = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
assert config["purpose"] == "Test" and config["delayMs"] == 10000
implementation_identity = {"implementation": "PythonWorkerAdapter/1", "pid": os.getpid(),
    "scriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper(),
    "configSha256": hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest().upper()}
root = Path.cwd().resolve()
audit_path = root / "worker-protocol.jsonl"

def audit(kind, **fields):
    with audit_path.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps({"event": kind, "atUtc": datetime.now(timezone.utc).isoformat(),
                                 **fields}, separators=(",", ":")) + "\n")

audit("Started", source="Test/actual-worker", **implementation_identity)

for line in sys.stdin:
    message = json.loads(line)
    if message.get("type") == "Shutdown":
        break
    if message.get("type") == "Hello":
        audit("HelloReceived", workerSessionId=message["workerSessionId"])
        print(json.dumps({"type": "Ready", "contractVersion": message["contractVersion"],
                          "workerSessionId": message["workerSessionId"],
                          "callId": message["callId"], "attempt": 1, "reason": json.dumps(implementation_identity)},
                         separators=(",", ":")), flush=True)
        audit("Ready", workerSessionId=message["workerSessionId"])
        continue
    if message.get("type") != "Execute":
        continue
    base = {key: message[key] for key in
            ("contractVersion", "workerSessionId", "callId", "attempt", "role", "leaseId")}
    def send(kind, **extra):
        print(json.dumps({"type": kind, **base, **extra}, separators=(",", ":")), flush=True)
        audit(kind, callId=message["callId"], role=message["role"],
              workerSessionId=message["workerSessionId"], **extra)
    try:
        inputs = message["inputs"]
        if len(inputs) not in (1, 2) or len({item["mediaId"] for item in inputs}) != len(inputs):
            raise ValueError("InputIdentityInvalid")
        digests = []
        for item in inputs:
            path = (root / item["inputKey"]).resolve()
            if root not in path.parents:
                raise ValueError("InputMediaOutsideRoot")
            # Keep the controlled-root check; only the Windows file API spelling changes.
            readable = path
            if os.name == "nt" and not str(path).startswith("\\\\?\\"):
                readable = Path("\\\\?\\UNC\\" + str(path)[2:] if str(path).startswith("\\\\") else "\\\\?\\" + str(path))
            if not readable.is_file():
                raise ValueError("InputMediaUnavailable")
            data = readable.read_bytes()
            if len(data) != item["byteLength"]:
                raise ValueError("InputMediaLengthMismatch")
            digest = hashlib.sha256(data).hexdigest().upper()
            if digest != item["sha256"].upper():
                raise ValueError("InputMediaDigestMismatch")
            digests.append(digest)
        send("Accepted", reason=":".join(digests))
        started = time.monotonic()
        seed = config["seed"] + sum(int(digest[:8], 16) for digest in digests)
        rng = random.Random(seed)
        role = message["role"]
        if role != "TrayPose":
            time.sleep(config["delayMs"] / 1000)  # Existing virtual roles retain their declared 10s delay.
        if role == "TrayPose":
            from importlib.util import spec_from_file_location, module_from_spec
            spec = spec_from_file_location("sample_worker", Path(__file__).with_name("010-content-sample-worker.py"))
            sample_worker = module_from_spec(spec); spec.loader.exec_module(sample_worker)
            samples = [sample_worker.decode_sample((root / item["inputKey"]).read_bytes()) for item in inputs]
            result = sample_worker.compute(role, inputs, samples, config["threshold"],
                message["observationContext"], config["normalTiltLimitDegrees"])
        elif role == "Height":
            values = config["heightMm"]
            result = {"heightSamples": [
                {"sourceElementId": "sample-a", "value": round(rng.uniform(values["min"], values["max"]), 3), "unit": "mm", "datum": "SIM_REFERENCE"},
                {"sourceElementId": "sample-b", "value": round(rng.uniform(values["min"], values["max"]), 3), "unit": "mm", "datum": "SIM_REFERENCE"}]}
        elif role == "FDecode":
            result = {"rawCodes": [config["fCode"]]}
        elif role == "EDecode":
            if config.get("eDecodeError"):
                raise ValueError(config["eDecodeError"])
            code = config.get("eCode")
            if code and config.get("eCodeIdentitySuffix"):
                code += ":" + inputs[0]["objectId"]
            result = {"rawCodes": [code] if code else []}
        elif role == "Detection":
            disposition = rng.choice(config["detectionDisposition"])
            for target in config.get("dispositionByTarget", []):
                if all(item["objectId"].endswith(target["objectIdSuffix"]) and
                       item["localFace"] == target["localFace"] for item in inputs) and (
                       "camera" not in target or inputs[0]["camera"] == target["camera"]):
                    disposition = target["disposition"]
                    assert disposition in ("OK", "NG", "Pending")
                    break
            result = {"classification": "Fused" if len(inputs) == 2 else "Detected",
                      "disposition": disposition,
                      "inputMediaIds": [item["mediaId"] for item in inputs],
                      "inputDigests": digests}
        else:
            raise ValueError("UnknownAlgorithmRole")
        result["elapsedMs"] = round((time.monotonic() - started) * 1000)
        send("Result", reason=":".join(digests), resultJson=json.dumps(result, separators=(",", ":")))
    except Exception as exc:
        send("Result", errorCode=str(exc), resultJson="{}")
    finally:
        for index, item in enumerate(message.get("inputs", [])):
            send("InputReleased", inputIndex=index,
                 reason=digests[index] if index < len(digests) else item.get("sha256"))
