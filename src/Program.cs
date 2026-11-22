using System.Diagnostics;
using System.Text;

namespace VRCXMods;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // --launch: der Autostart-Weg. Still nachpatchen, VRCX starten, beenden.
        // Nur wenn dabei etwas schiefgeht, zeigen wir ein Fenster.
        if (args.Any(a => a.Equals("--launch", StringComparison.OrdinalIgnoreCase)))
        {
            var problem = RunSilent(out var vrcxDir);
            if (problem is not null)
            {
                Application.Run(new MainForm(problem));
                return;
            }

            if (vrcxDir is not null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(Path.Combine(vrcxDir, "VRCX.exe"), "--startup")
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Application.Run(new MainForm("VRCX konnte nicht gestartet werden: " + ex.Message));
                }
            }
            return;
        }

        // --test-hooks <ordner>: Code-Hooks gegen eine KOPIE von VRCX testen (Entwicklung).
        // Erwartet <ordner>\html\assets und <ordner>\html\mods\manifest.json.
        var testIdx = Array.FindIndex(args, a => a.Equals("--test-hooks", StringComparison.OrdinalIgnoreCase));
        if (testIdx >= 0 && testIdx + 1 < args.Length)
        {
            var dir = args[testIdx + 1];
            var log = new StringBuilder();
            try
            {
                var n = BundlePatcher.ApplyAll(dir, Manifest.Load(Manifest.InstalledManifest(dir)), l => log.AppendLine(l));
                log.AppendLine($"=> {n} Hook(s) gesetzt");
                if (args.Any(a => a.Equals("--restore", StringComparison.OrdinalIgnoreCase)))
                    BundlePatcher.RestoreAll(dir, l => log.AppendLine(l));
            }
            catch (Exception ex)
            {
                log.AppendLine("FEHLER: " + ex);
            }
            Directory.CreateDirectory(Paths.LogDir);
            File.WriteAllText(Path.Combine(Paths.LogDir, "hooktest.log"), log.ToString());
            return;
        }

        Application.Run(new MainForm());
    }

    /// <summary>
    /// Prueft und patcht ohne Oberflaeche. Rueckgabe: null bei Erfolg, sonst der
    /// Grund - dann macht der Aufrufer das Fenster auf, statt still zu scheitern.
    /// </summary>
    private static string? RunSilent(out string? vrcxDir)
    {
        Paths.SyncBundledPayload();
        vrcxDir = Paths.FindVrcx();
        if (vrcxDir is null) return "VRCX wurde nicht gefunden - bitte den Ordner einmal manuell waehlen.";

        try
        {
            if (!Patcher.NeedsPatch(vrcxDir)) return null;

            var log = new StringBuilder();
            Patcher.Apply(vrcxDir, line => log.AppendLine(line));
            File.AppendAllText(
                EnsureLogFile(),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm}] nachgepatcht auf VRCX {Paths.ReadVersion(vrcxDir)}{Environment.NewLine}{log}"
            );
            return null;
        }
        catch (Exception ex)
        {
            return "Automatisches Nachpatchen fehlgeschlagen: " + ex.Message;
        }
    }

    private static string EnsureLogFile()
    {
        Directory.CreateDirectory(Paths.LogDir);
        return Path.Combine(Paths.LogDir, "autostart.log");
    }
}
