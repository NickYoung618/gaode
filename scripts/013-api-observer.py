"""013 backend load observer: real read-only HTTP every two seconds, no page evidence."""
import datetime
import json
import sys
import time
import urllib.request
from pathlib import Path


def main():
    url, pipe, target = sys.argv[1:]
    root = Path(target)
    root.mkdir(parents=True, exist_ok=False)
    with open("\\\\.\\pipe\\" + pipe, "r", encoding="utf-8-sig") as channel:
        token = channel.readline().strip()
    observations = []
    due = time.monotonic()
    while not (root / "stop").exists():
        request = urllib.request.Request(url.rstrip("/") + "/api/v1/station01/status",
                                         headers={"Authorization": "Bearer " + token})
        started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        with urllib.request.urlopen(request, timeout=15) as response:
            status = json.load(response)
            if response.status != 200 or not isinstance(status.get("plc"), dict):
                raise ValueError("ActualBackendStatusRequired")
        observations.append(dict(at=started, status=status))
        if len(observations) == 1:
            (root / "backend-observer-ready.json").write_text(json.dumps(dict(
                state="ActualBackendApiObserved", apiBaseUrl=url, observedUtc=started,
                intervalSeconds=2, evidenceLevel="BackendLoadOnly")), encoding="utf-8")
        # At most the fixed observation window. Overflow is an error, never silent evidence loss.
        if len(observations) > 360:
            raise RuntimeError("013ObserverWindowExceeded")
        due += 2
        time.sleep(max(0, due - time.monotonic()))
    (root / "observations.json").write_text(json.dumps(observations), encoding="utf-8")


if __name__ == "__main__":
    main()
