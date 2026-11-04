using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Voltura.HardTop.Platform;
using Voltura.HardTop.Presentation;

namespace HardTop.Checks;

internal static class PerformanceChecks
{
    internal static void RunSettingsRecovery()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/review"));
        var directory=Path.Combine(root,"recovery-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"settings.json");
        File.WriteAllText(path,"{}");
        _=new SettingsStore(directory); // Warm serializer metadata before measuring.
        using(var fixture=File.Create(path)) fixture.SetLength(16*1024*1024);
        var allocated=GC.GetAllocatedBytesForCurrentThread();
        var watch=Stopwatch.StartNew();
        var recovered=new SettingsStore(directory);
        var elapsed=watch.Elapsed.TotalMilliseconds;
        allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        if(!recovered.Recovered || Directory.GetFiles(directory,"*.recovered-*").Length!=1)
            throw new Exception("Oversized settings did not recover with an original backup.");
        Console.WriteLine(JsonSerializer.Serialize(new { inputBytes=16*1024*1024, elapsedMilliseconds=elapsed, managedBytes=allocated },new JsonSerializerOptions {WriteIndented=true}));
        if(!Path.GetFullPath(directory).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected recovery fixture path.");
        Directory.Delete(directory,true);
    }

    internal static void RunTray()
    {
        var operations = new List<System.Windows.Threading.DispatcherOperation>();
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        void Capture(object? sender, System.Windows.Threading.DispatcherHookEventArgs e) => operations.Add(e.Operation);
        dispatcher.Hooks.OperationPosted += Capture;
        Voltura.HardTop.App app;
        try { app = new Voltura.HardTop.App(); }
        finally { dispatcher.Hooks.OperationPosted -= Capture; }
        foreach (var operation in operations) operation.Abort();
        app.InitializeComponent();
        using var tray = new TrayService(dispatcher, new TrayActions(() => { }, () => { }, () => { }, () => { }, () => { }, () => { }));
        for (var warmup = 0; warmup < 5; warmup++) tray.Rebuild(false, false);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 500; iteration++) tray.Rebuild(false, false);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine(JsonSerializer.Serialize(new { calls = 500, elapsedMilliseconds = watch.Elapsed.TotalMilliseconds, bytesPerCall = allocated / 500 }, new JsonSerializerOptions { WriteIndented = true }));
        var icon=(System.Windows.Forms.NotifyIcon)typeof(TrayService).GetField("icon",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(tray)!;
        var original=icon.ContextMenuStrip;
        Voltura.HardTop.Localization.Strings.Current.SetLanguage("sv");
        tray.Rebuild(false,false);
        if(ReferenceEquals(original,icon.ContextMenuStrip) || icon.ContextMenuStrip!.Items[0].Text != Voltura.HardTop.Localization.Strings.Current["OpenApp"])
            throw new Exception("Tray cache did not follow a language change.");
        var localized=icon.ContextMenuStrip;
        tray.Rebuild(true,false);
        if(ReferenceEquals(localized,icon.ContextMenuStrip)) throw new Exception("Tray cache did not follow a theme change.");
        var themed=icon.ContextMenuStrip;
        tray.Rebuild(true,false,force:true);
        if(ReferenceEquals(themed,icon.ContextMenuStrip)) throw new Exception("System appearance refresh did not rebuild the tray.");
        Voltura.HardTop.Localization.Strings.Current.SetLanguage("en");
    }

    internal static void RunNative()
    {
        var source = new Voltura.HardTop.Platform.Windows();
        for (var warmup = 0; warmup < 5; warmup++) source.Enumerate();
        var samples = new List<double>();
        var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var allocated = GC.GetTotalAllocatedBytes(true);
        var windowCount = 0;
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var watch = Stopwatch.StartNew();
            windowCount = source.Enumerate().Count;
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        allocated = GC.GetTotalAllocatedBytes(true) - allocated;
        var cpuMillisecondsPerRefresh = (process.TotalProcessorTime - cpu).TotalMilliseconds / 100;
        samples.Sort();
        Console.WriteLine(JsonSerializer.Serialize(new { windowCount, medianMilliseconds = samples[50], p95Milliseconds = samples[95], cpuMillisecondsPerRefresh, bytesPerRefresh = allocated / 100 }, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static async Task RunAsync()
    {
        var measurements = new List<object>();
        foreach (var count in new[] { 100, 1000, 5000 })
        {
            var source = new FixedSource(count);
            var picker = new WindowPickerViewModel(source, new PinSession(source));
            for (var warmup = 0; warmup < 5; warmup++)
                await picker.RefreshAsync();
            var samples = new List<double>();
            var allocated = GC.GetTotalAllocatedBytes(true);
            for (var iteration = 0; iteration < 25; iteration++)
            {
                var watch = Stopwatch.StartNew();
                await picker.RefreshAsync();
                samples.Add(watch.Elapsed.TotalMilliseconds);
                if (picker.Rows.Count != count || picker.Rows[0].Window.Identity.Handle != 1)
                    throw new Exception("Performance workload changed the picker results.");
            }
            allocated = GC.GetTotalAllocatedBytes(true) - allocated;
            samples.Sort();
            measurements.Add(new { windows = count, medianMilliseconds = samples[12], p95Milliseconds = samples[23], bytesPerRefresh = allocated / 25 });
        }
        Console.WriteLine(JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class FixedSource(int count) : IWindowSource, IWindows
    {
        private readonly DesktopWindow[] windows = Enumerable.Range(1, count)
            .Select(index => new DesktopWindow(new(index, 1, 1), "Window " + index, "Application", index % 2 == 0)).ToArray();
        public IReadOnlyList<DesktopWindow> Enumerate() => windows;
        public bool IsCurrent(WindowIdentity identity) => true;
        public bool IsTopmost(WindowIdentity identity) => false;
        public bool SetTopmost(WindowIdentity identity, bool topmost) => false;
    }
}
