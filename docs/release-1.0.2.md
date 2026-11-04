# Voltura HardTop 1.0.2

Faster window refreshes, less idle work and more reliable pinning, settings and updates.

- Faster filtering and refreshes with many windows open. Unchanged rows and application icons are reused instead of recreated.
- Stops polling windows while minimized or showing Preferences. Update progress no longer repeatedly rebuilds the tray menu.
- Pin actions wait for overlapping refreshes, so the list reflects the new state. Double-clicking empty list space no longer toggles the selected window, and icon-only changes appear correctly.
- Handles window-refresh failures without closing the app. Pin restoration checks the resulting topmost state.
- Keeps pins if an update installer cannot start. Verified cached updates remain available after cancelled or failed rechecks and failed installer launches.
- Recovers safely from oversized or unreadable settings, handles unsupported saved languages, and reports shortcut save failures separately from conflicts, with a retry path.
- Handles restricted Windows startup and update registration without preventing the app from opening.
- Corrects modifier-key tracking during shortcut capture, refreshes empty-list labels when changing language, and applies Windows text scaling to the Support heading.
- Makes recovery notices accurate in all 21 languages and removes cached lightweight installers when uninstalling with Remove settings selected.

In a synthetic 5,000-window benchmark, median refresh time fell from **28.0 ms to 0.76 ms**, with about half the managed allocation. This is a stress-test result, not a promise of the same speedup on every desktop. A damaged 16 MB settings-file recovery test reduced managed allocation from about **134 MB to 10 KB**.

The existing interface, light/dark/system themes and installer choices are retained. Installed copies keep their installer type; portable updates remain manual.

The app and installers use Voltura's existing self-signed certificate with SHA-256 signatures and timestamps. Windows may show publisher or SmartScreen warnings. The published update verification key is unchanged.
