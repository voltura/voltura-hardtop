# Local releases

Prerequisites: Windows x64, PowerShell 7, pinned .NET 10 SDK, NSIS 3, Git, authenticated GitHub CLI and the Windows SDK resource/signing tools.

1. Run `scripts/setup.ps1` once. It creates a separate RSA-PSS update-signing key and signing configuration under `%LOCALAPPDATA%\Voltura\HardTop\Signing`. Back up the private key securely. Never commit it or silently rotate the public key in an installed app.
2. Review and run the app. Incorporate visual feedback before final release preparation. Keep the old HardTop untouched.
3. Update `Directory.Build.props` and `docs/release-VERSION.md`; verify the complete translations and changed behavior with `scripts/check.ps1`.
4. Commit and push the exact reviewed source on `main`.
5. Run `scripts/release.ps1`. It checks, builds once, patches native metadata using Worlds' conventions, signs the app, packages and signs Setup, and prepares a portable ZIP plus RSA-PSS-signed update metadata.
6. Run `scripts/release.ps1 -Publish` to upload those same bytes to a draft release and publish it only after all uploads succeed. Source, origin/main and the tag must match. Existing published bytes are never replaced.
7. Mirror the prepared directory with `scripts/publish-sourceforge.ps1 -ReleaseDirectory artifacts/releases/VERSION`. It requires an existing SourceForge SSH key and verified host key. Browser upload is available when SSH is not configured. Set the x64 installer as the Windows default download.
8. Run `scripts/publish-pages.ps1 -Publish` for the independent website. one.com supplies `hardtop CNAME voltura.github.io`; GitHub Pages supplies hosting and HTTPS.

`build.ps1` and `package.ps1 -SkipBuild` can be used independently during development. Use `package.ps1 -Development -OutputDirectory artifacts/installer-preview-N` for an isolated installer identity. `-Candidate` tests the real per-user install location and update capability; its manifest is marked as development and cannot be published. Never publish development or candidate packages.

Signing uses a configured CurrentUser certificate with SHA-256 and an RFC 3161 timestamp. Current local certificates may be self-signed: the release manifest must say `SelfSigned`; this does not establish public Windows trust or remove SmartScreen warnings. No certificate is added to trusted stores.

Verify app and installer metadata, signature/digest/timestamp, fresh install, upgrade, cancellation, uninstall, portable mode and the changed update path. Keep private keys and unrelated settings during uninstall. Validate live release metadata and product-site links after publication. There are no Actions-based build or release workflows.
