"""End-to-end ZIP tests against the shipped updater and genuine old EXEs."""
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
WORKSPACE = ROOT.parent
PACKAGE = WORKSPACE / "output-1.4.3" / "V15VoiceStudio_1.4.3_Update.zip"
MONO = "/tmp/v15-tools/usr/bin/mono"

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    env = os.environ.copy()
    env.update(MONO_CFG_DIR="/tmp/v15-tools/etc", MONO_PATH="/tmp/v15-tools/usr/lib/mono/4.5:" + str(ROOT), LD_LIBRARY_PATH="/tmp/v15-tools/usr/lib:/tmp/v15-tools/usr/lib/x86_64-linux-gnu")
    test_root = Path(tempfile.mkdtemp(prefix="v15-143-release-"))
    with zipfile.ZipFile(PACKAGE) as archive:
        assert archive.testzip() is None
        assert len(archive.namelist()) == len(set(archive.namelist()))
        manifest = ET.fromstring(archive.read("manifest.xml"))
        assert manifest.attrib["version"] == "1.4.3"
        for row in manifest:
            data = archive.read("payload/" + row.attrib["path"])
            assert len(data) == int(row.attrib["size"])
            assert hashlib.sha256(data).hexdigest() == row.attrib["sha256"]
        assert archive.read("V15Updater.exe") == archive.read("payload/V15Updater.exe")
        bootstrap=archive.read("CAP_NHAT.cmd")
        assert b"\r\n" in bootstrap and b"--restart" in bootstrap
        assert not any(row.attrib["path"].startswith(("model/","ai/models/","engine/")) for row in manifest)
        assert "chưa được thử trực tiếp" in archive.read("HUONG_DAN.txt").decode("utf-8")
        archive.extractall(test_root / "extracted")
    print("PASS ZIP CRC, unique entries, version, all payload SHA-256 hashes, bootstrap/payload updater parity")

    preserved = {
        "ai/runtime/python.exe": b"CPU PYTHON FIXTURE",
        "ai/runtime/Lib/site-packages/torch/lib/torch_cpu.dll": b"CPU TORCH FIXTURE",
        "ai/gpu/site-packages/torch/lib/torch_cuda.dll": b"EXISTING CUDA FIXTURE",
        "ai/models/qwen_base/model.safetensors": b"EXISTING MODEL FIXTURE",
        "ai/models/chatter_nano/conds.pt": b"EXISTING CHATTER FIXTURE",
        "my scripts/test.srt": b"1\n00:00:00,000 --> 00:00:02,000\nHello.",
        "my scripts/test.wav": b"EXISTING OUTPUT FIXTURE",
        "user_voices/sample.wav": b"PRIVATE VOICE FIXTURE",
        "user_voices/voices.xml": b"PRIVATE METADATA FIXTURE",
        "session.xml": b"SESSION FIXTURE",
        "queue.xml": b"QUEUE FIXTURE",
        "ffmpeg.exe": b"EXISTING FFMPEG FIXTURE",
        "voice_profiles.tsv": (ROOT / "voice_profiles.tsv").read_bytes(),
    }
    baseline140 = WORKSPACE / 'recover/installer/$_12_/V15VoiceStudio.exe'
    baseline141 = WORKSPACE / "baseline-1.4.2" / "V15VoiceStudio.exe"
    cases = [
        ("from-1.4.0-folder", baseline140, ROOT / "V15Updater.exe", test_root / "extracted"),
        ("from-1.4.2-zip", baseline141, ROOT / "V15Updater.exe", PACKAGE),
        ("from-1.4.2-installed-updater", baseline141, WORKSPACE / "baseline-1.4.2" / "V15Updater.exe", PACKAGE),
    ]
    for name, baseline, updater, package in cases:
        target = test_root / (name + " with spaces")
        target.mkdir()
        shutil.copyfile(baseline, target / "V15VoiceStudio.exe")
        for relative, data in preserved.items():
            file = target / relative
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(data)
        result = subprocess.run([MONO, str(updater), "--apply-cli", "--zip", str(package), "--target", str(target)], env=env, capture_output=True, text=True, timeout=30)
        assert result.returncode == 0, result.stdout + result.stderr
        for row in manifest:
            assert digest(target / row.attrib["path"]) == row.attrib["sha256"]
        for relative, expected in preserved.items():
            assert (target / relative).read_bytes() == expected, relative
        assert not list(target.glob(".v15-stage-*"))
        assert not list(target.glob(".v15-backup-*"))
        assert not (target / ".v15-update-journal.xml").exists()
        print("PASS", name, "all", len(manifest), "payload files match; CPU/GPU/models/voice/queue/settings/SRT/WAV retained; no unfinished transaction")
    # Keep the final updated fixture for running the UI test against the installed payload.
    Path("/tmp/v15-143-installed-test-path.txt").write_text(str(target))
    # Updated app must reject accidental rollback to the older update ZIP.
    old_package=WORKSPACE/"recover/V15VoiceStudio_1.4.2_Update.zip"
    before=digest(target/"V15VoiceStudio.exe")
    rejected=subprocess.run([MONO,str(ROOT/"V15Updater.exe"),"--apply-cli","--zip",str(old_package),"--target",str(target)],env=env,capture_output=True,text=True,timeout=30)
    assert rejected.returncode != 0 and "cũ hơn" in rejected.stderr
    assert digest(target/"V15VoiceStudio.exe")==before
    assert not list(target.glob(".v15-stage-*"))
    print("PASS downgrade to 1.4.2 rejected before replacing current app")
    print("PASS packaged app SHA-256", digest(target / "V15VoiceStudio.exe"))

if __name__ == "__main__":
    main()
