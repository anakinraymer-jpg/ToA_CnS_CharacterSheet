using System.IO;
using System.Text.Json;

namespace CharacterSheet.Models;

// ── New-format DTOs ───────────────────────────────────────────────────────────

public record EquipDto(string EquipName, string EquipSub, bool EquipUsed);

public record SkillDto(string SkillName, string SkillSub, bool SkillAdv, int SkillRating);

// ── Legacy DTO (kept for migrating saves written before the split) ────────────

public record RowDto(
    string EquipName, string EquipSub, bool EquipUsed,
    bool SkillAdv,
    string SkillName, string SkillSub,
    int SkillRating);

// ── Character DTO ─────────────────────────────────────────────────────────────

public record CharacterDto(
    string Name, string Lineage, string Hometown,
    string Flaw1, string Flaw2, string Flaw3, string Flaw4,
    string CoreAbility,
    string Summary,
    string Portrait,
    List<EquipDto>?  Equipment,   // new format
    List<SkillDto>?  Skills,      // new format
    List<RowDto>?    Rows,        // legacy — null in new saves, populated in old saves
    List<string>     Spells);

// ── Persistence ───────────────────────────────────────────────────────────────

public static class Persistence
{
    private static readonly string SaveDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ToA_CnS_CharacterSheet");
    private static readonly string SaveFile = Path.Combine(SaveDir, "character.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        UnknownTypeHandling = System.Text.Json.Serialization.JsonUnknownTypeHandling.JsonElement,
    };

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

    // ── Serialise ─────────────────────────────────────────────────────────────

    private static CharacterDto ToDto(CharacterState s) => new(
        s.Name, s.Lineage, s.Hometown,
        s.Flaw1, s.Flaw2, s.Flaw3, s.Flaw4,
        s.CoreAbility,
        s.Summary,
        s.Portrait,
        Equipment: s.Equipment.Select(e  => new EquipDto(e.EquipName, e.EquipSub, e.EquipUsed)).ToList(),
        Skills:    s.Skills.Select(sk => new SkillDto(sk.SkillName, sk.SkillSub, sk.SkillAdv, sk.SkillRating)).ToList(),
        Rows:      null,   // legacy field — intentionally null in new saves
        Spells:    [.. s.Spells]);

    // ── Deserialise ───────────────────────────────────────────────────────────

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

        if (d.Equipment != null)
        {
            // ── New format ────────────────────────────────────────────────────
            foreach (var e in d.Equipment)
                s.Equipment.Add(new EquipData
                {
                    EquipName = e.EquipName ?? "",
                    EquipSub  = e.EquipSub  ?? "",
                    EquipUsed = e.EquipUsed,
                });

            foreach (var sk in d.Skills ?? [])
            {
                // SkillName must be set before SkillRating so HasSkill is correct
                // when the SkillRating setter clamps the value.
                s.Skills.Add(new SkillData
                {
                    SkillName   = sk.SkillName ?? "",
                    SkillSub    = sk.SkillSub  ?? "",
                    SkillAdv    = sk.SkillAdv,
                    SkillRating = sk.SkillRating,
                });
            }
        }
        else
        {
            // ── Legacy format: split combined Rows into separate Equipment/Skills ──
            foreach (var r in d.Rows ?? [])
            {
                if (!string.IsNullOrWhiteSpace(r.EquipName))
                    s.Equipment.Add(new EquipData
                    {
                        EquipName = r.EquipName ?? "",
                        EquipSub  = r.EquipSub  ?? "",
                        EquipUsed = r.EquipUsed,
                    });

                if (!string.IsNullOrWhiteSpace(r.SkillName))
                    s.Skills.Add(new SkillData
                    {
                        SkillName   = r.SkillName ?? "",
                        SkillSub    = r.SkillSub  ?? "",
                        SkillAdv    = r.SkillAdv,
                        SkillRating = r.SkillRating,
                    });
            }
        }

        var sp = d.Spells ?? [];
        for (int i = 0; i < 3; i++) s.Spells.Add(i < sp.Count ? sp[i] : "");
        return s;
    }
}
