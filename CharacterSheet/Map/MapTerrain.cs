using System.Collections.Frozen;

namespace CharacterSheet.Map;

/// <summary>
/// Terrain type constants and lookup tables, ported from terrain.py.
/// </summary>
public static class MapTerrain
{
    public static readonly IReadOnlyList<string> All =
    [
        "Jungle",
        "Beach",
        "Mountains",
        "Rivers",
        "Swamp",
        "Wasteland",
        "Grassland",
        "Ocean",
        "Structure",
        "Cave",
    ];

    public static readonly IReadOnlyDictionary<string, string> Colors =
        new Dictionary<string, string>
        {
            ["Jungle"]    = "#1a5e1a",
            ["Beach"]     = "#c8a832",
            ["Mountains"] = "#706868",
            ["Rivers"]    = "#2a7dd4",
            ["Swamp"]     = "#3e5220",
            ["Wasteland"] = "#9c6b3a",
            ["Grassland"] = "#3db53d",
            ["Ocean"]     = "#1a3f8c",
            ["Structure"] = "#b0886a",
            ["Cave"]      = "#4a3a52",
        }.ToFrozenDictionary();

    public static readonly IReadOnlyDictionary<string, byte> Alpha =
        new Dictionary<string, byte>
        {
            ["Jungle"]    = 145,
            ["Beach"]     = 120,
            ["Mountains"] = 145,
            ["Rivers"]    = 155,
            ["Swamp"]     = 150,
            ["Wasteland"] = 135,
            ["Grassland"] = 115,
            ["Ocean"]     = 165,
            ["Structure"] = 160,
            ["Cave"]      = 170,
        }.ToFrozenDictionary();

    public static readonly IReadOnlyDictionary<string, string> Abbr =
        new Dictionary<string, string>
        {
            ["Jungle"]    = "JNG",
            ["Beach"]     = "BCH",
            ["Mountains"] = "MTN",
            ["Rivers"]    = "RVR",
            ["Swamp"]     = "SWP",
            ["Wasteland"] = "WLD",
            ["Grassland"] = "GRS",
            ["Ocean"]     = "OCN",
            ["Structure"] = "STR",
            ["Cave"]      = "CAV",
        }.ToFrozenDictionary();

    public static readonly FrozenSet<string> WaterTerrains =
        FrozenSet.Create("Ocean", "Rivers");

    public static readonly FrozenSet<string> IndoorTerrains =
        FrozenSet.Create("Structure", "Cave");

    public static string GetColor(string terrain) =>
        Colors.TryGetValue(terrain, out var c) ? c : "#888888";

    public static byte GetAlpha(string terrain) =>
        Alpha.TryGetValue(terrain, out var a) ? a : (byte)160;

    public static string GetAbbr(string terrain) =>
        Abbr.TryGetValue(terrain, out var s) ? s : terrain[..Math.Min(3, terrain.Length)].ToUpper();
}

/// <summary>
/// Maps hex node numbers (1-based) to terrain type strings.
/// Ported from TerrainMap in terrain.py.
/// </summary>
public class MapTerrainMap
{
    private readonly Dictionary<int, string> _data = [];

    /// <summary>Assigns a terrain type to a node. Silently ignores unknown terrain names.</summary>
    public void Set(int node, string terrain)
    {
        if (MapTerrain.All.Contains(terrain))
            _data[node] = terrain;
    }

    public void Clear(int node)    => _data.Remove(node);
    public void ClearAll()         => _data.Clear();

    public string? Get(int node)   => _data.TryGetValue(node, out var t) ? t : null;

    public int Count => _data.Count;

    /// <summary>Serialize to a string-keyed dict for JSON (matches Python to_dict).</summary>
    public Dictionary<string, string> ToDict() =>
        _data.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);

    /// <summary>Rebuild from a deserialized string-keyed dict.</summary>
    public void FromDict(Dictionary<string, string>? dict)
    {
        _data.Clear();
        if (dict is null) return;
        foreach (var (k, v) in dict)
            if (int.TryParse(k, out var node) && MapTerrain.All.Contains(v))
                _data[node] = v;
    }
}

/// <summary>Maps hex node numbers to curse levels ("Lesser Curse" or "Greater Curse").</summary>
public class MapCurseMap
{
    public const string LesserCurse  = "Lesser Curse";
    public const string GreaterCurse = "Greater Curse";

    private readonly Dictionary<int, string> _data = [];

    public void Set(int node, string level)  => _data[node] = level;
    public void Clear(int node)               => _data.Remove(node);
    public void ClearAll()                    => _data.Clear();
    public string? Get(int node)              => _data.TryGetValue(node, out var v) ? v : null;
    public int Count                          => _data.Count;
    public IEnumerable<KeyValuePair<int, string>> All => _data;

    public Dictionary<string, string> ToDict() =>
        _data.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);

    public void FromDict(Dictionary<string, string>? dict)
    {
        _data.Clear();
        if (dict is null) return;
        foreach (var (k, v) in dict)
            if (int.TryParse(k, out var node))
                _data[node] = v;
    }
}
