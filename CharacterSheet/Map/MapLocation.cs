namespace CharacterSheet.Map;

/// <summary>
/// Valid location types a named place on the map can have. Ported from locations.py.
/// </summary>
public static class MapLocationTypes
{
    public static readonly IReadOnlyList<string> All =
        ["Structure", "Outdoor Structure", "Cave"];
}

/// <summary>
/// A named location pinned to a hex node. Ported from Location dataclass in locations.py.
/// </summary>
public class MapLocation
{
    public string Id           { get; set; } = Guid.NewGuid().ToString();
    public string Name         { get; set; } = "";
    public int    Node         { get; set; }
    public string Color        { get; set; } = "#f39c12";
    public string Description  { get; set; } = "";
    public string LocationType { get; set; } = "";
}

/// <summary>
/// In-memory store for all named locations. Ported from LocationManager in locations.py.
/// </summary>
public class MapLocationManager
{
    private readonly Dictionary<string, MapLocation> _locs = [];

    public void Add(MapLocation loc)    => _locs[loc.Id] = loc;

    public void Remove(string id)       => _locs.Remove(id);

    public MapLocation? Get(string id)  =>
        _locs.TryGetValue(id, out var l) ? l : null;

    public void Update(string id, Action<MapLocation> mutate)
    {
        if (_locs.TryGetValue(id, out var loc))
            mutate(loc);
    }

    public IReadOnlyList<MapLocation> AtNode(int node) =>
        [.. _locs.Values.Where(l => l.Node == node)];

    /// <summary>All locations sorted by node then name (matches Python behaviour).</summary>
    public IReadOnlyList<MapLocation> All =>
        [.. _locs.Values.OrderBy(l => l.Node).ThenBy(l => l.Name)];

    /// <summary>Export for JSON serialization.</summary>
    public List<MapLocation> ToList() => [.. _locs.Values];

    /// <summary>Rebuild state from a deserialized list.</summary>
    public void FromList(IEnumerable<MapLocation> locs)
    {
        _locs.Clear();
        foreach (var l in locs)
            _locs[l.Id] = l;
    }
}
