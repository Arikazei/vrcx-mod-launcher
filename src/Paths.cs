using System.Diagnostics;
using Microsoft.Win32;

namespace VRCXMods;

/// <summary>
/// Findet die VRCX-Installation und die eigenen Ablageorte.
/// Die Mod-Dateien liegen bewusst AUSSERHALB von VRCX, weil der VRCX-Installer
/// den html-Ordner bei jedem Update komplett ersetzt.
/// </summary>
public static class Paths
{
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRCXMods");

    public static string PayloadDir => Path.Combine(DataDir, "payload");
    public static string StateFile => Path.Combine(DataDir, "state.json");
    public static string LogDir => Path.Combine(DataDir, "logs");

    /// <summary>Alle Fundorte in der Reihenfolge ihrer Verlaesslichkeit.</summary>
    public static IEnumerable<string> Candidates()
    {
        // 1. Laufender Prozess - zeigt garantiert auf die Installation, die der
        //    User wirklich benutzt (auch wenn sie auf einem anderen Laufwerk liegt).
        foreach (var proc in Process.GetProcessesByName("VRCX"))
        {
            string? file = null;
            try
            {
                file = proc.MainModule?.FileName;
            }
            catch
            {
                // Zugriff auf MainModule kann fehlschlagen (Rechte, 32/64-Bit)
            }

            if (!string.IsNullOrEmpty(file))
            {
                var dir = Path.GetDirectoryName(file);
                if (dir is not null) yield return dir;
            }
        }

        // 2. Zuletzt benutzter Pfad
        var saved = State.Load()?.VrcxDir;
        if (!string.IsNullOrEmpty(saved)) yield return saved;

        // 3. Registry (Uninstall-Eintrag)
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\VRCX");
            var loc = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrEmpty(loc)) yield return loc;
        }

        // 4. Standardpfad der offiziellen Installation
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "VRCX");
    }

    public static string? FindVrcx()
    {
        foreach (var dir in Candidates())
        {
            if (IsVrcxDir(dir)) return dir;
        }
        return null;
    }

    public static bool IsVrcxDir(string? dir) =>
        !string.IsNullOrWhiteSpace(dir)
        && File.Exists(Path.Combine(dir, "VRCX.exe"))
        && File.Exists(Path.Combine(dir, "html", "index.html"));

    public static string HtmlDir(string vrcxDir) => Path.Combine(vrcxDir, "html");
    public static string IndexHtml(string vrcxDir) => Path.Combine(HtmlDir(vrcxDir), "index.html");
    public static string BackupHtml(string vrcxDir) => IndexHtml(vrcxDir) + ".vrcxmods.bak";
    public static string ModsDir(string vrcxDir) => Path.Combine(HtmlDir(vrcxDir), "mods");

    public static string ReadVersion(string vrcxDir)
    {
        var f = Path.Combine(vrcxDir, "Version");
        return File.Exists(f) ? File.ReadAllText(f).Trim() : "unbekannt";
    }

    public static bool VrcxRunning() => Process.GetProcessesByName("VRCX").Length > 0;
}
