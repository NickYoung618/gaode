"""Affected worker components only; no production or equipment approval."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("worker011", Path(__file__).parents[1] / "010-content-sample-worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class TrayObservationWorkerTests(unittest.TestCase):
    def test_media_measurements_drive_pose_and_f_location(self):
        context = {"purpose": "InitialPreparation", "checkRound": 1, "relatedTransitionId": None}
        sample = {"traySlots": [{"physicalSlotIndex": 2, "present": True, "tiltDegrees": 1},
                                {"physicalSlotIndex": 7, "present": True, "tiltDegrees": 12},
                                {"physicalSlotIndex": 9, "present": False, "tiltDegrees": None}],
                  "fMarker": {"xMicrometres": 12500, "yMicrometres": 25750, "frame": "test-frame"}}
        result = worker.compute("TrayPose", [], [sample], 0.5, context, 5)["trayObservation"]
        self.assertEqual(["Normal", "Abnormal", "Unknown"], [s["pose"] for s in result["slots"]])
        self.assertEqual([2, 7, 9], [s["physicalSlotIndex"] for s in result["slots"]])
        self.assertEqual(12.5, result["fLocation"]["x"])
        changed = {**sample, "fMarker": {**sample["fMarker"], "xMicrometres": 18200}}
        self.assertEqual(18.2, worker.compute("TrayPose", [], [changed], 0.5, context, 5)["trayObservation"]["fLocation"]["x"])

    def test_missing_observation_never_becomes_normal(self):
        with self.assertRaises((ValueError, KeyError)):
            worker.compute("TrayPose", [], [{}], 0.5,
                           {"purpose": "InitialPreparation", "checkRound": 1, "relatedTransitionId": None}, 5)
        sample = {"traySlots": [{"physicalSlotIndex": 4, "present": None, "tiltDegrees": None}]}
        context = {"purpose": "PostPlacementCheck", "checkRound": 2, "relatedTransitionId": "cf37667c-a446-4452-9744-16dff4f9e148"}
        observed = worker.compute("TrayPose", [], [sample], 0.5, context, 5)["trayObservation"]
        self.assertEqual("Unknown", observed["slots"][0]["presence"])
        self.assertIsNone(observed["fLocation"])

    def test_owned_process_reads_observation_media_and_releases_inputs(self):
        import hashlib
        import json
        import os
        import queue
        import struct
        import subprocess
        import sys
        import threading
        import uuid
        import zlib

        root = Path(os.environ["GAODE_011_WORKER_EVIDENCE"]).resolve()
        allowed = (Path(__file__).parents[2] / "artifacts/011-plc-interaction-update").resolve()
        self.assertIn(allowed, root.parents)
        root.mkdir(parents=True, exist_ok=False)
        configuration = {"purpose": "Test", "implementation": worker.IMPLEMENTATION,
                         "delayMs": 100, "threshold": 0.5, "normalTiltLimitDegrees": 5}
        config = root / "component-worker.json"
        config.write_text(json.dumps(configuration), encoding="utf-8")
        def png(sample):
            def chunk(kind, body):
                return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xffffffff)
            return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0)) + \
                chunk(b"tEXt", worker.KEYWORD + b"\0" + json.dumps(sample).encode("ascii")) + \
                chunk(b"IDAT", zlib.compress(b"\0\x40\x80\xc0")) + chunk(b"IEND", b"")
        sample = {"traySlots": [{"physicalSlotIndex": 1, "present": True, "tiltDegrees": 2},
                                {"physicalSlotIndex": 3, "present": True, "tiltDegrees": 9}],
                  "fMarker": {"xMicrometres": 14100, "yMicrometres": 23600, "frame": "test-frame"}}
        responses = []
        with (root / "stderr.log").open("w", encoding="utf-8") as errors:
            process = subprocess.Popen([sys.executable, str(Path(worker.__file__).resolve()), str(config)], cwd=root,
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors, text=True, encoding="utf-8",
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            lines = queue.Queue()
            def collect():
                for line in process.stdout: lines.put(line)
                lines.put("")
            reader = threading.Thread(target=collect, daemon=True); reader.start()
            def send(value):
                process.stdin.write(json.dumps(value) + "\n"); process.stdin.flush()
            def read():
                raw = lines.get(timeout=5)
                self.assertTrue(raw, "Owned worker exited without response")
                value = json.loads(raw); responses.append(value); return value
            try:
                session = str(uuid.uuid4())
                send(dict(type="Hello", contractVersion=worker.CONTRACT, workerSessionId=session, callId=str(uuid.uuid4()), attempt=1))
                ready = read(); self.assertEqual("Ready", ready["type"])
                identity = json.loads(ready["reason"])
                self.assertEqual(process.pid, identity["pid"])
                self.assertEqual(hashlib.sha256(Path(worker.__file__).read_bytes()).hexdigest().upper(), identity["scriptSha256"])
                self.assertEqual(hashlib.sha256(config.read_bytes()).hexdigest().upper(), identity["configSha256"])
                for index in range(3):
                    payload = sample if index < 2 else {}
                    content = png(payload); name = str(uuid.uuid4()) + ".png"  # Names carry no business selection.
                    (root / name).write_bytes(content)
                    media = dict(leaseId=str(uuid.uuid4()), mediaId=str(uuid.uuid4()), captureId=str(uuid.uuid4()), inputKey=name,
                        contentType="image/png", byteLength=len(content), sha256=hashlib.sha256(content).hexdigest().upper())
                    context = dict(trayId=str(uuid.uuid4()), purpose="InitialPreparation" if index == 0 else "PostPlacementCheck",
                        checkRound=index + 1, relatedTransitionId=None if index == 0 else str(uuid.uuid4()))
                    call = str(uuid.uuid4()); lease = str(uuid.uuid4())
                    send(dict(type="Execute", contractVersion=worker.CONTRACT, workerSessionId=session, callId=call,
                        attempt=1, role="TrayPose", leaseId=lease, inputs=[media], observationContext=context))
                    accepted, result, released = read(), read(), read()
                    self.assertEqual(["Accepted", "Result", "InputReleased"], [v["type"] for v in (accepted, result, released)])
                    for message in (accepted, result, released):
                        self.assertEqual((session, call, lease), (message["workerSessionId"], message["callId"], message["leaseId"]))
                    self.assertEqual((0, media["sha256"]), (released["inputIndex"], released["reason"]))
                    if index == 2:
                        self.assertIn("errorCode", result)
                        self.assertNotIn("trayObservation", json.loads(result["resultJson"]))
                    else:
                        observed = json.loads(result["resultJson"])["trayObservation"]
                        self.assertEqual([1, 3], [v["physicalSlotIndex"] for v in observed["slots"]])
                        self.assertEqual(["Normal", "Abnormal"], [v["pose"] for v in observed["slots"]])
                        self.assertEqual(context["relatedTransitionId"], observed["relatedTransitionId"])
                        if index == 0: self.assertEqual(14.1, observed["fLocation"]["x"])
                        else: self.assertIsNone(observed["fLocation"])
                send(dict(type="Shutdown", contractVersion=worker.CONTRACT))
                self.assertEqual(0, process.wait(timeout=5))
            finally:
                if process.poll() is None: process.kill(); process.wait(timeout=5)
                process.stdin.close(); reader.join(timeout=2); process.stdout.close()
                (root / "responses.json").write_text(json.dumps(responses, indent=2), encoding="utf-8")
        audit = [json.loads(line) for line in (root / "worker-protocol.jsonl").read_text(encoding="utf-8").splitlines()]
        self.assertEqual(3, sum(v["event"] == "MediaRead" for v in audit))
        self.assertEqual(3, sum(v["event"] == "InputReleased" for v in audit))
    def test_e_and_both_camera_groups_use_content(self):
        self.assertEqual(["ACTUAL-E"], worker.compute("EDecode", [], [{"entityCode": "ACTUAL-E"}], 0.5)["rawCodes"])
        inputs = [{"objectId": "part", "localFace": 6, "heightRound": 3, "camera": c, "mediaId": c} for c in "AB"]
        samples = [{"camera": c, "valid": True, "score": 0.1} for c in "AB"]
        self.assertEqual("OK", worker.compute("Detection", inputs, samples, 0.5)["disposition"])
        samples[1]["score"] = 0.8
        self.assertEqual("NG", worker.compute("Detection", inputs, samples, 0.5)["disposition"])


if __name__ == "__main__":
    unittest.main()
