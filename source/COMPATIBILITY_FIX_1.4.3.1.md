# Windows updater compatibility repair 1.4.3.1

Reported error on Windows: `Method not found: Boolean System.String.Contains(Char)`.

The original 1.4.3 updater EXE contains five IL calls to `String.Contains(char)`. That overload exists in the Mono runtime used to build/test the release, but is absent from Windows .NET Framework. Mono execution tests therefore did not detect the incompatible binary references. With Microsoft reference assemblies, LINQ can bind source-level Contains(char) calls to a compatible extension method, so merely searching source text is insufficient to identify the binary defect.

Repair:

- Replace the updater's five character tests with explicit `IndexOf(char) >= 0`, preserving the same path traversal/filename validation rules.
- Compile both production EXEs against Microsoft's .NET Framework 4.5 reference assemblies using `-nostdlib -noconfig` and explicit framework references. Do not build production EXEs against Mono implementation assemblies.
- Add target-framework metadata and stamp both EXEs/UI as 1.4.3.1.
- Inspect compiled IL in separate test processes: original updater has 5 unsupported calls; new updater and app both have 0. Separate processes avoid Mono reflection binding across assemblies with the same simple name.
- Preserve existing GPU package names/version 1.4.3 and Torch packages. No GPU/models are included in this small repair ZIP.

Recovery installation must start the NEW EXTRACTED bootstrap through CAP_NHAT.cmd. Using the installed old app's update button would still launch the defective old updater before the repair can be staged.

Validation in this repair: production framework-reference compilation, binary IL checks, 21 existing updater tests, 6 ONNX multipart fixture tests, core/batch/state/pacing/GPU dispatch regression checks, and 29 new-version UI checks. ZIP integration tests cover genuine old 1.4.0 and 1.4.3 EXEs via both folder and ZIP inputs through the extracted repaired updater, retained user/model/runtime files, and rollback prevention to 1.4.3. This is not a physical Windows execution/GPU inference test.

Microsoft reference package:
https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net45/1.0.3

No new voice-cloning-render repair is claimed; that issue remains deferred at the user's request.
