# V15 Voice Studio 1.4.11 validation

Release target: Windows x64 / .NET Framework 4.5. Built against Microsoft's .NET 4.5 reference assemblies. Execution environment for this release's tests: Mono/Linux, FFmpeg; UI rendered through Xvfb. No physical Windows installation or RTX 2070 test in this session.

## New anchored timeline path

- `AnchoredTests`: 67 assertions passed in the final run including the supplied real voice. Covers stage construction, natural sentence requests across display cues, exact sample duration, speedup/slowdown and pitch, speed bounds, failure preventing WAV publication, original SRT preservation, hard anchors, source silence, long-unit word preservation, complete timed/plain-TXT batch-mode routing matrix, settings and queue persistence, Auto/custom breaths included in stage budgets, normalization, optional aligned SRT callback, alignment failure/text mismatch, cancellation, and batch continuation/rollback.
- Real supplied mono 24 kHz PCM voice: 70.177875 seconds fitted to 74.210 seconds (1,781,040 samples). Original source hash unchanged. This validates measured DSP duration, not manual listening, cloning similarity or transcript accuracy. Exact speed and tail adjustment are in `AnchoredTests.txt`.
- `AnchoredUiTests`: 28 assertions covering mode selection, hard-anchor editing, saved settings, reloading unchanged source anchors, stale-anchor rejection after source edits, queued anchors, preview/report controls, busy state and TXT/legacy routing. Screenshot inspected; screenshot and test media excluded from package.
- Python ASR/core/alignment wrapper tests: 76 tests passed with bundled local phrase model, nltk 3.9.1 and numpy 2.1.3. New wrapper's 8 tests cover measured timestamps versus search windows, hard boundaries, weak/missing/changed words, and invalid/overlong windows. Existing ASR model code unchanged.

## Regression checks

- Core 13; queue 12; feature/state 20; prior 1.4.1 behavior 17; breath streaming 8; punctuation 20; native GPU routing fixtures 14; subtitle document/process 17; updater 21; ONNX updater 6.
- UI: general 1.4.11 47; subtitle 18; quality 13; refinement 7; speech review 14, in addition to the new 28 anchored checks.
- Python GPU policy and worker probe passed. GPU routing checks use controlled executable fixtures; they do not demonstrate real CUDA/model inference.
- `model-gpu-and-legacy-unchanged.json`: 27 baseline model/GPU/audio/ASR files byte-identical; prior Renderer body identical after removing only the new mode dispatcher. Native ONNX and copied-voice engine/model code untouched.

## Release/install checks

`Release1411Tests.py` checks the exact packaged ZIP: CRC, all manifest lengths and SHA-256 hashes, updater parity, EXE versions, and no replacement model/runtime/CUDA/catalog/private voice data. Both installed 1.4.10 and new 1.4.11 updater are exercised with ZIP and extracted-folder inputs; each must preserve 11 fixture model/runtime/voice/settings/script/output files. Downgrade to 1.4.10 must be rejected. Actual outcome is recorded in `Release1411Tests.txt` after packaging.

## Limits

- Target T is the final source SRT end time. Without a video input this mode cannot infer a longer video duration or scene changes. Automatic stages are punctuation/gap heuristics; use explicit end-of-stage anchors for important scene boundaries.
- Default WAV-only output validates durations, not transcription or naturalness. No forced rewriting, deleted words, speed beyond bounds or multi-second padding to conceal missing text. Up to 20 ms tail sample adjustment is recorded in the report. Existing source gaps are deliberately retained.
- Optional CTC SRT generation reuses the installed subtitle environment and measures the final fitted PCM. Missing runtime/model, unresolved alignment or inconsistent stage content causes a review report; a valid WAV remains available. The complete CTC model was not rerun on actual audio in this session; wrapper logic and failure handling were tested. No new download is required for WAV-only mode.
- No new TTS model/voice catalogue, physical Windows/GPU benchmark, or listening-based quality score is claimed.
