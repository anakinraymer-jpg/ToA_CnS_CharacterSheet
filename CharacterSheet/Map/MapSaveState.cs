using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharacterSheet.Map;

/// <summary>
/// JSON-serializable snapshot of a complete map session.
/// Mirrors the save format used by the Python app (main_window.py _save_state).
/// </summary>
public class MapSaveState
{
    [JsonPropertyName("day")]
    public int Day { get; set; } = 1;

    [JsonPropertyName("grid")]
    public GridState Grid { get; set; } = new();

    [JsonPropertyName("entities")]
    public List<EntityState> Entities { get; set; } = [];

    [JsonPropertyName("locations")]
    public List<LocationState> Locations { get; set; } = [];

    /// <summary>Maps node numbers (as string keys) to terrain type names.</summary>
    [JsonPropertyName("terrain")]
    public Dictionary<string, string> Terrain { get; set; } = [];

    [JsonPropertyName("fog_revealed")]
    public List<int> FogRevealed { get; set; } = [];

    /// <summary>Maps node numbers (as string keys) to curse levels.</summary>
    [JsonPropertyName("curses")]
    public Dictionary<string, string> Curses { get; set; } = [];

    [JsonPropertyName("weather")]
    public string Weather { get; set; } = "";

    // ── Sub-DTOs ───────────────────────────────────────────────────────

    public class GridState
    {
        [JsonPropertyName("size")]        public double Size        { get; set; } = 87.0;
        [JsonPropertyName("origin")]      public double[] Origin    { get; set; } = [150.0, 138.0];
        [JsonPropertyName("orientation")] public string  Orientation { get; set; } = "flat";
        [JsonPropertyName("cols")]        public int     Cols        { get; set; } = 33;
        [JsonPropertyName("rows")]        public int     Rows        { get; set; } = 39;

        /// <summary>
        /// Null when no warp is active; otherwise [[tl_x,tl_y],[tr_x,tr_y],[bl_x,bl_y],[br_x,br_y]].
        /// </summary>
        [JsonPropertyName("warp_corners")]
        public double[][]? WarpCorners { get; set; }
    }

    public class EntityState
    {
        [JsonPropertyName("id")]          public string       Id         { get; set; } = "";
        [JsonPropertyName("name")]        public string       Name       { get; set; } = "";
        [JsonPropertyName("node")]        public int          Node       { get; set; }
        [JsonPropertyName("color")]       public string       Color      { get; set; } = "#e74c3c";
        [JsonPropertyName("is_group")]    public bool         IsGroup    { get; set; }
        [JsonPropertyName("members")]     public List<string> Members    { get; set; } = [];
        [JsonPropertyName("seafaring")]   public bool         Seafaring  { get; set; }
        [JsonPropertyName("flavor_text")] public string       FlavorText { get; set; } = "";
        [JsonPropertyName("is_player")]   public bool         IsPlayer   { get; set; }
        [JsonPropertyName("is_bot")]      public bool         IsBot      { get; set; }
        [JsonPropertyName("bot_target")]  public int?         BotTarget  { get; set; }
    }

    public class LocationState
    {
        [JsonPropertyName("id")]            public string Id           { get; set; } = "";
        [JsonPropertyName("name")]          public string Name         { get; set; } = "";
        [JsonPropertyName("node")]          public int    Node         { get; set; }
        [JsonPropertyName("color")]         public string Color        { get; set; } = "#f39c12";
        [JsonPropertyName("description")]   public string Description  { get; set; } = "";
        [JsonPropertyName("location_type")] public string LocationType { get; set; } = "";
    }

    // ── Serialization options ──────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented           = true,
        DefaultIgnoreCondition  = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Serialize() => JsonSerializer.Serialize(this, JsonOpts);

    public static MapSaveState? Deserialize(string json) =>
        JsonSerializer.Deserialize<MapSaveState>(json, JsonOpts);

    // ── Conversion helpers ─────────────────────────────────────────────

    public static MapSaveState From(
        HexGrid grid,
        MapEntityManager em,
        MapLocationManager lm,
        MapTerrainMap tm,
        MapCurseMap cm,
        IEnumerable<int> fogRevealed,
        int day,
        string weather)
    {
        var corners = grid.IsWarped ? grid.GetWarpCorners() : null;

        return new MapSaveState
        {
            Day     = day,
            Weather = weather,
            Grid = new GridState
            {
                Size        = grid.Size,
                Origin      = [grid.Origin.X, grid.Origin.Y],
                Orientation = grid.Orientation,
                Cols        = grid.Cols,
                Rows        = grid.Rows,
                WarpCorners = corners is null ? null :
                [
                    [corners.TL.X, corners.TL.Y],
                    [corners.TR.X, corners.TR.Y],
                    [corners.BL.X, corners.BL.Y],
                    [corners.BR.X, corners.BR.Y],
                ],
            },
            Entities = [.. em.All.Select(e => new EntityState
            {
                Id         = e.Id,
                Name       = e.Name,
                Node       = e.Node,
                Color      = e.Color,
                IsGroup    = e.IsGroup,
                Members    = [.. e.Members],
                Seafaring  = e.Seafaring,
                FlavorText = e.FlavorText,
                IsPlayer   = e.IsPlayer,
                IsBot      = e.IsBot,
                BotTarget  = e.BotTarget,
            })],
            Locations = [.. lm.All.Select(l => new LocationState
            {
                Id           = l.Id,
                Name         = l.Name,
                Node         = l.Node,
                Color        = l.Color,
                Description  = l.Description,
                LocationType = l.LocationType,
            })],
            Terrain     = tm.ToDict(),
            Curses      = cm.ToDict(),
            FogRevealed = [.. fogRevealed.OrderBy(n => n)],
        };
    }

    public (HashSet<int> FogRevealed, int Day, string Weather) Apply(
        HexGrid grid,
        MapEntityManager em,
        MapLocationManager lm,
        MapTerrainMap tm,
        MapCurseMap cm,
        string playerName = "")
    {
        // Grid
        grid.Reconfigure(
            size:        Grid.Size,
            origin:      (Grid.Origin[0], Grid.Origin[1]),
            orientation: Grid.Orientation,
            cols:        Grid.Cols,
            rows:        Grid.Rows);

        if (Grid.WarpCorners is { Length: 4 } wc)
            grid.SetWarpCorners(new WarpCorners(
                (wc[0][0], wc[0][1]),
                (wc[1][0], wc[1][1]),
                (wc[2][0], wc[2][1]),
                (wc[3][0], wc[3][1])));

        // Entities
        foreach (var e in em.All.ToList()) em.Remove(e.Id);
        foreach (var es in Entities)
        {
            bool isPlayer = !string.IsNullOrEmpty(playerName)
                && string.Equals(es.Name.Trim(), playerName.Trim(),
                                 StringComparison.OrdinalIgnoreCase);
            em.Add(new MapEntity
            {
                Id         = es.Id,
                Name       = es.Name,
                Node       = es.Node,
                Color      = es.Color,
                IsGroup    = es.IsGroup,
                Members    = [.. es.Members],
                Seafaring  = es.Seafaring,
                FlavorText = es.FlavorText,
                IsPlayer   = isPlayer,
                IsBot      = es.IsBot,
                BotTarget  = es.BotTarget,
            });
        }

        // Locations
        foreach (var l in lm.All.ToList()) lm.Remove(l.Id);
        foreach (var ls in Locations)
            lm.Add(new MapLocation
            {
                Id           = ls.Id,
                Name         = ls.Name,
                Node         = ls.Node,
                Color        = ls.Color,
                Description  = ls.Description,
                LocationType = ls.LocationType,
            });

        // Terrain
        tm.FromDict(Terrain);

        // Curses
        cm.FromDict(Curses);

        // Fog
        var fog = FogRevealed.ToHashSet();

        return (fog, Day, Weather);
    }
}
