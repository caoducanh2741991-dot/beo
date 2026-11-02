"""Real worker error isolation and offline checks; CPU only, no voice score."""
from pathlib import Path
import base64
import os
import subprocess
import sys
import tempfile
import wave

ROOT = Path(__file__).resolve().parent.parent
WORKER = ROOT / "source/community/rvc_worker.py"


def main():
    tmp = Path(tempfile.mkdtemp(prefix="v15-rvc-protocol-"))
    silent = tmp / "silent.wav"
    with wave.open(str(silent), "wb") as wav:
        wav.setparams((1, 2, 24000, 0, "NONE", "not compressed"))
        wav.writeframes(bytes(48000))
    enc = lambda s: base64.b64encode(str(s).encode()).decode()
    out = tmp / "never.wav"
    requests = ["INVALID", "SYNTH\t" + enc(silent) + "\t" + enc(out) + "\t0.9",
                "SYNTH\t" + enc(silent) + "\t" + enc(silent) + "\t1",
                "SYNTH\t" + enc(silent) + "\t" + enc(out) + "\t1"]
    run = subprocess.run([sys.executable, str(WORKER), "--device", "cpu", "--work-dir", str(tmp)],
                         input="\n".join(requests) + "\n", capture_output=True, text=True, timeout=120)
    assert run.returncode == 0, run.stdout + run.stderr
    lines = run.stdout.splitlines()
    assert sum(x.startswith("V15_READY\t") for x in lines) == 1
    assert sum(x.startswith("V15_ERROR\t") for x in lines) == 4
    assert not out.exists() and silent.stat().st_size == 48044
    print("PASS bad requests and silence return four errors without corrupting input/output or reloading worker")
    run = subprocess.run([sys.executable, str(WORKER), "--model", "unknown", "--work-dir", str(tmp)],
                         input="", capture_output=True, text=True, timeout=10)
    assert run.returncode != 0 and "V15_ERROR\t" in run.stdout and "V15_READY\t" not in run.stdout
    print("PASS unknown voice cannot silently load another voice")
    # Import alone must block network before any model is loaded. Separate child
    # avoids changing this test runner's socket behavior.
    code = "import runpy,socket;runpy.run_path(" + repr(str(WORKER)) + ");socket.create_connection(('example.org',443))"
    run = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, timeout=10)
    assert run.returncode != 0 and "network access is disabled" in run.stderr
    print("PASS worker network access is explicitly disabled")
    print("TOTAL RVC PROTOCOL 3 passed; real model load, CPU/Linux.")


if __name__ == "__main__":
    main()
