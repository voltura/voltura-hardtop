# Voltura HardTop

**Keep a window. Keep your focus.**

Pin the windows you need above the rest. A modern Windows tray companion built with C# and .NET 10.

[![Release](https://img.shields.io/github/v/release/voltura/voltura-hardtop)](https://github.com/voltura/voltura-hardtop/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/voltura/voltura-hardtop/total)](https://github.com/voltura/voltura-hardtop/releases)
[![MIT](https://img.shields.io/github/license/voltura/voltura-hardtop)](LICENSE)
[![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4)](https://hardtop.voltura.se/#download)
[![Issues](https://img.shields.io/github/issues/voltura/voltura-hardtop)](https://github.com/voltura/voltura-hardtop/issues)

**[Website](https://hardtop.voltura.se/)** · **[Download](https://github.com/voltura/voltura-hardtop/releases/latest)** · **[SourceForge](https://sourceforge.net/projects/voltura-hardtop/)** · **[Help](SUPPORT.md)**

![Voltura HardTop window picker](site/assets/app.png)

## Use

- Click the tray icon or press **Ctrl+Alt+Z** to find a window. Search by title or application, then select **Pin**.
- Press **Ctrl+Alt+T** to toggle the active window directly. Both shortcuts are configurable.
- Use **Pinned** to see topmost windows. Closing HardTop's window keeps it beside the clock; **Exit** restores its own pin changes and closes the app.
- Choose from 21 languages, light/dark/system appearance, and optional startup with Windows.

Windows already made topmost by another app are left alone unless you change them. HardTop restores its own changes on graceful exit, not after a crash. It does not remember pins across restarts. Some elevated or protected windows cannot be controlled without matching permissions; HardTop does not elevate automatically.

## Install

For Windows x64 with .NET 10-supported Windows versions. Windows 11 is recommended; Windows 10 22H2 requires Microsoft's applicable servicing/ESU support.

Download **VolturaHardTop-Setup-1.0.0-win-x64.exe** and run Setup. It installs for your Windows account and includes .NET: no separate runtime download is needed. The portable ZIP runs from a writable extracted folder without installation. Avoid running the original HardTop and Voltura HardTop simultaneously because their default picker shortcut is the same.

Installed copies check GitHub for stable updates at startup and every 24 hours. Verified updates download automatically; you choose when to install. Turn automatic downloads off in Preferences. Portable copies can check for updates and open the download page; they do not download or install updates automatically. Locally self-signed builds can show Windows publisher warnings; self-signing is not public certificate trust.

## Build and release locally

Requires Windows x64, PowerShell 7, .NET SDK 10.0.403, NSIS 3, Git, GitHub CLI, and Windows SDK signing tools.

```powershell
./scripts/setup.ps1
./scripts/check.ps1
./scripts/build.ps1
./scripts/package.ps1 -SkipBuild
```

Private signing keys stay outside Git. Published update keys must be preserved. Releases and installers are built locally, without GitHub Actions. See [release instructions](docs/releasing.md).

## Project

This is a new application based on [original HardTop](https://github.com/voltura/HardTop). The original project and its data remain unchanged. [MIT license](LICENSE), copyright Voltura AB.

[Contribute](CONTRIBUTING.md) · [Security](SECURITY.md) · [Privacy](PRIVACY.md) · [Code of conduct](CODE_OF_CONDUCT.md)

If HardTop helps you, [support Voltura via PayPal](https://www.paypal.me/voltura) or [buy me a coffee](https://ko-fi.com/G2G74W5F8).
