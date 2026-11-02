"""Package the compiled 2.0.12 binaries and sources; include new community data once; keep installed runtimes and other models."""
from pathlib import Path
import hashlib
import shutil
import xml.etree.ElementTree as ET
import zipfile
import sys
import tempfile

ROOT = Path(__file__).resolve().parent.parent
VERSION = "2.0.12"
WITH_COMMUNITY = "--with-community" in sys.argv

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
    def source_ignore(folder, names):
        ignored = set(shutil.ignore_patterns("__pycache__", "*.pyc", ".*")(folder, names))
        if not WITH_COMMUNITY and Path(folder).resolve() == (ROOT / "source/community/rvc").resolve():
            ignored.update({"weights", "deps", "sample.wav"})
        return ignored
    shutil.copytree(ROOT / "source", payload / "source", ignore=source_ignore)
    shutil.copytree(ROOT / "validation", payload / "source" / "validation-2.0.12", ignore=shutil.ignore_patterns("real-asr", "*.mp3", "*.mp4", "*.wav", "*.png", "*.xml", "*.srt", "qa-clips*", "*aligned-words*"))
    guide = ROOT / ("HUONG_DAN_" + VERSION + ".txt")
    shutil.copyfile(guide, payload / guide.name)
    manifest = ET.Element("update", product="V15VoiceStudio", version="2.0.12", kind="core", space=str(3 * sum(p.stat().st_size for p in payload.rglob("*") if p.is_file()) + 128 * 1024 * 1024))
    assert not list((payload / "source").glob("Creator*.cs")), "Removed video feature must not ship"
    for file in sorted(p for p in payload.rglob("*") if p.is_file()):
        ET.SubElement(manifest, "file", path=file.relative_to(payload).as_posix(), size=str(file.stat().st_size), sha256=hashlib.sha256(file.read_bytes()).hexdigest())
    output = ROOT.parent / ("output-" + VERSION)
    output.mkdir(exist_ok=True)
    package = output / ("V15VoiceStudio_" + VERSION + ("_Update.zip" if WITH_COMMUNITY else "_Core_Update.zip"))
    build_folder = Path(tempfile.mkdtemp(prefix="v15-release-210-"))
    built = build_folder / package.name
    with zipfile.ZipFile(built, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        archive.writestr("manifest.xml", ET.tostring(manifest, encoding="utf-8", xml_declaration=True))
        archive.write(ROOT / "V15Updater.exe", "V15Updater.exe")
        archive.write(ROOT / "source" / "CAP_NHAT.cmd", "CAP_NHAT.cmd")
        archive.write(guide, "HUONG_DAN.txt")
        for file in sorted(p for p in payload.rglob("*") if p.is_file()):
            archive.write(file, file.relative_to(ROOT).as_posix())
    shutil.copyfile(built, package)
    shutil.rmtree(build_folder)
    print(str(package))
    print("Payload files:", len(manifest), "ZIP bytes:", package.stat().st_size)

if __name__ == "__main__":
    main()
