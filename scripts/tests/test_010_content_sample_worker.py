"""Minimal implementation/content and actual process protocol checks for R10."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import unittest
import uuid
import queue
import threading

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "scripts/010-content-sample-worker.py"
spec = importlib.util.spec_from_file_location("content_sample", SCRIPT)
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)
FIXTURE = REPO / "backend/tests/Gaode.Integration.Tests/Fixtures/RecipeExecution010"


class ContentSampleWorkerTests(unittest.TestCase):
    def test_media_values_change_quality_without_lookup(self):
        samples = [worker.decode_sample((FIXTURE / ("media/" + camera + ".png")).read_bytes()) for camera in ("C", "D")]
        inputs = [dict(mediaId=str(uuid.uuid4()), objectId="declared-object", localFace=1, heightRound=1, camera=c) for c in ("C", "D")]
        self.assertEqual("OK", worker.compute("Detection", inputs, samples, .5)["disposition"])
        samples[1]["score"] = .6
        self.assertEqual("NG", worker.compute("Detection", inputs, samples, .5)["disposition"])
        samples[1]["valid"] = False
        self.assertEqual("Pending", worker.compute("Detection", inputs, samples, .5)["disposition"])
        samples[0]["score"] = .7
        self.assertEqual("NG", worker.compute("Detection", inputs, samples, .5)["disposition"])
        inputs[1]["objectId"] = "wrong-object"
        with self.assertRaisesRegex(ValueError, "SampleTargetIdentityMismatch"):
            worker.compute("Detection", inputs, samples, .5)

    def test_owned_process_reads_media_computes_and_releases_each_input(self):
        evidence = Path(os.environ["GAODE_010_WORKER_EVIDENCE"]).resolve()
        allowed = (REPO / "artifacts/recipe-execution-010").resolve()
        self.assertIn(allowed, evidence.parents)
        evidence.mkdir(parents=True, exist_ok=False)
        for name in ("F", "C", "D"):
            shutil.copyfile(FIXTURE / ("media/" + name + ".png"), evidence / (name + ".png"))
        config = FIXTURE / "content-worker.json"
        responses = []
        with (evidence / "stderr.log").open("w", encoding="utf-8") as stderr:
            process = subprocess.Popen([sys.executable, str(SCRIPT), str(config)], cwd=evidence,
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=stderr, text=True, encoding="utf-8",
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            lines = queue.Queue()
            def collect():
                for line in process.stdout: lines.put(line)
                lines.put("")
            reader = threading.Thread(target=collect, daemon=True); reader.start()
            try:
                session = str(uuid.uuid4())
                def send(message):
                    process.stdin.write(json.dumps(message) + "\n")
                    process.stdin.flush()
                def read():
                    line = lines.get(timeout=16)
                    self.assertTrue(line, "Owned worker exited before protocol completion")
                    result = json.loads(line); responses.append(result); return result
                send(dict(type="Hello", contractVersion=worker.CONTRACT, workerSessionId=session, callId=str(uuid.uuid4()), attempt=1))
                ready = read(); self.assertEqual("Ready", ready["type"])
                identity = json.loads(ready["reason"])
                self.assertEqual("ContentSampleWorker/1", identity["implementation"])
                self.assertEqual(process.pid, identity["pid"])
                self.assertEqual(hashlib.sha256(SCRIPT.read_bytes()).hexdigest().upper(), identity["scriptSha256"])
                for role, names, expected in (("FDecode", ["F"], None),
                        ("Detection", ["C"], "Detected"), ("Detection", ["C", "D"], "Fused")):
                    inputs = []
                    for name in names:
                        data = (evidence / (name + ".png")).read_bytes()
                        inputs.append(dict(leaseId=str(uuid.uuid4()), mediaId=str(uuid.uuid4()), captureId=str(uuid.uuid4()),
                            inputKey=name + ".png", contentType="image/png", byteLength=len(data), sha256=hashlib.sha256(data).hexdigest().upper(),
                            objectId="declared-object", localFace=1, heightRound=1, camera=name))
                    call = str(uuid.uuid4()); lease = str(uuid.uuid4())
                    send(dict(type="Execute", contractVersion=worker.CONTRACT, workerSessionId=session, callId=call,
                        attempt=1, role=role, leaseId=lease, inputs=inputs))
                    accepted, result = read(), read()
                    self.assertEqual("Accepted", accepted["type"])
                    self.assertEqual("Result", result["type"])
                    self.assertNotIn("errorCode", result)
                    for message in (accepted, result):
                        self.assertEqual((session, call, lease), (message["workerSessionId"], message["callId"], message["leaseId"]))
                    value = json.loads(result["resultJson"])
                    if role == "FDecode": self.assertEqual(["SAMPLE|TRAY|CD-010"], value["rawCodes"])
                    else:
                        self.assertEqual(expected, value["classification"]); self.assertEqual("OK", value["disposition"])
                        self.assertEqual([v["mediaId"] for v in inputs], value["inputMediaIds"])
                    for index, item in enumerate(inputs):
                        release = read()
                        self.assertEqual(("InputReleased", index, call, item["sha256"]),
                            (release["type"], release["inputIndex"], release["callId"], release["reason"]))
                send(dict(type="Shutdown", contractVersion=worker.CONTRACT))
                self.assertEqual(0, process.wait(timeout=5))
            finally:
                if process.poll() is None: process.kill(); process.wait(timeout=5)
                process.stdin.close(); reader.join(timeout=2); process.stdout.close()
                (evidence / "responses.json").write_text(json.dumps(responses, indent=2), encoding="utf-8")
        audit = [json.loads(line) for line in (evidence / "worker-protocol.jsonl").read_text(encoding="utf-8").splitlines()]
        self.assertEqual(4, sum(row["event"] == "MediaRead" for row in audit))
        self.assertEqual(4, sum(row["event"] == "InputReleased" for row in audit))


if __name__ == "__main__":
    unittest.main()
