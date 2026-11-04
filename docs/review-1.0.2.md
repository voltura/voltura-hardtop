# 1.0.2 review record

The review used the full user-authorized hour: the initial nine-minute session followed by 51 minutes of resumed review. Publication was held until 2026-11-04 13:28:59 UTC. Deployment and final publication verification follow the review interval.

The stopping threshold was elapsed review time, not 200 fixes. There were **31 distinct verified changes at review completion**: 22 correctness/reliability fixes, eight measured performance/build improvements and one publication-verification automation improvement. Assertions, benchmark fixtures, formatting and helper extraction are not additional fixes.

| # | Change | Evidence |
| --- | --- | --- |
| 1 | Settings recover when backup creation is denied | Blocked-backup fixture |
| 2 | Unsupported saved languages normalize to a supported choice | Invalid-preferences round trip |
| 3 | Pin restoration checks the result and retains failures | Failed/native-no-op restore and retry checks |
| 4 | Failed installer launch preserves pins | WPF handoff with an active pin |
| 5 | Pin-triggered refresh errors do not terminate the UI | Dispatcher-driven failing enumeration |
| 6 | Blank-space double-clicks do not toggle selection | Routed WPF input check |
| 7 | Empty picker labels follow language changes | English/Swedish empty-list check |
| 8 | Cleanup removes cached lightweight installers | Isolated maintenance fixture |
| 9 | Packaging rejects a different update key | Mismatched-key rejection before output creation |
| 10 | Linear picker ordering replaces repeated list searches | Before/after benchmark and stable-order checks |
| 11 | Pin actions wait for an overlapping scan and then refresh | Delayed pre-toggle snapshot fixture |
| 12 | Frozen native icons are reused | One conversion across 1,000 unchanged refreshes; invalidation/eviction checks |
| 13 | Unchanged rows are reused and filtering avoids repeated passes | Object-identity check and allocation benchmark |
| 14 | Icon-only changes update the picker | Replacement-image check |
| 15 | Failed install handoff retains a retry action | Installer-failure state and retry checks |
| 16 | Enumeration avoids repeated process opens | Native three-window benchmark |
| 17 | Minimized/Preferences views stop polling | Real WPF window-state and timer checks |
| 18 | Identical payloads reuse verified signatures | Development package: two signing calls and two reuses |
| 19 | Preview shortcuts, startup, settings and cleanup are isolated | Full/lite/full installation and uninstall smoke checks |
| 20 | Unchanged tray appearance reuses its menu | Allocation/timing benchmark plus language/theme/forced-refresh checks |
| 21 | Capture tracks individual physical modifier keys | Paired Ctrl-key release and complete-release checks |
| 22 | Restricted startup reads do not crash Preferences | Access-denied startup service fixture |
| 23 | Shortcut persistence failure is distinct and retryable | Blocked pending file followed by successful retry |
| 24 | Oversized settings are bounded before parsing | 16 MB recovery allocation measurement and preserved backup |
| 25 | Publication validates the complete, signed asset set | Missing assets, path, signature and signed-metadata mismatch fixtures |
| 26 | Failed/cancelled rechecks retain verified cached updates | HTTP failure/cancellation and offline install fixtures |
| 27 | SourceForge HTML download pages are followed correctly | Live 1.0.1 asset hashes plus restricted-redirect checks |
| 28 | Bad update registration falls back to manual updates | Invalid/null/relative location checks and guarded registry access |
| 29 | Recovery notices do not promise an unavailable backup | Existing backup-failure fixture and all 21 localized notices |
| 30 | Support heading follows Windows text scaling | Dynamic 45/20 font-size resource check |
| 31 | Public asset/site verification is automated | All seven existing GitHub and SourceForge 1.0.1 files verified |

Two additional deployment changes are included for verification during 1.0.2 publication: SourceForge's release-only webhook mirror and incremental/no-op Pages publication. The webhook's test delivery returned HTTP 200; read-only site comparison found one changed file and 14 unchanged files. Their new-release publication results are recorded in the validation document after deployment.

Scope covered application lifetime, native window/tray integration, shortcuts, settings, presentation, update verification/download/cache/handoff, WPF resources, translations, installers, build/sign/package/publication scripts, site and documentation. A time-limited review is not a guarantee that every defect has been found.
