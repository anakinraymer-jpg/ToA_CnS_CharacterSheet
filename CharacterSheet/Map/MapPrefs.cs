using System.IO;
using System.Text.Json;

namespace CharacterSheet.Map;

/// <summary>
/// Persists the two map paths that should survive an app restart:
/// the background image file and the most-recently-used map save file.
/// Stored as a tiny JSON file in %AppData%\ToA_CnS_CharacterSheet\.
/// All methods are fire-and-forget; failures are silently swallowed.
/// </summary>
internal sealed class MapPrefs
{
    public string? LastMapImage { get; set; }
    public string? LastMapSave  { get; set; }

    private static readonly string s_prefsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToA_CnS_CharacterSheet",
        "map_prefs.json");

    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };

    public static MapPrefs Load()
    {
        try
        {
            if (File.Exists(s_prefsPath))
                return JsonSerializer.Deserialize<MapPrefs>(
                           File.ReadAllText(s_prefsPath), s_opts) ?? new();
        }
        catch { }
        return new();
    }

    private static void Write(MapPrefs p)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(s_prefsPath)!);
            File.WriteAllText(s_prefsPath, JsonSerializer.Serialize(p, s_opts));
        }
        catch { }
    }

    public static void UpdateImage(string path) { var p = Load(); p.LastMapImage = path; Write(p); }
    public static void UpdateSave (string path) { var p = Load(); p.LastMapSave  = path; Write(p); }
}
