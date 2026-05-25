using System.IO;
using System.Text.Json;

namespace CharacterSheet.Data;

/// <summary>
/// App-wide store for user-created custom skills, equipment, core abilities,
/// and flaws.  All lists are persisted to AppData between sessions and shared
/// across all characters.
/// </summary>
public static class CustomEntryStore
{
    // ── Paths ─────────────────────────────────────────────────────────────

    private static readonly string SaveDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ToA_CnS_CharacterSheet");

    private static readonly string SkillsFile        = Path.Combine(SaveDir, "custom_skills.json");
    private static readonly string EquipmentFile     = Path.Combine(SaveDir, "custom_equipment.json");
    private static readonly string CoreAbilitiesFile = Path.Combine(SaveDir, "custom_core_abilities.json");
    private static readonly string FlawsFile         = Path.Combine(SaveDir, "custom_flaws.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // ── In-memory collections ─────────────────────────────────────────────

    private static readonly List<SkillEntry> _customSkills        = [];
    private static readonly List<SkillEntry> _customEquipment     = [];
    private static readonly List<SkillEntry> _customCoreAbilities = [];
    private static readonly List<SkillEntry> _customFlaws         = [];

    /// <summary>All skills: built-in + custom, alphabetical.</summary>
    public static IReadOnlyList<SkillEntry> AllSkills        { get; private set; } = SkillList.All;

    /// <summary>Custom equipment entries.</summary>
    public static IReadOnlyList<SkillEntry> AllEquipment     => _customEquipment;

    /// <summary>All core abilities: built-in + custom, alphabetical.</summary>
    public static IReadOnlyList<SkillEntry> AllCoreAbilities { get; private set; } = CoreAbilityList.All;

    /// <summary>All flaws: built-in + custom, alphabetical.</summary>
    public static IReadOnlyList<SkillEntry> AllFlaws         { get; private set; } = FlawList.All;

    // ── Initialiser ───────────────────────────────────────────────────────

    static CustomEntryStore()
    {
        Load();
        RebuildAllSkills();
        RebuildAllCoreAbilities();
        RebuildAllFlaws();
    }

    // ── Public API ────────────────────────────────────────────────────────

    public static void AddSkill(string name, string description)
    {
        if (_customSkills.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
        _customSkills.Add(new SkillEntry(name, description));
        RebuildAllSkills();
        SaveList(SkillsFile, _customSkills);
    }

    public static void AddEquipment(string name, string description)
    {
        if (_customEquipment.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
        _customEquipment.Add(new SkillEntry(name, description));
        SaveList(EquipmentFile, _customEquipment);
    }

    public static void AddCoreAbility(string name, string description)
    {
        if (_customCoreAbilities.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
        _customCoreAbilities.Add(new SkillEntry(name, description));
        RebuildAllCoreAbilities();
        SaveList(CoreAbilitiesFile, _customCoreAbilities);
    }

    public static void AddFlaw(string name, string description)
    {
        if (_customFlaws.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
        _customFlaws.Add(new SkillEntry(name, description));
        RebuildAllFlaws();
        SaveList(FlawsFile, _customFlaws);
    }

    // ── Description helpers ───────────────────────────────────────────────

    public static string? GetSkillDescription(string? name) =>
        name == null ? null :
        AllSkills.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Description;

    public static string? GetEquipmentDescription(string? name) =>
        name == null ? null :
        _customEquipment.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Description;

    // ── Private helpers ───────────────────────────────────────────────────

    private static void RebuildAllSkills() =>
        AllSkills = Merge(SkillList.All, _customSkills);

    private static void RebuildAllCoreAbilities() =>
        AllCoreAbilities = Merge(CoreAbilityList.All, _customCoreAbilities);

    private static void RebuildAllFlaws() =>
        AllFlaws = Merge(FlawList.All, _customFlaws);

    private static IReadOnlyList<SkillEntry> Merge(
        IReadOnlyList<SkillEntry> builtIn, List<SkillEntry> custom) =>
        builtIn.Concat(custom)
               .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
               .ToList().AsReadOnly();

    private static void Load()
    {
        try
        {
            TryLoad(SkillsFile,        _customSkills);
            TryLoad(EquipmentFile,     _customEquipment);
            TryLoad(CoreAbilitiesFile, _customCoreAbilities);
            TryLoad(FlawsFile,         _customFlaws);
        }
        catch { /* bad file → start empty */ }
    }

    private static void TryLoad(string path, List<SkillEntry> target)
    {
        if (!File.Exists(path)) return;
        foreach (var e in Deserialize(path))
            target.Add(new SkillEntry(e.Name, e.Description));
    }

    private static void SaveList(string path, List<SkillEntry> list)
    {
        Directory.CreateDirectory(SaveDir);
        File.WriteAllText(path,
            JsonSerializer.Serialize(
                list.Select(s => new StoredEntry(s.Name, s.Description)), JsonOpts));
    }

    private static List<StoredEntry> Deserialize(string path)
        => JsonSerializer.Deserialize<List<StoredEntry>>(File.ReadAllText(path), JsonOpts) ?? [];

    private record StoredEntry(string Name, string Description);
}
