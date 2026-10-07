from pathlib import Path
import zipfile

archive = Path(r"E:\dzk\gaode\原型.zip")
root = Path(__file__).resolve().parents[1] / "src"
with zipfile.ZipFile(archive, metadata_encoding="gbk") as source:
    for entry in source.infolist():
        if entry.is_dir() or not entry.filename.startswith("原型/"):
            continue
        relative = Path(entry.filename).relative_to("原型")
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read(entry))
print("Extracted approved prototype pages and assets with GBK filename decoding.")
