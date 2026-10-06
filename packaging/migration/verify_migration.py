"""Read-only verification of an extracted Gaode migration delivery."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify(root):
    manifest = json.loads((root / "MIGRATION-MANIFEST.json").read_text(encoding="utf-8"))
    failures = []
    for index, entry in enumerate(manifest["files"], 1):
        path = (root / entry["path"]).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            failures.append(f'Missing or invalid path: {entry["path"]}')
        elif path.stat().st_size != entry["bytes"] or sha256(path) != entry["sha256"]:
            failures.append(f'Changed file: {entry["path"]}')
        if index % 1000 == 0:
            print(f'Checked {index}/{len(manifest["files"])} files...', flush=True)
    if failures:
        raise RuntimeError("\n".join(failures))
    print(f'All {len(manifest["files"])} file hashes match.', flush=True)
    if not shutil.which("git"):
        raise RuntimeError("File hashes passed, but Git is missing; install Git and run again.")
    repo = root / manifest["repositoryPath"]

    def git(*args):
        return subprocess.check_output(["git", "-C", str(repo), *args], text=True, encoding="utf-8").strip()

    if git("rev-parse", "HEAD") != manifest["commit"]:
        raise RuntimeError("Git HEAD differs from migration baseline.")
    if git("rev-parse", manifest["tag"] + "^{commit}") != manifest["commit"]:
        raise RuntimeError("Git tag differs from migration baseline.")
    if git("remote", "get-url", "origin") != manifest["repositoryUrl"]:
        raise RuntimeError("Unexpected Git origin.")
    if git("status", "--porcelain"):
        raise RuntimeError("Tracked source or untracked development files differ from baseline.")
    subprocess.run(["git", "-C", str(repo), "bundle", "verify", str(root / "gaode-repository.bundle")], check=True)
    subprocess.run(["git", "-C", str(repo), "fsck", "--full"], check=True)
    print(f'PASS: {manifest["tag"]} / {manifest["commit"]}', flush=True)
    print("This verifies the delivery, not PLC/camera operation.")


if __name__ == "__main__":
    try:
        verify(Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).parent).resolve())
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(1)
