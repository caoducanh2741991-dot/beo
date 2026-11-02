# V15 Voice Studio 1.4.5 verification

Environment: Linux/Mono, Microsoft .NET Framework 4.5 reference assemblies, real FFmpeg. No physical Windows or RTX 2070 test in this release.

New behavior: 53 assertions in Update145Tests, including actual uploaded ADAM/EXCITER WAV normalization, actual VANZAI MP4 selection, source-waveform correlation, exact PCM length, true peak, custom LUFS target, cancellation, failed finishing, preserving existing files, gentle/custom SRT fitting, five-cue calibration, bounded cache invalidation and older settings compatibility. All pass.

Actual WAV measurements: ADAM -26.35 -> -18.05 LUFS; EXCITER -26.57 -> -18.03 LUFS; both -1 dBTP, 1,652,400 samples at 24 kHz (68.85 s) before and after. Actual MP4 sample: VANZAI 4-14 s, 240,000 samples, -1 dBTP; waveform correlation > .9999 against a directly decoded, attenuated excerpt.

Regression suites: 13 core, 12 batch, 20 features, 17 update141, 8 breath streaming, 20 punctuation, 14 native-device policy fixtures, 21 updater, 6 ONNX updater, 11 Python GPU-policy, 4 worker probe. Legacy UI suite and 34 UI145 assertions pass. Six tabs remain attached after native-handle recreation; Studio quality defaults, preset restore, queue settings, MP4 preview button and device controls verified. UI rendered and visually inspected at 1280x845 on Xvfb.

Clone regressions: 18 generation-policy tests, 9 worker-protocol tests, 23 C# external-worker lifecycle assertions. These use fault injection and fixtures, not fresh neural model or GPU inference. The production AI worker, GPU policy, external speech lifecycle and native GPU source match 1.4.4 byte for byte (see Unchanged-AI-GPU.json).

Reproduce: source/run_validation.py; source/run_clone_validation.py --python <Python-with-numpy-soundfile>; source/package_release.py; source/Release145Tests.py. FFmpeg, Mono and official net45 reference assemblies required; the two real WAV and MP4 tests additionally require the original uploaded test assets. Release145Tests verifies exact ZIP CRC/hashes, allowed core payload, upgrades with existing/new updaters, preservation fixtures, downgrade rejection, version 1.4.5.0 and .NET compatibility IL scan.

Scope limitations: no ASR verification of every word at runtime; no claim of objective clone likeness percentage. Fixed SRT starts and natural speed cannot always eliminate silence for short lines. Loudness targets may be approximate for material needing peak protection. Calibration cache is bounded and process-local. No GPU optimization, model replacement or CUDA change.
