using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharacterSheet.Models;

// Flat DTO for serialisation (ObservableCollection doesn't serialise cleanly without extra work)
public record RowDto(
    string EquipName, string EquipSub, bool EquipUsed,
    bool SkillAdv,
    string SkillName, string SkillSub,
    string Die);

public record CharacterDto(
    string Name, string Lineage, string Hometown, string Flaws, string Summa,
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
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static void Save(CharacterState state)
    {
        Directory.CreateDirectory(SaveDir);
        var dto = ToDto(state);
        File.WriteAllText(SaveFile, JsonSerializer.Serialize(dto, Opts));
    }

    public static CharacterState Load()
    {
        if (!File.Exists(SaveFile)) return CharacterState.CreateDefault();
        try
        {
            var json = File.ReadAllText(SaveFile);
            var dto  = JsonSerializer.Deserialize<CharacterDto>(json, Opts);
            if (dto == null) return CharacterState.CreateDefault();
            return FromDto(dto);
        }
        catch
        {
            return CharacterState.CreateDefault();
        }
    }

    public static CharacterState LoadFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<CharacterDto>(json, Opts);
        if (dto == null) return CharacterState.CreateDefault();
        return FromDto(dto);
    }

    public static string ExportJson(CharacterState state)
        => JsonSerializer.Serialize(ToDto(state), Opts);

    private static CharacterDto ToDto(CharacterState s) => new(
        s.Name, s.Lineage, s.Hometown, s.Flaws, s.Summa,
        s.Portrait,
        s.Rows.Select(r => new RowDto(
            r.EquipName, r.EquipSub, r.EquipUsed,
            r.SkillAdv,
            r.SkillName, r.SkillSub,
            r.Die)).ToList(),
        [.. s.Spells]);

    private static CharacterState FromDto(CharacterDto dto)
    {
        var s = new CharacterState
        {
            Name = dto.Name, Lineage = dto.Lineage, Hometown = dto.Hometown,
            Flaws = dto.Flaws, Summa = dto.Summa, Portrait = dto.Portrait,
        };
        var rows = dto.Rows ?? [];
        for (int i = 0; i < 10; i++)
        {
            if (i < rows.Count)
            {
                var r = rows[i];
                s.Rows.Add(new RowData
                {
                    EquipName = r.EquipName, EquipSub = r.EquipSub,
                    EquipUsed = r.EquipUsed, SkillAdv  = r.SkillAdv,
                    SkillName = r.SkillName, SkillSub  = r.SkillSub,
                    Die       = r.Die,
                });
            }
            else s.Rows.Add(new RowData());
        }
        var spells = dto.Spells ?? [];
        for (int i = 0; i < 3; i++)
            s.Spells.Add(i < spells.Count ? spells[i] : "");
        return s;
    }
}
