using System.IO;
using System.Text.Json;

namespace CharacterSheet.Models;

// ── New-format DTOs ───────────────────────────────────────────────────────────

public record EquipDto(string EquipName, string EquipSub, bool EquipUsed, int ArmorValue = 0);

public record InventoryItemDto(string Name, string Description = "");
public record InventoryCategoryDto(string Name, List<InventoryItemDto> Items);

public record SkillDto(
    string SkillName, string SkillSub, bool SkillAdv, int SkillRating,
    // Added for core-ability skills — defaults keep old saves valid
    int  MinRating          = 3,
    int  FreeRating         = 0,
    int  RatingStep         = 1,
    bool IsAlwaysFree       = false,
    bool IsLocked           = false,
    bool IsCoreAbilitySkill = false);

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
    List<string>     Spells,
    // Added later — default values keep old saves valid
    int  Defense          = 6,    // kept as "Defense" in JSON for backward compatibility
    bool Hit1             = false,
    bool Hit2             = false,
    bool Hit3             = false,
    bool Hit4             = false,
    bool Hit5             = false,
    int  HeroPointsMax     = 50,
    int  HeroPointsCurrent = 50,
    List<InventoryCategoryDto>? Inventory    = null,
    List<string>?               SectionOrder = null);

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
        Equipment: s.Equipment.Select(e  => new EquipDto(e.EquipName, e.EquipSub, e.EquipUsed, e.ArmorValue)).ToList(),
        Skills:    s.Skills
                       .Where(sk => !sk.IsConditionalBonus)   // conditional terrain bonuses are never persisted
                       .Select(sk => new SkillDto(
                           sk.SkillName, sk.SkillSub, sk.SkillAdv, sk.SkillRating,
                           sk.MinRating, sk.FreeRating, sk.RatingStep,
                           sk.IsAlwaysFree, sk.IsLocked, sk.IsCoreAbilitySkill)).ToList(),
        Rows:      null,   // legacy field — intentionally null in new saves
        Spells:    s.Spells.Select(sp => sp.Name).ToList(),
        Defense:           s.DefenseBase,
        Hit1: s.Hit1, Hit2: s.Hit2, Hit3: s.Hit3, Hit4: s.Hit4, Hit5: s.Hit5,
        HeroPointsMax:     s.HeroPointsMax,
        HeroPointsCurrent: s.HeroPointsCurrent,
        Inventory: s.Inventory.Select(c => new InventoryCategoryDto(
            c.Name,
            c.Items.Select(i => new InventoryItemDto(i.Name, i.Description)).ToList()
        )).ToList(),
        SectionOrder: s.SectionOrder.Count > 0 ? [.. s.SectionOrder] : null);

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
                    EquipName  = e.EquipName ?? "",
                    EquipSub   = e.EquipSub  ?? "",
                    EquipUsed  = e.EquipUsed,
                    ArmorValue = e.ArmorValue,
                });

            foreach (var sk in d.Skills ?? [])
            {
                // MinRating must be set before SkillName so the auto-promote clamps correctly.
                // SkillName must be set before SkillRating so HasSkill is correct.
                s.Skills.Add(new SkillData
                {
                    MinRating          = sk.MinRating,
                    FreeRating         = sk.FreeRating,
                    RatingStep         = sk.RatingStep,
                    IsAlwaysFree       = sk.IsAlwaysFree,
                    IsLocked           = sk.IsLocked,
                    IsCoreAbilitySkill = sk.IsCoreAbilitySkill,
                    SkillName          = sk.SkillName   ?? "",
                    SkillSub           = sk.SkillSub    ?? "",
                    SkillAdv           = sk.SkillAdv,
                    SkillRating        = sk.SkillRating,
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
                        EquipName  = r.EquipName ?? "",
                        EquipSub   = r.EquipSub  ?? "",
                        EquipUsed  = r.EquipUsed,
                        ArmorValue = 0,   // legacy rows have no armor
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

        foreach (var sp in d.Spells ?? [])
            if (!string.IsNullOrWhiteSpace(sp))
                s.Spells.Add(new SpellData { Name = sp });

        s.SectionOrder = d.SectionOrder?.ToList() ?? [];

        foreach (var cat in d.Inventory ?? [])
        {
            var category = new InventoryCategory { Name = cat.Name ?? "" };
            foreach (var itm in cat.Items ?? [])
                category.Items.Add(new InventoryItem { Name = itm.Name ?? "", Description = itm.Description ?? "" });
            s.Inventory.Add(category);
        }

        s.DefenseBase       = d.Defense;   // DTO field stays "Defense" for backward compat; defaults to 6
        s.Hit1             = d.Hit1;
        s.Hit2             = d.Hit2;
        s.Hit3             = d.Hit3;
        s.Hit4             = d.Hit4;
        s.Hit5             = d.Hit5;
        s.HeroPointsMax     = d.HeroPointsMax;      // defaults to 50 for old saves
        s.HeroPointsCurrent = d.HeroPointsCurrent;  // defaults to 50 for old saves

        return s;
    }
}
