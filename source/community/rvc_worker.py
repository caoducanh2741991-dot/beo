"""V15 audio-to-audio RVC adapter. No downloads or remote inference.

Protocol matches ExternalSpeech: SYNTH <b64 input WAV> <b64 output WAV> 1.
The caller synthesizes Kokoro Michael first and measures the returned waveform.
Upstream inference modules and separately attributed weights live in ./rvc.
"""
import argparse
import base64
import contextlib
import json
import os
from pathlib import Path
import socket
import sys
import traceback

OUT = sys.stdout
ACTIVE_DEVICE = "cpu"
DEVICE_MODE = "cpu"
HOME = Path(__file__).resolve().parent
APP = HOME.parents[1]
os.environ.update(HF_HUB_OFFLINE="1", TRANSFORMERS_OFFLINE="1", HF_HUB_DISABLE_TELEMETRY="1",
                  DO_NOT_TRACK="1", TOKENIZERS_PARALLELISM="false", RVC_CUDA_GRAPH="0")
os.environ.setdefault("NUMBA_CACHE_DIR", str(Path(os.environ.get("TEMP", "/tmp")) / "V15_rvc_numba"))


def no_network(*args, **kwargs):
    raise OSError("V15 RVC runs offline; network access is disabled")


socket.create_connection = no_network
socket.socket.connect = no_network
socket.socket.connect_ex = no_network


def emit(kind, message):
    print("V15_" + kind + "\t" + base64.b64encode(message.encode("utf-8")).decode("ascii"), file=OUT, flush=True)


def dec(value):
    return base64.b64decode(value, validate=True).decode("utf-8")


def run():
    global ACTIVE_DEVICE, DEVICE_MODE
    parser = argparse.ArgumentParser()
    parser.add_argument("--device", choices=["auto", "gpu", "cpu"], default="auto")
    parser.add_argument("--model", default="rvc_adam")
    parser.add_argument("--speaker", default="Adam")
    parser.add_argument("--threads", type=int, default=2)
    parser.add_argument("--work-dir", required=True)
    parser.add_argument("--seed-offset", type=int, default=0)
    a = parser.parse_args()
    DEVICE_MODE = a.device
    if a.model != "rvc_adam":
        raise ValueError("Unsupported RVC voice")
    sys.path.insert(0, str(APP / "ai"))
    from gpu_policy import runtime_path, select_device, is_gpu_error
    installed = runtime_path(APP / "ai", a.device)
    if os.name == "nt" and a.device == "gpu" and not installed:
        raise RuntimeError("Chưa có gói CUDA124 cho RVC. Chọn Auto hoặc cài đủ gói GPU.")
    # Only isolated additional FAISS modules are supplied here; existing AI
    # libraries (torch/transformers/librosa) retain their installed versions.
    if os.name == "nt":
        deps = HOME / "rvc" / "deps" / ("cp%d%d" % sys.version_info[:2])
        if not deps.is_dir():
            raise RuntimeError("Gói Adam RVC cần Python 3.11 / 3.12 x64 của bản cài V15 đầy đủ.")
        sys.path.insert(0, str(deps))
    sys.path.insert(0, str(HOME / "rvc"))
    weights = HOME / "rvc" / "weights"
    os.environ["rmvpe_root"] = str(weights)
    with contextlib.redirect_stdout(sys.stderr):
        import numpy as np
        import soundfile as sf
        import torch
        import faiss
        import librosa
        from transformers import HubertConfig
        from infer.hubert import HubertModelWithFinalProj
        from infer.module.models import SynthesizerTrnMs768NSFsid
        from infer.pipeline import Pipeline
        from infer.rmvpe import RMVPE
        from types import SimpleNamespace
        device, info = select_device(torch, a.device)
        ACTIVE_DEVICE = device
        info += " · HuBERT/RVC/RMVPE: " + ("CUDA" if device.startswith("cuda") else "CPU") + "; index: CPU"
        torch.set_num_threads(max(1, min(4, a.threads)))
        torch.set_num_interop_threads(1)
        faiss.omp_set_num_threads(max(1, min(4, a.threads)))
        emit("STATUS", "Đang nạp Adam RVC, HuBERT, RMVPE và index · " + info)
        checkpoint = torch.load(str(weights / "adam.pth"), map_location="cpu", weights_only=True)
        if checkpoint.get("version") != "v2" or checkpoint.get("f0", 1) != 1:
            raise ValueError("Expected Adam v2 F0 checkpoint")
        config = list(checkpoint["config"])
        config[-3] = checkpoint["weight"]["emb_g.weight"].shape[0]
        rate = int(config[-1])
        synth = SynthesizerTrnMs768NSFsid(*config, is_half=False)
        del synth.enc_q
        result = synth.load_state_dict(checkpoint["weight"], strict=False)
        if result.missing_keys or result.unexpected_keys:
            raise ValueError("Adam checkpoint does not match inference architecture: " + str(result))
        synth = synth.float().eval().to(device)
        del checkpoint
        hc = HubertConfig.from_dict(json.loads((weights / "hubert-config.json").read_text()))
        hubert = HubertModelWithFinalProj(hc)
        result = hubert.load_state_dict(torch.load(str(weights / "hubert.bin"), map_location="cpu", weights_only=True), strict=False)
        if result.missing_keys or result.unexpected_keys:
            raise ValueError("HuBERT checkpoint does not match configuration: " + str(result))
        hubert = hubert.float().eval().to(device)
        pipeline = Pipeline(rate, SimpleNamespace(x_pad=1, x_query=3, x_center=12, x_max=15, is_half=False, device=device))
        pipeline.model_rmvpe = RMVPE(str(weights / "rmvpe.pt"), is_half=False, device=device)
        index_path = str(weights / "adam.index")
        pipeline._index = faiss.read_index(index_path)
        if pipeline._index.d != 768 or pipeline._index.ntotal < 8:
            raise ValueError("Invalid Adam retrieval index")
        pipeline._index_vectors = pipeline._index.reconstruct_n(0, pipeline._index.ntotal)
        pipeline._index_path = index_path
    emit("READY", info + " · RVC v2 / RMVPE / index 0.75")
    for line in sys.stdin:
        if not line.strip():
            continue
        try:
            fields = line.rstrip("\r\n").split("\t")
            if len(fields) != 4 or fields[0] != "SYNTH" or abs(float(fields[3]) - 1) > .0001:
                raise ValueError("Invalid RVC protocol request")
            source, output = Path(dec(fields[1])), Path(dec(fields[2]))
            if source.resolve() == output.resolve() or source.suffix.lower() != ".wav" or output.suffix.lower() != ".wav":
                raise ValueError("RVC requires distinct input/output WAV files")
            emit("STATUS", "Đang chuyển giọng Adam RVC · " + info)
            with contextlib.redirect_stdout(sys.stderr), torch.inference_mode():
                meta = sf.info(str(source))
                if not .1 <= meta.duration <= 180 or meta.channels != 1:
                    raise ValueError("RVC input must be mono speech, 0.1–180 seconds")
                data, sr = sf.read(str(source), dtype="float32")
                if not np.isfinite(data).all() or float(np.max(np.abs(data))) < .0001:
                    raise ValueError("Invalid or silent RVC input")
                data = librosa.resample(data, orig_sr=sr, target_sr=16000)
                peak = float(np.max(np.abs(data))) / .95
                if peak > 1:
                    data = data / peak
                torch.manual_seed(a.seed_offset)
                result = pipeline.pipeline(hubert, synth, 0, data, [0., 0., 0.], 0, "rmvpe", index_path, .75, 1, rate, 0, 1., "v2", .33)
                seconds = len(result) / rate
                if not np.isfinite(result).all() or not max(.08, meta.duration * .65) <= seconds <= meta.duration * 1.35 + .25 or np.max(np.abs(result.astype(np.float32))) < 4:
                    raise ValueError("RVC returned invalid, silent or truncated audio")
                sf.write(str(output), result, rate, subtype="PCM_16")
            emit("OK", info + " · RVC v2 / RMVPE / index 0.75")
        except Exception as error:
            traceback.print_exc(file=sys.stderr)
            if a.device == "auto" and device.startswith("cuda") and is_gpu_error(error):
                emit("ERROR", "GPU_FALLBACK: " + str(error))
                return
            emit("ERROR", str(error))


if __name__ == "__main__":
    try:
        run()
    except Exception as error:
        traceback.print_exc(file=sys.stderr)
        prefix = ""
        if DEVICE_MODE == "auto" and ACTIVE_DEVICE.startswith("cuda"):
            from gpu_policy import is_gpu_error
            if is_gpu_error(error):
                prefix = "GPU_FALLBACK: "
        emit("ERROR", prefix + str(error))
        sys.exit(1)
