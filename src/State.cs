using System.Text.Json;

namespace VRCXMods;

/// <summary>Merkt sich, welche VRCX-Version zuletzt gepatcht wurde.</summary>
public class State
{
    public string? VrcxDir { get; set; }
    public string? VrcxVersion { get; set; }
    public string? LoaderVersion { get; set; }
    public DateTime? PatchedAt { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static State? Load()
    {
        try
        {
            if (!File.Exists(Paths.StateFile)) return null;
            return JsonSerializer.Deserialize<State>(File.ReadAllText(Paths.StateFile));
        }
        catch
        {
            return null;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.DataDir);
            File.WriteAllText(Paths.StateFile, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // Zustand ist Komfort, kein Muss - ein Fehler hier darf nichts blockieren
        }
    }
}
