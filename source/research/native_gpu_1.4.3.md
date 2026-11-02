# Native CUDA source pins and validation limits

- sherpa-onnx 1.13.8, official Windows x64 CUDA 12.x / cuDNN 9.x build with ONNX Runtime 1.28.2.
  https://github.com/k2-fsa/sherpa-onnx/releases/tag/v1.13.8
  Archive SHA256: 066c5b54dbafaa1388001a9c9837ac1374dbba6d6678f193ca06aa0d8e94d8c3.
- NVIDIA CUDA libraries from the official NVIDIA Windows wheels on PyPI, with per-wheel SHA256 from PyPI metadata verified before extraction. Version pins are in prepare_native_gpu.py and actual URLs/hashes in the native pack info.json.
- CUDA runtime 12.8.90, cuBLAS 12.8.4.1, cuDNN 9.8.0.87, cuFFT 11.3.3.83, NVRTC / nvJitLink 12.8.93.
- CUDA EP compatibility and system requirements:
  https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html
- cuDNN 9.8 documents support for Turing, compute capability 7.5, and CUDA 12.8:
  https://docs.nvidia.com/deeplearning/cudnn/backend/v9.8.0/reference/support-matrix.html
- Microsoft x64 redistributable from https://aka.ms/vs/17/release/vc_redist.x64.exe; app-local minimum x64 CRT DLLs extracted with original license. Its current download SHA256 is retained in info.json.

The package deliberately isolates native ONNX DLLs in engine/gpu. Existing Torch CUDA 12.4 and CPU runtimes are retained. A GPU selection requests the CUDA provider. Auto retries with the original CPU engine if CUDA fails; strict GPU rejects a full CPU fallback. Partial CPU graph operations remain possible with ONNX Runtime.

Regression tests validate process dispatch, protocol, fallback, cancellation, timeline/WAV output and updater transactions using fixtures on Linux/Mono. Static PE architecture/import checks and full archive reassembly/hash checks validate packaging. These checks are not an actual Windows DLL-load or RTX 2070 synthesis test. Use the app's selected-voice GPU test on the target Windows machine.

Per user instruction, cloning-render bug diagnosis is deferred. No change in this release claims to repair that issue.
