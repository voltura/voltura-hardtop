# Validation — 1.0.2

Observed on Windows x64 before final release preparation:

- 101 behavior checks, including real Win32 topmost operations, settings recovery, shortcut persistence, delayed refreshes, update verification/tampering, installation failure and cached-update recovery.
- Nine runtime prerequisite checks, isolated maintenance/signing/release-validation fixtures, script parsing and Git whitespace checks.
- WPF checks for both themes, 21 languages, minimum/normal window sizes, large text, raster scaling, reachable content, empty-list localization, blank-space double-clicks, dispatcher error handling, idle polling and Support text scaling.
- Native UI smoke check of isolated portable search, pinning, pinned filtering, shortcut capture/cancellation, close-to-tray, second-instance activation and graceful maintenance exit.
- Signed development installers compiled and passed self-contained → lightweight → self-contained installation and uninstall. Production preferences, startup entry and shortcut hashes were unchanged; unrelated files survived. Preview installation durations were approximately 5.57, 5.08 and 4.71 seconds on this machine with the shared runtime already installed.
- Existing public 1.0.1 release: all seven GitHub and all seven SourceForge asset hashes matched the prepared release; website links matched that version.

Measured workloads (single-machine observations, not universal performance guarantees):

| Workload | Before | After |
| --- | ---: | ---: |
| 100-window refresh, median | 0.235 ms | 0.070 ms |
| 1,000-window refresh, median | 1.92 ms | 0.53 ms |
| 5,000-window refresh, median | 28.0 ms | 0.76 ms |
| 5,000-window refresh, allocation | 668 KB | 338 KB |
| Native three-window enumeration, median | 0.244 ms | 0.157 ms |
| Native enumeration, allocation | 26.9 KB | 24.4 KB |
| 500 unchanged tray updates, total | 1,005 ms | 1.49 ms |
| Tray update allocation per call | 46.4 KB | 0 bytes |
| Damaged 16 MB settings recovery, allocation | 134.4 MB | 10.1 KB |
| Damaged settings recovery duration | 121 ms | 31.9 ms |
| Payload signing calls for identical full/lite binaries | 4 | 2 |

Native enumeration's CPU measurement was below useful timer resolution in this small workload; no CPU percentage is claimed. No installer/executable size reduction or whole-app startup speedup is claimed.

Final 1.0.2 publication verification is performed after the review deadline with `scripts/deploy.ps1` and `scripts/verify-publication.ps1`. The published release manifest records the clean source commit, signing identity and exact asset digests. Live deployment results are appended after publication.

Physical mixed-monitor/DPI reconnection, runtime installation on a clean machine, UAC denial/reboot and crash-time pin restoration remain unverified here. Restoration is still a graceful-exit feature. There is no new visual direction in this release.

Published 1.0.2 from source commit ca96598a07acecb3dacd917386ee8c52e6e2c0d0. All seven actual GitHub downloads and all seven SourceForge v1.0.2 downloads matched the prepared release hashes. The release-only SourceForge webhook mirrored the complete asset set. Pages publication uploaded one changed file and retained 14 unchanged files; a second publication uploaded zero files and created no commit. The public product site linked 1.0.2. A bounded GitHub draft-visibility retry was added during deployment after the initial draft was not immediately readable; no published bytes were replaced.
