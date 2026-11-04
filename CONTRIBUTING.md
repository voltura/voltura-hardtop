# Contributing

Read the README for prerequisites, then run `scripts/setup.ps1` and `scripts/check.ps1`. Keep changes focused, preserve unrelated work, and include evidence for behavior changes. Never commit generated packages, private keys or credentials.

Use feature-focused C# modules. UI work must retain keyboard access, theme support, DPI scaling and all 21 translations. Add focused checks for meaningful state transitions, not tests that mirror implementation details.

Run `scripts/build.ps1` for a release build. Publication is separate and uses the reviewed source commit. See [releasing](docs/releasing.md).
