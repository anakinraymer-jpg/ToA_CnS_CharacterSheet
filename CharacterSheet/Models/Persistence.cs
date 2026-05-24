using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharacterSheet.Models;

// ── Current DTOs ──────────────────────────────────────────────────────────────

public record EquipmentItemDto(string Name, string Description, bool Used);

public record SkillItemDto(string Name, string Description, bool Adv, int Rating);

// ── Legacy DTO (kept for migration of saves made before the split) ────────────

public record LegacyRowDto(
    string EquipName, string EquipSub, bool EquipUsed,
    bool SkillAdv,
    string SkillName, string SkillSub,
    int SkillRating);

// ── Top-level save DTO ────────────────────────────────────────────────────────

/// <summary>
/// Flexible DTO that handles both the current (Equipment/Skills) and the
/// legacy (Rows) save formats.  Unknown fields are silently ignored.
/// </summary>
public class CharacterDto
{
    public string Name        { get; init; } = "";
    public string Lineage     { get; init; } = "";
    public string Hometown    { get; init; } = "";
    public string Flaw1       { get; init; } = "";
    public string Flaw2       { get; init; } = "";
    public string Flaw3       { get; init; } = "";
    public string Flaw4       { get; init; } = "";
    public string CoreAbility { get; init; } = "";
    public string Summary     { get; init; } = "";
    public string Portrait    { get; init; } = "";

    // Current format
    public List<EquipmentItemDto>? Equipment { get; init; }
    public List<SkillItemDto>?     Skills    { get; init; }

    // Legacy format (Rows) — present in saves made before the equipment/skill split
    [JsonPropertyName("Rows")]
    public List<LegacyRowDto>? LegacyRows { get; init; }

    public List<string>? Spells { get; init; }
}

// ── Persistence service ───────────────────────────────────────────────────────

public static class Persistence
{
    private static readonly string SaveDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ToA_CnS_CharacterSheet");
    private static readonly string SaveFile = Path.Combine(SaveDir, "character.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = null,   // PascalCase matches DTO property names
    };

    // ── Public API ────────────────────────────────────────────────────────────

    public static void Save(CharacterState state)
    {
        Directory.CreateDirectory(SaveDir);
        File.WriteAllText(SaveFile, JsonSerializer.Serialize(ToDto(state), Opts));
    }

    public static CharacterState Load()
    {
        if (!File.Exists(SaveFile)) return CharacterState.CreateDefault();
        try { return FromDto(JsonSerializer.Deserialize<CharacterDto>(File.ReadAllText(SaveFile), Opts)!); }
        catch { return CharacterState.CreateDefault(); }
    }

    public static CharacterState LoadFromJson(string json)
    {
        try { return FromDto(JsonSerializer.Deserialize<CharacterDto>(json, Opts)!); }
        catch { return CharacterState.CreateDefault(); }
    }

    public static string ExportJson(CharacterState state) =>
        JsonSerializer.Serialize(ToDto(state), Opts);

    // ── Serialisation helpers ─────────────────────────────────────────────────

    private static CharacterDto ToDto(CharacterState s) => new()
    {
        Name        = s.Name,
        Lineage     = s.Lineage,
        Hometown    = s.Hometown,
        Flaw1       = s.Flaw1,
        Flaw2       = s.Flaw2,
        Flaw3       = s.Flaw3,
        Flaw4       = s.Flaw4,
        CoreAbility = s.CoreAbility,
        Summary     = s.Summary,
        Portrait    = s.Portrait,
        Equipment   = s.Equipment.Select(e => new EquipmentItemDto(e.Name, e.Description, e.Used)).ToList(),
        Skills      = s.Skills.Select(sk => new SkillItemDto(sk.Name, sk.Description, sk.Adv, sk.Rating)).ToList(),
        Spells      = [.. s.Spells],
    };

    private static CharacterState FromDto(CharacterDto d)
    {
        var s = new CharacterState
        {
            Name        = d.Name        ?? "",
            Lineage     = d.Lineage     ?? "",
            Hometown    = d.Hometown    ?? "",
            Flaw1       = d.Flaw1       ?? "",
            Flaw2       = d.Flaw2       ?? "",
            Flaw3       = d.Flaw3       ?? "",
            Flaw4       = d.Flaw4       ?? "",
            CoreAbility = d.CoreAbility ?? "",
            Summary     = d.Summary     ?? "",
            Portrait    = d.Portrait    ?? "",
        };

        // ── Equipment / Skills ──────────────────────────────────────────────
        if (d.Equipment != null)
        {
            // Current format
            foreach (var e in d.Equipment)
                s.Equipment.Add(new EquipmentItem { Name = e.Name, Description = e.Description, Used = e.Used });
        }

        if (d.Skills != null)
        {
            foreach (var sk in d.Skills)
            {
                // SkillName MUST be assigned before Rating so HasSkill is true when the
                // Rating setter runs and clamps to [3, 18].
                var item = new SkillItem { Name = sk.Name, Description = sk.Description, Adv = sk.Adv };
                item.Rating = sk.Rating;
                s.Skills.Add(item);
            }
        }

        // ── Legacy migration: old saves used paired "Rows" ──────────────────
        if (d.Equipment == null && d.LegacyRows != null)
        {
            foreach (var r in d.LegacyRows)
            {
                if (!string.IsNullOrWhiteSpace(r.EquipName))
                    s.Equipment.Add(new EquipmentItem
                    {
                        Name        = r.EquipName,
                        Description = r.EquipSub ?? "",
                        Used        = r.EquipUsed,
                    });

                if (!string.IsNullOrWhiteSpace(r.SkillName))
                {
                    var item = new SkillItem
                    {
                        Name        = r.SkillName,
                        Description = r.SkillSub ?? "",
                        Adv         = r.SkillAdv,
                    };
                    item.Rating = r.SkillRating;
                    s.Skills.Add(item);
                }
            }
        }

        // ── Spells ──────────────────────────────────────────────────────────
        var sp = d.Spells ?? [];
        for (int i = 0; i < 3; i++) s.Spells.Add(i < sp.Count ? sp[i] : "");

        return s;
    }
}
