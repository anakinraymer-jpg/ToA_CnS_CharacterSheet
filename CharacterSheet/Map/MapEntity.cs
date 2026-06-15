using System.Text.Json.Serialization;

namespace CharacterSheet.Map;

/// <summary>
/// A single entity (character, group, or bot) on the map. Ported from entities.py.
/// </summary>
public class MapEntity
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string Name        { get; set; } = "";
    public int    Node        { get; set; }
    public string Color       { get; set; } = "#e74c3c";
    public bool   IsGroup     { get; set; }
    public bool   IsBot       { get; set; }
    public bool   IsPlayer    { get; set; }
    public bool   Seafaring   { get; set; }
    public int?   BotTarget   { get; set; }
    public string FlavorText  { get; set; } = "";

    /// <summary>IDs of member entities when IsGroup is true.</summary>
    public List<string> Members { get; set; } = [];

    /// <summary>
    /// Short display label: up to 3 initials of the name words.
    /// Groups show initials × member count.
    /// </summary>
    [JsonIgnore]
    public string Label
    {
        get
        {
            var words    = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var initials = string.Concat(words.Take(3).Select(w => char.ToUpper(w[0])));
            if (IsGroup && Members.Count > 0)
                return $"{initials}×{Members.Count}";
            if (words.Length == 1)
                return Name[..Math.Min(3, Name.Length)].ToUpper();
            return initials;
        }
    }
}

/// <summary>
/// In-memory store for all map entities. Ported from EntityManager in entities.py.
/// </summary>
public class MapEntityManager
{
    private readonly Dictionary<string, MapEntity> _entities = [];

    public void Add(MapEntity e)    => _entities[e.Id] = e;

    public void Remove(string id)
    {
        if (!_entities.ContainsKey(id)) return;
        foreach (var g in _entities.Values.Where(x => x.IsGroup))
            g.Members.Remove(id);
        _entities.Remove(id);
    }

    public MapEntity? Get(string id) =>
        _entities.TryGetValue(id, out var e) ? e : null;

    /// <summary>Moves an entity and all its group members to a new node.</summary>
    public void Move(string id, int node)
    {
        if (!_entities.TryGetValue(id, out var e)) return;
        e.Node = node;
        if (!e.IsGroup) return;
        foreach (var mid in e.Members)
            if (_entities.TryGetValue(mid, out var m))
                m.Node = node;
    }

    public bool IsGroupMember(string id) =>
        _entities.Values.Any(e => e.IsGroup && e.Members.Contains(id));

    public MapEntity? GroupOf(string id) =>
        _entities.Values.FirstOrDefault(e => e.IsGroup && e.Members.Contains(id));

    public void AddToGroup(string groupId, string memberId)
    {
        if (_entities.TryGetValue(groupId, out var g) && g.IsGroup
                && !g.Members.Contains(memberId))
            g.Members.Add(memberId);
    }

    public void RemoveFromGroup(string groupId, string memberId)
    {
        if (_entities.TryGetValue(groupId, out var g) && g.IsGroup)
            g.Members.Remove(memberId);
    }

    /// <summary>
    /// Folds all members of <paramref name="otherId"/> (or otherId itself)
    /// into <paramref name="groupId"/>.
    /// </summary>
    public void MergeIntoGroup(string groupId, string otherId)
    {
        if (!_entities.TryGetValue(groupId, out var g) || !g.IsGroup) return;
        if (!_entities.TryGetValue(otherId, out var o))               return;

        if (o.IsGroup)
        {
            foreach (var mid in o.Members.Where(m => !g.Members.Contains(m)))
                g.Members.Add(mid);
            o.Members.Clear();
        }
        else if (!g.Members.Contains(otherId))
        {
            g.Members.Add(otherId);
        }
    }

    public void DisbandGroup(string groupId)
    {
        if (_entities.TryGetValue(groupId, out var g) && g.IsGroup)
        {
            g.Members.Clear();
            g.IsGroup = false;
        }
    }

    /// <summary>All entities including group members.</summary>
    public IReadOnlyList<MapEntity> All => [.. _entities.Values];

    /// <summary>Top-level entities (not inside any group).</summary>
    public IReadOnlyList<MapEntity> TopLevel
    {
        get
        {
            var memberIds = _entities.Values
                .Where(e => e.IsGroup)
                .SelectMany(e => e.Members)
                .ToHashSet();
            return [.. _entities.Values.Where(e => !memberIds.Contains(e.Id))];
        }
    }

    /// <summary>Top-level entities at a specific node.</summary>
    public IReadOnlyList<MapEntity> AtNode(int node) =>
        [.. TopLevel.Where(e => e.Node == node)];

    /// <summary>
    /// Creates a new group entity containing all entities in <paramref name="memberIds"/>
    /// and adds it to the manager. Returns the new group.
    /// </summary>
    public MapEntity CreateGroupFromEntities(
        IEnumerable<string> memberIds, string groupName, string color)
    {
        var group = new MapEntity
        {
            Name    = groupName,
            Color   = color,
            IsGroup = true,
            Members = [.. memberIds],
        };
        // Place group at the first member's node
        var firstId = group.Members.FirstOrDefault();
        if (firstId != null && _entities.TryGetValue(firstId, out var first))
            group.Node = first.Node;

        _entities[group.Id] = group;
        return group;
    }

    /// <summary>Export all entities for JSON serialization.</summary>
    public List<MapEntity> ToList() => [.. _entities.Values];

    /// <summary>Rebuild state from a deserialized list.</summary>
    public void FromList(IEnumerable<MapEntity> entities)
    {
        _entities.Clear();
        foreach (var e in entities)
            _entities[e.Id] = e;
    }
}
