"""Package the compiled 1.4.6 binaries and sources; never include model data."""
from pathlib import Path
import hashlib
import shutil
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
VERSION = "1.4.6"

def main():
    payload = ROOT / "payload"
    if payload.exists():
        shutil.rmtree(payload)
    payload.mkdir()
    for name in ("V15VoiceStudio.exe", "V15Updater.exe"):
        shutil.copyfile(ROOT / name, payload / name)
    (payload / "ai").mkdir()
    for source, destination in (("ai_worker.py", "worker.py"), ("gpu_policy.py", "gpu_policy.py")):
        shutil.copyfile(ROOT / "source" / source, payload / "ai" / destination)
    shutil.copytree(ROOT / "source", payload / "source", ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    shutil.copytree(ROOT / "validation", payload / "source" / "validation-1.4.6")
    guide = ROOT / ("HUONG_DAN_" + VERSION + ".txt")
    shutil.copyfile(guide, payload / guide.name)
    manifest = ET.Element("update", product="V15VoiceStudio", version=VERSION, kind="core", space=str(96 * 1024 * 1024))
    for file in sorted(p for p in payload.rglob("*") if p.is_file()):
        ET.SubElement(manifest, "file", path=file.relative_to(payload).as_posix(), size=str(file.stat().st_size), sha256=hashlib.sha256(file.read_bytes()).hexdigest())
    output = ROOT.parent / ("output-" + VERSION)
    output.mkdir(exist_ok=True)
    package = output / ("V15VoiceStudio_" + VERSION + "_Update.zip")
    with zipfile.ZipFile(package, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        archive.writestr("manifest.xml", ET.tostring(manifest, encoding="utf-8", xml_declaration=True))
        archive.write(ROOT / "V15Updater.exe", "V15Updater.exe")
        archive.write(ROOT / "source" / "CAP_NHAT.cmd", "CAP_NHAT.cmd")
        archive.write(guide, "HUONG_DAN.txt")
        for file in sorted(p for p in payload.rglob("*") if p.is_file()):
            archive.write(file, file.relative_to(ROOT).as_posix())
    print(str(package))
    print("Payload files:", len(manifest), "ZIP bytes:", package.stat().st_size)

if __name__ == "__main__":
    main()
