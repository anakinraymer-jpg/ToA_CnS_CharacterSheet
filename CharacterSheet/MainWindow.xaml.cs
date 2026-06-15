using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    /// <summary>The conditional Scout skill granted to Mountaineer characters on Mountains/Cave terrain, or null.</summary>
    private SkillData? _mountaineerScoutSkill;

    // ── Map integration ───────────────────────────────────────────────
    private Map.MapView? _mapView;
    private string _currentMapWeather = "";

    // ── Weather particle effect ────────────────────────────────────────
    private DispatcherTimer? _mistTimer;
    private DispatcherTimer? _drizzleTimer;
    private DispatcherTimer? _drizzleRenderTimer;
    private int              _drizzleWaveToggle;
    private readonly List<DrizzleState> _activeDrizzles = [];
    private int                         _activeMistCount;
    private static readonly Random _particleRng = new();

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

    private bool IsMountaineerActive
        => _state.CoreAbility.Equals("Mountaineer", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnowledgeSkill(SkillData sk)
        => sk.SkillName.StartsWith("Knowledge", StringComparison.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        _sectionPanels  = [SecFlaws, SecCoreAbility, SecSummary, SecDefense, SecEquipSkills, SecSpells, SecInventory];
        _sectionHandles = [SecFlawsHandle, SecCoreAbilityHandle, SecSummaryHandle, SecDefenseHandle, SecEquipSkillsHandle, SecSpellsHandle, SecInventoryHandle];
        WireUiEvents();
        LoadState(Persistence.Load());
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
        BtnAddSpell.Click    += (_, _) => OnAddSpellClicked();

        BtnAddEquip.Click += OnAddEquipClicked;
        BtnAddSkill.Click += OnAddSkillClicked;

        PortraitBox.MouseLeftButtonDown += OnPortraitClick;
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
        UnsubscribeSpells(_state);
        UnsubscribeInventory(_state);

        _state = state;
        DataContext = _state;

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
        SubscribeSpells(_state);
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
        ApplyWeatherPenalties();
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
                ApplyWeatherPenalties();

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
        int regularSkillCount = _state.Skills.Count(sk => !sk.IsConditionalBonus);
        BtnAddEquip.IsEnabled = _state.Equipment.Count < 10;
        BtnAddSkill.IsEnabled = regularSkillCount < 10 && _state.HeroPointsCurrent >= 3;
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
            // Keep map's player label in sync with the character name
            case nameof(CharacterState.Name):
                if (_mapView != null) _mapView.PlayerName = _state.Name;
                break;

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

            // Weather toggle changed: reapply (or clear) penalties
            case nameof(CharacterState.WeatherEffectsEnabled):
                ApplyWeatherPenalties();
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

    private void SubscribeSpells(CharacterState s)
    {
        s.Spells.CollectionChanged += OnSpellsCollectionChanged;
        foreach (var sp in s.Spells) sp.PropertyChanged += OnSpellDataChanged;
    }

    private void UnsubscribeSpells(CharacterState s)
    {
        s.Spells.CollectionChanged -= OnSpellsCollectionChanged;
        foreach (var sp in s.Spells) sp.PropertyChanged -= OnSpellDataChanged;
    }

    private void OnSpellsCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (SpellData sp in e.NewItems) sp.PropertyChanged += OnSpellDataChanged;
        if (e.OldItems != null)
            foreach (SpellData sp in e.OldItems) sp.PropertyChanged -= OnSpellDataChanged;
        if (!_loading) Save();
    }

    private void OnSpellDataChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading) return;
        if (e.PropertyName == nameof(SpellData.IsEditing)) return; // UI state, not data
        Save();
    }

    private void OnToggleSpellEditClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is SpellData sp)
            sp.IsEditing = !sp.IsEditing;
    }

    private void OnAddSpellClicked()
    {
        _state.Spells.Add(new SpellData());
        Save();
    }

    private void OnRemoveSpellClicked(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not SpellData sp) return;
        _state.Spells.Remove(sp);
        Save();
    }

    private void OnMoveSpellUpClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not SpellData sp) return;
        int idx = _state.Spells.IndexOf(sp);
        if (idx > 0) _state.Spells.Move(idx, idx - 1);
        Save();
    }

    private void OnMoveSpellDownClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is not SpellData sp) return;
        int idx = _state.Spells.IndexOf(sp);
        if (idx >= 0 && idx < _state.Spells.Count - 1) _state.Spells.Move(idx, idx + 1);
        Save();
    }

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
            Filter           = "Character Sheet files|*.json",
            Title            = "Open Character Sheet",
            InitialDirectory = AppFolders.Sheets,
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
                FileName         = $"{safe}_cs_sheet.json",
                DefaultExt       = ".json",
                Filter           = "Character Sheet files|*.json",
                Title            = "Save Character Sheet",
                InitialDirectory = AppFolders.Sheets,
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
            FileName         = $"{safe}_cs_sheet.json",
            DefaultExt       = ".json",
            Filter           = "JSON files|*.json",
            InitialDirectory = AppFolders.Sheets,
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, Persistence.ExportJson(_state));
    }

    private void OnBtnImport(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter           = "JSON files|*.json",
            Title            = "Import Character",
            InitialDirectory = AppFolders.Sheets,
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

        if (isMapTab && _mapView == null)
            EmbedMapView();
    }

    private void EmbedMapView()
    {
        _mapView = new Map.MapView { PlayerName = _state.Name };
        _mapView.PlayerLocationChanged += OnPlayerLocationChanged;
        _mapView.WeatherChanged        += OnMapWeatherChanged;
        MapContainer.Children.Add(_mapView);
        MapPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // nothing to stop — map is in-process
    }

    // ── Druid Wild Bonus ──────────────────────────────────────────────

    /// <summary>
    /// Receives in-process player location data directly from MapView —
    /// replaces the old file-poll approach.
    /// </summary>
    private void OnPlayerLocationChanged(int node, string terrain, string locName, string locType)
    {
        _state.PlayerNode         = node;
        _state.PlayerTerrain      = terrain;
        _state.PlayerLocationName = locName;
        _state.PlayerLocationType = locType;

        bool isDruid = _state.CoreAbility.Equals("Druid", StringComparison.OrdinalIgnoreCase);
        // Bonus active when outdoors (no location type on the current hex).
        // A hex with a named location that has a LocationType set (e.g. "City", "Ruin")
        // is considered indoors/civilised and removes the bonus.
        _state.DruidWildBonusActive =
            isDruid && node >= 0 && string.IsNullOrEmpty(locType);

        UpdateMountaineerScoutBonus(terrain);
    }

    /// <summary>
    /// Re-evaluates Druid wild bonus and Mountaineer Scout bonus using the latest map state.
    /// Called when CoreAbility changes so bonuses update even if the player hasn't moved.
    /// </summary>
    private void UpdatePlayerLocation()
    {
        if (_mapView is null)
        {
            _state.DruidWildBonusActive = false;
            UpdateMountaineerScoutBonus(""); // no map → terrain unknown → remove bonus
            return;
        }
        var (node, terrain, locName, locType) = _mapView.PlayerLocation;
        OnPlayerLocationChanged(node, terrain, locName, locType);
    }

    // ── Mountaineer Scout Bonus ───────────────────────────────────────

    /// <summary>
    /// Adds or removes the Mountaineer conditional Scout-15 skill based on the
    /// player's current terrain.  The skill is locked, free, and excluded from
    /// the 10-skill count and from persistence.
    /// </summary>
    private void UpdateMountaineerScoutBonus(string terrain)
    {
        bool wantsScout = IsMountaineerActive &&
                          (terrain == "Mountains" || terrain == "Cave");

        if (wantsScout && _mountaineerScoutSkill == null)
        {
            var sk = new SkillData
            {
                MinRating          = 15,
                IsAlwaysFree       = true,
                IsLocked           = true,
                IsConditionalBonus = true,
                SkillName          = "Scout",  // triggers auto-promote → clamped to MinRating 15
            };
            sk.PropertyChanged += OnItemChanged;
            _mountaineerScoutSkill = sk;
            _state.Skills.Add(sk);
        }
        else if (!wantsScout && _mountaineerScoutSkill != null)
        {
            var sk = _mountaineerScoutSkill;
            _mountaineerScoutSkill = null;
            sk.PropertyChanged -= OnItemChanged;
            _state.Skills.Remove(sk);
        }
    }

    // ── Weather Skill Penalties ───────────────────────────────────────

    private static readonly System.Collections.Generic.HashSet<string> WeatherAffectedSkills =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Breakfall", "Climber", "Forage", "Hunting", "Investigate",
            "Scout", "Tracking", "Cartographer", "Traps",
        };

    private static int WeatherPenaltyFor(string weather) => weather switch
    {
        "Misty"                    => -2,
        "Heavy Mist"               => -3,
        "Rainy"                    => -4,
        "Extremely Warm and Rainy" => -4,
        "Heavy Rain"               => -5,
        "Tropical Storm"           => -10,
        _                          => 0,
    };

    private void OnMapWeatherChanged(string weather)
    {
        _currentMapWeather = weather;
        ApplyWeatherPenalties();
        UpdateWeatherParticles(weather);
    }

    // ── Weather particles ──────────────────────────────────────────────

    private static int MistDensityFor(string weather) => weather switch
    {
        "Misty"                    => 1,
        "Heavy Mist"               => 3,
        "Rainy"                    => 1,
        "Sunny with Rain Showers"  => 1,
        "Extremely Warm and Rainy" => 1,
        "Heavy Rain"               => 4,
        "Tropical Storm"           => 6,
        _                          => 0,
    };

    private static int DrizzleDensityFor(string weather) => weather switch
    {
        "Rainy"                    => 2,
        "Sunny with Rain Showers"  => 1,
        "Extremely Warm and Rainy" => 2,
        "Heavy Rain"               => 3,
        "Tropical Storm"           => 4,
        _                          => 0,
    };

    private void UpdateWeatherParticles(string weather)
    {
        _mistTimer?.Stop();          _mistTimer = null;
        _drizzleTimer?.Stop();       _drizzleTimer = null;
        _drizzleRenderTimer?.Stop(); _drizzleRenderTimer = null;
        _activeDrizzles.Clear();
        _activeMistCount = 0;
        WeatherCanvas.Children.Clear();

        int mistDensity = MistDensityFor(weather);
        if (mistDensity > 0)
        {
            // Misty → ~480 ms per drop, Tropical Storm → ~130 ms
            int intervalMs = Math.Max(130, 550 - mistDensity * 70);
            _mistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
            _mistTimer.Tick += (_, _) => SpawnDrop(mistDensity);
            _mistTimer.Start();
        }

        int drizzleDensity = DrizzleDensityFor(weather);
        if (drizzleDensity > 0)
        {
            // Spawn timer: Density 1 → 800 ms, Density 2 → 400 ms
            int spawnMs = Math.Max(400, 1400 - drizzleDensity * 500);
            _drizzleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(spawnMs) };
            _drizzleTimer.Tick += (_, _) => SpawnDrizzle(drizzleDensity);
            _drizzleTimer.Start();

            // Shared 30-fps render loop rebuilds every trail's shape each tick
            _drizzleRenderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _drizzleRenderTimer.Tick += (_, _) => UpdateDrizzles();
            _drizzleRenderTimer.Start();
        }
    }

    private void SpawnDrop(int density)
    {
        var canvas = WeatherCanvas;
        double cw = canvas.ActualWidth  > 10 ? canvas.ActualWidth  : SheetContent.ActualWidth;
        double ch = canvas.ActualHeight > 10 ? canvas.ActualHeight : SheetContent.ActualHeight;
        if (cw < 10) return;

        int maxDrops = density * 6 + 4;
        if (_activeMistCount >= maxDrops) return;

        var    rng    = _particleRng;
        double dw     = rng.NextDouble() * 16 + 12;             // 12–28 px wide
        double dh     = dw * (rng.NextDouble() * 0.30 + 1.05);  // 1.05–1.35× tall
        double sw     = dw * (rng.NextDouble() * 0.40 + 1.45);  // wet spot 1.45–1.85× drop width
        double sh     = dh * (rng.NextDouble() * 0.30 + 1.22);  // wet spot 1.22–1.52× drop height
        double spread = 1.28 + rng.NextDouble() * 0.22;          // spread factor during absorption

        // ── Wet-paper patch ───────────────────────────────────────────────────
        var wetFill = new RadialGradientBrush
            { GradientOrigin = new Point(0.5,0.5), Center = new Point(0.5,0.5), RadiusX = 0.5, RadiusY = 0.5 };
        wetFill.GradientStops.Add(new GradientStop(Color.FromArgb(210,  82, 54, 16), 0.00));
        wetFill.GradientStops.Add(new GradientStop(Color.FromArgb(145, 108, 74, 26), 0.38));
        wetFill.GradientStops.Add(new GradientStop(Color.FromArgb( 65, 132, 96, 40), 0.70));
        wetFill.GradientStops.Add(new GradientStop(Color.FromArgb(  0, 152,116, 52), 1.00));

        var wetSpot = new System.Windows.Shapes.Ellipse
        {
            Width = sw, Height = sh, Fill = wetFill, Opacity = 0,
            IsHitTestVisible      = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = new ScaleTransform(1.0, 1.0),
        };

        // ── Drop body ─────────────────────────────────────────────────────────
        var bodyFill = new RadialGradientBrush
            { GradientOrigin = new Point(0.32,0.27), Center = new Point(0.5,0.5), RadiusX = 0.55, RadiusY = 0.55 };
        bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb(215, 245, 252, 255), 0.00));
        bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb(110, 185, 222, 245), 0.28));
        bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb(140, 125, 178, 220), 0.65));
        bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb(210,  72, 122, 192), 0.90));
        bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb(180,  50,  95, 165), 1.00));

        var body = new System.Windows.Shapes.Ellipse
        {
            Width = dw, Height = dh, Fill = bodyFill,
            Effect = new DropShadowEffect
                { Color = Color.FromRgb(55,35,15), Direction = 295, ShadowDepth = 2.5, BlurRadius = 5.0, Opacity = 0.28 },
        };

        var highlight = new System.Windows.Shapes.Ellipse
        {
            Width = dw * 0.38, Height = dh * 0.25,
            Fill  = new SolidColorBrush(Color.FromArgb(195, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top,
            Margin              = new Thickness(dw * 0.13, dh * 0.09, 0, 0),
            IsHitTestVisible    = false,
        };

        // inner grid: body + highlight, centred in root
        var inner = new Grid
        {
            Width = dw, Height = dh, Opacity = 0,
            HorizontalAlignment   = HorizontalAlignment.Center,
            VerticalAlignment     = VerticalAlignment.Center,
            IsHitTestVisible      = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = new ScaleTransform(1.0, 1.0),
        };
        inner.Children.Add(body);
        inner.Children.Add(highlight);

        // root: wet spot fills it, drop is centred inside
        var root = new Grid { Width = sw, Height = sh, IsHitTestVisible = false };
        root.Children.Add(wetSpot);
        root.Children.Add(inner);

        double dropX = rng.NextDouble() * Math.Max(1, cw - dw);
        double dropY = rng.NextDouble() * Math.Max(1, ch - dh);
        Canvas.SetLeft(root, dropX - (sw - dw) / 2.0);
        Canvas.SetTop (root, dropY - (sh - dh) / 2.0);
        canvas.Children.Add(root);
        _activeMistCount++;

        // ── Timeline ──────────────────────────────────────────────────────────
        // t0──fadeIn──t1──holdDur──t2──absorbDur──t3──dryDur──t4
        //   drop/wet appear   hold     drop shrinks     wet fades
        //                              wet spreads
        double fadeIn    = 0.30 + rng.NextDouble() * 0.20;
        double holdDur   = 0.35 + rng.NextDouble() * 0.35;
        double absorbDur = 0.50 + rng.NextDouble() * 0.30;
        double dryDur    = 0.75 + rng.NextDouble() * 0.40;
        double t1 = fadeIn;
        double t2 = t1 + holdDur;
        double t3 = t2 + absorbDur;
        double t4 = t3 + dryDur;

        double dropPeak = 0.42 + density * 0.04 + rng.NextDouble() * 0.08;
        double wetPeak  = 0.50 + density * 0.04 + rng.NextDouble() * 0.08;

        // Local helpers — create fresh easing instances each call to avoid freeze conflicts
        DoubleAnimationUsingKeyFrames WetScale()
        {
            var a = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            var e = new SineEase { EasingMode = EasingMode.EaseInOut };
            a.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,    KeyTime.FromTimeSpan(TimeSpan.Zero)));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t2))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(spread, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t3)), e));
            return a;
        }
        DoubleAnimationUsingKeyFrames DropScale()
        {
            var a = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            var e = new CubicEase { EasingMode = EasingMode.EaseIn };
            a.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t2))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t3)), e));
            return a;
        }

        // ── Drop: fade in → hold → shrink to zero (absorbed into paper) ───────
        var dropOp = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        dropOp.KeyFrames.Add(new EasingDoubleKeyFrame(0,        KeyTime.FromTimeSpan(TimeSpan.Zero)));
        dropOp.KeyFrames.Add(new EasingDoubleKeyFrame(dropPeak, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t1)),
                             new SineEase { EasingMode = EasingMode.EaseInOut }));
        dropOp.KeyFrames.Add(new EasingDoubleKeyFrame(dropPeak, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t3))));
        inner.BeginAnimation(UIElement.OpacityProperty, dropOp);

        var dst = (ScaleTransform)inner.RenderTransform;
        dst.BeginAnimation(ScaleTransform.ScaleXProperty, DropScale());
        dst.BeginAnimation(ScaleTransform.ScaleYProperty, DropScale());

        // ── Wet spot: appear → hold → spread outward → fade (paper dries) ─────
        var wetOp = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        wetOp.KeyFrames.Add(new EasingDoubleKeyFrame(0,       KeyTime.FromTimeSpan(TimeSpan.Zero)));
        wetOp.KeyFrames.Add(new EasingDoubleKeyFrame(wetPeak, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t1)),
                            new SineEase { EasingMode = EasingMode.EaseInOut }));
        wetOp.KeyFrames.Add(new EasingDoubleKeyFrame(wetPeak, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t3))));
        wetOp.KeyFrames.Add(new EasingDoubleKeyFrame(0,       KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t4)),
                            new SineEase { EasingMode = EasingMode.EaseInOut }));
        wetOp.Completed += (_, _) => { canvas.Children.Remove(root); _activeMistCount--; };
        wetSpot.BeginAnimation(UIElement.OpacityProperty, wetOp);

        var wst = (ScaleTransform)wetSpot.RenderTransform;
        wst.BeginAnimation(ScaleTransform.ScaleXProperty, WetScale());
        wst.BeginAnimation(ScaleTransform.ScaleYProperty, WetScale());
    }


    // Drizzle state: a rolling water droplet + wet trail that moves with it.
    // The drizzle trail (teardrop) is a SHORT MOVING SEGMENT of water.
    // The wet trail (thin amber stroke) is the damp paper mark immediately above it.
    // Both scroll down together as one unit; the wet trail fades from the top after landing.
    private sealed class DrizzleState
    {
        public required Canvas                      Container;    // WetPath / WaterPath / GlintPath
        public required Canvas                      TrailCanvas;  // WetTrailPath (sibling on canvas)
        public required System.Windows.Shapes.Path WetPath;
        public required System.Windows.Shapes.Path WaterPath;
        public required System.Windows.Shapes.Path GlintPath;
        public required System.Windows.Shapes.Path WetTrailPath;
        public double OriginY;
        public double TotalDist;
        public double LeanX;
        public double PeakX;
        public bool   IsArc;
        public double Progress;    // 0 to 1
        public double Speed;
        public double MaxHW;
        public double BotExt;
        public double TrailLen;    // drizzle segment length (progress units, e.g. 0.30)
        public double LandingPTail; // pTail frozen at landing, used during fade
        public bool   IsFading;
        public double FadeProgress;
        public double FadeSpeed;
    }

    private static Point PathPt(DrizzleState d, double p)
    {
        double y = p * d.TotalDist;
        double x = d.IsArc ? d.PeakX * Math.Sin(Math.PI * p) : d.LeanX * p;
        return new Point(x, y);
    }

    private void SpawnDrizzle(int density)
    {
        var canvas = WeatherCanvas;
        double cw = canvas.ActualWidth  > 10 ? canvas.ActualWidth  : SheetContent.ActualWidth;
        double ch = canvas.ActualHeight > 10 ? canvas.ActualHeight : SheetContent.ActualHeight;
        if (cw < 10 || ch < 10) return;

        int maxDrizzles = density * 2 + 2;
        if (_activeDrizzles.Count >= maxDrizzles) return;

        var rng = _particleRng;

        int    pathType  = _drizzleWaveToggle++ % 3;
        double totalDist = 130 + rng.NextDouble() * 130;
        double speed     = 0.25 + rng.NextDouble() * 0.18;
        double maxHW     = 7 + rng.NextDouble() * 5;
        double botExt    = maxHW * (0.65 + rng.NextDouble() * 0.30);
        double trailLen  = 0.28 + rng.NextDouble() * 0.15;

        double sign  = rng.NextDouble() < 0.5 ? -1 : 1;
        bool   isArc = pathType == 2;
        double leanX = pathType == 1 ? sign * (22 + rng.NextDouble() * 30) : 0;
        double peakX = pathType == 2 ? sign * (28 + rng.NextDouble() * 28) : 0;

        static LinearGradientBrush VGrad(params (double off, byte a, byte r, byte g, byte b)[] stops)
        {
            var b = new LinearGradientBrush
            {
                StartPoint  = new Point(0.5, 0),
                EndPoint    = new Point(0.5, 1),
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
            };
            foreach (var (off, a, r, g, bv) in stops)
                b.GradientStops.Add(new GradientStop(Color.FromArgb(a, r, g, bv), off));
            return b;
        }

        var wetPath = new System.Windows.Shapes.Path
        {
            Fill             = VGrad(
                (0.00,  0, 88, 60, 25),
                (0.28, 38, 88, 60, 25),
                (0.62, 68, 98, 70, 30),
                (0.88, 52, 88, 60, 25),
                (1.00, 28, 88, 60, 25)),
            IsHitTestVisible = false,
        };

        var waterPath = new System.Windows.Shapes.Path
        {
            Fill             = VGrad(
                (0.00,   0, 192, 215, 232),
                (0.28,  55, 192, 215, 232),
                (0.62, 130, 198, 220, 238),
                (0.88, 175, 205, 228, 244),
                (1.00, 200, 215, 235, 252)),
            IsHitTestVisible = false,
            Effect = new DropShadowEffect
            {
                Color       = Color.FromRgb(70, 130, 200),
                ShadowDepth = 2.0,
                BlurRadius  = 5.0,
                Opacity     = 0.50,
            },
        };

        var glintPath = new System.Windows.Shapes.Path
        {
            Fill             = VGrad(
                (0.00,   0, 255, 255, 255),
                (0.25,  95, 248, 252, 255),
                (0.60, 215, 255, 255, 255),
                (0.88, 185, 255, 255, 255),
                (1.00, 160, 240, 250, 255)),
            IsHitTestVisible = false,
        };

        // Thin amber stroke: damp paper where the water has already been.
        var wetTrailPath = new System.Windows.Shapes.Path
        {
            Stroke             = new SolidColorBrush(Color.FromArgb(95, 90, 58, 18)),
            StrokeThickness    = 2.5 + maxHW * 0.22,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap   = PenLineCap.Round,
            StrokeLineJoin     = PenLineJoin.Round,
            IsHitTestVisible   = false,
        };

        var container   = new Canvas { IsHitTestVisible = false, Opacity = 0 };
        container.Children.Add(wetPath);
        container.Children.Add(waterPath);
        container.Children.Add(glintPath);

        var trailCanvas = new Canvas { IsHitTestVisible = false, Opacity = 0 };
        trailCanvas.Children.Add(wetTrailPath);

        double startX = rng.NextDouble() * cw;
        double startY = rng.NextDouble() * ch * 0.65;
        Canvas.SetLeft(trailCanvas, startX);
        Canvas.SetTop (trailCanvas, startY);
        Canvas.SetLeft(container,   startX);
        Canvas.SetTop (container,   startY);
        canvas.Children.Add(trailCanvas);   // add trail first so it renders behind teardrop
        canvas.Children.Add(container);

        _activeDrizzles.Add(new DrizzleState
        {
            Container    = container,
            TrailCanvas  = trailCanvas,
            WetPath      = wetPath,
            WaterPath    = waterPath,
            GlintPath    = glintPath,
            WetTrailPath = wetTrailPath,
            OriginY      = startY,
            TotalDist    = totalDist,
            LeanX        = leanX,
            PeakX        = peakX,
            IsArc        = isArc,
            Progress     = 0,
            Speed        = speed,
            MaxHW        = maxHW,
            BotExt       = botExt,
            TrailLen     = trailLen,
            FadeSpeed    = 0.40 + rng.NextDouble() * 0.22,
        });
    }

    // Spawns a spreading wet-paper mark on the canvas at (cx, cy) when a drizzle lands.
    private void SpawnWetSpot(double cx, double cy, double hw)
    {
        var canvas = WeatherCanvas;
        var rng    = _particleRng;

        double sw = hw * 3.0 + 10;
        double sh = sw * (0.50 + rng.NextDouble() * 0.22);

        var fill = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center         = new Point(0.5, 0.5),
            RadiusX        = 0.5,
            RadiusY        = 0.5,
        };
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(160,  82,  54, 16), 0.00));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(105, 108,  74, 26), 0.40));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb( 48, 132,  96, 40), 0.72));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(  0, 152, 116, 52), 1.00));

        var spot = new System.Windows.Shapes.Ellipse
        {
            Width                 = sw,
            Height                = sh,
            Fill                  = fill,
            Opacity               = 0,
            IsHitTestVisible      = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = new ScaleTransform(1.0, 1.0),
        };
        Canvas.SetLeft(spot, cx - sw / 2);
        Canvas.SetTop (spot, cy - sh * 0.2);
        canvas.Children.Add(spot);

        double spread  = 1.28 + rng.NextDouble() * 0.24;
        double wetPeak = 0.36 + rng.NextDouble() * 0.10;
        var    ease    = new SineEase { EasingMode = EasingMode.EaseInOut };

        static KeyTime KT(double s) => KeyTime.FromTimeSpan(TimeSpan.FromSeconds(s));

        var opAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        opAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0,       KT(0.00), ease));
        opAnim.KeyFrames.Add(new EasingDoubleKeyFrame(wetPeak, KT(0.28), ease));
        opAnim.KeyFrames.Add(new EasingDoubleKeyFrame(wetPeak, KT(1.20)));
        opAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0,       KT(2.30), ease));
        opAnim.Completed += (_, _) => canvas.Children.Remove(spot);
        spot.BeginAnimation(UIElement.OpacityProperty, opAnim);

        var spreadAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
        spreadAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0,    KT(0.00)));
        spreadAnim.KeyFrames.Add(new EasingDoubleKeyFrame(spread, KT(1.20), ease));
        var st = (ScaleTransform)spot.RenderTransform;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, spreadAnim);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, spreadAnim);
    }

    private void UpdateDrizzles()
    {
        const double dt = 1.0 / 30.0;
        var canvas = WeatherCanvas;
        double ch  = canvas.ActualHeight > 10 ? canvas.ActualHeight : SheetContent.ActualHeight;

        for (int i = _activeDrizzles.Count - 1; i >= 0; i--)
        {
            var d = _activeDrizzles[i];

            // Smooth bezier stroke from clipStart to clipEnd along the path curve.
            StreamGeometry TrailStroke(double clipStart, double clipEnd)
            {
                var geo = new StreamGeometry();
                if (clipEnd - clipStart < 0.005) { geo.Freeze(); return geo; }
                const int segs = 10;
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(PathPt(d, clipStart), isFilled: false, isClosed: false);
                    var pts = new Point[segs * 2];
                    double span = clipEnd - clipStart;
                    for (int j = 0; j < segs; j++)
                    {
                        pts[j * 2]     = PathPt(d, clipStart + span * (j + 0.5) / segs);
                        pts[j * 2 + 1] = PathPt(d, clipStart + span * (j + 1.0) / segs);
                    }
                    ctx.PolyQuadraticBezierTo(pts, isStroked: true, isSmoothJoin: true);
                }
                geo.Freeze();
                return geo;
            }

            // ── Fading phase: wet trail clips from the tail downward, then disappears ─
            if (d.IsFading)
            {
                d.FadeProgress += d.FadeSpeed * dt;
                if (d.FadeProgress >= 1.0)
                {
                    canvas.Children.Remove(d.TrailCanvas);
                    _activeDrizzles.RemoveAt(i);
                    continue;
                }
                d.TrailCanvas.Opacity = Math.Max(0, 1.0 - d.FadeProgress);
                double clipStart = d.FadeProgress * d.LandingPTail;
                d.WetTrailPath.Data = TrailStroke(clipStart, d.LandingPTail);
                continue;
            }

            // ── Moving phase ──────────────────────────────────────────────────────────
            d.Progress += d.Speed * dt;
            var head = PathPt(d, Math.Min(d.Progress, 1.0));

            if (d.Progress >= 1.0 || d.OriginY + head.Y + d.BotExt > ch)
            {
                // Landing: wet spot at endpoint, teardrop removed, wet trail fades from top.
                SpawnWetSpot(Canvas.GetLeft(d.Container) + head.X,
                             Canvas.GetTop (d.Container) + head.Y,
                             d.MaxHW);
                canvas.Children.Remove(d.Container);
                d.IsFading      = true;
                d.FadeProgress  = 0;
                d.LandingPTail  = Math.Max(0.0, Math.Min(d.Progress, 1.0) - d.TrailLen);
                d.WetTrailPath.Data = TrailStroke(0.0, d.LandingPTail);
                continue;
            }

            // pTail is the upper boundary of the drizzle segment / lower boundary of wet trail.
            // While Progress < TrailLen the drizzle is still growing in; pTail stays at 0.
            double pTail = Math.Max(0.0, d.Progress - d.TrailLen);

            double op = d.Progress < 0.08 ? d.Progress / 0.08 : 1.0;
            d.Container.Opacity   = op;
            d.TrailCanvas.Opacity = op * 0.82;

            // Teardrop drawn from pTail to Progress (a fixed-length moving window).
            StreamGeometry MakeTeardrop(double hw, double xOff, double ext)
            {
                const int n    = 12;
                const int arcN =  9;
                double seg = d.Progress - pTail;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var tail = PathPt(d, pTail);
                    ctx.BeginFigure(new Point(tail.X + xOff, tail.Y), isFilled: true, isClosed: true);

                    var right = new Point[n];
                    for (int j = 1; j <= n; j++)
                    {
                        double p = pTail + seg * j / n;
                        var  c = PathPt(d, p);
                        right[j - 1] = new Point(c.X + xOff + hw * Math.Pow((double)j / n, 0.35), c.Y);
                    }
                    ctx.PolyLineTo(right, isStroked: false, isSmoothJoin: true);

                    var arc = new Point[arcN];
                    for (int k = 0; k < arcN; k++)
                    {
                        double angle = Math.PI * (k + 1) / (arcN + 1);
                        arc[k] = new Point(head.X + xOff + hw * Math.Cos(angle),
                                           head.Y + ext  * Math.Sin(angle));
                    }
                    ctx.PolyLineTo(arc, isStroked: false, isSmoothJoin: true);

                    var left = new Point[n + 1];
                    for (int j = n; j >= 0; j--)
                    {
                        double p = pTail + seg * j / n;
                        var  c = PathPt(d, p);
                        left[n - j] = new Point(c.X + xOff - hw * Math.Pow((double)j / n, 0.35), c.Y);
                    }
                    ctx.PolyLineTo(left, isStroked: false, isSmoothJoin: true);
                }
                geo.Freeze();
                return geo;
            }

            double hw2 = d.MaxHW;
            d.WetPath.Data      = MakeTeardrop(hw2 + 5,           0,               d.BotExt + 3);
            d.WaterPath.Data    = MakeTeardrop(hw2,                0,               d.BotExt);
            d.GlintPath.Data    = MakeTeardrop(hw2 * 0.32, -hw2 * 0.25, d.BotExt * 0.55);
            d.WetTrailPath.Data = TrailStroke(0.0, pTail);
        }
    }

    private void ApplyWeatherPenalties()
    {
        int penalty = _state.WeatherEffectsEnabled
            ? WeatherPenaltyFor(_currentMapWeather)
            : 0;

        foreach (var sk in _state.Skills)
            sk.WeatherPenalty = sk.HasSkill && WeatherAffectedSkills.Contains(sk.SkillName)
                ? penalty : 0;
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
