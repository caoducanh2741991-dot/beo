"""Regression for the real RMVPE CUDA constructor; allocation is mocked.

This executes device selection in RMVPE, not CUDA kernels. Separately run the
real CPU speech and cancellation tests. A physical GPU is not available here.
"""
import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parent.parent
RVC = ROOT / "source/community/rvc"
sys.path.insert(0, str(RVC))
import torch
from infer import rmvpe


class FakeMel:
    def __init__(self, half, *args):
        self.half = half

    def to(self, device):
        self.device = str(device)
        return self


class FakeNet:
    def __init__(self, *args):
        self.precision = None

    def load_state_dict(self, weights):
        pass

    def eval(self):
        return self

    def half(self):
        self.precision = "half"
        return self

    def float(self):
        self.precision = "float"
        return self

    def to(self, device):
        self.device = str(device)
        return self


class DeviceRegression(unittest.TestCase):
    def create(self, device, half=False, module=rmvpe):
        with patch.object(module, "MelSpectrogram", FakeMel), patch.object(module, "E2E", FakeNet), \
             patch.object(torch, "load", return_value={}) as load, \
             patch.object(torch.cuda, "current_device", side_effect=AssertionError("unexpected GPU re-selection")), \
             patch.object(torch.cuda, "is_available", side_effect=AssertionError("unexpected GPU re-selection")):
            obj = module.RMVPE("unused-checkpoint.pt", half, device=device)
            load.assert_called_once_with("unused-checkpoint.pt", map_location="cpu", weights_only=True)
        return obj

    def test_01_original_cuda_failure_reproduced(self):
        original = ROOT.parent / "V15VoiceStudio-2.0.10/source/community/rvc/infer/rmvpe.py"
        spec = importlib.util.spec_from_file_location("old_rmvpe_failure_fixture", original)
        old = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(old)
        with patch.object(old, "MelSpectrogram", FakeMel), patch.object(old, "E2E", FakeNet):
            with self.assertRaises(ModuleNotFoundError) as error:
                old.RMVPE("unused.pt", False, device="cuda:0")
        self.assertEqual(error.exception.name, "configs")

    def test_02_cuda_zero_constructor_passes_without_configs(self):
        obj = self.create("cuda:0")
        self.assertEqual((obj.device, obj.model.device, obj.mel_extractor.device), ("cuda:0",) * 3)
        self.assertFalse(obj.is_half)
        self.assertEqual(obj.model.precision, "float")

    def test_03_requested_cuda_index_is_preserved(self):
        obj = self.create(torch.device("cuda:2"))
        self.assertEqual(obj.device, "cuda:2")

    def test_04_cuda_without_index_means_zero(self):
        self.assertEqual(self.create("cuda").device, "cuda:0")

    def test_05_cpu_remains_float32(self):
        obj = self.create("cpu", True)
        self.assertFalse(obj.is_half)
        self.assertEqual((obj.model.precision, obj.model.device), ("float", "cpu"))

    def test_06_explicit_half_remains_explicit_on_cuda(self):
        obj = self.create("cuda:0", "true")
        self.assertTrue(obj.is_half)
        self.assertEqual(obj.model.precision, "half")

    def test_07_missing_device_fails_before_checkpoint_load(self):
        with patch.object(torch, "load") as load:
            with self.assertRaisesRegex(ValueError, "explicitly selected device"):
                rmvpe.RMVPE("unused.pt", False)
            load.assert_not_called()

    def test_08_unsupported_backend_fails_before_load(self):
        with patch.object(torch, "load") as load:
            with self.assertRaisesRegex(ValueError, "CPU or NVIDIA CUDA"):
                rmvpe.RMVPE("unused.pt", False, "mps")
            load.assert_not_called()

    def test_09_no_webui_config_import_remains(self):
        self.assertNotIn("configs.config", (RVC / "infer/rmvpe.py").read_text())

    def test_10_local_packages_win_over_installed_name_collisions(self):
        with tempfile.TemporaryDirectory(prefix="v15-other-packages-") as tmp:
            for name in ["infer", "tools"]:
                p = Path(tmp) / name
                p.mkdir()
                (p / "__init__.py").write_text("raise RuntimeError('foreign package loaded')\n")
            code = "import sys;sys.path.insert(0," + repr(tmp) + ");sys.path.insert(0," + repr(str(RVC)) + ");import infer,tools;print(infer.__file__);print(tools.__file__)"
            result = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, timeout=10)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(sum(str(RVC) in x for x in result.stdout.splitlines()), 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
