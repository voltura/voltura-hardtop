using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Voltura.HardTop;
using Voltura.HardTop.Platform;
using Voltura.HardTop.Presentation;
using Voltura.HardTop.Updates;

namespace HardTop.Checks;

internal static class DemoExport
{
    internal static void Run()
    {
        var repository=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(repository,"Directory.Build.props")))
            repository=Directory.GetParent(repository)?.FullName??throw new DirectoryNotFoundException("Repository root not found.");
        var output=Path.Combine(repository,"artifacts/video");
        Directory.CreateDirectory(output);
        var posted=new List<DispatcherOperation>();
        var dispatcher=Dispatcher.CurrentDispatcher;
        void Posted(object? sender,DispatcherHookEventArgs e)=>posted.Add(e.Operation);
        dispatcher.Hooks.OperationPosted+=Posted;
        App app;
        try {app=new App();} finally {dispatcher.Hooks.OperationPosted-=Posted;}
        foreach(var operation in posted) operation.Abort();
        app.InitializeComponent();
        var settings=new SettingsStore(Path.Combine(output,"Data"));
        settings.Value.Theme="dark";settings.Value.Language="en";settings.Value.Introduced=true;
        typeof(App).GetProperty("Settings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,settings);
        var updates=new UpdateService(new(settings.DirectoryPath,false,true),eligible:true);
        typeof(App).GetProperty("Updates",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,updates);
        var source=new SampleSource();
        var pins=new PinSession(source);
        typeof(App).GetProperty("Pins",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,pins);
        var picker=new WindowPickerViewModel(source,pins);
        picker.RefreshAsync().GetAwaiter().GetResult();
        var preferences=new PreferencesViewModel(settings,new Startup(),new Registration(),()=>{},()=>{});
        var window=new MainWindow(app,picker,preferences);
        var content=(FrameworkElement)window.Content;
        void Layout(){content.Measure(new(700,760));content.Arrange(new Rect(0,0,700,760));content.UpdateLayout();}
        void Save(string name)
        {
            Layout();
            var bitmap=new RenderTargetBitmap(1400,1520,192,192,PixelFormats.Pbgra32);bitmap.Render(content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);
        }
        void Complete(Func<Task> action)
        {
            var previous=SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var task=action();
                if(!task.IsCompleted)
                {
                    var frame=new DispatcherFrame();
                    _=task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false),TaskScheduler.Default);
                    Dispatcher.PushFrame(frame);
                }
                task.GetAwaiter().GetResult();
            }
            finally {SynchronizationContext.SetSynchronizationContext(previous);}
        }
        window.ShowWindows();Save("01-picker");
        ((TextBox)window.FindName("Search")).Text="notes";Save("02-search");
        Complete(async()=>{await picker.ToggleAsync(source.First);});Save("03-pinned-result");
        ((TextBox)window.FindName("Search")).Clear();picker.PinnedOnly=true;Save("04-pinned-view");
        window.ShowSettings();Layout();((ScrollViewer)window.FindName("SettingsPanel")).ScrollToTop();Save("05-preferences");
        ((ScrollViewer)window.FindName("SettingsPanel")).ScrollToVerticalOffset(340);Save("06-shortcuts");
        preferences.SetTheme("light");picker.PinnedOnly=false;window.ShowWindows();Save("07-light");
        updates.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Console.WriteLine("Exported actual WPF app views with isolated sample-window data: "+output);
    }
    private sealed class Startup:IStartupService {public bool Enabled {get;set;}}
    private sealed class Registration:IHotkeyRegistrar {public bool Replace(string slot,string text,Action action)=>true;}
    private sealed class SampleSource:IWindowSource,IWindows
    {
        public WindowIdentity First {get;}=new(11,1,1);
        private bool pinned;
        public IReadOnlyList<DesktopWindow> Enumerate()=>[
            new(First,"Project notes — planning and research","Notes",pinned),
            new(new(22,1,1),"Reference guide — useful details","Browser",false),
            new(new(33,1,1),"Weekly checklist","Notepad",false),
        ];
        public bool IsCurrent(WindowIdentity identity)=>identity==First;
        public bool IsTopmost(WindowIdentity identity)=>pinned;
        public bool SetTopmost(WindowIdentity identity,bool value){pinned=value;return true;}
    }
}
