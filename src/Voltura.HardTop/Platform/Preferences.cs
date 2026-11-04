namespace Voltura.HardTop.Platform;

public sealed record Preferences
{
    public string Language { get; set; } = "system";
    public string Theme { get; set; } = "system";
    public string PickerShortcut { get; set; } = "Ctrl+Alt+Z";
    public string ToggleShortcut { get; set; } = "Ctrl+Alt+T";
    public bool AutomaticUpdates { get; set; } = true;
    public bool Introduced
    {
        get; set;
    }
    public int OpacityPercent { get; set; } = 100;
}

