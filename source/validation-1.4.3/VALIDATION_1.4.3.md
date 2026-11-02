# V15 Voice Studio 1.4.3 validation

Validated on Linux with Mono 6.8 and real FFmpeg. Windows .NET x64 application and updater binaries compiled successfully.

- Existing SRT/TXT/WAV core checks: 13 passed.
- Batch and queue checks: 12 passed; state/presets/custom-voice metadata checks: 20 passed.
- Timeline speed fitting and 5,001-cue streaming regression checks: 17 passed; pitch preservation measured using real FFmpeg.
- Streaming breath checks: 8 passed.
- Punctuation pacing checks: 20 passed, covering decimals, abbreviations, paragraph breaks, silence deduplication, pause reduction before speed-up, shared settings and unchanged locked SRT timestamps.
- Native CUDA process checks: 14 passed using executable fixtures for Kokoro, LibriTTS, Kitten and Piper; explicit CPU, Auto recovery, sticky CPU fallback, strict-GPU rejection, cancellation and invalid-output cleanup included.
- GPU policy: 11 tests passed. Actual worker probe subprocess: 4 fixture cases passed, including UTF-8 protocol on stdout.
- Existing updater transaction, path, rollback and recovery checks: 21 passed.
- Native ONNX multipart updater: 7 checks passed including actual reassembly and final SHA256 verification of all 42 shipped files (2,848,813,714 bytes total).
- Static PE checks: all 32 DLL/EXE files are x64; static dependency names resolve within the pack or to standard Windows system DLLs. Original source/archive/wheel hashes and licenses are retained.
- Existing UI checks passed. New 1.4.3 UI suite: 29 checks passed, repeated against the actual installed payload. All six tabs, device/pause settings, queue sharing and Tools buttons are accessible. Studio/Tools screenshots inspected internally.
- Release ZIP was applied from genuine 1.4.0 and 1.4.2 EXEs, via extracted bootstrap, ZIP and the 1.4.2 installed updater. All payload hashes matched; CPU/GPU runtimes, models, reference voices, scripts, WAV files and session/queue data were retained.
- Downgrade to the older 1.4.2 ZIP was rejected before executable replacement.

Limitations: no real Windows DLL-loading test or physical RTX 2070 synthesis test was possible in this environment. Process fixtures establish dispatch/error behavior, not GPU inference performance or every model's audible quality. The app provides a selected-voice GPU synthesis check for the target machine. Cloning-render bug diagnosis is deferred at the user's request; no repair of that bug is claimed.
