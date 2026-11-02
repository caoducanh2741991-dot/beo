import importlib.util
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import types
import unittest

spec = importlib.util.spec_from_file_location("gpu_policy", Path(__file__).with_name("gpu_policy.py"))
policy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(policy)


def torch_stub(available=True, capability=(7, 5), architectures=None):
    return types.SimpleNamespace(cuda=types.SimpleNamespace(
        is_available=lambda: available, get_device_capability=lambda _: capability,
        get_arch_list=lambda: architectures or ["sm_75", "sm_80", "sm_86"],
        mem_get_info=lambda _: (6 * 2**30, 8 * 2**30),
        get_device_name=lambda _: "RTX 2070 (test simulation)"))


class DeviceTests(unittest.TestCase):
    def test_rtx2070_auto(self):
        device, info = policy.select_device(torch_stub(), "auto")
        self.assertEqual(device, "cuda:0")
        self.assertIn("6.0 / 8.0 GB", info)

    def test_forced_cpu_does_not_probe_cuda(self):
        self.assertEqual(policy.select_device(object(), "cpu")[0], "cpu")

    def test_missing_cuda_falls_back_in_auto(self):
        device, info = policy.select_device(torch_stub(False), "auto")
        self.assertEqual(device, "cpu")
        self.assertIn("dự phòng", info)

    def test_missing_cuda_is_explicit_error_in_gpu_mode(self):
        with self.assertRaisesRegex(RuntimeError, "Không thể dùng NVIDIA GPU"):
            policy.select_device(torch_stub(False), "gpu")

    def test_older_architecture_is_not_claimed_supported(self):
        self.assertEqual(policy.select_device(torch_stub(capability=(6, 1)), "auto")[0], "cpu")

    def test_incompatible_runtime_does_not_run(self):
        self.assertEqual(policy.select_device(torch_stub(architectures=["sm_90"]), "auto")[0], "cpu")

    def test_driver_error_falls_back(self):
        module = torch_stub()
        def fail():
            raise RuntimeError("CUDA driver version is insufficient")
        module.cuda.is_available = fail
        self.assertIn("driver", policy.select_device(module, "auto")[1])

    def test_recoverable_errors_are_specific(self):
        self.assertTrue(policy.is_gpu_error(RuntimeError("CUDA out of memory")))
        self.assertTrue(policy.is_gpu_error(RuntimeError("device-side assert triggered")))
        self.assertFalse(policy.is_gpu_error(RuntimeError("Missing reference audio file")))

    def test_gpu_runtime_is_separate_from_cpu(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "gpu/site-packages/torch"
            path.mkdir(parents=True)
            (path / "__init__.py").write_text("")
            original = list(sys.path)
            try:
                self.assertFalse(policy.runtime_path(root, "cpu"))
                self.assertEqual(sys.path, original)
                self.assertTrue(policy.runtime_path(root, "auto"))
                self.assertEqual(Path(sys.path[0]), path.parent)
            finally:
                sys.path[:] = original

    def test_offline_worker_blocks_python_connections(self):
        worker = str(Path(__file__).with_name("ai_worker.py"))
        code = "import runpy,socket;runpy.run_path(%r);socket.create_connection(('example.com',443))" % worker
        result = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("network access is disabled", result.stderr)

    def test_isolated_python_loads_own_device_module(self):
        worker = str(Path(__file__).with_name("ai_worker.py"))
        code = "import runpy;runpy.run_path(%r);import gpu_policy;print(gpu_policy.__file__)" % worker
        result = subprocess.run([sys.executable, "-I", "-c", code], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(str(Path(worker).parent / "gpu_policy.py"), result.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
