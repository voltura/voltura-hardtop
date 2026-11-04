using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;
using Voltura.HardTop.Updates;
using NativeWindows = Voltura.HardTop.Platform.Windows;
using Voltura.HardTop.Presentation;

namespace HardTop.Checks;

internal static class Program
{
    private static int checks;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--demo")) { Demo(); return 0; }
        if (args.Contains("--performance")) { PerformanceChecks.RunAsync().GetAwaiter().GetResult(); return 0; }
        if (args.Contains("--native-performance")) { PerformanceChecks.RunNative(); return 0; }
        if (args.Contains("--tray-performance")) { PerformanceChecks.RunTray(); return 0; }
        if (args.Contains("--settings-performance")) { PerformanceChecks.RunSettingsRecovery(); return 0; }
        if (args.Contains("--layout")) { try { LayoutChecks.Run(); return 0; } catch (Exception e) { Console.Error.WriteLine(e); return 1; } }
        try { Run().GetAwaiter().GetResult(); Console.WriteLine($"Passed {checks} checks."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task Run()
    {
        Assert(HotkeyGesture.TryParse("Ctrl+Alt+T", out _) && !HotkeyGesture.TryParse("T", out _) && !HotkeyGesture.TryParse("Ctrl+Ctrl+T", out _), "Shortcut parsing rejects unsafe or duplicate modifiers");
        Assert(UpdateService.MatchesInstallationDirectory(AppContext.BaseDirectory.ToUpperInvariant(),AppContext.BaseDirectory)
            && !UpdateService.MatchesInstallationDirectory("relative-path",AppContext.BaseDirectory)
            && !UpdateService.MatchesInstallationDirectory("C:\\invalid\0location",AppContext.BaseDirectory)
            && !UpdateService.MatchesInstallationDirectory(null,AppContext.BaseDirectory), "Corrupt or relative update registration falls back safely to manual updates");
        var heldKeys = new ShortcutKeyState();
        heldKeys.Set(0xA2, true); heldKeys.Set(0xA3, true); heldKeys.Set(0xA2, false);
        Assert(heldKeys.Modifiers == 2, "Releasing one Ctrl key preserves the other held Ctrl key");
        heldKeys.Set(0xA4, true); heldKeys.Set(0x54, true); heldKeys.Set(0x54, false);
        Assert(heldKeys.Modifiers == 3 && heldKeys.AnyHeld, "Capture retains modifier state until all physical keys are released");
        heldKeys.Set(0xA3, false); heldKeys.Set(0xA4, false);
        Assert(heldKeys.Modifiers == 0 && !heldKeys.AnyHeld, "Releasing all captured keys clears the modifier state");
        using (var first = new Hotkeys())
        using (var competing = new Hotkeys())
        {
            Assert(first.Replace("picker", "Ctrl+Alt+Shift+F11", () => { }), "Global shortcut registers");
            Assert(competing.Replace("other", "Ctrl+Alt+Shift+F12", () => { }), "Competing shortcut registers");
            Assert(!first.Replace("picker", "Ctrl+Alt+Shift+F12", () => { }), "Conflicting replacement rejected");
            Assert(!competing.Replace("other", "Ctrl+Alt+Shift+F11", () => { }), "Original binding survives replacement failure");
        }
        var fake = new FakeWindows(); var session = new PinSession(fake);
        var id = new WindowIdentity(44, 55, 66);
        Assert(session.Toggle(id) && fake.Topmost, "Pin changes topmost state");
        Assert(session.Restore() == 0 && !fake.Topmost, "Graceful exit restores a new pin");
        fake.Topmost = true;
        Assert(session.Restore() == 0 && fake.Topmost, "Preexisting topmost window untouched");
        Assert(session.Toggle(id) && !fake.Topmost, "Preexisting topmost window can be explicitly unpinned");
        session.Restore(); Assert(fake.Topmost, "Explicit unpin restored to original topmost state");
        fake.Current = false; Assert(!session.Toggle(id), "Stale identity rejected");
        fake.Current = true; fake.Topmost = false; fake.Allowed = false; Assert(!session.Toggle(id), "Permission failure is reported");
        fake.Allowed = true; session.Toggle(id); fake.Current = false; fake.Topmost = false; session.Restore(); Assert(!fake.Topmost, "Reused handle is not restored into another process");
        fake.Current = true; session.Toggle(id); fake.Allowed = false;
        Assert(session.Restore() == 1 && fake.Topmost, "Failed pin restoration reports failure");
        fake.Allowed = true;
        Assert(session.Restore() == 0 && !fake.Topmost, "Failed pin restoration can be retried");
        session.Toggle(id); fake.IgnoreChanges = true;
        Assert(session.Restore() == 1 && fake.Topmost, "A successful native call without a state change is a restore failure");
        fake.IgnoreChanges = false; session.Restore();
        session.Toggle(id); fake.Topmost = false;
        Assert(session.Restore() == 0 && !fake.Topmost, "External topmost changes are preserved during restoration");
        var native = new NativeWindows();
        var testWindow = new Window { Title = "HardTop verification window", Width = 250, Height = 150, ShowInTaskbar = false, WindowStyle = WindowStyle.None, AllowsTransparency = true };
        var handle = new WindowInteropHelper(testWindow).EnsureHandle();
        var nativeId = NativeWindows.Identify(handle)!.Value;
        Assert(native.IsCurrent(nativeId), "Real Win32 window identity recognized");
        testWindow.Show();
        WindowTransparency.Apply(testWindow, 60);
        Assert(testWindow.Opacity == 0.6 && HwndSource.FromHwnd(handle)?.CompositionTarget.UsesPerPixelOpacity == true, "60 percent opacity uses WPF's transparent window compositor");
        WindowTransparency.Apply(testWindow, 100);
        Assert(testWindow.Opacity == 1.0, "100 percent opacity restores a fully opaque window");
        Assert(native.SetTopmost(nativeId, true) && native.IsTopmost(nativeId), "Real SetWindowPos topmost works");
        Assert(native.SetTopmost(nativeId, false) && !native.IsTopmost(nativeId), "Real SetWindowPos unpin works");
        testWindow.Close(); Assert(!native.IsCurrent(nativeId), "Closed native window rejected");
        var dir = Path.Combine(Path.GetTempPath(), "VolturaHardTop.Checks-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(dir); store.Value.Language = "sv"; store.Save();
        Assert(new SettingsStore(dir).Value.Language == "sv", "Atomic settings round trip");
        File.WriteAllText(Path.Combine(dir, "settings.json"), "broken");
        Assert(new SettingsStore(dir).Recovered && Directory.GetFiles(dir, "*.recovered-*").Length == 1, "Damaged settings recovered with original copy");
        // A backup destination that is a directory produces access denied on Windows.
        // Keep all fixtures in our temporary directory, without changing any ACLs.
        var blockedBackup = Path.Combine(dir, "blocked-backup");
        Directory.CreateDirectory(blockedBackup);
        File.WriteAllText(Path.Combine(blockedBackup, "settings.json"), "broken");
        var backupTime = DateTime.UtcNow;
        for (var second = -2; second <= 30; second++)
            Directory.CreateDirectory(Path.Combine(blockedBackup, "settings.json.recovered-" + backupTime.AddSeconds(second).ToString("yyyyMMddHHmmss")));
        Assert(new SettingsStore(blockedBackup).Recovered, "Settings recover even when the backup cannot be written");
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"Language\":\"unsupported\",\"Theme\":\"invalid\",\"OpacityPercent\":999,\"PickerShortcut\":null}");
        var normalized = new SettingsStore(dir).Value;
        Assert(normalized.Language == "system" && normalized.Theme == "system" && normalized.OpacityPercent == 100 && normalized.PickerShortcut == "Ctrl+Alt+Z", "Invalid saved preferences normalize to supported values");
        var model = new WindowPickerViewModel(new SampleWindows(), new PinSession(new FakeWindows()));
        await model.RefreshAsync();
        Assert(model.Rows.Count == 3, "Picker retains duplicate titles as distinct windows");
        model.SearchText = "editor";
        Assert(model.Rows.Count == 2, "Search filters by application name");
        Assert(model.MatchingCount == 2 && model.MatchingPinnedCount == 1, "Filter counts follow search before the pinned filter");
        model.PinnedOnly = true;
        Assert(model.Rows.Count == 1 && model.Rows[0].Window.Pinned, "Pinned filter combines with search");
        Assert(model.MatchingCount == 2 && model.MatchingPinnedCount == 1, "Pinned view retains both search-based filter counts");
        model.SearchText = ""; model.PinnedOnly = false;
        Strings.Current.SetLanguage("sv"); model.RefreshLabels();
        Assert(model.Rows.Any(row => row.ActionLabel == "Fäst"), "Existing picker rows relocalize immediately");
        Strings.Current.SetLanguage("en");
        var iconLoads = 0;
        var iconCache = new WindowIconCache(_ => { iconLoads++; return new DrawingImage(); });
        var iconIdentity = new WindowIdentity(9, 9, 9);
        var cachedIcon = iconCache.Get(iconIdentity, 10);
        for (var repeat = 0; repeat < 1000; repeat++)
            AssertSameIcon(iconCache.Get(iconIdentity, 10), cachedIcon);
        Assert(iconLoads == 1 && cachedIcon!.IsFrozen, "Unchanged icon uses one frozen snapshot across 1000 refreshes");
        Assert(!ReferenceEquals(iconCache.Get(iconIdentity, 11), cachedIcon) && iconLoads == 2, "Changed native icon invalidates its cached snapshot");
        iconCache.Retain(new HashSet<WindowIdentity>());
        iconCache.Get(iconIdentity, 11);
        Assert(iconLoads == 3, "Closed window icon is evicted from the cache");
        Assert(iconCache.Get(iconIdentity, 0) is null, "Removed native icon clears the cached image");
        var delayed = new DelayedWindows();
        var delayedPicker = new WindowPickerViewModel(delayed, new PinSession(delayed));
        var inProgress = delayedPicker.RefreshAsync();
        await delayed.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pinDuringRefresh = delayedPicker.ToggleAsync(delayed.Identity);
        Assert(!pinDuringRefresh.IsCompleted, "Pin action waits for an overlapping refresh");
        delayed.Release.Set();
        await inProgress;
        Assert(await pinDuringRefresh && delayedPicker.Rows.Single().Window.Pinned, "Pin action refreshes after the older enumeration snapshot completes");
        var mutableSource = new MutableSource
        {
            Snapshot = [new(new(11, 1, 1), "First", "Fixture", false), new(new(22, 1, 1), "Second", "Fixture", true)],
        };
        var stablePicker = new WindowPickerViewModel(mutableSource, new PinSession(new FakeWindows()));
        await stablePicker.RefreshAsync();
        var stableSecond = stablePicker.Rows[1];
        var changedIcon = new DrawingImage(); changedIcon.Freeze();
        mutableSource.Snapshot = [mutableSource.Snapshot[1], mutableSource.Snapshot[0] with { Title = "Renamed", Icon = changedIcon }, new(new(33, 1, 1), "New", "Fixture", false)];
        await stablePicker.RefreshAsync();
        Assert(stablePicker.Rows.Select(row => row.Window.Identity.Handle).SequenceEqual(new nint[] { 11, 22, 33 }), "Refresh preserves surviving order and appends new windows");
        Assert(ReferenceEquals(stableSecond, stablePicker.Rows[1]), "Unchanged picker rows reuse their existing presentation objects");
        var replacementIcon = new DrawingImage(); replacementIcon.Freeze();
        mutableSource.Snapshot = mutableSource.Snapshot.Select(window => window.Identity.Handle == 11 ? window with { Icon = replacementIcon } : window).ToArray();
        await stablePicker.RefreshAsync();
        Assert(ReferenceEquals(stablePicker.Rows[0].Window.Icon, replacementIcon), "Icon-only window changes reach the picker");
        var registrar = new MemoryHotkeys(); var startup = new MemoryStartup();
        var preferences = new PreferencesViewModel(store, startup, registrar, () => { }, () => { });
        var previousShortcut = store.Value.PickerShortcut;
        preferences.ApplyPickerShortcut("Ctrl+Shift+F10");
        Assert(store.Value.PickerShortcut == previousShortcut, "Failed shortcut registration preserves stored preference");
        registrar.Accept = true; preferences.ApplyPickerShortcut("Ctrl+Shift+F10");
        Assert(new SettingsStore(dir).Value.PickerShortcut == "Ctrl+Shift+F10", "Successful shortcut registration persists across restart");
        preferences.SetOpacity(12);
        Assert(store.Value.OpacityPercent == 60 && new SettingsStore(dir).Value.OpacityPercent == 60, "Opacity is bounded and persisted by presentation model");
        preferences.SetStartup(true); Assert(startup.Enabled, "Startup preference uses its dedicated OS service");
        var restrictedPreferences = new PreferencesViewModel(store, new RestrictedStartup(), registrar, () => { }, () => { });
        Assert(!restrictedPreferences.StartWithWindows && !restrictedPreferences.StartupAvailable, "Unreadable startup registration disables the setting without crashing preferences");
        var failedSaveStore = new SettingsStore(Path.Combine(dir, "write-failure"));
        var blockedPending = Path.Combine(failedSaveStore.DirectoryPath, "settings.json.pending");
        Directory.CreateDirectory(blockedPending);
        var failedSavePreferences = new PreferencesViewModel(failedSaveStore, startup, registrar, () => { }, () => { });
        Assert(failedSavePreferences.ApplyPickerShortcut("Ctrl+Shift+F11") == ShortcutApplyResult.SaveFailed && failedSavePreferences.HasUnsavedChanges,
            "Shortcut save failure is distinct from a registration conflict and remains retryable");
        Directory.Delete(blockedPending);
        Assert(failedSavePreferences.ApplyPickerShortcut("Ctrl+Shift+F11") == ShortcutApplyResult.Applied && !failedSavePreferences.HasUnsavedChanges
            && new SettingsStore(failedSaveStore.DirectoryPath).Value.PickerShortcut == "Ctrl+Shift+F11", "Shortcut persistence can be retried after storage access recovers");
        var english = LanguageCatalog.Load("en");
        foreach (var language in LanguageCatalog.All)
        {
            var entries = LanguageCatalog.Load(language.Id);
            Assert(entries.Keys.Order().SequenceEqual(english.Keys.Order()) && entries.Values.All(v => !string.IsNullOrWhiteSpace(v)), "Translation completeness: " + language.Id);
        }
        Assert(LanguageCatalog.Resolve("system", System.Globalization.CultureInfo.GetCultureInfo("zh-TW")).Id == "zh-Hant", "System language resolves Traditional Chinese");
        Assert(LanguageCatalog.Resolve("system", System.Globalization.CultureInfo.GetCultureInfo("ar-SA")).Id == "en", "Unsupported system language falls back to English");
        var trayKey = @"Software\Voltura\HardTop\Checks\" + Guid.NewGuid().ToString("N");
        using (var trayRoot = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(trayKey))
        {
            using (var own = trayRoot.CreateSubKey("1")) { own.SetValue("ExecutablePath", @"C:\Apps\Voltura.HardTop.exe"); own.SetValue("IsPromoted", 0); }
            using (var otherEntry = trayRoot.CreateSubKey("2")) { otherEntry.SetValue("ExecutablePath", @"C:\Apps\Other.exe"); otherEntry.SetValue("IsPromoted", 0); }
            Assert(TrayIconVisibilityPromoter.TryPromoteEntries(trayRoot, @"c:\apps\VOLTURA.HARDTOP.exe", out var changed) && changed, "Tray promotion flag set using normalized exact path");
            using var ownCheck = trayRoot.OpenSubKey("1"); using var otherCheck = trayRoot.OpenSubKey("2");
            Assert(Equals(ownCheck!.GetValue("IsPromoted"), 1) && Equals(otherCheck!.GetValue("IsPromoted"), 0), "Tray promotion leaves other applications unchanged");
            Assert(TrayIconVisibilityPromoter.TryPromoteEntries(trayRoot, @"C:\Apps\Voltura.HardTop.exe", out changed) && !changed, "Tray promotion is idempotent");
        }
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(trayKey);
        using var rsa = RSA.Create(2048);
        var bytes = Encoding.UTF8.GetBytes("Signed installer fixture. Never distributed.");
        var asset = new { name = "VolturaHardTop-Setup-1.0.0-win-x64.exe", size = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, product = "VolturaHardTop", architecture = "win-x64", version = "1.0.0", assets = new[] { asset } });
        var signature = rsa.SignData(manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var verified = UpdateVerifier.Verify(manifest, signature, rsa.ExportSubjectPublicKeyInfoPem(), false);
        Assert(verified.Version == "1.0.0", "Signed update metadata accepted");
        var liteAsset = new { name = "VolturaHardTop-Setup-1.0.0-win-x64-lite.exe", size = bytes.Length, sha256 = asset.sha256 };
        var variants = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, product = "VolturaHardTop", architecture = "win-x64", version = "1.0.0", assets = new[] { asset, liteAsset } });
        var variantsSignature = rsa.SignData(variants, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Assert(UpdateVerifier.Verify(variants, variantsSignature, rsa.ExportSubjectPublicKeyInfoPem(), false).Asset.Name == asset.name, "Existing installations keep the self-contained update");
        Assert(UpdateVerifier.Verify(variants, variantsSignature, rsa.ExportSubjectPublicKeyInfoPem(), true).Asset.Name == liteAsset.name, "Lightweight installations select the signed lightweight update");
        bool missingLiteRejected = false;
        try { UpdateVerifier.Verify(manifest, signature, rsa.ExportSubjectPublicKeyInfoPem(), true); } catch (InvalidDataException) { missingLiteRejected = true; }
        Assert(missingLiteRejected, "Missing lightweight asset cannot silently switch installer type");
        var altered = manifest.ToArray(); altered[8] ^= 1;
        bool rejected = false; try { UpdateVerifier.Verify(altered, signature, rsa.ExportSubjectPublicKeyInfoPem(), false); } catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "Tampered manifest rejected");
        using var other = RSA.Create(2048); rejected = false;
        try { UpdateVerifier.Verify(manifest, signature, other.ExportSubjectPublicKeyInfoPem(), false); } catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "Wrong update signing key rejected");
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new { tag_name = "v1.0.0", draft = false, prerelease = false, assets = new[] { asset.name, "VolturaHardTop-Update-1.0.0.json", "VolturaHardTop-Update-1.0.0.sig" }.Select(name => new { name, browser_download_url = "https://github.com/voltura/voltura-hardtop/releases/download/v1.0.0/" + name }) });
        var files = new Dictionary<string, byte[]> { ["latest"] = metadata, [asset.name] = bytes, ["VolturaHardTop-Update-1.0.0.json"] = manifest, ["VolturaHardTop-Update-1.0.0.sig"] = signature };
        var installed = false;
        var rejectInstallation = true;
        var liteMetadata = JsonSerializer.SerializeToUtf8Bytes(new { tag_name = "v1.0.0", draft = false, prerelease = false, assets = new[] { asset.name, liteAsset.name, "VolturaHardTop-Update-1.0.0.json", "VolturaHardTop-Update-1.0.0.sig" }.Select(name => new { name, browser_download_url = "https://github.com/voltura/voltura-hardtop/releases/download/v1.0.0/" + name }) });
        var liteFiles = new Dictionary<string, byte[]> { ["latest"] = liteMetadata, [liteAsset.name] = bytes, ["VolturaHardTop-Update-1.0.0.json"] = variants, ["VolturaHardTop-Update-1.0.0.sig"] = variantsSignature };
        var liteHttp = new FakeHttp(liteFiles);
        await using (var updates = new UpdateService(new(Path.Combine(dir, "lite"), false, true), liteHttp, rsa.ExportSubjectPublicKeyInfoPem(), true, true, new(0, 9, 0)))
        {
            await updates.CheckAsync();
            Assert(updates.State.Ready && Path.GetFileName(updates.State.Installer) == liteAsset.name, "Lightweight update downloads and verifies end to end");
            Assert(!liteHttp.Requests.Contains(asset.name), "Lightweight update never requests the self-contained installer");
        }
        await using (var updates = new UpdateService(new(dir, false, true), new FakeHttp(files), rsa.ExportSubjectPublicKeyInfoPem(), true, false, new(0, 9, 0), install: (_, _) => { if (rejectInstallation) throw new System.ComponentModel.Win32Exception(1223); installed = true; return Task.CompletedTask; }))
        {
            await updates.CheckAsync(); Assert(updates.State.Ready, "Complete HTTP download verifies and stages an update");
            Assert(!await updates.InstallAsync() && !installed && updates.State.Status == UpdateStatus.UpdateInstallFailed, "Cancelled installer launch reports failure without a successful handoff");
            Assert(updates.State.Ready, "Failed installer launch keeps the verified package available for retry");
            rejectInstallation = false;
            Assert(await updates.InstallAsync() && installed, "Verified installer handed off only after explicit installation");
        }
        await using (var updates = new UpdateService(new(dir, false, true), new FakeHttp(files), rsa.ExportSubjectPublicKeyInfoPem(), true, false, new(0, 9, 0), install: (_, _) => throw new Exception("Must never execute")))
        {
            await updates.RestorePendingAsync(); Assert(updates.State.Ready, "Signed pending update survives restart");
            File.AppendAllText(updates.State.Installer!, "tampered");
            Assert(!await updates.InstallAsync() && updates.State.Status == UpdateStatus.UpdateVerificationFailed, "Modified cached installer blocked before handoff");
        }
        await using (var updates = new UpdateService(new(dir, false, true), new FakeHttp(files), rsa.ExportSubjectPublicKeyInfoPem(), true, false, new(1, 1, 0)))
        { await updates.CheckAsync(); Assert(updates.State.Status == UpdateStatus.Current, "Older release cannot downgrade current app"); }
        var manualHttp = new FakeHttp(files);
        await using (var updates = new UpdateService(new(dir, true, true), manualHttp, eligible: false, current: new(0, 9, 0)))
        {
            await updates.CheckAsync();
            Assert(updates.State.Status == UpdateStatus.Available, "Manual build can identify an available update");
            Assert(manualHttp.Requests.SequenceEqual(new[] { "latest" }), "Manual update check downloads release metadata only");
        }
        await using (var updates = new UpdateService(new(dir, true, true), new FailingHttp(), eligible: false))
        {
            await updates.CheckAsync();
            Assert(updates.State.Status == UpdateStatus.UpdateCheckFailed, "Manual update failure produces an actionable state");
        }
        var retainedHttp = new FakeHttp(files);
        await using (var updates = new UpdateService(new(Path.Combine(dir,"retained"),false,true),retainedHttp,rsa.ExportSubjectPublicKeyInfoPem(),true,false,new(0,9,0),install:(_,_)=>Task.CompletedTask))
        {
            await updates.CheckAsync();
            retainedHttp.FailRequests=true;
            await updates.CheckAsync();
            Assert(updates.State.Status==UpdateStatus.UpdateCheckFailed && updates.State.Ready, "A failed recheck retains a verified cached installer for offline installation");
            Assert(await updates.InstallAsync(), "Retained verified update installs without another network request");
        }
        var cancelledHttp = new FakeHttp(files);
        await using (var updates = new UpdateService(new(Path.Combine(dir,"cancelled"),false,true),cancelledHttp,rsa.ExportSubjectPublicKeyInfoPem(),true,false,new(0,9,0)))
        {
            await updates.CheckAsync();
            var cached=updates.State.Installer;
            cancelledHttp.HoldRequests=true;
            var checking=updates.CheckAsync();
            await cancelledHttp.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            updates.CancelCheck();
            await checking;
            Assert(updates.State.Ready && updates.State.Installer==cached, "Cancelling a recheck restores the verified ready update");
        }
        // Temporary directory is created by this process, with a random fixed-prefix name.
        Assert(Path.GetFileName(dir).StartsWith("VolturaHardTop.Checks-") && Path.GetFullPath(dir).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), "Fixture cleanup stays in the test directory");
        Directory.Delete(dir, true);
    }
    private static void Demo()
    {
        var app = new Application();
        var titles = new[] { "Notes — HardTop demo", "Project checklist — HardTop demo", "Reference — HardTop demo" };
        for (int i = 0; i < titles.Length; i++)
        {
            var panel = new StackPanel { Margin = new Thickness(28) };
            panel.Children.Add(new TextBlock { Text = titles[i].Split('—')[0], FontSize = 24, Foreground = Brushes.White });
            panel.Children.Add(new TextBlock { Text = "Sample content for the Voltura HardTop screenshots.", Margin = new Thickness(0, 20, 0, 0), Foreground = Brushes.LightGray });
            new Window { Title = titles[i], Width = 480, Height = 250, Left = 120 + 70 * i, Top = 150 + 60 * i, Background = new SolidColorBrush(Color.FromRgb(26, 38, 49)), Content = panel }.Show();
        }
        app.Run();
    }
    private static void AssertSameIcon(ImageSource? actual, ImageSource? expected)
    {
        if (!ReferenceEquals(actual, expected)) throw new Exception("Unchanged native icon was converted again.");
    }
    private sealed class FakeWindows : IWindows
    {
        public bool Topmost, Current = true, Allowed = true, IgnoreChanges;
        public bool IsCurrent(WindowIdentity id) => Current;
        public bool IsTopmost(WindowIdentity id) => Topmost;
        public bool SetTopmost(WindowIdentity id, bool topmost) { if (!Allowed || !Current) return false; if (!IgnoreChanges) Topmost = topmost; return true; }
    }
    private sealed class FakeHttp(Dictionary<string, byte[]> files) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public bool FailRequests { get; set; }
        public bool HoldRequests { get; set; }
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if(FailRequests) return Task.FromException<HttpResponseMessage>(new HttpRequestException("Fixture recheck failure"));
            if(HoldRequests) {RequestStarted.TrySetResult();return new TaskCompletionSource<HttpResponseMessage>().Task.WaitAsync(token);}
            var name = request.RequestUri!.Segments[^1]; Requests.Add(name);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(files[name]) });
        }
    }
    private sealed class FailingHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Fixture connection failure"));
    }
    private sealed class SampleWindows : IWindowSource
    {
        public IReadOnlyList<DesktopWindow> Enumerate() =>
        [
            new(new(1, 10, 100), "Notes", "editor", false),
            new(new(2, 20, 200), "Notes", "editor", true),
            new(new(3, 30, 300), "Reference", "browser", false),
        ];
    }
    private sealed class MemoryHotkeys : IHotkeyRegistrar
    {
        public bool Accept { get; set; }
        public bool Replace(string slot, string text, Action action) => Accept;
    }
    private sealed class MemoryStartup : IStartupService
    {
        public bool Enabled { get; set; }
    }
    private sealed class DelayedWindows : IWindowSource, IWindows
    {
        public WindowIdentity Identity { get; } = new(50, 50, 50);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        private volatile bool pinned;
        private int calls;
        public IReadOnlyList<DesktopWindow> Enumerate()
        {
            var snapshot = new DesktopWindow(Identity, "Delayed window", "Fixture", pinned);
            if (Interlocked.Increment(ref calls) == 1)
            {
                Started.SetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Fixture was not released.");
            }
            return [snapshot];
        }
        public bool IsCurrent(WindowIdentity identity) => identity == Identity;
        public bool IsTopmost(WindowIdentity identity) => pinned;
        public bool SetTopmost(WindowIdentity identity, bool value) { pinned = value; return true; }
    }
    private sealed class MutableSource : IWindowSource
    {
        public IReadOnlyList<DesktopWindow> Snapshot { get; set; } = [];
        public IReadOnlyList<DesktopWindow> Enumerate() => Snapshot;
    }
    private sealed class RestrictedStartup : IStartupService
    {
        public bool Enabled { get => throw new UnauthorizedAccessException("Fixture registry policy"); set => throw new UnauthorizedAccessException("Fixture registry policy"); }
    }
}
