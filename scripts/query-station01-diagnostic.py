"""Read-only, redacted SQLite correlation index for one isolated Test run."""

import json
import sqlite3
import sys


def ids(value):
    found = {}
    if isinstance(value, dict):
        for key, item in value.items():
            if key.lower() in {"operationid", "actionid", "deviceepoch", "connectionepoch"}:
                found.setdefault(key, []).append(item)
            nested = ids(item)
            for name, entries in nested.items():
                found.setdefault(name, []).extend(entries)
    elif isinstance(value, list):
        for item in value:
            nested = ids(item)
            for name, entries in nested.items():
                found.setdefault(name, []).extend(entries)
    return found


def main(db_path, run_id):
    connection = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
    try:
        operations = [dict(zip(("operationId", "kind", "attempt", "state", "intentWriteId"), row))
                      for row in connection.execute(
                          "SELECT OperationId, Kind, Attempt, State, IntentWriteId FROM Operations WHERE lower(RunId)=lower(?)",
                          (run_id,))]
        writes = []
        for write_id, revision, kind, payload, committed in connection.execute(
                "SELECT WriteId, Revision, Kind, PayloadJson, CommittedUtc FROM Writes WHERE lower(RunId)=lower(?) ORDER BY Revision",
                (run_id,)):
            try:
                links = ids(json.loads(payload))
            except (ValueError, TypeError):
                links = {}
            writes.append({"writeId": write_id, "revision": revision, "kind": kind,
                           "committedUtc": committed, "links": links})
        print(json.dumps({"runId": run_id, "operations": operations, "writes": writes}, ensure_ascii=False))
    finally:
        connection.close()


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
