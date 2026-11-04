# Lightweight installer validation — 1.0.1

Observed locally on Windows x64:

- 74 behavior checks passed, including signed selection and complete download verification for both installer variants.
- Nine prerequisite checks cover missing runtimes, Core-only/Desktop-only installations, old/new major versions, preview rejection, minimum patch and side-by-side versions.
- Existing layout/localization checks passed for all 21 app languages.
- Both NSIS variants compiled and their EXE/DLL/installer/uninstaller signatures were verified with the configured self-signed certificate and timestamp.
- Isolated preview setup installed self-contained, switched to lightweight (bundled `coreclr.dll` removed and `PackageVariant=lite`), switched back (runtime restored), and uninstalled successfully. The system Desktop runtime was retained.
- A real runtime downloaded from Microsoft's official endpoint passed trusted Microsoft Authenticode verification. A modified copy and a Voltura-signed installer were rejected.
- Native Windows PowerShell runtime discovery found the installed x64 Desktop/Core runtime successfully.

The machine's shared .NET runtime was not uninstalled to manufacture a missing-runtime environment. Actual runtime installation/elevation denial/reboot on a runtime-free Windows machine remains unverified here; the prerequisite launch and exit-code handling reuse Voltura Air's native NSIS implementation. No claim of a clean-machine/UAC or physical mixed-DPI test is made.
