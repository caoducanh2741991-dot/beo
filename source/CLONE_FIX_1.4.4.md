# V15 Voice Studio 1.4.4 — clone generation and worker recovery

The reported failures were unusually long model output and a stalled cloned
voice at cue 10 of a 21-cue SRT. The supplied GPU probe recognized RTX 2070
and passed CUDA arithmetic, but did not record GPU usage during synthesis.
The user's Reno SRTs and ADAM reference audio were not available for an exact
private-input reproduction.

## Changes

Qwen now uses sampling settings consistent with its model interface, changes
the seed and sampling parameters when a waveform is invalid, and retries up
to three times per phrase. Text is split into phrases of at most 24 words.
The phrase token ceiling depends on its length. Empty, nonfinite, silent,
extremely short, excessively long or cap-truncated audio is rejected, rather
than published as a successful cue. This validation does not establish that
every spoken word is correct; runtime ASR is not part of the app.

Installed qwen-tts 0.1.1 drops arbitrary stopping-criteria arguments at its
outer API before calling the talker generator. The worker therefore injects
the finite generation deadline at the actual talker.generate boundary and
restores the original method in a finally block. This was checked both with
a fixture that drops outer kwargs and with the real installed Qwen model.

The C# owner enforces separate absolute startup and synthesis deadlines,
consumes activity messages without extending those deadlines, and allows at
most one restart for a stalled or crashed process. It drains final output
before classifying a rapid child exit. Auto can start a fresh CPU worker on
a recognized CUDA error; required-GPU mode reports the error. Cancellation
does not restart generation. Terminal errors clear the worker before the
next batch file. Old callbacks are isolated by a worker epoch.

A failed attempt cannot replace a previously valid cached phrase or output
WAV. Tempo changes reuse accepted audio. Failed files do not publish an
incomplete master WAV. Existing output names continue to be protected.

Activity includes file/cue/timecodes, phrase/attempt, actual device and
elapsed wait. Terminal diagnostics go to the application's local data log,
with full failure details available from the batch-result tooltip. The
source folder still receives only the final WAV.

## Completed validation, 2026-10-22

| Suite | Passed | Evidence |
| --- | ---: | --- |
| Clone generation policy | 18 | Invalid waveforms, bounded attempts, new seeds, custom voice, token ceiling, inner deadline and restoration, word order, CUDA/reference errors |
| Worker subprocess protocol | 9 | Real NumPy/SoundFile/FFmpeg with a fake Qwen model; reference modes, cache integrity, atomic WAV conversion, benign ONNX import |
| C# child-process lifecycle | 23 | Timeout/crash/startup recovery, CPU fallback, required GPU, malformed protocol, invalid WAV, cancellation and no remaining workers |
| Core WAV/timeline | 13 | SRT and audio operations |
| Batch | 12 | Multiple files, output locations and name protection |
| Features | 20 | Reference handling, settings, voice storage |
| Timeline fitting / 1.4.1 regression | 17 | Locked SRT, bounded tempo, streaming 5,001 cues, device settings |
| Breath streaming | 8 | Final WAV publishing |
| Punctuation | 20 | Pauses and limits |
| Native GPU routing fixtures | 14 | Selection and fallback policy; not GPU computation |
| Core updater | 21 | Validation, rollback and interrupted-update handling |
| ONNX add-on updater fixtures | 6 | Preserves CPU/Torch data |
| Python GPU policy | 11 | Device/fallback selection fixtures |
| Worker probe | 4 | UTF-8 and device/error protocol |
| UI | Existing suite + 29 release checks | Tools version, clone settings, punctuation, shared batch options |

The 21-cue batch failure was simulated at cue 10: the first file failed with
the precise cue in its diagnostic and no partial WAV; the next file completed
using a fresh process. The fixture does not reproduce the user's specific
audio or claim a particular cause for every possible model failure.

### Real Qwen model, not only fixtures

Test environment: Linux CPU, torch 2.6.0+cpu, qwen-tts 0.1.1,
transformers 4.57.3, official Qwen3-TTS-12Hz-0.6B-Base weights and codec,
official public reference clip. The worker ran with its offline network
restriction enabled. Dependencies and model weights are not part of this ZIP.

Two reference-plus-transcript samples produced valid PCM16 mono 24 kHz WAVs
of 2.455 and 2.548 seconds. The real production C# renderer also called the
worker with reference-audio-only cloning and produced an exact 12-second
locked WAV, with 3.352 seconds of accepted voiced audio and unchanged SRT.
Reusing that audio fitted the same sentence to an exact 3.300-second locked
cue at speed 1.034, below its configured maximum of 1.3.

All four outputs were checked using a separately installed Whisper tiny.en
model. Their recognized sentences matched the target text. This is a small
content-correctness smoke test, not a statistical quality assessment or an
evaluation of how closely the speaker matches the reference. See
`real-qwen-clone-asr.json` and `RealCloneIntegrationTests.txt` in the validation
folder. ASR is used only for these tests and is not shipped in the application.

### Release and platform compatibility

The EXEs target x64 .NET Framework 4.5 and are compiled against Microsoft's
reference assemblies with -nostdlib/-noconfig. The prior fix avoiding the
unsupported String.Contains(Char) overload is retained. Release144Tests.py
checks both installed EXE versions and IL, all ZIP hashes and CRCs, extracted
and ZIP update routes, the installed 1.4.3.1 updater, preservation fixtures,
and rejection of a downgrade. The exact final-package test log is retained
outside the ZIP to avoid recursive package hashes.

No physical Windows/RTX 2070 test was possible here. Real Chatterbox generation
and the user's ADAM/Reno input remain untested. GPU error behavior above was
failure-injected. Existing native ONNX/Torch CUDA packs are unchanged.

## Reproduce the checks

1. `python source/run_validation.py` — uses the configured Mono tools and
   Microsoft net45 reference assemblies to build and test the app/updater.
2. `python source/run_clone_validation.py --python PYTHON_WITH_NUMPY_SOUNDFILE`
   — deterministic clone regressions with FFmpeg on PATH; no model downloads.
3. Compile RealCloneIntegrationTests.cs against the built app and run with a
   prepared offline installation root and V15_PYTHON set to its Python runtime
   on Linux. This optional test needs the genuine Qwen Base model and reference.
4. `python source/package_release.py`, then `python source/Release144Tests.py`.

Do not claim the fixture checks constitute physical Windows/GPU testing.
