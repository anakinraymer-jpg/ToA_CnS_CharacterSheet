using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharacterSheet.Models;

public record RowDto(
    string EquipName, string EquipSub, bool EquipUsed,
    bool SkillAdv,
    string SkillName, string SkillSub,
    string Die);

public record CharacterDto(
    string Name, string Lineage, string Hometown,
    string Flaw1, string Flaw2,
    string Extra1, string Extra2,
    string Summa1, string Summa2,
    string CoreAbility,
    string Portrait,
    List<RowDto> Rows,
    List<string> Spells);

public static class Persistence
{
    private static readonly string SaveDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ToA_CnS_CharacterSheet");
    private static readonly string SaveFile = Path.Combine(SaveDir, "character.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

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
        s.Flaw1, s.Flaw2,
        s.Extra1, s.Extra2,
        s.Summa1, s.Summa2,
        s.CoreAbility,
        s.Portrait,
        s.Rows.Select(r => new RowDto(
            r.EquipName, r.EquipSub, r.EquipUsed,
            r.SkillAdv,
            r.SkillName, r.SkillSub, r.Die)).ToList(),
        [.. s.Spells]);

    private static CharacterState FromDto(CharacterDto d)
    {
        var s = new CharacterState
        {
            Name = d.Name ?? "", Lineage = d.Lineage ?? "", Hometown = d.Hometown ?? "",
            Flaw1 = d.Flaw1 ?? "", Flaw2 = d.Flaw2 ?? "",
            Extra1 = d.Extra1 ?? "", Extra2 = d.Extra2 ?? "",
            Summa1 = d.Summa1 ?? "", Summa2 = d.Summa2 ?? "",
            CoreAbility = d.CoreAbility ?? "",
            Portrait = d.Portrait ?? "",
        };
        var rows = d.Rows ?? [];
        for (int i = 0; i < 10; i++)
        {
            if (i < rows.Count)
            {
                var r = rows[i];
                s.Rows.Add(new RowData { EquipName=r.EquipName, EquipSub=r.EquipSub,
                    EquipUsed=r.EquipUsed, SkillAdv=r.SkillAdv,
                    SkillName=r.SkillName, SkillSub=r.SkillSub, Die=r.Die });
            }
            else s.Rows.Add(new RowData());
        }
        var sp = d.Spells ?? [];
        for (int i = 0; i < 3; i++) s.Spells.Add(i < sp.Count ? sp[i] : "");
        return s;
    }
}
