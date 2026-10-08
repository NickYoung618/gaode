"""Read-only camera evidence audit after stopping Host/workers; Python stdlib only.

Does not trigger cameras, change a database, delete files, or include authentication tokens.
SDK layout expectations below are independent of the product payloads list.
"""
import argparse
import hashlib
import json
import pathlib
import re
import sqlite3
import uuid
import zipfile
from datetime import datetime, timezone


def digest(path):
    before = path.stat()
    with path.open("rb") as stream:
        value = hashlib.file_digest(stream, "sha256").hexdigest().upper()
    after = path.stat()
    assert (before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns), f"File changed while freezing: {path}"
    return value


def guid(value):
    return str(uuid.UUID(value))


def frame_metadata(value):
    # PowerShell records the same .NET timestamp in local offset. Preserve sub-microsecond digits.
    result = dict(value)
    for key in ("triggeredUtc", "receivedUtc"):
        stamp = result[key]
        fraction = re.search(r"\.(\d+)", stamp)
        result[key] = (datetime.fromisoformat(stamp).astimezone(timezone.utc).isoformat(),
                       (fraction.group(1)[6:] if fraction else "").rstrip("0"))
    return result


def audit(root, captures):
    database = root / "store" / "camera.db"
    wal = pathlib.Path(str(database) + "-wal")
    assert not wal.exists() or wal.stat().st_size == 0, "Stop and checkpoint database before declaring immutable evidence"
    db = sqlite3.connect(database.as_uri() + "?mode=ro&immutable=1", uri=True)
    db.row_factory = sqlite3.Row
    rows = {guid(row["MediaId"]): dict(row) for row in db.execute("SELECT * FROM Media")}
    writes = list(db.execute("SELECT * FROM Writes WHERE Kind IN ('Media','CaptureFact')"))
    results = []
    for path in sorted(captures.glob("*.json")):
        record = json.loads(path.read_text(encoding="utf-8-sig"))
        if not isinstance(record, dict) or "media" not in record or "metadata" not in record:
            continue
        media, metadata = record["media"], record["metadata"]
        row = rows[guid(media["mediaId"])]
        for field in ("RunId", "CaptureId"):
            assert guid(row[field]) == guid(media[field[0].lower() + field[1:]])
        for field in ("RelativeKey", "ByteLength", "Format", "Source"):
            assert row[field] == media[field[0].lower() + field[1:]]
        assert row["State"] == "FileCompleted"
        data_path = root / "store" / "media" / media["relativeKey"]
        assert data_path.resolve().is_relative_to((root / "store" / "media").resolve())
        sidecar_path = pathlib.Path(str(data_path) + ".metadata.json")
        sidecar = json.loads(sidecar_path.read_text(encoding="utf-8-sig"))
        sha = digest(data_path)
        assert sha == sidecar["sha256"] and data_path.stat().st_size == metadata["payloadBytes"] == media["byteLength"]
        assert frame_metadata(sidecar["fact"]["frameMetadata"]) == frame_metadata(metadata)
        committed_media = committed_fact = None
        for write in writes:
            assert hashlib.sha256(write["PayloadJson"].encode()).hexdigest().upper() == write["PayloadDigest"]
            payload = json.loads(write["PayloadJson"])
            if write["Kind"] == "Media" and guid(payload["mediaId"]) == guid(media["mediaId"]):
                committed_media = write["WriteId"]
            if write["Kind"] == "CaptureFact" and guid(payload["mediaId"]) == guid(media["mediaId"]):
                assert frame_metadata(payload["captureFact"]["frameMetadata"]) == frame_metadata(metadata)
                committed_fact = write["WriteId"]
        assert committed_media and committed_fact
        if metadata["role"] == "3D":
            p = metadata["actualParameters"]
            pixels = int(p["irWidth"]) * int(p["irHeight"])
            assert (metadata["width"], metadata["height"]) == (int(p["irWidth"]), int(p["irHeight"]))
            depth = {1: int(p["textureWidth"]) * int(p["textureHeight"]), 2: pixels}[int(p["depthType"])]
            groups = {0: 2, 2: 1}[int(p["reconstructionType"])]
            pixel_bytes = int(p["pixelBytes"])
            assert pixel_bytes in (1, 2) and int(p["irImagesPerCamera"]) == 2 and int(p["irCameraGroups"]) == groups
            expected = {"points.xyz.f32": (pixels * 3, 4), "depth.f32": (depth, 4), "ir.bytes": (pixels * 2 * groups, pixel_bytes)}
            with zipfile.ZipFile(data_path) as archive:
                assert len(archive.infolist()) == 4 and set(archive.namelist()) == {*expected, "metadata.json"}
                for name, (count, size) in expected.items():
                    channel = next(x for x in metadata["payloads"] if x["name"] == name)
                    content = archive.read(name)
                    assert len(content) == channel["byteLength"] == count * size
                    assert (channel["elementCount"], channel["elementBytes"]) == (count, size)
                    assert hashlib.sha256(content).hexdigest().upper() == channel["sha256"]
                manifest = json.loads(archive.read("metadata.json"))
                assert manifest["serial"] == metadata["serial"] and manifest["frameIndex"] == metadata["frameId"]
                assert guid(manifest["workerSessionId"]) == guid(metadata["workerSessionId"])
                assert manifest["payloads"] == metadata["payloads"]
        else:
            p = metadata["actualParameters"]
            assert media["format"] == "GalaxyRaw"
            assert int(p["Width"]) == metadata["width"] and int(p["Height"]) == metadata["height"]
            assert int(p["PayloadSize"]) == metadata["payloadBytes"]
            assert metadata["pixelFormat"] == "GX_PIXEL_FORMAT_MONO8", "Define layout rule before freezing a new pixel format"
            assert metadata["payloadBytes"] == metadata["width"] * metadata["height"]
            assert sha == metadata["payloads"][0]["sha256"]
        downloaded = path.with_suffix(".content")
        assert digest(downloaded) == sha
        results.append(dict(role=metadata["role"], serial=metadata["serial"], mediaId=media["mediaId"],
                            captureId=media["captureId"], runId=media["runId"], session=metadata["workerSessionId"],
                            frameId=metadata["frameId"], file=str(data_path), bytes=media["byteLength"], sha256=sha,
                            sidecar=str(sidecar_path), sidecarSha256=digest(sidecar_path), database=str(database),
                            mediaWriteId=committed_media, captureFactWriteId=committed_fact, verified=True))
    db.close()
    assert results, "No capture records found"
    return dict(database=str(database), databaseMediaCount=len(rows), verifiedMediaCount=len(results), media=results)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=pathlib.Path, required=True)
    parser.add_argument("--captures", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    assert args.root.is_absolute() and args.captures.is_absolute() and args.output.is_absolute()
    assert not args.output.exists(), "Preserve existing evidence; choose a new output"
    # The product owns this file with FileShare.None. Opening it fails if this data root's Host is live;
    # holding it open also prevents the product acquiring exclusive ownership during the read-only audit.
    with (args.root / "store" / ".camera-host.lock").open("rb"):
        result = audit(args.root, args.captures)
        result["recordedUtc"] = datetime.now(timezone.utc).isoformat()
        result["inventory"] = [dict(path=str(p), bytes=p.stat().st_size, sha256=digest(p))
                               for p in sorted(args.root.rglob("*")) if p.is_file()
                               and "headers" not in p.name and p.name != "camera.settings.json"]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Verified {result['verifiedMediaCount']} media; inventory {len(result['inventory'])} files")
