"""Validate small 2.0.12 patch and preserve previously installed RVC data."""
from pathlib import Path
import hashlib
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile
import run_validation as v

root = v.ROOT
base = root.parent / "V15VoiceStudio-2.0.11"
package = root.parent / "output-2.0.12/V15VoiceStudio_2.0.12_Core_Update.zip"
with zipfile.ZipFile(package) as z:
    assert z.testzip() is None
    assert len(z.namelist()) == len(set(z.namelist()))
    manifest = ET.fromstring(z.read("manifest.xml"))
    assert manifest.attrib["version"] == "2.0.12"
    paths = [item.attrib["path"] for item in manifest]
    for item in manifest:
        path = item.attrib["path"]
        data = z.read("payload/" + path)
        assert len(data) == int(item.attrib["size"])
        assert hashlib.sha256(data).hexdigest() == item.attrib["sha256"], path
        assert "/weights/" not in path and "/deps/" not in path, path
        assert path not in ["voice_profiles.tsv", "voices.bin"]
        assert not any(part.startswith(".") for part in path.split("/")), path
    assert "source/TwoVoices.cs" in paths and "source/TwoVoiceControls.cs" in paths
    for relative in ["infer/__init__.py", "infer/module/__init__.py", "tools/__init__.py"]:
        assert "source/community/rvc/" + relative in paths
    model_code = z.read("payload/source/community/rvc/infer/rmvpe.py")
    assert b"configs.config" not in model_code
    assert b"weights_only=True" in model_code
    assert b"RVC runtime 2.0.11" in z.read("payload/source/community/rvc_worker.py")
    assert package.stat().st_size < 50 * 1024 * 1024
    print("PASS ZIP CRC/all hashes; fixed runtime present; no models, FAISS, catalog or GPU data in patch", flush=True)
    folder = Path(tempfile.mkdtemp(prefix="v15-release212-package-"))
    z.extractall(folder)

keep = ["ai/runtime/python.exe", "ai/models/weights.bin", "ai/runtime/torch.dll", "engine/gpu/onnxruntime.dll",
        "model/model.onnx", "breaths/custom.wav", "libritts/voice.bin", "kitten/model.onnx",
        "piper_john/model.onnx", "piper_kristin/model.onnx", "voice_profiles.tsv", "voices.bin",
        "user_voices/voices.xml", "vietnamese_pack/state.dat", "subtitles/config.xml", "settings.xml",
        "example.srt", "example.wav", "source/community/rvc/sample.wav",
        *["source/community/rvc/weights/" + n for n in ["adam.pth", "adam.index", "hubert.bin", "hubert-config.json", "rmvpe.pt"]],
        "source/community/rvc/deps/cp311/faiss/__init__.py",
        "source/community/rvc/deps/cp311/faiss_cpu.libs/openblas.dll",
        "source/community/rvc/deps/cp312/faiss/__init__.py"]

for updater in [base / "V15Updater.exe", root / "V15Updater.exe"]:
    for pack in [package, folder]:
        target = Path(tempfile.mkdtemp(prefix="v15-release212-installed-"))
        for name in ["V15VoiceStudio.exe", "V15Updater.exe"]:
            shutil.copyfile(base / name, target / name)
        for name in keep:
            path = target / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(("PRESERVE " + name).encode())
        old = target / "source/community/rvc/infer/rmvpe.py"
        old.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(base / "source/community/rvc/infer/rmvpe.py", old)
        before = {name: hashlib.sha256((target / name).read_bytes()).hexdigest() for name in keep}
        result = subprocess.run([v.MONO, str(updater), "--apply-cli", "--zip" if pack == package else "--folder",
                                 str(pack), "--target", str(target)], env=v.ENV, capture_output=True, text=True, timeout=90)
        assert result.returncode == 0, result.stdout + result.stderr
        for name in ["V15VoiceStudio.exe", "V15Updater.exe"]:
            assert (target / name).read_bytes() == (root / name).read_bytes()
        assert b"configs.config" not in old.read_bytes()
        for name, digest in before.items():
            assert hashlib.sha256((target / name).read_bytes()).hexdigest() == digest, name
        print("PASS", updater.parent.name, "ZIP" if pack == package else "folder", "upgrade; preserves", len(keep), "model/reference/settings/source/audio paths", flush=True)
        result = subprocess.run([v.MONO, str(root / "V15Updater.exe"), "--apply-cli", "--zip",
                                 str(root.parent / "output-2.0.9/V15VoiceStudio_2.0.9_Update.zip"), "--target", str(target)],
                                env=v.ENV, capture_output=True, text=True, timeout=90)
        assert result.returncode != 0
        assert (target / "V15VoiceStudio.exe").read_bytes() == (root / "V15VoiceStudio.exe").read_bytes()
        print("PASS downgrade rejected", flush=True)
        shutil.rmtree(target)
shutil.rmtree(folder)
print("RELEASE 2.0.12 VERIFIED (Mono/Linux; physical Windows/CUDA not tested)")
