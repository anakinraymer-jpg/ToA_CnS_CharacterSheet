using System.IO;
using System.Text.Json;

namespace CharacterSheet.Data;

/// <summary>
/// App-wide store for user-created custom skills and equipment entries.
/// Persisted to AppData between sessions, shared across all characters.
/// </summary>
public static class CustomEntryStore
{
    // ── Paths ─────────────────────────────────────────────────────────────

    private static readonly string SaveDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ToA_CnS_CharacterSheet");

    private static readonly string SkillsFile    = Path.Combine(SaveDir, "custom_skills.json");
    private static readonly string EquipmentFile = Path.Combine(SaveDir, "custom_equipment.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // ── In-memory collections ─────────────────────────────────────────────

    private static readonly List<SkillEntry> _customSkills    = [];
    private static readonly List<SkillEntry> _customEquipment = [];

    /// <summary>All skills available for the picker: built-in + custom, alphabetical.</summary>
    public static IReadOnlyList<SkillEntry> AllSkills { get; private set; } = SkillList.All;

    /// <summary>Custom equipment entries available for the equipment picker.</summary>
    public static IReadOnlyList<SkillEntry> AllEquipment => _customEquipment;

    // ── Initialiser ───────────────────────────────────────────────────────

    static CustomEntryStore()
    {
        Load();
        RebuildAllSkills();
    }

    // ── Public API ────────────────────────────────────────────────────────

    public static void AddSkill(string name, string description)
    {
        if (_customSkills.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;                                       // no duplicates
        _customSkills.Add(new SkillEntry(name, description));
        RebuildAllSkills();
        SaveSkills();
    }

    public static void AddEquipment(string name, string description)
    {
        if (_customEquipment.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;
        _customEquipment.Add(new SkillEntry(name, description));
        SaveEquipment();
    }

    public static string? GetSkillDescription(string? name) =>
        name == null ? null :
        AllSkills.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Description;

    public static string? GetEquipmentDescription(string? name) =>
        name == null ? null :
        _customEquipment.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Description;

    // ── Private helpers ───────────────────────────────────────────────────

    private static void RebuildAllSkills()
    {
        AllSkills = SkillList.All
            .Concat(_customSkills)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(SkillsFile))
                foreach (var e in Deserialize(SkillsFile))
                    _customSkills.Add(new SkillEntry(e.Name, e.Description));

            if (File.Exists(EquipmentFile))
                foreach (var e in Deserialize(EquipmentFile))
                    _customEquipment.Add(new SkillEntry(e.Name, e.Description));
        }
        catch { /* bad file → start empty */ }
    }

    private static void SaveSkills()
    {
        Directory.CreateDirectory(SaveDir);
        File.WriteAllText(SkillsFile,
            JsonSerializer.Serialize(_customSkills.Select(s => new StoredEntry(s.Name, s.Description)), JsonOpts));
    }

    private static void SaveEquipment()
    {
        Directory.CreateDirectory(SaveDir);
        File.WriteAllText(EquipmentFile,
            JsonSerializer.Serialize(_customEquipment.Select(e => new StoredEntry(e.Name, e.Description)), JsonOpts));
    }

    private static List<StoredEntry> Deserialize(string path)
        => JsonSerializer.Deserialize<List<StoredEntry>>(File.ReadAllText(path), JsonOpts) ?? [];

    private record StoredEntry(string Name, string Description);
}
