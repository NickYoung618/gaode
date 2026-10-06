"""Test-only worker: deliberately returns another call ID after the real Execute."""
import json
import sys
import uuid
from pathlib import Path

audit = Path.cwd() / "wrong-call-id-protocol.jsonl"

for line in sys.stdin:
    message = json.loads(line)
    if message["type"] == "Shutdown":
        break
    if message["type"] == "Hello":
        ready = {"type": "Ready", "contractVersion": message["contractVersion"],
                 "workerSessionId": message["workerSessionId"],
                 "callId": message["callId"], "attempt": 1}
        print(json.dumps(ready), flush=True)
        continue
    if message["type"] == "Execute":
        wrong = dict(message)
        wrong["type"] = "Accepted"
        wrong["callId"] = str(uuid.uuid4())
        audit.write_text(json.dumps({"actualCallId": wrong["callId"],
                                     "requestedCallId": message["callId"],
                                     "sessionId": message["workerSessionId"]}))
        print(json.dumps(wrong), flush=True)
        break
