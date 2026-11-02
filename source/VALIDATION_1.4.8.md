# V15 Voice Studio 1.4.8 verification

Scope: conservative audio-backed duplicate repair, punctuation/case restoration without
lexical rewriting, contextual CTC alignment and a bounded retry for weak words,
phrase-aware subtitle grouping, explicit review/approval state and versioned cache.
The voice synthesis/GPU implementation is unchanged (see TTS-GPU-unchanged.json).

## Environment and limits
Windows x64 EXEs compiled against Microsoft .NET Framework 4.5 reference assemblies.
Logic and WinForms tests executed using Mono/Linux and Xvfb. ASR/alignment tests use
real local large-v3, distil-large-v3 and wav2vec2 models on CPU. No physical Windows,
RTX 2070, fresh Windows embedded-Python install, or GPU speed test is claimed.
Existing local ASR/TTS models and GPU packages are not distributed/replaced by this ZIP.

## Actual supplied media
Wild Night Out P1 - Mr Ballen New Episode.mp4: container 250.033008s; decoded AAC
250.057125s (last AAC frame padding). Input media and original SRT are unchanged.
Original supplied SRT: 81 cues; 6 under 1s; 26 over 25 characters/s; 11 cues with
one or more lines exceeding 42 characters.
New output: 87 cues; 0 under 1s; 3 over 25 characters/s; no line exceeding 42.
49 cues remain marked for review, including low alignment/recognition confidence and
remaining dense speech. Successful export is not represented as a clean transcript.
The new CPU primary recognition reproduced playing/playing, just/just, so/So and
his/his; all four were removed only after two actual local audio contexts agreed.
The original GPU-created the/the and into/to variants were not both reproduced by
this CPU recognition. Those patterns and the reported he's/he false-start are covered
by regression fixtures; this does not claim identical CPU/GPU recognition hypotheses.
Grammatical had/had and emphatic really/really remain. Segmentation preserves every
verified word. All timestamps are positive, ordered and within decoded duration.

Fresh primary baseline used the production recognition settings. QC hypotheses were
measured with actual local clips and cached for retries during development. The
continuous punctuation hypothesis reuses an earlier actual full-file large-v3 run
with the exact production secondary settings. The full result was aligned and
assembled by the production worker through its normal verified cache path. Python
socket connect/getaddrinfo events were blocked during full-video QC and alignment.
There is no manually labelled full transcript or word-boundary ground truth, so the
counts demonstrate regression improvements, not a universal recognition accuracy.

Fresh, uncached production runs also covered an 18s MP3 with distil-large-v3 and an
MP4 whose audio starts 2.5s late: first cue 2.498s, original offset retained. An edited
authoritative transcript was realigned with compact lines <=24 characters and exact
edited-word preservation. Real XML was loaded/exported/round-tripped by the compiled
C# production SubtitleDocument, preserving pipeline, audit, review flags and basename;
existing SRT files receive a numeric suffix instead of being overwritten.

## Automated regression checks
45 Python cases: reported repeat variants, two-context agreement/disagreement,
grammatical/emphatic repetitions, changed-meaning protection, punctuation-only edits,
missing word timestamps, old/corrupt cache, weak-alignment retries, phrase grouping,
strict line wrapping and existing 1,000 seeded adversarial timing scenarios.
WinForms: 18 subtitle integration checks, 11 new quality/review state checks,
47 main UI checks, and legacy UI regression checks. Includes failed middle file,
continued batching, stop, rerun skipping, review persistence, edit invalidation,
old-result handling and separate ASR device controls.
Existing core/batch/feature/pacing/native/updater suites pass: 13/12/20/20/14,
update141 17, update145 53, update146 32, breath streaming 8, updater 21,
ONNX updater 6. Clone generation policy 18 and worker protocol 9 checks use fake
models/failure injection, not physical GPU inference.

Release148Tests.py validates CRC, every manifest hash, bootstrap parity, absence of
model/runtime payload, installed versions, update through previous updaters, preservation
of existing voices/runtime/settings/media and rejection of downgrade to 1.4.7.
