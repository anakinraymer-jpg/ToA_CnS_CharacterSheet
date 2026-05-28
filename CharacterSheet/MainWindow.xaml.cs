using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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

    public MainWindow()
    {
        InitializeComponent();
        WireUiEvents();
        LoadState(Persistence.Load());
        Loaded += (_, _) => FitToWindow();
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
        BtnZoomFit.Click += (_, _) => FitToWindow();

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

        // Recompute armor bonus from loaded equipment (no save triggered by this)
        RecomputeArmorBonus();

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
                    _state.HeroPointsCurrent -= sk.SkillRating;
            }

        if (e.OldItems != null)
            foreach (SkillData sk in e.OldItems)
            {
                sk.PropertyChanged -= OnSkillDataChanged;

                if (!_loading && _prevSkillRating.TryGetValue(sk, out int prev) && prev > 0)
                    _state.HeroPointsCurrent += prev;

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
                    int cost = sk.SkillRating;   // auto-promoted to 3 by the model
                    _state.HeroPointsCurrent -= cost;
                    _prevSkillRating[sk]      = cost;
                }
                else if (hadSkill && !hasSkill)
                {
                    _state.HeroPointsCurrent += prevRating;
                    _prevSkillRating[sk]      = 0;
                }

                _state.RefreshSelectedSkillNames();
                break;
            }

            case nameof(SkillData.SkillRating):
            {
                int prevRating = _prevSkillRating.TryGetValue(sk, out int pr) ? pr : 0;
                int newRating  = sk.SkillRating;
                int delta      = newRating - prevRating;
                if (delta != 0)
                {
                    _state.HeroPointsCurrent -= delta;
                    _prevSkillRating[sk]      = newRating;
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
            _state.HeroPointsCurrent -= 15;   // acquiring a Core Ability costs 15
        else if (!wasEmpty && nowEmpty)
            _state.HeroPointsCurrent += 15;   // full refund on removal
        // non-empty → different non-empty (rename): no change

        prev = next;
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
}
