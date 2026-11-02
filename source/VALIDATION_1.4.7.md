# V15 Voice Studio 1.4.7 verification

Scope: offline English MP4/MP3-to-SRT, word alignment, multiple-file queue,
review/edit/re-align, optional matching reference TXT, original timeline preservation,
safe adjacent SRT export, and restoring speed controls when switching TXT to SRT.

Environment: Microsoft .NET Framework 4.5 reference assemblies, Mono/Linux, Xvfb UI.
Windows x64 application/updater are compiled here. No physical Windows, RTX 2070,
or Windows embedded-Python/CUDA installation execution is claimed.

Regression suites: 13 core, 12 batch, 20 feature, 17 update141, 53 update145,
32 update146, 8 breath streaming, 20 punctuation, 14 native device fixtures,
21 updater, 6 ONNX updater, legacy UI and 47 Ui147 assertions.
New coverage: 17 subtitle/process checks, 18 subtitle UI integration checks,
11 Python segmentation/reference checks. Subtitle UI tests inject real subprocess
failures/cancellation and exercise batching, completed-file skipping, adjacent exports,
manual corrections, source timing and TXT-to-SRT unlock without preview.
Clone failure-injection suites: generation, worker protocol and 23 external lifecycle
checks. Production ai_worker.py, gpu_policy.py, NativeGpu.cs and ExternalSpeech.cs
remain byte-identical to 1.4.6; the new ASR runtime is isolated from voice dependencies.

Actual ASR QA: run_asr_validation.py tests the supplied clean 68.85s ADAM reference,
MP3, MP4 with 2.5s audio delay, both model choices, matching TXT, and re-alignment.
Its offline audit rejects Python socket connect/address-lookup events while executing
recognition and alignment. See validation-1.4.7/asr-quality-summary.json and
real-asr-verification.txt for results after execution. The 171-word reference result
is a sample-specific normalized word comparison, not a universal accuracy estimate.
Timeline bounds/monotonicity and the known media delay are checked; there is no
manually annotated ground truth for every word boundary, and no frame-accuracy claim.
Test audio/video, model weights and ASR runtime are excluded from the update archive.

Release147Tests.py checks exact ZIP integrity/hashes, all manifest files, supported
core paths, prior-version upgrade fixtures and existing-updater compatibility,
model/runtime/GPU/voice/catalog/script preservation, downgrade rejection and net45
compatibility. Run after package_release.py; detailed results are kept outside the
archive to avoid circular packaging. Included FFmpeg is unchanged from 1.4.6 with
its matching LGPL source, license and build recipe in source/media.

Operational limits: English speech only, up to 2 hours per input / 500 queue entries.
Model estimates can be wrong with music, overlapped speech, names/numbers and accents.
Unaligned words use real ASR fallback only where available and are marked for review;
invalid timestamps or unmatched edited text fail instead of silently dropping words.
First-time ASR installation downloads several GB into its own user-data folder.
Repeated ASR failures can reuse only completed transcript caches, not partially emitted
model decoder state. Existing SRT files are never overwritten; suffixes are used.
