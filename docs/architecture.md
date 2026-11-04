# Architecture

The application uses .NET 10 WPF with a small Windows Forms notification-area integration. There is no web view, dependency-injection container or application framework dependency.

`App` is the composition root and lifetime coordinator. It creates the per-user single-instance guard, native services, update service and tray service, then connects their events. It owns graceful exit and update handoff. It does not enumerate windows, paint controls, write JSON or implement cryptography.

`Presentation` contains the picker and preferences models. Search and pinned filtering, translated row labels, conflict-safe shortcut preference changes and persistence requests are independent of WPF controls. The models depend on meaningful external boundaries: `IWindowSource`, `IWindows`, `IHotkeyRegistrar` and `IStartupService`.

`MainWindow` is a view adapter. It forwards input to the presentation models and handles focus, visibility, native window placement, opacity and themed confirmation dialogs. `UI/ShortcutEditor` is a reusable shortcut-capture field. A temporary keyboard hook consumes capture input before Windows dispatches global shortcuts; it ends on completion, cancellation, focus loss or hiding. The field composes a canonical shortcut but never registers it itself.

`Platform` owns Win32 window identity and topmost operations, global hotkey registration, per-user startup registration, atomic settings storage, theme application, tray rendering and visibility promotion. `PinSession` is the policy layer for restoring only this session's changes, guarded by handle, process ID and process start time.

`Updates` owns bounded HTTPS downloads, cancellation, signed metadata verification, cache recovery and explicit installer handoff. Keys and installer bytes are verified again before handoff. UI text mapping is separate in `UpdatePresentation`.

All UI state is accessed on the WPF dispatcher. Window enumeration and downloads run asynchronously. Surviving windows retain their list order across refreshes, and the view preserves its scroll offset. Search-derived counts are independent of the selected filter. Manual packages can check release metadata but never download or stage installers. Native resources and event subscriptions have explicit lifetime owners. Tests use fake OS boundaries for policy checks and real Win32 windows for interop checks; no production test switches are shipped.

Picker refreshes share their in-flight scan; pin actions request a subsequent scan when the previous snapshot predates the action. Ordering uses dictionary lookups, unchanged rows and frozen icon snapshots are reused, and polling stops while minimized or showing Preferences. Tray menus are rebuilt when language/appearance changes, not for every download-progress event. Failed/cancelled update rechecks reverify any remaining cached package before retaining its install action.

Release building, native metadata, signing, packaging and publishing are separate local scripts. The product website is independent static source under `site`; it never shares the README as its page content.
