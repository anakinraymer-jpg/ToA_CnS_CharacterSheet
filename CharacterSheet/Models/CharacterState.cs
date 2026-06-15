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

    // ── Advanced skill properties ──────────────────────────────────────
    private int  _minRating          = 3;   // floor for SkillRating (default 3)
    private int  _freeRating         = 0;   // how much of the base rating costs no AP
    private int  _ratingStep         = 1;   // DieBubble step + AP-cost divisor
    private bool _isAlwaysFree       = false; // no AP cost ever (e.g. Create Device)
    private bool _isLocked           = false; // DieBubble is display-only (e.g. Create Device)
    private bool _isCoreAbilitySkill = false; // auto-added by a core ability

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
    /// 0 when no skill is assigned; MinRating–18 when a skill is assigned (clamped).
    /// </summary>
    public int SkillRating
    {
        get => _skillRating;
        set
        {
            _skillRating = HasSkill ? Math.Clamp(value, _minRating, 18) : 0;
            OnPropertyChanged();
        }
    }

    /// <summary>Minimum allowed rating (default 3). Auto-added skills use their grant level.</summary>
    public int MinRating
    {
        get => _minRating;
        set { _minRating = Math.Max(1, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Portion of the base rating that costs no Available Points.
    /// AP cost formula: max(0, SkillRating − FreeRating) / RatingStep.
    /// </summary>
    public int FreeRating
    {
        get => _freeRating;
        set { _freeRating = Math.Max(0, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// DieBubble increment/decrement per click, and AP-cost divisor.
    /// 1 = normal; 2 = Loremaster Knowledge (1 AP per 2 rating points).
    /// </summary>
    public int RatingStep
    {
        get => _ratingStep;
        set { _ratingStep = Math.Max(1, value); OnPropertyChanged(); }
    }

    /// <summary>When true the skill never costs or refunds Available Points (e.g. Create Device).</summary>
    public bool IsAlwaysFree
    {
        get => _isAlwaysFree;
        set { _isAlwaysFree = value; OnPropertyChanged(); }
    }

    /// <summary>When true the DieBubble is display-only — the user cannot change the rating (e.g. Create Device).</summary>
    public bool IsLocked
    {
        get => _isLocked;
        set { _isLocked = value; OnPropertyChanged(); }
    }

    /// <summary>True for skills auto-added by a core ability (Druid, Empath, Mountaineer, Inventor).</summary>
    public bool IsCoreAbilitySkill
    {
        get => _isCoreAbilitySkill;
        set { _isCoreAbilitySkill = value; OnPropertyChanged(); }
    }

    private bool _isConditionalBonus = false;
    /// <summary>
    /// True for skills granted conditionally by location/terrain (e.g. Mountaineer Scout at 15).
    /// These are excluded from the 10-skill maximum and never persisted to disk.
    /// </summary>
    public bool IsConditionalBonus
    {
        get => _isConditionalBonus;
        set { _isConditionalBonus = value; OnPropertyChanged(); }
    }

    private int _weatherPenalty = 0;
    /// <summary>Weather-driven penalty (0 or negative). Set by MainWindow; display-only, never persisted.</summary>
    public int WeatherPenalty
    {
        get => _weatherPenalty;
        set { _weatherPenalty = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Inventory item ────────────────────────────────────────────────────────────

public class InventoryItem : INotifyPropertyChanged
{
    private string _name        = "";
    private string _description = "";
    private bool   _isEditing   = false;

    public string Name        { get => _name;        set { _name        = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    /// <summary>UI-only flag — not persisted. True while the description TextBox is expanded.</summary>
    public bool   IsEditing   { get => _isEditing;   set { _isEditing   = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Inventory category ────────────────────────────────────────────────────────

public class InventoryCategory : INotifyPropertyChanged
{
    private string _name = "New Category";

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public ObservableCollection<InventoryItem> Items { get; set; } = [];

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
    private bool   _druidWildBonusActive  = false;
    private bool   _weatherEffectsEnabled = true;
    private int    _playerNode            = -1;
    private string _playerTerrain         = "";
    private string _playerLocationName    = "";
    private string _playerLocationType    = "";

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

    /// <summary>True when the Druid Wild Bonus (+3 to all active skill rolls) is currently active.</summary>
    public bool DruidWildBonusActive
    {
        get => _druidWildBonusActive;
        set { _druidWildBonusActive = value; OnPropertyChanged(); }
    }

    /// <summary>When false the weather penalty is not applied to skills (UI toggle).</summary>
    public bool WeatherEffectsEnabled
    {
        get => _weatherEffectsEnabled;
        set { _weatherEffectsEnabled = value; OnPropertyChanged(); }
    }

    // ── Map location tracking ─────────────────────────────────────────

    /// <summary>Hex node the player entity is currently on, or -1 when the map is not connected.</summary>
    public int PlayerNode
    {
        get => _playerNode;
        set
        {
            _playerNode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayerLocationVisible));
            OnPropertyChanged(nameof(PlayerLocationSummary));
        }
    }

    /// <summary>Terrain type painted on the player's current hex (empty = unpainted).</summary>
    public string PlayerTerrain
    {
        get => _playerTerrain;
        set { _playerTerrain = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayerLocationSummary)); }
    }

    /// <summary>Name of any named location on the player's current hex, or empty.</summary>
    public string PlayerLocationName
    {
        get => _playerLocationName;
        set { _playerLocationName = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayerLocationSummary)); }
    }

    /// <summary>Type tag of the named location on the player's hex (e.g. "City", "Ruin"), or empty.</summary>
    public string PlayerLocationType
    {
        get => _playerLocationType;
        set { _playerLocationType = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayerLocationSummary)); }
    }

    /// <summary>True when the player's map position is known (map is connected).</summary>
    public bool PlayerLocationVisible => _playerNode >= 0;

    /// <summary>Human-readable summary of the player's current map position for display in the UI.</summary>
    public string PlayerLocationSummary
    {
        get
        {
            if (_playerNode < 0) return "";
            string locPart = string.IsNullOrWhiteSpace(_playerLocationName) ? "" :
                (string.IsNullOrWhiteSpace(_playerLocationType)
                    ? _playerLocationName
                    : $"{_playerLocationName}  [{_playerLocationType}]");
            return "⬡  " + string.Join("  ·  ",
                new[] { locPart, _playerTerrain, $"Hex {_playerNode}" }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }
    }

    /// <summary>True when there is at least one Available Point left to spend on a skill rating increase.</summary>
    public bool CanSpendPoint => _heroPointsCurrent > 0;

    public ObservableCollection<EquipData>         Equipment    { get; set; } = [];
    public ObservableCollection<SkillData>         Skills       { get; set; } = [];
    public ObservableCollection<string>            Spells       { get; set; } = [];
    public ObservableCollection<InventoryCategory> Inventory    { get; set; } = [];
    /// <summary>Ordered section IDs as rearranged by the user. Empty = default order.</summary>
    public List<string>                            SectionOrder { get; set; } = [];

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
