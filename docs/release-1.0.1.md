# Voltura HardTop 1.0.1

- Added a lightweight Windows x64 installer, alongside the existing self-contained installer and portable ZIP.
- Lightweight setup reuses a compatible .NET 10 Desktop x64 runtime. If missing, it downloads Microsoft’s runtime installer, verifies its trusted Microsoft signature, and installs it. Internet access and administrator approval may be required for that shared runtime.
- Both installed editions keep verified, user-controlled automatic updates and retain their installer type. Portable updates remain manual.
- Switching between installed editions preserves preferences and removes obsolete bundled runtime files.

The app, installers and uninstallers are SHA-256 signed and timestamped using Voltura’s self-signed certificate. This is not a publicly trusted code-signing certificate; Windows may show publisher or SmartScreen warnings.
