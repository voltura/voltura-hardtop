using Voltura.HardTop.Platform;

namespace Voltura.HardTop.Presentation;

[Flags]
public enum PreferenceChange
{
    Language = 1, Appearance = 2, Opacity = 4, Updates = 8
}

public sealed class PreferencesViewModel(
    SettingsStore store,
    IStartupService startup,
    IHotkeyRegistrar hotkeys,
    Action openPicker,
    Action toggleForeground)
{
    public event Action<PreferenceChange>? Changed;
    public event Action<string>? Feedback;
    public Preferences Value => store.Value;
    public bool StartupAvailable => !store.Portable;
    public bool StartWithWindows => startup.Enabled;

    public void SetLanguage(string language)
    {
        Value.Language = language;
        Save();
        Changed?.Invoke(PreferenceChange.Language);
    }

    public void SetTheme(string theme)
    {
        if (theme is not ("system" or "light" or "dark"))
            return;
        Value.Theme = theme;
        Save();
        Changed?.Invoke(PreferenceChange.Appearance);
    }

    public void SetOpacity(int percent)
    {
        Value.OpacityPercent = Math.Clamp(percent, 60, 100);
        Save();
        Changed?.Invoke(PreferenceChange.Opacity);
    }

    public void SetAutomaticUpdates(bool enabled)
    {
        Value.AutomaticUpdates = enabled;
        Save();
        Changed?.Invoke(PreferenceChange.Updates);
    }

    public void SetStartup(bool enabled)
    {
        if (!StartupAvailable)
            return;
        try
        {
            startup.Enabled = enabled;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Feedback?.Invoke("SaveFailed");
        }
    }

    public bool ApplyPickerShortcut(string text) => ApplyShortcut("picker", text, openPicker);
    public bool ApplyToggleShortcut(string text) => ApplyShortcut("toggle", text, toggleForeground);

    private bool ApplyShortcut(string slot, string text, Action action)
    {
        if (!hotkeys.Replace(slot, text, action))
        {
            return false;
        }

        if (slot == "picker")
            Value.PickerShortcut = text.Trim();
        else
            Value.ToggleShortcut = text.Trim();

        return Save();
    }

    public void CompleteIntroduction()
    {
        Value.Introduced = true;
        Save();
    }

    private bool Save()
    {
        try
        {
            store.Save();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Feedback?.Invoke("SaveFailed");
            return false;
        }
    }
}
