# V15 Voice Studio 1.4.10 — release validation

## Scope
Improve phrase grouping on the user's actual 89-cue 1.4.9 SRT and its source video;
stop interpreting every normalized recognition hypothesis as proof of a false repeat;
review uncertain cue endpoints without inventing timing or cutting audible speech.
The working voice synthesis/GPU implementation remains unchanged.

## Repetition and timing policy
The new default preserves every primary-recognition word and flags possible repeats
or false starts for listening. Explicit clean mode requires two distinct supporting
windows and vetoes deletion when an anchored window retains the repetition. Non-exact
false starts, including he's/he, remain for review even in clean mode. Punctuation
transfer never substitutes lexical wording. Old caches/pipeline stamps are invalidated;
cache identity, queued-job profile and saved preferences include the repetition mode.

Word-tail trimming requires measured quiet PCM and an agreeing independent ASR end.
CTC measurements from a draft are explicitly marked and cannot vote as independent ASR.
Refinement matches unchanged words to an actual ASR pass or a matching ASR cache; it
never adopts the alternative transcript as the user's edited text. Conflicting cue
endpoints are marked rather than automatically shifted. Measured word metadata and
policy survive C# project save/load; editing captions invalidates stale word metadata.

## Completed actual-media tests
Source: the supplied Wild Night Out video, decoded duration 250.057125 seconds, and
the user's exact uploaded 1.4.9 SRT (89 cues, 1,048 whitespace words).

Completed full-video production CLI tests ran sequentially using local Distil large-v3
English on CPU, behind the native offline validation filter:

| Path | Cues | Words | Under 1 s | Over 25 chars/s | Review cues |
|---|---:|---:|---:|---:|---:|
| Fresh, uncached recognition | 83 | 1041 | 0 | 3 | 43 |
| Refine uploaded 1.4.9 SRT | 84 | 1048 | 0 | 4 | 38 |

Both outputs have positive, ordered, non-overlapping times within the decoded duration,
no line over 42 characters and all 13 reported phrase cases within individual cues.
The refined path preserves all 1,048 input words after punctuation/case normalization,
including the/the. Fresh assembly preserves every primary-recognition word. The
1,041 versus 1,048 difference is a recognition difference, not seven words discarded
by the new assembler, and is not evidence of word-perfect transcription.

The refined result has one more dense cue than the uploaded 1.4.9 result (4 versus 3)
while fixing the reported phrase cuts. Dense speech remains visibly flagged; no timing
was stretched to hide the trade-off. Review cues are uncertainties, not proven errors.
No silence-based tail trim was forced on this source: none met both conservative
conditions. Synthetic PCM tests prove the guarded trim when genuine silence and an
independent anchor agree, and reject audible tails, music, absent or conflicting anchors.

An uncached 18-second MP3 completes with Distil large-v3 and separately with Whisper
large-v3. A generated MP4 with a real 2.5-second delayed audio track starts its first
caption at 2.530 seconds. A real 22-second excerpt containing the reported repeated
article and he's/he exercises explicit clean refinement; the false start is retained.
The final C# executable loads and exports all six actual-media results, preserves the
source and adjacent filename/suffix behavior, and round-trips policy, review and all
measured word attributes. Acceptance script: verify_completion1410.py.

An earlier attempt to run multiple real models concurrently exceeded this validation
host's 8 GiB memory cap; two processes were killed before producing final results.
Those incomplete runs are not counted as passed. Completed full-media runs were then
executed sequentially. The full-video completed backend is Distil; a complete full-video
Whisper large-v3 run is not claimed. Whisper large-v3 has the completed 18-second smoke
test above. This host event is not a physical Windows/RTX 2070 test.

## Regression and UI
68 Python subtitle tests pass, including the existing 1,000 seeded adversarial timing
scenarios, safe repetition policy, contradictory audio evidence, phrase boundaries,
independent-anchor provenance, guarded PCM trim and cache policy isolation.
C# suites pass: core 13, batch 12, feature 20, update141 17, breath 8, punctuation 20,
native GPU routing 14, subtitle/process 17, updater 21 and ONNX updater 6.
UI: main 47, subtitle batch 18, review/metadata 13, refine/cancellation 7, new repetition
policy/persistence/layout 14. Six real-result integration cases pass. Minimum-size
subtitle UI was visually inspected. Test-only voice catalogue is not included in ZIP.

## Offline, compatibility and release checks
All real model runs use local weights and a Linux native syscall filter installed
before model import, denying socket/connect/sendto/sendmsg. Its self-test passes.
Production retains ORT telemetry opt-out, Python network denial and local-file/pipe
FFmpeg decoding; setup downloads remain a separate explicit user action.

App and updater are compiled Windows x64 binaries against .NET Framework 4.5 references.
Logic/UI execute under Mono/Linux/Xvfb; no physical Windows or RTX 2070 inference claim.
Six TTS/GPU implementation files are byte-identical to 1.4.9 (validation hash list).
The payload contains no TTS weights/runtime, CUDA files, user voice catalogue, user media
or complete model hypotheses. The bundled small POS model retains its pinned hash.
Release1410Tests.py verifies exact ZIP CRC, all manifest file hashes, bootstrap parity,
upgrade via installed 1.4.9/new updater in ZIP/folder modes, eleven existing user
voice/model/settings/output fixtures, downgrade rejection and EXE version 1.4.10.0.
