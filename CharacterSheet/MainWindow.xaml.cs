using System.ComponentModel;
using System.IO;
using System.Windows;
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
    private const double SheetMarginH = 20;   // horizontal margin each side
    private const double SheetMarginT = 20;   // top margin (for vertical zoom anchor)

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

        _state.PropertyChanged -= OnStateChanged;
        foreach (var row in _state.Rows)
            row.PropertyChanged -= OnRowChanged;

        _state = state;
        DataContext = _state;

        TbSpell0.Text = state.Spells.Count > 0 ? state.Spells[0] : "";
        TbSpell1.Text = state.Spells.Count > 1 ? state.Spells[1] : "";
        TbSpell2.Text = state.Spells.Count > 2 ? state.Spells[2] : "";

        if (!string.IsNullOrEmpty(state.Portrait))
            ShowPortrait(state.Portrait);
        else
            ClearPortrait();

        _state.PropertyChanged += OnStateChanged;
        foreach (var row in _state.Rows)
            row.PropertyChanged += OnRowChanged;

        _loading = false;
    }

    // ── Zoom core ─────────────────────────────────────────────────────

    private void ApplyZoom(double z)
    {
        _zoom = Math.Clamp(z, ZoomMin, ZoomMax);
        SheetScale.ScaleX = SheetScale.ScaleY = _zoom;
        TbZoom.Text = $"{(int)Math.Round(_zoom * 100)}%";
    }

    // Returns the horizontal content-coordinate of SheetContent's left edge.
    // When the scaled sheet is narrower than the viewport the Grid centres it.
    private double ContentLeft()
    {
        double scaledW = SheetWidth * _zoom + SheetMarginH * 2;
        double vw = SheetScroll.ViewportWidth;
        return vw > scaledW
            ? (vw - scaledW) / 2.0 + SheetMarginH
            : SheetMarginH;
    }

    // Zoom to newZoom keeping ptContent (in SheetContent local coords)
    // pinned under ptViewport (in ScrollViewer viewport coords).
    private void ZoomToPoint(double newZoom, Point ptViewport, Point ptContent)
    {
        double clamped = Math.Clamp(newZoom, ZoomMin, ZoomMax);
        if (Math.Abs(clamped - _zoom) < 0.001) return;

        ApplyZoom(clamped);
        SheetScroll.UpdateLayout();

        // SheetContent has Margin.Top = SheetMarginT, VerticalAlignment="Top"
        double newH = ContentLeft()  + ptContent.X * _zoom - ptViewport.X;
        double newV = SheetMarginT   + ptContent.Y * _zoom - ptViewport.Y;

        SheetScroll.ScrollToHorizontalOffset(newH);
        SheetScroll.ScrollToVerticalOffset(newV);
    }

    // Zoom keeping the viewport centre fixed.
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

    // Fit the full sheet into the window.
    private void FitToWindow()
    {
        SheetScroll.UpdateLayout();
        double vw = SheetScroll.ViewportWidth  - SheetMarginH * 2;
        double vh = SheetScroll.ViewportHeight - SheetMarginH * 2;
        if (vw <= 0 || vh <= 0) return;

        double contentH = SheetContent.ActualHeight > 0
            ? SheetContent.ActualHeight + SheetMarginT + 40   // +top margin + bottom margin
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
        var ptContent  = e.GetPosition(SheetContent);   // SheetContent local coords
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
        }
    }

    // ── Auto-save ─────────────────────────────────────────────────────
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_loading) Save();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_loading) Save();
    }

    // ── Add equipment / skill rows ────────────────────────────────────
    private void OnAddEquipClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEntryDialog(isSkill: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var row = new RowData
        {
            EquipName = dlg.EntryName,
            EquipSub  = dlg.EntryDescription,
        };
        row.PropertyChanged += OnRowChanged;
        _state.Rows.Add(row);
        Save();
    }

    private void OnAddSkillClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEntryDialog(isSkill: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var row = new RowData
        {
            SkillName = dlg.EntryName,
            SkillSub  = dlg.EntryDescription,
        };
        row.PropertyChanged += OnRowChanged;
        _state.Rows.Add(row);
        Save();
    }

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
        Save();
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
