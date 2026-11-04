using Voltura.HardTop.Platform;

namespace Voltura.HardTop.Presentation;

[Flags]
public enum PreferenceChange
{
    Language = 1, Appearance = 2, Opacity = 4, Updates = 8
}
public enum ShortcutApplyResult { Applied, RegistrationFailed, SaveFailed }

public sealed class PreferencesViewModel(
    SettingsStore store,
    IStartupService startup,
    IHotkeyRegistrar hotkeys,
    Action openPicker,
    Action toggleForeground)
{
    private bool startupUnavailable;
    public event Action<PreferenceChange>? Changed;
    public event Action<string>? Feedback;
    public Preferences Value => store.Value;
    public bool HasUnsavedChanges { get; private set; }
    public bool StartupAvailable => !store.Portable && !startupUnavailable;
    public bool StartWithWindows
    {
        get
        {
            if (!StartupAvailable) return false;
            try { return startup.Enabled; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                startupUnavailable = true;
                Feedback?.Invoke("ActionFailed");
                return false;
            }
        }
    }

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

    public ShortcutApplyResult ApplyPickerShortcut(string text) => ApplyShortcut("picker", text, openPicker);
    public ShortcutApplyResult ApplyToggleShortcut(string text) => ApplyShortcut("toggle", text, toggleForeground);

    private ShortcutApplyResult ApplyShortcut(string slot, string text, Action action)
    {
        if (!hotkeys.Replace(slot, text, action))
        {
            return ShortcutApplyResult.RegistrationFailed;
        }

        if (slot == "picker")
            Value.PickerShortcut = text.Trim();
        else
            Value.ToggleShortcut = text.Trim();

        return Save() ? ShortcutApplyResult.Applied : ShortcutApplyResult.SaveFailed;
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
            HasUnsavedChanges = false;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            HasUnsavedChanges = true;
            Feedback?.Invoke("SaveFailed");
            return false;
        }
    }
}
