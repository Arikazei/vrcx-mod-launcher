using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VRCXMods;

public class BundlePatch
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    /// <summary>Text, an dem die richtige JS-Datei erkannt wird (Dateinamen enthalten Hashes).</summary>
    [JsonPropertyName("contains")] public string Contains { get; set; } = "";
    /// <summary>.NET-Regex, muss genau einmal treffen.</summary>
    [JsonPropertyName("find")] public string Find { get; set; } = "";
    [JsonPropertyName("replace")] public string Replace { get; set; } = "";
}

/// <summary>
/// Kleine Code-Hooks in VRCXs gebauten JS-Dateien - fuer Stellen, an die ein Mod
/// von aussen nicht herankommt (z.B. die intern gebaute Freundesliste).
///
/// Sicherheitsnetz:
///  - jede veraenderte Datei bekommt ein Backup (*.vrcxmods.bak) und eine Markierung
///  - vor jedem Patchen wird alles auf das Original zurueckgesetzt
///  - trifft ein Muster nicht GENAU einmal (z.B. nach einem VRCX-Update), wird
///    dieser Hook uebersprungen und die Datei bleibt original
/// </summary>
public static class BundlePatcher
{
    public const string FileMarker = "/*vrcx-mods-patch*/";
    private const string BakSuffix = ".vrcxmods.bak";
    private static readonly UTF8Encoding Utf8 = new(false);

    private static string AssetsDir(string vrcxDir) => Path.Combine(Paths.HtmlDir(vrcxDir), "assets");

    /// <summary>Setzt alle gepatchten JS-Dateien auf das Original zurueck.</summary>
    public static int RestoreAll(string vrcxDir, Action<string> log)
    {
        var dir = AssetsDir(vrcxDir);
        if (!Directory.Exists(dir)) return 0;

        var restored = 0;
        foreach (var bak in Directory.GetFiles(dir, "*" + BakSuffix))
        {
            var js = bak[..^BakSuffix.Length];
            // Ohne Markierung ist die Datei schon original (z.B. nach einem VRCX-Update) -> Backup ist veraltet
            if (File.Exists(js) && IsPatched(js))
            {
                File.Copy(bak, js, true);
                restored++;
            }
            File.Delete(bak);
        }
        if (restored > 0) log($"{restored} JS-Datei(en) auf Original zurueckgesetzt");
        return restored;
    }

    public static bool IsPatched(string file)
    {
        using var fs = File.OpenRead(file);
        var buf = new byte[FileMarker.Length];
        var n = fs.Read(buf, 0, buf.Length);
        return n == buf.Length && Encoding.ASCII.GetString(buf) == FileMarker;
    }

    /// <summary>Wendet die Hooks aller aktiven Mods an. Gibt die Zahl erfolgreicher Hooks zurueck.</summary>
    public static int ApplyAll(string vrcxDir, Manifest? manifest, Action<string> log)
    {
        RestoreAll(vrcxDir, log);
        if (manifest is null) return 0;

        var dir = AssetsDir(vrcxDir);
        if (!Directory.Exists(dir)) return 0;

        var files = Directory.GetFiles(dir, "*.js");
        var contents = new Dictionary<string, string>();
        string Read(string f) => contents.TryGetValue(f, out var c) ? c : contents[f] = File.ReadAllText(f, Utf8);
        var changed = new HashSet<string>();
        var applied = 0;

        foreach (var mod in manifest.Mods.Where(m => m.Enabled && !string.IsNullOrEmpty(m.Patches)))
        {
            var pf = Path.Combine(Paths.ModsDir(vrcxDir), mod.Id, mod.Patches!);
            List<BundlePatch>? patches;
            try
            {
                patches = JsonSerializer.Deserialize<List<BundlePatch>>(File.ReadAllText(pf));
            }
            catch (Exception ex)
            {
                log($"[{mod.Id}] Hook-Datei nicht lesbar: {ex.Message}");
                continue;
            }

            foreach (var p in patches ?? [])
            {
                var file = files.FirstOrDefault(f => Read(f).Contains(p.Contains, StringComparison.Ordinal));
                if (file is null)
                {
                    log($"[{mod.Id}] Hook '{p.Id}': Stelle in VRCX nicht gefunden - uebersprungen");
                    continue;
                }

                try
                {
                    var rx = new Regex(p.Find, RegexOptions.None, TimeSpan.FromSeconds(15));
                    var text = Read(file);
                    var hits = rx.Matches(text).Count;
                    if (hits != 1)
                    {
                        log($"[{mod.Id}] Hook '{p.Id}': {hits} Treffer statt 1 - uebersprungen (VRCX-Version passt nicht)");
                        continue;
                    }
                    contents[file] = rx.Replace(text, p.Replace, 1);
                    changed.Add(file);
                    applied++;
                    log($"[{mod.Id}] Hook '{p.Id}' gesetzt in {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    log($"[{mod.Id}] Hook '{p.Id}' fehlgeschlagen: {ex.Message}");
                }
            }
        }

        foreach (var file in changed)
        {
            File.Copy(file, file + BakSuffix, true);
            File.WriteAllText(file, FileMarker + contents[file], Utf8);
        }
        return applied;
    }
}
