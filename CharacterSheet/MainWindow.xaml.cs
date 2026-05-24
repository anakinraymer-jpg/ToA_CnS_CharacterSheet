using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        WireEvents();
    }

    // ── Load / Populate ───────────────────────────────────────────────
    private void LoadState(CharacterState state)
    {
        _loading = true;
        _state = state;

        TbName.Text     = state.Name;
        TbLineage.Text  = state.Lineage;
        TbHometown.Text = state.Hometown;
        TbFlaws.Text    = state.Flaws;
        TbSumma.Text    = state.Summa;

        TbSpell0.Text = state.Spells.Count > 0 ? state.Spells[0] : "";
        TbSpell1.Text = state.Spells.Count > 1 ? state.Spells[1] : "";
        TbSpell2.Text = state.Spells.Count > 2 ? state.Spells[2] : "";

        if (!string.IsNullOrEmpty(state.Portrait))
            ShowPortrait(state.Portrait);
        else
            ClearPortrait();

        RowsControl.ItemsSource = state.Rows;

        // Listen for property changes on each row to trigger auto-save
        foreach (var row in state.Rows)
            row.PropertyChanged += OnRowChanged;

        _loading = false;
    }

    // ── Event wiring ──────────────────────────────────────────────────
    private void WireEvents()
    {
        // Header text inputs
        TbName.TextChanged     += (_, _) => { if (!_loading) { _state.Name     = TbName.Text;     Save(); } };
        TbLineage.TextChanged  += (_, _) => { if (!_loading) { _state.Lineage  = TbLineage.Text;  Save(); } };
        TbHometown.TextChanged += (_, _) => { if (!_loading) { _state.Hometown = TbHometown.Text; Save(); } };
        TbFlaws.TextChanged    += (_, _) => { if (!_loading) { _state.Flaws    = TbFlaws.Text;    Save(); } };
        TbSumma.TextChanged    += (_, _) => { if (!_loading) { _state.Summa    = TbSumma.Text;    Save(); } };

        // Spells
        TbSpell0.TextChanged += (_, _) => { if (!_loading) { _state.Spells[0] = TbSpell0.Text; Save(); } };
        TbSpell1.TextChanged += (_, _) => { if (!_loading) { _state.Spells[1] = TbSpell1.Text; Save(); } };
        TbSpell2.TextChanged += (_, _) => { if (!_loading) { _state.Spells[2] = TbSpell2.Text; Save(); } };

        // Portrait click
        PortraitBox.MouseLeftButtonDown += OnPortraitClick;

        // Control buttons
        BtnNew.Click    += OnBtnNew;
        BtnExport.Click += OnBtnExport;
        BtnImport.Click += OnBtnImport;
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_loading) Save();
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

        var bytes  = File.ReadAllBytes(dlg.FileName);
        var b64    = Convert.ToBase64String(bytes);
        var ext    = Path.GetExtension(dlg.FileName).TrimStart('.').ToLower();
        var mime   = ext == "jpg" ? "jpeg" : ext;
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
            PortraitHint.Visibility = Visibility.Collapsed;
        }
        catch { /* bad image data — leave hint visible */ }
    }

    private void ClearPortrait()
    {
        PortraitImg.Source      = null;
        PortraitImg.Visibility  = Visibility.Collapsed;
        PortraitHint.Visibility = Visibility.Visible;
    }

    // ── Controls ──────────────────────────────────────────────────────
    private void OnBtnNew(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear this character sheet and start fresh?",
                            "New Character",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        // Detach old row listeners
        foreach (var row in _state.Rows)
            row.PropertyChanged -= OnRowChanged;

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

            foreach (var row in _state.Rows)
                row.PropertyChanged -= OnRowChanged;

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
