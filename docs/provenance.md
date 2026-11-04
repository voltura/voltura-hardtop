# Source provenance

Original: https://github.com/voltura/HardTop at `74c4c30674be36a68066dddb850a66d951e4006a`.

The original author, acting for Voltura AB, explicitly authorized using one MIT license for the new project, including the original code. The old repository and its license are untouched.

`Platform/Windows.cs` ports the original EnumWindows, GetWindowText, WS_EX_TOPMOST and SetWindowPos approach. `Platform/Hotkeys.cs` retains RegisterHotKey/WM_HOTKEY and adds safe replacement/disposal. Configuration moves from executable-side .config to atomic per-user JSON. Legacy forced GC, timer-driven DoEvents, title blacklists, strong-name private key and fixed offscreen menu window were not carried forward.

Adapted Voltura sister sources: Earner's localization catalog, binding extension, updater and verifier; NetOn's single-instance signaling; Markdown's signing verification and smooth NSIS artwork renderer. Worlds informs build/package separation. Places informs concise copy and keyboard/focus behavior. Music informs consistent themes and responsive presentation. Repository-specific policies and unrelated features were not copied.
