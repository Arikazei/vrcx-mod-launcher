namespace VRCXMods;

public record PatchStatus(bool Patched, string VrcxVersion, bool BackupExists, int ModCount, string? PatchedVersion);

/// <summary>
/// Patcht genau zwei Zeilen in die offizielle index.html: eine Import-Map, die
/// 'vue' auf unsere mitgelieferte Kopie zeigt, und das Loader-Script. VRCX selbst
/// bleibt unveraendert - die Installation ist damit weiterhin die signierte
/// Originalversion und aktualisiert sich normal weiter.
/// </summary>
public static class Patcher
{
    public const string Marker = "data-vrcx-mods";

    private const string ImportMap =
        "<script type=\"importmap\" data-vrcx-mods=\"map\">{\"imports\":{\"vue\":\"./mods/vendor/vue.js\"}}</script>";

    private const string LoaderTag =
        "<script type=\"module\" src=\"./mods/loader.js\" data-vrcx-mods=\"loader\"></script>";

    private const string AnchorHead = "<meta charset=\"utf-8\" />";
    private const string AnchorBody = "</body>";

    public static PatchStatus GetStatus(string vrcxDir)
    {
        var index = File.ReadAllText(Paths.IndexHtml(vrcxDir));
        var modsDir = Paths.ModsDir(vrcxDir);
        var count = Directory.Exists(modsDir)
            ? Directory.GetDirectories(modsDir).Count(d => Path.GetFileName(d) != "vendor")
            : 0;

        return new PatchStatus(
            index.Contains(Marker),
            Paths.ReadVersion(vrcxDir),
            File.Exists(Paths.BackupHtml(vrcxDir)),
            count,
            State.Load()?.VrcxVersion);
    }

    public static void Apply(string vrcxDir, Action<string> log)
    {
        if (!Paths.IsVrcxDir(vrcxDir)) throw new InvalidOperationException("Kein gueltiger VRCX-Ordner: " + vrcxDir);
        if (!Directory.Exists(Paths.PayloadDir) || !File.Exists(Path.Combine(Paths.PayloadDir, "loader.js")))
            throw new InvalidOperationException("Keine Mod-Dateien in " + Paths.PayloadDir);

        var index = Paths.IndexHtml(vrcxDir);
        var backup = Paths.BackupHtml(vrcxDir);
        var current = File.ReadAllText(index);

        string source;
        if (current.Contains(Marker))
        {
            if (!File.Exists(backup))
                throw new InvalidOperationException("index.html ist gepatcht, aber das Backup fehlt. Bitte VRCX neu installieren oder reparieren.");
            source = File.ReadAllText(backup);
            log("bereits gepatcht - wird sauber vom Backup neu aufgebaut");
        }
        else
        {
            File.Copy(index, backup, true);
            source = current;
            log("Backup angelegt: " + Path.GetFileName(backup));
        }

        if (!source.Contains(AnchorHead) || !source.Contains(AnchorBody))
            throw new InvalidOperationException("Ankerpunkte in index.html nicht gefunden - VRCX-Layout hat sich geaendert.");

        CopyPayload(vrcxDir, log);

        var patched = ReplaceFirst(source, AnchorHead, AnchorHead + "\n        " + ImportMap);
        patched = ReplaceFirst(patched, AnchorBody, "        " + LoaderTag + "\n    " + AnchorBody);
        File.WriteAllText(index, patched);
        log("index.html gepatcht (2 Zeilen)");

        new State
        {
            VrcxDir = vrcxDir,
            VrcxVersion = Paths.ReadVersion(vrcxDir),
            LoaderVersion = ReadLoaderVersion(),
            PatchedAt = DateTime.Now
        }.Save();
    }

    public static void Remove(string vrcxDir, Action<string> log)
    {
        var index = Paths.IndexHtml(vrcxDir);
        var backup = Paths.BackupHtml(vrcxDir);

        if (File.Exists(backup))
        {
            File.Copy(backup, index, true);
            File.Delete(backup);
            log("index.html aus dem Backup wiederhergestellt");
        }
        else
        {
            log("kein Backup vorhanden - index.html bleibt unveraendert");
        }

        var mods = Paths.ModsDir(vrcxDir);
        if (Directory.Exists(mods))
        {
            Directory.Delete(mods, true);
            log("Mod-Dateien entfernt");
        }
    }

    private static void CopyPayload(string vrcxDir, Action<string> log)
    {
        var target = Paths.ModsDir(vrcxDir);
        if (Directory.Exists(target)) Directory.Delete(target, true);
        Directory.CreateDirectory(target);

        var files = 0;
        foreach (var src in Directory.GetFiles(Paths.PayloadDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(Paths.PayloadDir, src);
            var dst = Path.Combine(target, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
            files++;
        }
        log($"{files} Datei(en) nach html\\mods kopiert");
    }

    private static string ReadLoaderVersion()
    {
        try
        {
            var manifest = Path.Combine(Paths.PayloadDir, "manifest.json");
            if (!File.Exists(manifest)) return "?";
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
            return doc.RootElement.TryGetProperty("loaderVersion", out var v) ? v.GetString() ?? "?" : "?";
        }
        catch
        {
            return "?";
        }
    }

    private static string ReplaceFirst(string haystack, string needle, string replacement)
    {
        var i = haystack.IndexOf(needle, StringComparison.Ordinal);
        return i < 0 ? haystack : haystack[..i] + replacement + haystack[(i + needle.Length)..];
    }
}
