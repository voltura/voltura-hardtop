# Working on Voltura HardTop

- Preserve the original HardTop and all sibling repositories. Work only in this repository.
- Keep UI concise, localized and accessible; retain light/dark/system themes and DPI awareness.
- Use local scripts for builds, packages and publication. Do not add Actions-based installers or releases.
- Preserve signing keys outside Git. Never rotate a published update key silently.
- Run focused checks and compile before handing off changes. Report observed behavior separately from unverified device/display behavior.
- Present changed UI for user feedback before preparing a final release around its visual direction. Silence is not visual approval.
- Preserve unrelated edits. Do not reset, force-push or overwrite published release bytes.
