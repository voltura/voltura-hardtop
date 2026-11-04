using System.Text.Json;

namespace Voltura.HardTop.Platform;

public sealed class SettingsStore
{
    public bool Portable
    {
        get;
    }
    public string DirectoryPath
    {
        get;
    }
    public Preferences Value
    {
        get; private set;
    }
    public bool Recovered
    {
        get; private set;
    }
    public SettingsStore(string? isolatedPath = null)
    {
        Portable = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.marker"));
        DirectoryPath = isolatedPath ?? (Portable ? Path.Combine(AppContext.BaseDirectory, "Data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voltura", "HardTop"));
        Value = Read();
    }
    private Preferences Read()
    {
        var path = Path.Combine(DirectoryPath, "settings.json");
        if (!File.Exists(path))
            return new();
        try
        {
            var settings = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? throw new JsonException();
            if (settings.Theme is not ("system" or "light" or "dark"))
                settings.Theme = "system";
            settings.OpacityPercent = Math.Clamp(settings.OpacityPercent, 60, 100);
            if (string.IsNullOrWhiteSpace(settings.Language))
                settings.Language = "system";
            if (!HotkeyGesture.TryParse(settings.PickerShortcut, out _))
                settings.PickerShortcut = "Ctrl+Alt+Z";
            if (!HotkeyGesture.TryParse(settings.ToggleShortcut, out _))
                settings.ToggleShortcut = "Ctrl+Alt+T";
            return settings;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Recovered = true;
            try
            {
                File.Copy(path, path + ".recovered-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), false);
            }
            catch (IOException) { }
            return new();
        }
    }
    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, "settings.json");
        var pending = path + ".pending";
        File.WriteAllText(pending, JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(pending, path, true);
    }
}
