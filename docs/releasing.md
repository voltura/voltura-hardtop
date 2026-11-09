# Local releases

Prerequisites: Windows x64, PowerShell 7, pinned .NET 10 SDK, NSIS 3, Git, authenticated GitHub CLI and the Windows SDK resource/signing tools.

1. Run `scripts/setup.ps1` once. It creates a separate RSA-PSS update-signing key and signing configuration under `%LOCALAPPDATA%\Voltura\HardTop\Signing`. Back up the private key securely. Never commit it or silently rotate the public key in an installed app.
2. Review and run the app. Incorporate visual feedback before final release preparation. Keep the old HardTop untouched.
3. Update `Directory.Build.props` and `docs/release-VERSION.md`; verify the complete translations and changed behavior with `scripts/check.ps1`.
4. Commit and push the exact reviewed source on `main`.
5. Run `scripts/release.ps1`. It checks, builds each runtime variant once, patches native metadata using Worlds' conventions, signs the app, packages and signs the self-contained and lightweight installers, and prepares a portable ZIP plus RSA-PSS-signed update metadata.
6. Run `scripts/release.ps1 -Publish` to upload those same bytes to a draft release and publish it only after all uploads succeed. Source, origin/main and the tag must match. Existing published bytes are never replaced.
7. SourceForge's configured release-only GitHub webhook mirrors new published release assets automatically. Its setup does not add a download button to GitHub notes. Verify the new folder and set the self-contained x64 installer as the Windows default. `scripts/publish-sourceforge.ps1 -ReleaseDirectory artifacts/releases/VERSION` remains an SSH fallback and requires an existing key and verified host key; it never replaces different remote bytes.
8. Run `scripts/publish-pages.ps1 -Publish` for the independent website. It uploads changed files only and skips unchanged deployments. `-CheckRemote` performs a read-only comparison. Existing DNS/HTTPS settings are preserved. one.com supplies `hardtop CNAME voltura.github.io`; GitHub Pages supplies hosting and HTTPS.

For a combined local deployment, use `scripts/deploy.ps1 -Publish`. It prepares/publishes the exact release bytes, publishes site changes and verifies GitHub asset hashes and live download links. Add `-SourceForgeFolder FOLDER` to verify all seven mirrored file sizes and SHA-256 hashes too; use the folder shown by SourceForge's import. `scripts/verify-publication.ps1 -ReleaseDirectory artifacts/releases/VERSION -SourceForgeFolder FOLDER` can be rerun independently. SourceForge public download pages are followed only to the requested asset, and bytes are checked before accepting a download.

Release notes must exist before release building starts. Publication validates the complete asset set, update signature and consistency between signed installer metadata and prepared files. Packaging also checks the private/public update-key match and reuses existing signatures only when unsigned payload bytes are identical.

`build.ps1`, `build.ps1 -FrameworkDependent` and `package.ps1 -SkipBuild` can be used independently during development. Use `package.ps1 -Development -OutputDirectory artifacts/installer-preview-N` for isolated preview shortcuts/startup integration and portable settings. `scripts/check-installers.ps1 -ReleaseDirectory artifacts/installer-preview-N` tests full/lite/full upgrades and uninstall while checking that production state and unrelated files survive. It refuses an existing preview installation. `-Candidate` tests the real per-user install location and update capability; its manifest is marked as development and cannot be published. Never publish development or candidate packages.

Performance workloads are available through `dotnet run --project tests/HardTop.Checks -c Release -- --performance`, `--native-performance`, `--tray-performance` and `--settings-performance`. Keep benchmark observations separate from physical device/display claims.

Signing uses a configured CurrentUser certificate with SHA-256 and an RFC 3161 timestamp. Current local certificates may be self-signed: the release manifest must say `SelfSigned`; this does not establish public Windows trust or remove SmartScreen warnings. No certificate is added to trusted stores.

HardTop 1.0.3 uses the same encrypted update-signing key as Voltura Air. The local `signing.json` can reference that file through `updateKeyPath` and name the passphrase environment variable through `updateKeyPassphraseVariable`. Back up the encrypted key and keep its passphrase separately. Releases before 1.0.3 need a one-time manual upgrade to trust the new key.

Verify app and installer metadata, signature/digest/timestamp, fresh install, upgrade, cancellation, uninstall, portable mode and the changed update path. Keep private keys and unrelated settings during uninstall. Validate live release metadata and product-site links after publication. There are no Actions-based build or release workflows.

## Runtime variants

Build outputs are isolated by version, source commit and variant under `artifacts/builds`. A build requires an empty output directory to prevent stale runtime files contaminating the lightweight payload. Package both variants from the same clean commit. The portable ZIP remains self-contained.

The lightweight installer checks the actual payload runtimeconfig minimum against installed stable x64 Core and Desktop 10.0 patches. When missing, it downloads Microsoft's current Desktop 10.0 installer through the official `aka.ms` URL, requires valid Authenticode trust with Microsoft Corporation as authenticated publisher, then invokes it through Windows elevation. Denied elevation, invalid signatures, download failures and installer failures abort before closing HardTop or modifying application files. Exit 3010 preserves the reboot requirement. The shared runtime is never removed during HardTop uninstall.

Both installer hashes are covered by the same RSA-PSS manifest. A missing `PackageVariant` registry value identifies existing 1.0.0 self-contained installs; `lite` selects lightweight updates. The standard asset name stays compatible with the 1.0.0 update verifier. Switching variants removes only explicitly known obsolete bundled runtime files, preserving application settings and unrelated files.
