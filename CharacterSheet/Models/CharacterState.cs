using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace CharacterSheet.Models;

// ── Equipment row ─────────────────────────────────────────────────────────────

public class EquipData : INotifyPropertyChanged
{
    private string _equipName  = "";
    private string _equipSub   = "";
    private bool   _equipUsed;
    private int    _armorValue = 0;

    public string EquipName  { get => _equipName;  set { _equipName  = value; OnPropertyChanged(); } }
    public string EquipSub   { get => _equipSub;   set { _equipSub   = value; OnPropertyChanged(); } }
    public bool   EquipUsed  { get => _equipUsed;  set { _equipUsed  = value; OnPropertyChanged(); } }
    /// <summary>Armor class contribution. 0 = no armor. Active only when EquipUsed is false.</summary>
    public int    ArmorValue { get => _armorValue; set { _armorValue = Math.Max(0, value); OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Skill row ─────────────────────────────────────────────────────────────────

public class SkillData : INotifyPropertyChanged
{
    private string _skillName   = "";
    private string _skillSub    = "";
    private bool   _skillAdv;
    private int    _skillRating = 0;   // 0 = no skill; 3–18 = active

    public string SkillName
    {
        get => _skillName;
        set
        {
            bool hadSkill = HasSkill;
            _skillName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSkill));

            // Auto-promote: empty → typed → set rating to 3
            if (!hadSkill && HasSkill && _skillRating == 0)
                SkillRating = 3;
            // Auto-reset: skill cleared → rating back to 0
            else if (hadSkill && !HasSkill)
                SkillRating = 0;
        }
    }

    public string SkillSub { get => _skillSub; set { _skillSub = value; OnPropertyChanged(); } }
    public bool   SkillAdv { get => _skillAdv; set { _skillAdv = value; OnPropertyChanged(); } }

    /// <summary>True when SkillName contains non-whitespace text.</summary>
    public bool HasSkill => !string.IsNullOrWhiteSpace(_skillName);

    /// <summary>
    /// 0 when no skill is assigned; 3–18 when a skill is assigned (clamped).
    /// </summary>
    public int SkillRating
    {
        get => _skillRating;
        set
        {
            _skillRating = HasSkill ? Math.Clamp(value, 3, 18) : 0;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Character ─────────────────────────────────────────────────────────────────

public class CharacterState : INotifyPropertyChanged
{
    private string _name        = "";
    private string _lineage     = "";
    private string _hometown    = "";
    private string _flaw1       = "";
    private string _flaw2       = "";
    private string _flaw3       = "";
    private string _flaw4       = "";
    private string _coreAbility = "";
    private string _summary     = "";
    private string _portrait    = "";
    private int    _defenseBase       = 6;    // manual shield value (6-18)
    private int    _armorBonus        = 0;    // computed by MainWindow from active armor
    private bool   _hit1, _hit2, _hit3, _hit4, _hit5;
    private int    _heroPointsMax     = 50;
    private int    _heroPointsCurrent = 50;

    public string Name        { get => _name;        set { _name        = value; OnPropertyChanged(); } }
    public string Lineage     { get => _lineage;     set { _lineage     = value; OnPropertyChanged(); } }
    public string Hometown    { get => _hometown;    set { _hometown    = value; OnPropertyChanged(); } }
    public string Flaw1       { get => _flaw1;       set { _flaw1       = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedFlaws)); } }
    public string Flaw2       { get => _flaw2;       set { _flaw2       = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedFlaws)); } }
    public string Flaw3       { get => _flaw3;       set { _flaw3       = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedFlaws)); } }
    public string Flaw4       { get => _flaw4;       set { _flaw4       = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedFlaws)); } }
    public string CoreAbility { get => _coreAbility; set { _coreAbility = value; OnPropertyChanged(); } }
    public string Summary     { get => _summary;     set { _summary     = value; OnPropertyChanged(); } }
    public string Portrait    { get => _portrait;    set { _portrait    = value; OnPropertyChanged(); } }
    /// <summary>Manual shield value set by the user (clamped 6–18).</summary>
    public int    DefenseBase  { get => _defenseBase;  set { _defenseBase = Math.Clamp(value, 6, 18); OnPropertyChanged(); } }
    /// <summary>Sum of armor values from non-attritioned equipment. Set by MainWindow.</summary>
    public int    ArmorBonus   { get => _armorBonus;   set { _armorBonus  = Math.Max(0, value);       OnPropertyChanged(); } }
    public bool   Hit1        { get => _hit1;        set { _hit1 = value; OnPropertyChanged(); } }
    public bool   Hit2        { get => _hit2;        set { _hit2 = value; OnPropertyChanged(); } }
    public bool   Hit3        { get => _hit3;        set { _hit3 = value; OnPropertyChanged(); } }
    public bool   Hit4        { get => _hit4;        set { _hit4 = value; OnPropertyChanged(); } }
    public bool   Hit5           { get => _hit5;           set { _hit5           = value; OnPropertyChanged(); } }
    public int    HeroPointsMax
    {
        get => _heroPointsMax;
        set { _heroPointsMax = Math.Max(value, 50); OnPropertyChanged(); }
    }
    public int    HeroPointsCurrent
    {
        get => _heroPointsCurrent;
        set
        {
            _heroPointsCurrent = Math.Clamp(value, 0, _heroPointsMax);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSpendPoint));
        }
    }

    /// <summary>True when there is at least one Available Point left to spend on a skill rating increase.</summary>
    public bool CanSpendPoint => _heroPointsCurrent > 0;

    public ObservableCollection<EquipData> Equipment { get; set; } = [];
    public ObservableCollection<SkillData> Skills    { get; set; } = [];
    public ObservableCollection<string>    Spells    { get; set; } = [];

    // ── Computed exclusion lists (used by SkillPickerBox.ExcludedValues) ─────

    /// <summary>All non-empty flaw values across all four slots. Refreshed whenever any Flaw setter fires.</summary>
    public IReadOnlyList<string> SelectedFlaws =>
        new[] { _flaw1, _flaw2, _flaw3, _flaw4 }
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList().AsReadOnly();

    /// <summary>All non-empty SkillName values from the Skills collection. Call <see cref="RefreshSelectedSkillNames"/> to update.</summary>
    public IReadOnlyList<string> SelectedSkillNames { get; private set; } = Array.Empty<string>();

    /// <summary>Recomputes <see cref="SelectedSkillNames"/> and raises PropertyChanged. Call when any SkillName changes.</summary>
    internal void RefreshSelectedSkillNames()
    {
        SelectedSkillNames = Skills
            .Select(s => s.SkillName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList().AsReadOnly();
        OnPropertyChanged(nameof(SelectedSkillNames));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static CharacterState CreateDefault()
    {
        var s = new CharacterState();
        s.Spells.Add(""); s.Spells.Add(""); s.Spells.Add("");
        return s;
    }
}
