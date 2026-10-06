"""Test-only independent worker: normal Height/F, Detection arrives after the 15 s call budget."""
import json
import hashlib
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

root = Path.cwd().resolve()
audit = root / "late-worker-protocol.jsonl"

def record(kind, message):
    with audit.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps({"event": kind, "utc": datetime.now(timezone.utc).isoformat(),
                                 "callId": message.get("callId"), "role": message.get("role")}) + "\n")

for line in sys.stdin:
    message = json.loads(line)
    if message["type"] == "Shutdown":
        break
    if message["type"] == "Hello":
        print(json.dumps({"type": "Ready", "contractVersion": message["contractVersion"],
                          "workerSessionId": message["workerSessionId"],
                          "callId": message["callId"], "attempt": 1,
                          "reason": json.dumps({"implementation": "PythonWorkerAdapter/1", "pid": os.getpid(),
                              "scriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper(),
                              "configSha256": hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest().upper()})}), flush=True)
        record("Ready", message)
        continue
    if message["type"] != "Execute":
        continue
    for item in message["inputs"]:
        path = (root / item["inputKey"]).resolve()
        if root not in path.parents:
            raise ValueError("Test worker input media outside root")
        # Same contained file, using the Windows long-path file API spelling.
        # The current verifier's isolated evidence paths can exceed MAX_PATH.
        readable = path
        if os.name == "nt" and not str(path).startswith("\\\\?\\"):
            readable = Path("\\\\?\\UNC\\" + str(path)[2:] if str(path).startswith("\\\\") else "\\\\?\\" + str(path))
        if not readable.is_file() or readable.stat().st_size != item["byteLength"]:
            raise ValueError("Test worker input media unavailable")
        data = readable.read_bytes()
        if hashlib.sha256(data).hexdigest().upper() != item["sha256"].upper():
            raise ValueError("Test worker input digest mismatch")
    base = {key: message[key] for key in
            ("contractVersion", "workerSessionId", "callId", "attempt", "role", "leaseId")}
    print(json.dumps({"type": "Accepted", **base}), flush=True)
    record("Accepted", message)
    time.sleep(16 if message["role"] == "Detection" else 10)
    role = message["role"]
    if role == "Height":
        result = {"heightSamples": [{"sourceElementId": "sample-a", "value": 11.0,
                                     "unit": "mm", "datum": "SIM_REFERENCE"}]}
    elif role == "FDecode":
        result = {"rawCodes": ["TEST-TRAY-0999"]}
    else:
        result = {"disposition": "OK"}
    print(json.dumps({"type": "Result", **base,
                      "resultJson": json.dumps(result)}), flush=True)
    record("ResultLate" if role == "Detection" else "Result", message)
    for index, item in enumerate(message["inputs"]):
        print(json.dumps({"type": "InputReleased", **base, "inputIndex": index,
                          "reason": item["sha256"]}), flush=True)
    record("InputReleased", message)
