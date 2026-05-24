using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CharacterSheet.Models;
using Microsoft.Win32;

namespace CharacterSheet;

public partial class MainWindow : Window
{
    private CharacterState _state = CharacterState.CreateDefault();
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        LoadState(Persistence.Load());
    }

    // ── Load / Populate ───────────────────────────────────────────────
    private void LoadState(CharacterState state)
    {
        _loading = true;

        // Detach listeners from old state / rows
        if (_state != state)
        {
            _state.PropertyChanged -= OnStateChanged;
            foreach (var row in _state.Rows)
                row.PropertyChanged -= OnRowChanged;
        }

        _state = state;

        // All header fields and row fields are resolved through DataContext bindings.
        DataContext = _state;

        // Spell textboxes are named controls — populate them manually.
        TbSpell0.Text = state.Spells.Count > 0 ? state.Spells[0] : "";
        TbSpell1.Text = state.Spells.Count > 1 ? state.Spells[1] : "";
        TbSpell2.Text = state.Spells.Count > 2 ? state.Spells[2] : "";

        // Portrait
        if (!string.IsNullOrEmpty(state.Portrait))
            ShowPortrait(state.Portrait);
        else
            ClearPortrait();

        // Subscribe to auto-save triggers
        _state.PropertyChanged += OnStateChanged;
        foreach (var row in _state.Rows)
            row.PropertyChanged += OnRowChanged;

        // Wire spell boxes once (first call); subsequent LoadState calls reuse them.
        TbSpell0.TextChanged -= OnSpell0Changed;
        TbSpell1.TextChanged -= OnSpell1Changed;
        TbSpell2.TextChanged -= OnSpell2Changed;
        TbSpell0.TextChanged += OnSpell0Changed;
        TbSpell1.TextChanged += OnSpell1Changed;
        TbSpell2.TextChanged += OnSpell2Changed;

        // Portrait click — wire once
        PortraitBox.MouseLeftButtonDown -= OnPortraitClick;
        PortraitBox.MouseLeftButtonDown += OnPortraitClick;

        // Control buttons — wire once
        BtnNew.Click    -= OnBtnNew;
        BtnExport.Click -= OnBtnExport;
        BtnImport.Click -= OnBtnImport;
        BtnNew.Click    += OnBtnNew;
        BtnExport.Click += OnBtnExport;
        BtnImport.Click += OnBtnImport;

        _loading = false;
    }

    // ── Auto-save callbacks ───────────────────────────────────────────
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        // "Portrait" changes are handled separately; everything else auto-saves.
        if (!_loading) Save();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_loading) Save();
    }

    private void OnSpell0Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loading) { _state.Spells[0] = TbSpell0.Text; Save(); }
    }
    private void OnSpell1Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loading) { _state.Spells[1] = TbSpell1.Text; Save(); }
    }
    private void OnSpell2Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loading) { _state.Spells[2] = TbSpell2.Text; Save(); }
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
        catch { /* bad image data — leave portrait hidden */ }
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

        var fresh = CharacterState.CreateDefault();
        LoadState(fresh);
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

    // ── Auto-save ─────────────────────────────────────────────────────
    private void Save() => Persistence.Save(_state);
}
