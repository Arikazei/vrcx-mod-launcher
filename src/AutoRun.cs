using Microsoft.Win32;

namespace VRCXMods;

public enum AutoRunState
{
    /// <summary>Kein Autostart-Eintrag vorhanden.</summary>
    None,

    /// <summary>VRCX startet direkt - Mods wuerden nach einem Update fehlen.</summary>
    Vrcx,

    /// <summary>Unser Launcher startet und patcht vorher nach.</summary>
    Mods
}

/// <summary>
/// VRCX traegt seinen Autostart selbst als HKCU\...\Run\VRCX ein
/// ("...\VRCX.exe" --startup). Wir biegen genau diesen Wert auf den Launcher um,
/// damit nach einem VRCX-Update zuerst gepatcht und dann gestartet wird.
/// </summary>
public static class AutoRun
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VRCX";

    public static string? CurrentValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as string;
    }

    public static AutoRunState GetState()
    {
        var value = CurrentValue();
        if (string.IsNullOrWhiteSpace(value)) return AutoRunState.None;
        return value.Contains("VRCXMods.exe", StringComparison.OrdinalIgnoreCase)
            ? AutoRunState.Mods
            : AutoRunState.Vrcx;
    }

    /// <summary>Autostart auf den Launcher umbiegen; der Originalwert wird gesichert.</summary>
    public static void Enable(string launcherExe)
    {
        var previous = CurrentValue();
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true)
            ?? throw new InvalidOperationException("Autostart-Schluessel nicht schreibbar");

        if (!string.IsNullOrWhiteSpace(previous) && !previous.Contains("VRCXMods.exe", StringComparison.OrdinalIgnoreCase))
        {
            var st = State.Load() ?? new State();
            st.PreviousAutoRun = previous;
            st.Save();
        }

        key.SetValue(ValueName, $"\"{launcherExe}\" --launch");
    }

    /// <summary>Ursprungszustand wiederherstellen: alter VRCX-Eintrag oder nichts.</summary>
    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
        if (key is null) return;

        var previous = State.Load()?.PreviousAutoRun;
        if (!string.IsNullOrWhiteSpace(previous)) key.SetValue(ValueName, previous);
        else key.DeleteValue(ValueName, false);
    }
}
