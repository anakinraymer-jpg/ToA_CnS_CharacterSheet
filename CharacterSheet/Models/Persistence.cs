using System.IO;
using System.Text.Json;

namespace CharacterSheet.Models;

public record RowDto(
    string EquipName, string EquipSub, bool EquipUsed,
    bool SkillAdv,
    string SkillName, string SkillSub,
    int SkillRating);   // was string Die — old saves will read 0 (clamped to 3 if skill exists)

public record CharacterDto(
    string Name, string Lineage, string Hometown,
    string Flaw1, string Flaw2, string Flaw3, string Flaw4,
    string CoreAbility,
    string Summary,
    string Portrait,
    List<RowDto> Rows,
    List<string> Spells);

public static class Persistence
{
    private static readonly string SaveDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ToA_CnS_CharacterSheet");
    private static readonly string SaveFile = Path.Combine(SaveDir, "character.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        // Unknown fields in old saves are silently ignored
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

    private static CharacterDto ToDto(CharacterState s) => new(
        s.Name, s.Lineage, s.Hometown,
        s.Flaw1, s.Flaw2, s.Flaw3, s.Flaw4,
        s.CoreAbility,
        s.Summary,
        s.Portrait,
        s.Rows.Select(r => new RowDto(
            r.EquipName, r.EquipSub, r.EquipUsed,
            r.SkillAdv,
            r.SkillName, r.SkillSub, r.SkillRating)).ToList(),
        [.. s.Spells]);

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
        var rows = d.Rows ?? [];
        for (int i = 0; i < 10; i++)
        {
            if (i < rows.Count)
            {
                var r = rows[i];
                // SkillName MUST be assigned before SkillRating so HasSkill is correct
                // when the SkillRating setter clamps the value.
                s.Rows.Add(new RowData {
                    EquipName   = r.EquipName,   EquipSub  = r.EquipSub,
                    EquipUsed   = r.EquipUsed,   SkillAdv  = r.SkillAdv,
                    SkillName   = r.SkillName,   SkillSub  = r.SkillSub,
                    SkillRating = r.SkillRating });
            }
            else s.Rows.Add(new RowData());
        }
        var sp = d.Spells ?? [];
        for (int i = 0; i < 3; i++) s.Spells.Add(i < sp.Count ? sp[i] : "");
        return s;
    }
}
