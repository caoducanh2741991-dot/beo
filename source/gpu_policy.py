"""Device selection shared by the offline worker and its hardware probe."""
from pathlib import Path
import sys


def runtime_path(root, mode):
    path = Path(root) / "gpu" / "site-packages"
    if mode != "cpu" and (path / "torch" / "__init__.py").is_file():
        sys.path.insert(0, str(path))
        return True
    return False


def select_device(torch, mode):
    if mode == "cpu":
        return "cpu", "CPU · đã chọn thủ công"
    try:
        if not torch.cuda.is_available():
            raise RuntimeError("CUDA chưa sẵn sàng; cần gói GPU và driver NVIDIA tương thích")
        major, minor = torch.cuda.get_device_capability(0)
        if (major, minor) < (7, 5):
            raise RuntimeError("Gói này hỗ trợ NVIDIA Turing (RTX 20) trở lên")
        archs = torch.cuda.get_arch_list()
        if not any(x.startswith("sm_") and int(x[3:]) // 10 == major and int(x[3:]) % 10 <= minor for x in archs) and not any(x.startswith("compute_") and int(x[8:]) <= major * 10 + minor for x in archs):
            raise RuntimeError("Thư viện CUDA không chứa mã phù hợp GPU này")
        free, total = torch.cuda.mem_get_info(0)
        return "cuda:0", "%s · VRAM trống %.1f / %.1f GB" % (torch.cuda.get_device_name(0), free / 2**30, total / 2**30)
    except Exception as exc:
        if mode == "gpu":
            raise RuntimeError("Không thể dùng NVIDIA GPU: " + str(exc)) from exc
        return "cpu", "CPU dự phòng · " + str(exc)


def is_gpu_error(exc):
    message = str(exc).lower()
    return any(term in message for term in ("cuda", "cublas", "cudnn", "out of memory", "device-side assert", "no kernel image"))
