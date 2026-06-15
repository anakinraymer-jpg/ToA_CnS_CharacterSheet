using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CharacterSheet.Controls;
using CharacterSheet.Models;
using Microsoft.Win32;

namespace CharacterSheet;

public partial class MainWindow : Window
{
    private CharacterState _state = CharacterState.CreateDefault();
    private bool _loading;

    // ── Zoom ──────────────────────────────────────────────────────────
    private double _zoom = 1.0;
    private const double ZoomMin  = 0.25;
    private const double ZoomMax  = 3.0;
    private const double ZoomStep = 0.10;

    // Must match SheetContent Width and Margin in XAML
    private const double SheetWidth   = 680;
    private const double SheetMarginH = 20;
    private const double SheetMarginT = 20;

    // ── Save-file tracking ────────────────────────────────────────────
    private string? _currentFilePath;

    private void SetCurrentFilePath(string? path)
    {
        _currentFilePath = path;
        var tag = path != null ? $" — {Path.GetFileName(path)}" : "";
        Title = $"Crown & Skull — Character Sheet{tag}";
    }

    // ── Hero-point tracking ───────────────────────────────────────────
    private string _prevCoreAbility = "";
    private string _prevFlaw1 = "", _prevFlaw2 = "", _prevFlaw3 = "", _prevFlaw4 = "";
    private readonly Dictionary<SkillData, int> _prevSkillRating = new();

    // ── Core-ability auto-added skills ────────────────────────────────
    /// <summary>Skills automatically added by the current Core Ability (Druid, Empath, etc.).</summary>
    private readonly List<SkillData> _coreAbilitySkills = new();

    // ── Map integration ───────────────────────────────────────────────
    private MapHost? _mapHost;
    private string _mapDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Dev", "ToA_Delver_Map");
    private FileSystemWatcher? _locationWatcher;
    private static readonly string LocationFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToA_CnS_CharacterSheet", "player_location.json");

    // ── Legend window (single modeless instance) ──────────────────────
    private LegendWindow? _legendWindow;

    // ── Layout edit mode ──────────────────────────────────────────────
    private bool _isEditMode;
    private StackPanel[] _sectionPanels  = null!;
    private Border[]     _sectionHandles = null!;

    private static readonly string[] DefaultSectionOrder =
        ["Flaws", "CoreAbility", "Summary", "Defense", "EquipSkills", "Spells", "Inventory"];

    // ── AP helpers ────────────────────────────────────────────────────

    private static int ApCostForAdd(SkillData sk)
        => sk.IsAlwaysFree ? 0
         : Math.Max(0, sk.SkillRating - sk.FreeRating) / sk.RatingStep;

    private static int ApRefundForRemove(SkillData sk, int prevRating)
        => sk.IsAlwaysFree ? 0
         : Math.Max(0, prevRating - sk.FreeRating) / sk.RatingStep;

    /// <summary>
    /// AP cost when rating changes from oldRating to newRating.
    /// Negative = refund. Respects FreeRating zone and RatingStep.
    /// </summary>
    private static int ApDeltaForChange(SkillData sk, int oldRating, int newRating)
    {
        if (sk.IsAlwaysFree) return 0;
        int oldPaid = Math.Max(0, oldRating - sk.FreeRating);
        int newPaid = Math.Max(0, newRating - sk.FreeRating);
        return (newPaid - oldPaid) / sk.RatingStep;
    }

    private bool IsLoremasterActive
        => _state.CoreAbility.Equals("Loremaster", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnowledgeSkill(SkillData sk)
        => sk.SkillName.StartsWith("Knowledge", StringComparison.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        _sectionPanels  = [SecFlaws, SecCoreAbility, SecSummary, SecDefense, SecEquipSkills, SecSpells, SecInventory];
        _sectionHandles = [SecFlawsHandle, SecCoreAbilityHandle, SecSummaryHandle, SecDefenseHandle, SecEquipSkillsHandle, SecSpellsHandle, SecInventoryHandle];
        WireUiEvents();
        LoadState(Persistence.Load());
        StartLocationWatcher();
        Loaded += (_, _) => FitToWindow();
        Closing += OnWindowClosing;
    }

    // ── One-time UI event wiring ──────────────────────────────────────
    private void WireUiEvents()
    {
        BtnNew.Click    += OnBtnNew;
        BtnOpen.Click   += OnBtnOpen;
        BtnSave.Click   += OnBtnSave;
        BtnExport.Click += OnBtnExport;
        BtnImport.Click += OnBtnImport;

        BtnZoomIn.Click  += (_, _) => ZoomAroundCenter(_zoom + ZoomStep);
        BtnZoomOut.Click += (_, _) => ZoomAroundCenter(_zoom - ZoomStep);
        BtnZoomFit.Click    += (_, _) => FitToWindow();
        BtnLegend.Click     += (_, _) => OpenLegend();
        BtnLayoutEdit.Checked   += (_, _) => SetLayoutEditMode(true);
        BtnLayoutEdit.Unchecked += (_, _) => SetLayoutEditMode(false);
        BtnAddCategory.Click += (_, _) => OnAddCategoryClicked();

        BtnAddEquip.Click += OnAddEquipClicked;
        BtnAddSkill.Click += OnAddSkillClicked;

        PortraitBox.MouseLeftButtonDown += OnPortraitClick;

        TbSpell0.TextChanged += OnSpell0Changed;
        TbSpell1.TextChanged += OnSpell1Changed;
        TbSpell2.TextChanged += OnSpell2Changed;
    }

    // ── Load / Populate ───────────────────────────────────────────────
    private void LoadState(CharacterState state)
    {
        _loading = true;

        // Unsubscribe from old state
        _state.PropertyChanged -= OnStateChanged;
        _state.Equipment.CollectionChanged -= OnEquipmentCollectionChanged;
        _state.Skills.CollectionChanged    -= OnSkillsCollectionChanged;
        foreach (var e  in _state.Equipment) e.PropertyChanged  -= OnItemChanged;
        foreach (var sk in _state.Skills)
        {
            sk.PropertyChanged -= OnItemChanged;
            sk.PropertyChanged -= OnSkillDataChanged;
        }
        UnsubscribeInventory(_state);

        _state = state;
        DataContext = _state;

        TbSpell0.Text = state.Spells.Count > 0 ? state.Spells[0] : "";
        TbSpell1.Text = state.Spells.Count > 1 ? state.Spells[1] : "";
        TbSpell2.Text = state.Spells.Count > 2 ? state.Spells[2] : "";

        if (!string.IsNullOrEmpty(state.Portrait))
            ShowPortrait(state.Portrait);
        else
            ClearPortrait();

        // Subscribe to new state
        _state.PropertyChanged += OnStateChanged;
        _state.Equipment.CollectionChanged += OnEquipmentCollectionChanged;
        _state.Skills.CollectionChanged    += OnSkillsCollectionChanged;
        foreach (var e  in _state.Equipment) e.PropertyChanged  += OnItemChanged;
        foreach (var sk in _state.Skills)
        {
            sk.PropertyChanged += OnItemChanged;
            sk.PropertyChanged += OnSkillDataChanged;
        }
        SubscribeInventory(_state);
        _state.RefreshSelectedSkillNames();

        // Seed hero-point trackers from loaded state — no adjustments during load
        _prevCoreAbility = _state.CoreAbility;
        _prevFlaw1 = _state.Flaw1;
        _prevFlaw2 = _state.Flaw2;
        _prevFlaw3 = _state.Flaw3;
        _prevFlaw4 = _state.Flaw4;

        _prevSkillRating.Clear();
        foreach (var sk in _state.Skills)
            _prevSkillRating[sk] = sk.SkillRating;

        _loading = false;

        ApplySectionOrder(_state.SectionOrder);

        // Recompute armor bonus from loaded equipment (no save triggered by this)
        RecomputeArmorBonus();

        // Re-populate the core-ability skill list from the saved IsCoreAbilitySkill flags
        RepopulateCoreAbilitySkills();

        UpdateAddButtonStates();
    }

    // ── Collection change tracking ────────────────────────────────────
    private void OnEquipmentCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RecomputeArmorBonus();
        UpdateAddButtonStates();
    }

    private void OnSkillsCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (SkillData sk in e.NewItems)
            {
                sk.PropertyChanged += OnSkillDataChanged;
                _prevSkillRating[sk] = sk.SkillRating;

                if (!_loading && sk.HasSkill)
                {
                    int cost = ApCostForAdd(sk);
                    if (cost > 0) _state.HeroPointsCurrent -= cost;
                }

                // If Inventor is active and Repair just joined, refresh the mirror
                if (!_loading &&
                    _state.CoreAbility.Equals("Inventor", StringComparison.OrdinalIgnoreCase) &&
                    sk.SkillName.Equals("Repair", StringComparison.OrdinalIgnoreCase))
                    UpdateCreateDeviceMirror();
            }

        if (e.OldItems != null)
            foreach (SkillData sk in e.OldItems)
            {
                sk.PropertyChanged -= OnSkillDataChanged;

                if (!_loading && _prevSkillRating.TryGetValue(sk, out int prev))
                {
                    int refund = ApRefundForRemove(sk, prev);
                    if (refund > 0) _state.HeroPointsCurrent += refund;
                }

                _prevSkillRating.Remove(sk);
            }

        _state.RefreshSelectedSkillNames();
        UpdateAddButtonStates();
    }

    /// <summary>
    /// Handles SkillName and SkillRating changes for hero-point adjustments
    /// and exclusion-list refresh.
    /// </summary>
    private void OnSkillDataChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading) return;
        if (sender is not SkillData sk) return;

        switch (e.PropertyName)
        {
            case nameof(SkillData.SkillName):
            {
                bool hadSkill = _prevSkillRating.TryGetValue(sk, out int prevRating) && prevRating > 0;
                bool hasSkill = sk.HasSkill;

                if (!hadSkill && hasSkill)
                {
                    int cost = ApCostForAdd(sk);
                    if (cost > 0) _state.HeroPointsCurrent -= cost;
                    _prevSkillRating[sk] = sk.SkillRating;
                }
                else if (hadSkill && !hasSkill)
                {
                    int refund = ApRefundForRemove(sk, prevRating);
                    if (refund > 0) _state.HeroPointsCurrent += refund;
                    _prevSkillRating[sk] = 0;
                }

                _state.RefreshSelectedSkillNames();

                // Repair name change while Inventor is active → update mirror
                if (_state.CoreAbility.Equals("Inventor", StringComparison.OrdinalIgnoreCase))
                    UpdateCreateDeviceMirror();
                break;
            }

            case nameof(SkillData.SkillRating):
            {
                int prevRating = _prevSkillRating.TryGetValue(sk, out int pr) ? pr : 0;
                int newRating  = sk.SkillRating;
                if (newRating != prevRating)
                {
                    int apDelta = ApDeltaForChange(sk, prevRating, newRating);
                    if (apDelta != 0) _state.HeroPointsCurrent -= apDelta;
                    _prevSkillRating[sk] = newRating;

                    // If Repair changed while Inventor is active, mirror to Create Device
                    if (_state.CoreAbility.Equals("Inventor", StringComparison.OrdinalIgnoreCase) &&
                        sk.SkillName.Equals("Repair", StringComparison.OrdinalIgnoreCase))
                        UpdateCreateDeviceMirror();
                }
                break;
            }
        }
    }

    private void UpdateAddButtonStates()
    {
        BtnAddEquip.IsEnabled = _state.Equipment.Count < 10;
        BtnAddSkill.IsEnabled = _state.Skills.Count < 10 && _state.HeroPointsCurrent >= 3;
    }

    // ── Armor bonus ───────────────────────────────────────────────────
    /// <summary>
    /// Recomputes ArmorBonus from all equipment items that have ArmorValue > 0
    /// and are NOT attritioned (EquipUsed = false).
    /// </summary>
    private void RecomputeArmorBonus()
    {
        int bonus = _state.Equipment
            .Where(eq => eq.ArmorValue > 0 && !eq.EquipUsed)
            .Sum(eq => eq.ArmorValue);
        int cap = Math.Max(0, 18 - _state.DefenseBase);
        _state.ArmorBonus = Math.Min(bonus, cap);
    }

    // ── Zoom core ─────────────────────────────────────────────────────

    private void ApplyZoom(double z)
    {
        _zoom = Math.Clamp(z, ZoomMin, ZoomMax);
        SheetScale.ScaleX = SheetScale.ScaleY = _zoom;
        TbZoom.Text = $"{(int)Math.Round(_zoom * 100)}%";
    }

    private double ContentLeft()
    {
        double scaledW = SheetWidth * _zoom + SheetMarginH * 2;
        double vw = SheetScroll.ViewportWidth;
        return vw > scaledW
            ? (vw - scaledW) / 2.0 + SheetMarginH
            : SheetMarginH;
    }

    private void ZoomToPoint(double newZoom, Point ptViewport, Point ptContent)
    {
        double clamped = Math.Clamp(newZoom, ZoomMin, ZoomMax);
        if (Math.Abs(clamped - _zoom) < 0.001) return;

        ApplyZoom(clamped);
        SheetScroll.UpdateLayout();

        double newH = ContentLeft()  + ptContent.X * _zoom - ptViewport.X;
        double newV = SheetMarginT   + ptContent.Y * _zoom - ptViewport.Y;

        SheetScroll.ScrollToHorizontalOffset(newH);
        SheetScroll.ScrollToVerticalOffset(newV);
    }

    private void ZoomAroundCenter(double newZoom)
    {
        double clamped = Math.Clamp(newZoom, ZoomMin, ZoomMax);
        if (Math.Abs(clamped - _zoom) < 0.001) return;

        var ptViewport = new Point(SheetScroll.ViewportWidth  / 2.0,
                                   SheetScroll.ViewportHeight / 2.0);
        double left = ContentLeft();
        var ptContent = new Point(
            (SheetScroll.HorizontalOffset + ptViewport.X - left)        / _zoom,
            (SheetScroll.VerticalOffset   + ptViewport.Y - SheetMarginT) / _zoom);

        ZoomToPoint(clamped, ptViewport, ptContent);
    }

    private void FitToWindow()
    {
        SheetScroll.UpdateLayout();
        double vw = SheetScroll.ViewportWidth  - SheetMarginH * 2;
        double vh = SheetScroll.ViewportHeight - SheetMarginH * 2;
        if (vw <= 0 || vh <= 0) return;

        double contentH = SheetContent.ActualHeight > 0
            ? SheetContent.ActualHeight + SheetMarginT + 40
            : 1400;

        ApplyZoom(Math.Min(vw / SheetWidth, vh / contentH));
        SheetScroll.ScrollToHorizontalOffset(0);
        SheetScroll.ScrollToVerticalOffset(0);
    }

    // ── Input: scroll wheel & keyboard ───────────────────────────────

    private void OnScrollWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;

        var ptViewport = e.GetPosition(SheetScroll);
        var ptContent  = e.GetPosition(SheetContent);
        ZoomToPoint(_zoom + (e.Delta > 0 ? ZoomStep : -ZoomStep), ptViewport, ptContent);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                ZoomAroundCenter(_zoom + ZoomStep);
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomAroundCenter(_zoom - ZoomStep);
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                FitToWindow();
                e.Handled = true;
                break;
            case Key.S:
                OnBtnSave(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.O:
                OnBtnOpen(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    // ── Auto-save & hero-point / armor automation ─────────────────────

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading) return;

        switch (e.PropertyName)
        {
            // Core ability: −15 Available on acquisition, +15 on removal
            case nameof(CharacterState.CoreAbility):
                ApplyCoreAbilityDelta(ref _prevCoreAbility, _state.CoreAbility);
                UpdatePlayerLocation();
                break;

            // Flaws: ±5 both Total and Available
            case nameof(CharacterState.Flaw1): ApplyFlawDelta(ref _prevFlaw1, _state.Flaw1); break;
            case nameof(CharacterState.Flaw2): ApplyFlawDelta(ref _prevFlaw2, _state.Flaw2); break;
            case nameof(CharacterState.Flaw3): ApplyFlawDelta(ref _prevFlaw3, _state.Flaw3); break;
            case nameof(CharacterState.Flaw4): ApplyFlawDelta(ref _prevFlaw4, _state.Flaw4); break;

            // DefenseBase changed: re-enforce armor cap (bonus ≤ 18 − base)
            case nameof(CharacterState.DefenseBase):
                RecomputeArmorBonus();
                break;

            // Available Points changed: refresh whether Add Skill is allowed
            case nameof(CharacterState.HeroPointsCurrent):
                UpdateAddButtonStates();
                break;
        }

        Save();
    }

    private void ApplyCoreAbilityDelta(ref string prev, string next)
    {
        bool wasEmpty = string.IsNullOrWhiteSpace(prev);
        bool nowEmpty = string.IsNullOrWhiteSpace(next);

        if (wasEmpty && !nowEmpty)
        {
            _state.HeroPointsCurrent -= 15;
            ApplyCoreAbilitySkills(next);
        }
        else if (!wasEmpty && nowEmpty)
        {
            _state.HeroPointsCurrent += 15;
            RemoveCoreAbilitySkills();
        }
        else if (!wasEmpty && !nowEmpty &&
                 !prev.Equals(next, StringComparison.OrdinalIgnoreCase))
        {
            // Core ability swapped — remove old bonus skills, add new ones
            RemoveCoreAbilitySkills();
            ApplyCoreAbilitySkills(next);
        }

        prev = next;
    }

    // ── Core-ability skill management ─────────────────────────────────

    /// <summary>
    /// Grants the bonus skills for the given Core Ability.
    /// Skills are added for free (FreeRating = initial rating) with a minimum floor.
    /// </summary>
    private void ApplyCoreAbilitySkills(string ability)
    {
        if (ability.Equals("Druid", StringComparison.OrdinalIgnoreCase))
        {
            AddCoreAbilitySkill("Tracking", initialRating: 9,  minRating: 9);
            AddCoreAbilitySkill("Hunting",  initialRating: 9,  minRating: 9);
        }
        else if (ability.Equals("Empath", StringComparison.OrdinalIgnoreCase))
        {
            AddCoreAbilitySkill("Investigation", initialRating: 7, minRating: 7);
            AddCoreAbilitySkill("Streetwise",    initialRating: 7, minRating: 7);
            AddCoreAbilitySkill("Oratory",       initialRating: 7, minRating: 7);
        }
        else if (ability.Equals("Mountaineer", StringComparison.OrdinalIgnoreCase))
        {
            AddCoreAbilitySkill("Climber",   initialRating: 10, minRating: 10);
            AddCoreAbilitySkill("Breakfall", initialRating: 10, minRating: 10);
        }
        else if (ability.Equals("Inventor", StringComparison.OrdinalIgnoreCase))
        {
            AddCoreAbilitySkill("Create Device", initialRating: 3, minRating: 3,
                                isAlwaysFree: true, isLocked: true);
            UpdateCreateDeviceMirror();
        }
    }

    /// <summary>
    /// Removes all skills auto-added by the current Core Ability and refunds any
    /// Available Points the player spent above the free base.
    /// </summary>
    private void RemoveCoreAbilitySkills()
    {
        foreach (var sk in _coreAbilitySkills.ToList())
        {
            sk.PropertyChanged -= OnItemChanged;
            _state.Skills.Remove(sk);   // OnSkillsCollectionChanged handles refund
        }
        _coreAbilitySkills.Clear();
    }

    /// <summary>
    /// Creates and adds a single Core-Ability bonus skill to both the skill list
    /// and the tracking collection.  The skill costs 0 AP (free up to initialRating).
    /// </summary>
    private void AddCoreAbilitySkill(
        string name, int initialRating, int minRating,
        bool isAlwaysFree = false, bool isLocked = false)
    {
        var sk = new SkillData
        {
            MinRating          = minRating,
            FreeRating         = isAlwaysFree ? 0 : initialRating,
            IsAlwaysFree       = isAlwaysFree,
            IsLocked           = isLocked,
            IsCoreAbilitySkill = true,
            // SkillName last: auto-promote clamps to MinRating, setting the correct initial rating
            SkillName          = name,
        };

        sk.PropertyChanged += OnItemChanged;
        _coreAbilitySkills.Add(sk);
        _state.Skills.Add(sk);   // OnSkillsCollectionChanged sets _prevSkillRating and charges 0 AP
    }

    /// <summary>
    /// Re-populates _coreAbilitySkills from the loaded skill collection
    /// (skills with IsCoreAbilitySkill = true).  Called after LoadState.
    /// </summary>
    private void RepopulateCoreAbilitySkills()
    {
        _coreAbilitySkills.Clear();
        foreach (var sk in _state.Skills)
            if (sk.IsCoreAbilitySkill)
                _coreAbilitySkills.Add(sk);

        // Re-apply mirror in case Inventor is the loaded ability
        if (_state.CoreAbility.Equals("Inventor", StringComparison.OrdinalIgnoreCase))
            UpdateCreateDeviceMirror();
    }

    /// <summary>
    /// Sets Create Device's rating to match Repair's current rating (if Repair is present).
    /// No-op when Create Device is not in the core-ability list.
    /// </summary>
    private void UpdateCreateDeviceMirror()
    {
        var createDevice = _coreAbilitySkills.FirstOrDefault(s =>
            s.SkillName.Equals("Create Device", StringComparison.OrdinalIgnoreCase));
        if (createDevice == null) return;

        var repair = _state.Skills.FirstOrDefault(s =>
            s.SkillName.Equals("Repair", StringComparison.OrdinalIgnoreCase));
        if (repair != null)
            createDevice.SkillRating = repair.SkillRating;
    }

    private void ApplyFlawDelta(ref string prev, string next)
    {
        bool wasEmpty = string.IsNullOrWhiteSpace(prev);
        bool nowEmpty = string.IsNullOrWhiteSpace(next);

        if (wasEmpty && !nowEmpty)
        {
            _state.HeroPointsMax     += 5;
            _state.HeroPointsCurrent += 5;
        }
        else if (!wasEmpty && nowEmpty)
        {
            int oldMax = _state.HeroPointsMax;
            _state.HeroPointsMax -= 5;
            int actualDrop = oldMax - _state.HeroPointsMax;
            _state.HeroPointsCurrent -= actualDrop;
        }

        prev = next;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading) return;

        // Recompute armor bonus when EquipUsed (attrition) or ArmorValue changes
        if (sender is EquipData &&
            (e.PropertyName == nameof(EquipData.EquipUsed) ||
             e.PropertyName == nameof(EquipData.ArmorValue)))
        {
            RecomputeArmorBonus();
        }

        Save();
    }

    // ── Add equipment / skill rows ────────────────────────────────────
    private void OnAddEquipClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEntryDialog(isSkill: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var item = new EquipData
        {
            EquipName  = dlg.EntryName,
            EquipSub   = dlg.EntryDescription,
            ArmorValue = dlg.EntryArmorValue,
        };
        item.PropertyChanged += OnItemChanged;
        _state.Equipment.Add(item);   // OnEquipmentCollectionChanged → RecomputeArmorBonus
        Save();
    }

    private void OnAddSkillClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEntryDialog(isSkill: true, excludedNames: _state.SelectedSkillNames) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var item = new SkillData
        {
            SkillName = dlg.EntryName,
            SkillSub  = dlg.EntryDescription,
        };
        // item.SkillRating is now 3 (auto-promoted by SkillName setter)

        // ── Loremaster: Knowledge starts at 6, each AP spent gives 2 rating points ──
        // FreeRating stays 0 so the 6-point start costs 3 AP (6 / step-of-2 = 3).
        if (IsLoremasterActive && IsKnowledgeSkill(item))
        {
            item.RatingStep  = 2;
            item.SkillRating = 6;  // override before Add so OnSkillsCollectionChanged sees 6
        }

        item.PropertyChanged += OnItemChanged;
        _state.Skills.Add(item);   // OnSkillsCollectionChanged handles point deduction
        Save();
    }

    // ── Remove equipment / skill rows ─────────────────────────────────
    private void OnRemoveEquipClicked(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not EquipData item) return;
        item.PropertyChanged -= OnItemChanged;
        _state.Equipment.Remove(item);   // OnEquipmentCollectionChanged → RecomputeArmorBonus
        Save();
    }

    private void OnRemoveSkillClicked(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not SkillData item) return;
        item.PropertyChanged -= OnItemChanged;
        _state.Skills.Remove(item);   // OnSkillsCollectionChanged handles point refund
        Save();
    }

    // ── Spells ────────────────────────────────────────────────────────
    private void OnSpell0Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    { if (!_loading) { _state.Spells[0] = TbSpell0.Text; Save(); } }

    private void OnSpell1Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    { if (!_loading) { _state.Spells[1] = TbSpell1.Text; Save(); } }

    private void OnSpell2Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    { if (!_loading) { _state.Spells[2] = TbSpell2.Text; Save(); } }

    // ── Portrait ──────────────────────────────────────────────────────
    private void OnPortraitClick(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
            Title  = "Choose Portrait",
        };
        if (dlg.ShowDialog() != true) return;

        var bytes   = File.ReadAllBytes(dlg.FileName);
        var b64     = Convert.ToBase64String(bytes);
        var ext     = Path.GetExtension(dlg.FileName).TrimStart('.').ToLower();
        var mime    = ext == "jpg" ? "jpeg" : ext;
        var dataUrl = $"data:image/{mime};base64,{b64}";

        ShowPortrait(dataUrl);
        _state.Portrait = dataUrl;
        Save();
    }

    private void ShowPortrait(string dataUrl)
    {
        try
        {
            var base64 = dataUrl.Contains(',') ? dataUrl.Split(',')[1] : dataUrl;
            var bytes  = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();

            PortraitImg.Source     = bmp;
            PortraitImg.Visibility = Visibility.Visible;
        }
        catch { /* bad image data */ }
    }

    private void ClearPortrait()
    {
        PortraitImg.Source     = null;
        PortraitImg.Visibility = Visibility.Collapsed;
    }

    // ── Control buttons ───────────────────────────────────────────────
    private void OnBtnNew(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear this character sheet and start fresh?",
                            "New Character",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        LoadState(CharacterState.CreateDefault());
        SetCurrentFilePath(null);
        Save();
    }

    private void OnBtnOpen(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Character Sheet files|*.json",
            Title  = "Open Character Sheet",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json  = File.ReadAllText(dlg.FileName);
            var state = Persistence.LoadFromJson(json);
            LoadState(state);
            SetCurrentFilePath(dlg.FileName);
            Save();   // sync AppData so the app resumes this file on next launch
        }
        catch
        {
            MessageBox.Show("Could not read that file. Make sure it's a valid character JSON.",
                            "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnBtnSave(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath == null)
        {
            // No current file yet — prompt for a location (Save As)
            var name = string.IsNullOrWhiteSpace(_state.Name) ? "character" : _state.Name;
            var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            var saveDlg = new SaveFileDialog
            {
                FileName   = $"{safe}_cs_sheet.json",
                DefaultExt = ".json",
                Filter     = "Character Sheet files|*.json",
                Title      = "Save Character Sheet",
            };
            if (saveDlg.ShowDialog() != true) return;
            SetCurrentFilePath(saveDlg.FileName);
        }
        File.WriteAllText(_currentFilePath!, Persistence.ExportJson(_state));
    }

    private void OnBtnExport(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(_state.Name) ? "character" : _state.Name;
        var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

        var dlg = new SaveFileDialog
        {
            FileName   = $"{safe}_cs_sheet.json",
            DefaultExt = ".json",
            Filter     = "JSON files|*.json",
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, Persistence.ExportJson(_state));
    }

    private void OnBtnImport(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON files|*.json",
            Title  = "Import Character",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json  = File.ReadAllText(dlg.FileName);
            var state = Persistence.LoadFromJson(json);
            LoadState(state);
            Save();
        }
        catch
        {
            MessageBox.Show("Could not read that file. Make sure it's a valid character JSON.",
                            "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save() => Persistence.Save(_state);

    // ── Inventory ─────────────────────────────────────────────────────

    private void SubscribeInventory(CharacterState s)
    {
        s.Inventory.CollectionChanged += OnInventoryCategoriesChanged;
        foreach (var cat in s.Inventory) SubscribeCategory(cat);
    }

    private void UnsubscribeInventory(CharacterState s)
    {
        s.Inventory.CollectionChanged -= OnInventoryCategoriesChanged;
        foreach (var cat in s.Inventory) UnsubscribeCategory(cat);
    }

    private void SubscribeCategory(InventoryCategory cat)
    {
        cat.PropertyChanged += OnInventoryItemChanged;
        cat.Items.CollectionChanged += OnInventoryItemsChanged;
        foreach (var item in cat.Items) item.PropertyChanged += OnInventoryItemChanged;
    }

    private void UnsubscribeCategory(InventoryCategory cat)
    {
        cat.PropertyChanged -= OnInventoryItemChanged;
        cat.Items.CollectionChanged -= OnInventoryItemsChanged;
        foreach (var item in cat.Items) item.PropertyChanged -= OnInventoryItemChanged;
    }

    private void OnInventoryCategoriesChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (InventoryCategory cat in e.NewItems) SubscribeCategory(cat);
        if (e.OldItems != null)
            foreach (InventoryCategory cat in e.OldItems) UnsubscribeCategory(cat);
        if (!_loading) Save();
    }

    private void OnInventoryItemsChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (InventoryItem item in e.NewItems) item.PropertyChanged += OnInventoryItemChanged;
        if (e.OldItems != null)
            foreach (InventoryItem item in e.OldItems) item.PropertyChanged -= OnInventoryItemChanged;
        if (!_loading) Save();
    }

    private void OnInventoryItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading) return;
        if (e.PropertyName == nameof(InventoryItem.IsEditing)) return; // UI state, not data
        Save();
    }

    private void OnAddCategoryClicked()
    {
        _state.Inventory.Add(new InventoryCategory { Name = "New Category" });
        Save();
    }

    private void OnRemoveCategoryClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryCategory cat) return;
        _state.Inventory.Remove(cat);
        Save();
    }

    private void OnMoveCategoryUpClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryCategory cat) return;
        int idx = _state.Inventory.IndexOf(cat);
        if (idx > 0) _state.Inventory.Move(idx, idx - 1);
        Save();
    }

    private void OnMoveCategoryDownClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryCategory cat) return;
        int idx = _state.Inventory.IndexOf(cat);
        if (idx >= 0 && idx < _state.Inventory.Count - 1) _state.Inventory.Move(idx, idx + 1);
        Save();
    }

    private void OnAddInventoryItemClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryCategory cat) return;
        cat.Items.Add(new InventoryItem());
        Save();
    }

    private void OnRemoveInventoryItemClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryItem item) return;
        var cat = _state.Inventory.FirstOrDefault(c => c.Items.Contains(item));
        cat?.Items.Remove(item);
        Save();
    }

    private void OnMoveItemUpClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryItem item) return;
        var cat = _state.Inventory.FirstOrDefault(c => c.Items.Contains(item));
        if (cat == null) return;
        int idx = cat.Items.IndexOf(item);
        if (idx > 0)
        {
            cat.Items.Move(idx, idx - 1);
        }
        else
        {
            // Move to bottom of previous category
            int catIdx = _state.Inventory.IndexOf(cat);
            if (catIdx > 0)
            {
                cat.Items.Remove(item);
                _state.Inventory[catIdx - 1].Items.Add(item);
            }
        }
        Save();
    }

    private void OnMoveItemDownClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryItem item) return;
        var cat = _state.Inventory.FirstOrDefault(c => c.Items.Contains(item));
        if (cat == null) return;
        int idx = cat.Items.IndexOf(item);
        if (idx < cat.Items.Count - 1)
        {
            cat.Items.Move(idx, idx + 1);
        }
        else
        {
            // Move to top of next category
            int catIdx = _state.Inventory.IndexOf(cat);
            if (catIdx < _state.Inventory.Count - 1)
            {
                cat.Items.Remove(item);
                _state.Inventory[catIdx + 1].Items.Insert(0, item);
            }
        }
        Save();
    }

    private void OnToggleItemEditClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not InventoryItem item) return;
        item.IsEditing = !item.IsEditing;
    }

    // ── Layout edit mode ──────────────────────────────────────────────

    private void SetLayoutEditMode(bool active)
    {
        _isEditMode = active;
        var vis = active ? Visibility.Visible : Visibility.Collapsed;
        foreach (var h in _sectionHandles) h.Visibility = vis;
    }

    private void OnMoveSectionUp(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        MoveSectionBy(tag, -1);
    }

    private void OnMoveSectionDown(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        MoveSectionBy(tag, +1);
    }

    private void MoveSectionBy(string tag, int delta)
    {
        var panel = _sectionPanels.FirstOrDefault(p => (string?)p.Tag == tag);
        if (panel == null) return;

        int idx    = SheetStack.Children.IndexOf(panel);
        int newIdx = idx + delta;

        // Sections start at index 3 (after title×2 + identity grid)
        if (newIdx < 3 || newIdx >= SheetStack.Children.Count) return;

        SheetStack.Children.RemoveAt(idx);
        SheetStack.Children.Insert(newIdx, panel);

        SaveCurrentSectionOrder();
    }

    private void SaveCurrentSectionOrder()
    {
        _state.SectionOrder = _sectionPanels
            .OrderBy(p => SheetStack.Children.IndexOf(p))
            .Select(p => (string)p.Tag!)
            .ToList();
        Save();
    }

    private void ApplySectionOrder(List<string> order)
    {
        var panelByTag = _sectionPanels.ToDictionary(p => (string)p.Tag!);

        var savedPanels = (order ?? [])
            .Where(panelByTag.ContainsKey)
            .Select(id => panelByTag[id])
            .ToList();
        var missing = _sectionPanels.Where(p => !savedPanels.Contains(p)).ToList();
        var orderedPanels = savedPanels.Concat(missing).ToList();

        foreach (var p in _sectionPanels) SheetStack.Children.Remove(p);
        for (int i = 0; i < orderedPanels.Count; i++)
            SheetStack.Children.Insert(3 + i, orderedPanels[i]);
    }

    // ── Map ───────────────────────────────────────────────────────────

    private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isMapTab = MainTabControl.SelectedItem == TabMap;
        SheetOnlyControls.Visibility = isMapTab ? Visibility.Collapsed : Visibility.Visible;

        if (isMapTab && _mapHost == null)
            StartMap();
    }

    private void StartMap()
    {
        if (!Directory.Exists(_mapDir))
        {
            TbMapStatus.Text = $"Map not found at: {_mapDir}";
            BtnConfigureMap.Visibility = Visibility.Visible;
            return;
        }

        TbMapStatus.Text = "Starting map — this may take a few seconds…";

        _mapHost = new MapHost(_mapDir, _state.Name);

        _mapHost.EmbedReady += () =>
        {
            MapPlaceholder.Visibility = Visibility.Collapsed;
        };

        _mapHost.EmbedFailed += msg =>
        {
            TbMapStatus.Text = $"Could not embed map: {msg}";
            BtnConfigureMap.Visibility = Visibility.Visible;
            _mapHost.Stop();
            MapContainer.Children.Remove(_mapHost);
            _mapHost = null;
        };

        MapContainer.Children.Add(_mapHost);
    }

    private void OnConfigureMapClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select the ToA_Delver_Map folder",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        if (dlg.ShowDialog() == true)
        {
            _mapDir = dlg.FolderName;
            // Reset and try again
            if (_mapHost != null)
            {
                _mapHost.Stop();
                MapContainer.Children.Remove(_mapHost);
                _mapHost = null;
            }
            BtnConfigureMap.Visibility = Visibility.Collapsed;
            MapPlaceholder.Visibility = Visibility.Visible;
            StartMap();
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _mapHost?.Stop();
        _locationWatcher?.Dispose();
    }

    // ── Druid Wild Bonus ──────────────────────────────────────────────

    private void StartLocationWatcher()
    {
        var dir = Path.GetDirectoryName(LocationFilePath)!;
        Directory.CreateDirectory(dir);
        _locationWatcher = new FileSystemWatcher(dir, "player_location.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _locationWatcher.Changed += (_, _) => Task.Delay(100).ContinueWith(
            _ => Dispatcher.Invoke(UpdatePlayerLocation), TaskScheduler.Default);
        _locationWatcher.Created += (_, _) => Task.Delay(100).ContinueWith(
            _ => Dispatcher.Invoke(UpdatePlayerLocation), TaskScheduler.Default);
        UpdatePlayerLocation();
    }

    private void UpdatePlayerLocation()
    {
        int    node         = -1;
        string terrain      = "";
        string locName      = "";
        string locType      = "";

        try
        {
            if (File.Exists(LocationFilePath))
            {
                var text = File.ReadAllText(LocationFilePath);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                node    = root.TryGetProperty("node",          out var nProp) ? nProp.GetInt32()    : -1;
                terrain = root.TryGetProperty("terrain",       out var tProp) ? tProp.GetString() ?? "" : "";
                locName = root.TryGetProperty("location_name", out var lnProp) ? lnProp.GetString() ?? "" : "";
                locType = root.TryGetProperty("location_type", out var ltProp) ? ltProp.GetString() ?? "" : "";
            }
        }
        catch { }

        _state.PlayerNode         = node;
        _state.PlayerTerrain      = terrain;
        _state.PlayerLocationName = locName;
        _state.PlayerLocationType = locType;

        // Druid wild bonus: active when Druid AND not in a Structure or Cave hex
        bool isDruid = _state.CoreAbility.Equals("Druid", StringComparison.OrdinalIgnoreCase);
        _state.DruidWildBonusActive = isDruid && node >= 0 && terrain != "Structure" && terrain != "Cave";
    }

    // ── Legend ────────────────────────────────────────────────────────
    private void OpenLegend()
    {
        if (_legendWindow is { IsLoaded: true })
        {
            _legendWindow.Activate();
            return;
        }
        _legendWindow = new LegendWindow { Owner = this };
        _legendWindow.Show();
    }
}
