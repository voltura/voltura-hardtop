using Microsoft.Win32;

namespace Voltura.HardTop.Platform;

public interface IStartupService
{
    bool Enabled
    {
        get; set;
    }
}

public sealed class StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VolturaHardTop";

    public bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --startup");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
    }
}
