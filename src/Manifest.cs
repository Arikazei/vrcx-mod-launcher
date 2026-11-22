using System.Text.Json;
using System.Text.Json.Serialization;

namespace VRCXMods;

public class ModEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("apiVersion")] public string? ApiVersion { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

    // Muessen beim Speichern erhalten bleiben (sonst fehlt z.B. das CSS eines Mods)
    [JsonPropertyName("entry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Entry { get; set; }

    [JsonPropertyName("style"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Style { get; set; }

    /// <summary>Optionale Code-Hooks in VRCXs JS (Datei im Mod-Ordner, siehe BundlePatcher).</summary>
    [JsonPropertyName("patches"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Patches { get; set; }

    public string Display => $"{Name ?? Id}   v{Version ?? "?"}";
}

public class Manifest
{
    [JsonPropertyName("loaderVersion")] public string? LoaderVersion { get; set; }
    [JsonPropertyName("mods")] public List<ModEntry> Mods { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Liest das Manifest aus dem Payload (Quelle) oder aus einer Installation.</summary>
    public static Manifest? Load(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(file), Options);
        }
        catch
        {
            return null;
        }
    }

    public void Save(string file)
    {
        File.WriteAllText(file, JsonSerializer.Serialize(this, Options));
    }

    public static string PayloadManifest => Path.Combine(Paths.PayloadDir, "manifest.json");
    public static string InstalledManifest(string vrcxDir) => Path.Combine(Paths.ModsDir(vrcxDir), "manifest.json");
}
