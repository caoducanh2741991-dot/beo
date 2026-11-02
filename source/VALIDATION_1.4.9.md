# V15 Voice Studio 1.4.9 — completion verification

## Scope and acceptance
Fix the two residual cases reported from the actual 1.4.8 output (the/the and he's/he),
protect the three reported phrase boundaries, preserve all other words, retain valid
source timing, avoid short/overlong captions and preserve the functioning TTS/GPU path.
All of these specific acceptance checks passed on the supplied regression media.
This is not a universal ASR accuracy percentage or a guarantee for all future videos.

## Actual supplied video and SRT
Input: Wild Night Out P1 - Mr Ballen New Episode.mp4 and its uploaded _2.srt.
The production refine command measured the existing words against the actual audio,
ran actual large-v3 recognition on distinct local contexts, used a wider context for
the difficult article repetition, and aligned the corrected words again. The final
caption cost parameters were checked/reassembled against those actual measured word
times; no word times were interpolated to create the caption-count improvements.
Local hypotheses were recorded as private QA traces; media, complete transcripts and
QA traces are not included in this update ZIP.

Before (uploaded _2): 85 cues; 1049 whitespace words; 0 cues under 1s; 5 over 25
characters/s; 0 overlong lines; the/the and he's/he still present; the three phrases
split across cues.
After: 87 cues; 1047 words; exactly two lexical deletions (the and he's); all remaining
words preserved after case/punctuation normalization; 0 cues under 1s; 3 over 25
characters/s; no lines over 42 characters; positive, ordered timestamps within decoded
audio duration. Smelled like vomit, throws it and being gone are each inside one cue.
Grammatical had had and emphatic really really remain. 39 cues retain review notes
about timing/recognition confidence or dense speech; these are not 39 proven errors.
No manually annotated word-boundary or full-transcript ground truth was available.

Fresh, uncached production recognition covered an 18s MP3 using distil-large-v3 and
an MP4 with an actual 2.5s audio start delay: first caption 2.498s. Real C# production
XML loading/SRT export verifies the result, audit, timestamp bounds, original basename,
non-overwrite suffix and project/review round-trip.

## Offline validation
An initial host validation was stopped after an ONNX Runtime telemetry connection
attempt was detected. The production worker now explicitly disables ORT telemetry,
blocks Python connect/address-lookup events, and restricts media decoding to local
file/pipe protocols. Subsequent audio validation ran behind an additional Linux
seccomp filter denying native socket/connect/sendto/sendmsg syscalls before any ASR
library or user media was opened. The native socket-denial self-test passed. No claim
is made about the unverified payload of the initial attempted connection.
The download/setup command remains separate and is only used when explicitly invoked.

## Regression coverage
52 Python tests, including the previous 1,000 seeded adversarial timing scenarios:
local anchor evidence with nearby edits, time-locality rejection, repeated speech
retention, retry despite initial disagreement, protected grammar, bounded long phrases,
line width, exact word preservation, old/corrupt cache and alignment retry.
Core 13, batch 12, feature 20, update141 17, breath streaming 8, punctuation 20,
native GPU routing 14, subtitle/process 17, updater 21 and native-addon updater 6 pass.
Main UI 47, subtitle integration 18, quality/review UI 11 and new refine UI 7 pass.
UI wiring uses one clearly labelled test-only voice profile, which is not packaged.
Legacy catalogue-star tests and old 1.4.5/1.4.6 real voice audition suites require
private installed catalogue/media fixtures absent from this restored environment;
they are not claimed as executed here. Clone policy 18 and worker protocol 9 use
failure injection/fake models, not physical GPU inference.

## Release and environment
Windows x64 app/updater compiled against Microsoft .NET Framework 4.5 references.
UI/logic tested with Mono/Linux and Xvfb; ASR/alignment tested using real local CPU
models. No physical Windows/RTX 2070 execution or GPU speed claim is made.
The implementation of ai_worker.py, gpu_policy.py, NativeGpu.cs, ExternalSpeech.cs,
Core.cs and VoiceCatalog.cs is byte-identical to the released 1.4.8 baseline. The
payload contains no TTS weights, voice runtime, CUDA bundle or voice catalogue.
The small local POS model is bundled with its MIT licence, NLTK licence and SHA256.
Release149Tests.py checks exact ZIP CRC, every manifest hash, the bundled POS hash,
updates through installed 1.4.8/new updater in ZIP/folder modes, preservation of
existing models/settings/voices/media, downgrade rejection and actual EXE 1.4.9.0.
