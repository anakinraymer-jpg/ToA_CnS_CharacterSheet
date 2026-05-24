using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CharacterSheet.Models;

// ── Equipment row ─────────────────────────────────────────────────────────────

public class EquipData : INotifyPropertyChanged
{
    private string _equipName = "";
    private string _equipSub  = "";
    private bool   _equipUsed;

    public string EquipName { get => _equipName; set { _equipName = value; OnPropertyChanged(); } }
    public string EquipSub  { get => _equipSub;  set { _equipSub  = value; OnPropertyChanged(); } }
    public bool   EquipUsed { get => _equipUsed; set { _equipUsed = value; OnPropertyChanged(); } }

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

    public string Name        { get => _name;        set { _name        = value; OnPropertyChanged(); } }
    public string Lineage     { get => _lineage;     set { _lineage     = value; OnPropertyChanged(); } }
    public string Hometown    { get => _hometown;    set { _hometown    = value; OnPropertyChanged(); } }
    public string Flaw1       { get => _flaw1;       set { _flaw1       = value; OnPropertyChanged(); } }
    public string Flaw2       { get => _flaw2;       set { _flaw2       = value; OnPropertyChanged(); } }
    public string Flaw3       { get => _flaw3;       set { _flaw3       = value; OnPropertyChanged(); } }
    public string Flaw4       { get => _flaw4;       set { _flaw4       = value; OnPropertyChanged(); } }
    public string CoreAbility { get => _coreAbility; set { _coreAbility = value; OnPropertyChanged(); } }
    public string Summary     { get => _summary;     set { _summary     = value; OnPropertyChanged(); } }
    public string Portrait    { get => _portrait;    set { _portrait    = value; OnPropertyChanged(); } }

    public ObservableCollection<EquipData> Equipment { get; set; } = [];
    public ObservableCollection<SkillData> Skills    { get; set; } = [];
    public ObservableCollection<string>    Spells    { get; set; } = [];

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
