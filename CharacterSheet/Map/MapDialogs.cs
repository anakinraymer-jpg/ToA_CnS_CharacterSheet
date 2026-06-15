using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CharacterSheet.Map;

// ────────────────────────────────────────────────────────────────────────────
// Shared helpers
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Grid settings value bag used by MapGridSettingsDialog.SettingsChanged.</summary>
public record GridSettings(
    double Size,
    (double X, double Y) Origin,
    string Orientation,
    int Cols,
    int Rows);

/// <summary>Base class providing the shared dark-green dialog style.</summary>
public abstract class MapDialog : Window
{
    private static readonly Brush DlgBg  = new SolidColorBrush(Color.FromRgb(13, 19, 9));
    private static readonly Brush DlgFg  = new SolidColorBrush(Color.FromRgb(176, 200, 160));
    private static readonly Brush BtnBg  = new SolidColorBrush(Color.FromRgb(26, 46, 20));
    private static readonly Brush BtnBdr = new SolidColorBrush(Color.FromRgb(58, 90, 42));
    private static readonly Brush BoxBg  = new SolidColorBrush(Color.FromRgb(13, 26, 11));

    protected MapDialog(string title, Window? owner, double width = 340, double height = 200)
    {
        Title               = title;
        Owner               = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent       = SizeToContent.Height;
        Width               = width;
        MinHeight           = height;
        Background          = DlgBg;
        Foreground          = DlgFg;
        ResizeMode          = ResizeMode.NoResize;
        ShowInTaskbar       = false;
    }

    protected static TextBlock Label(string text, bool bold = false) => new()
    {
        Text       = text,
        Foreground = DlgFg,
        FontSize   = 11,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Margin     = new Thickness(0, 0, 0, 2),
    };

    protected static TextBox MkTextBox(string init = "") => new()
    {
        Text             = init,
        Background       = BoxBg,
        Foreground       = DlgFg,
        BorderBrush      = BtnBdr,
        BorderThickness  = new Thickness(1),
        Padding          = new Thickness(4, 2, 4, 2),
        FontSize         = 11,
        CaretBrush       = DlgFg,
        Margin           = new Thickness(0, 0, 0, 6),
    };

    protected static CheckBox Check(string text, bool isChecked = false) => new()
    {
        Content    = text,
        IsChecked  = isChecked,
        Foreground = DlgFg,
        FontSize   = 11,
        Margin     = new Thickness(0, 0, 0, 6),
    };

    protected Button OkButton(string text = "OK")
    {
        var btn = Btn(text);
        btn.IsDefault = true;
        btn.Click    += (_, _) => { DialogResult = true; };
        return btn;
    }

    protected Button CancelButton()
    {
        var btn = Btn("Cancel");
        btn.IsCancel = true;
        btn.Click   += (_, _) => { DialogResult = false; };
        return btn;
    }

    protected static Button Btn(string text, int padding = 6) => new()
    {
        Content         = text,
        Background      = BtnBg,
        Foreground      = DlgFg,
        BorderBrush     = BtnBdr,
        BorderThickness = new Thickness(1),
        Padding         = new Thickness(padding, 3, padding, 3),
        FontSize        = 11,
        MinWidth        = 60,
        Margin          = new Thickness(2),
    };

    protected static ComboBox Combo(IEnumerable<string> items, string? selected = null)
    {
        var cb = new ComboBox
        {
            Background      = BoxBg,
            Foreground      = DlgFg,
            BorderBrush     = BtnBdr,
            BorderThickness = new Thickness(1),
            FontSize        = 11,
            Margin          = new Thickness(0, 0, 0, 6),
        };
        foreach (var item in items) cb.Items.Add(item);
        if (selected != null)
        {
            int idx = cb.Items.IndexOf(selected);
            cb.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else cb.SelectedIndex = 0;
        return cb;
    }

    protected static ListBox MapList(double height = 100)
    {
        var lb = new ListBox
        {
            Background      = BoxBg,
            Foreground      = DlgFg,
            BorderBrush     = BtnBdr,
            BorderThickness = new Thickness(1),
            FontSize        = 11,
            Height          = height,
            Margin          = new Thickness(0, 0, 0, 6),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(lb, ScrollBarVisibility.Disabled);
        return lb;
    }

    protected static IntegerBox IntInput(int value, int min = 1, int max = int.MaxValue) =>
        new(value, min, max);

    protected static DoubleBox DblInput(double value, double min = 0) =>
        new(value, min);

    protected static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 0, 0, 6),
        };
        foreach (var c in children) row.Children.Add(c);
        return row;
    }

    protected static StackPanel Col(params UIElement[] children)
    {
        var col = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var c in children) col.Children.Add(c);
        return col;
    }

    protected static Separator HSep() => new() { Margin = new Thickness(0, 4, 0, 4) };
}

// ── Small helper controls ────────────────────────────────────────────────────

/// <summary>TextBox that only accepts integer values.</summary>
public class IntegerBox : TextBox
{
    public int Min { get; }
    public int Max { get; }

    public int IntValue
    {
        get => int.TryParse(Text, out var v) ? Math.Clamp(v, Min, Max) : Min;
        set => Text = value.ToString();
    }

    public IntegerBox(int value = 1, int min = 1, int max = int.MaxValue)
    {
        Min             = min;
        Max             = max;
        Text            = value.ToString();
        Background      = new SolidColorBrush(Color.FromRgb(13, 26, 11));
        Foreground      = new SolidColorBrush(Color.FromRgb(176, 200, 160));
        BorderBrush     = new SolidColorBrush(Color.FromRgb(58, 90, 42));
        BorderThickness = new Thickness(1);
        Padding         = new Thickness(4, 2, 4, 2);
        FontSize        = 11;
        Width           = 70;
        Margin          = new Thickness(0, 0, 0, 6);
        CaretBrush      = Foreground;

        PreviewTextInput += (_, e) =>
        {
            e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '-');
        };
    }
}

/// <summary>TextBox that only accepts double values.</summary>
public class DoubleBox : TextBox
{
    public double Min { get; }

    public double DblValue
    {
        get => double.TryParse(Text, out var v) ? Math.Max(v, Min) : Min;
        set => Text = value.ToString("0.##");
    }

    public DoubleBox(double value = 0, double min = 0)
    {
        Min             = min;
        Text            = value.ToString("0.##");
        Background      = new SolidColorBrush(Color.FromRgb(13, 26, 11));
        Foreground      = new SolidColorBrush(Color.FromRgb(176, 200, 160));
        BorderBrush     = new SolidColorBrush(Color.FromRgb(58, 90, 42));
        BorderThickness = new Thickness(1);
        Padding         = new Thickness(4, 2, 4, 2);
        FontSize        = 11;
        Width           = 70;
        Margin          = new Thickness(0, 0, 0, 6);
        CaretBrush      = Foreground;

        PreviewTextInput += (_, e) =>
        {
            e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.' || c == '-');
        };
    }
}

// ── ColorPicker ──────────────────────────────────────────────────────────────

internal class ColorPickerBox : StackPanel
{
    private readonly TextBox    _hexBox;
    private readonly System.Windows.Shapes.Rectangle _preview;

    private static readonly string[] Presets =
    [
        "#e74c3c", "#e67e22", "#f1c40f", "#2ecc71", "#1abc9c",
        "#3498db", "#9b59b6", "#e91e63", "#ffffff", "#aaaaaa",
        "#f39c12", "#27ae60", "#16a085", "#2980b9", "#8e44ad",
    ];

    public string HexColor
    {
        get => _hexBox.Text.Trim();
        set { _hexBox.Text = value; UpdatePreview(); }
    }

    public ColorPickerBox(string initial = "#e74c3c")
    {
        Orientation = Orientation.Vertical;
        Margin      = new Thickness(0, 0, 0, 6);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _hexBox = new TextBox
        {
            Text            = initial,
            Width           = 90,
            Background      = new SolidColorBrush(Color.FromRgb(13, 26, 11)),
            Foreground      = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(58, 90, 42)),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(4, 2, 4, 2),
            FontSize        = 11,
            CaretBrush      = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            Margin          = new Thickness(0, 0, 4, 0),
        };
        _hexBox.TextChanged += (_, _) => UpdatePreview();

        _preview = new System.Windows.Shapes.Rectangle
        {
            Width   = 24,
            Height  = 24,
            Stroke  = new SolidColorBrush(Color.FromRgb(58, 90, 42)),
            StrokeThickness = 1,
        };
        row.Children.Add(_hexBox);
        row.Children.Add(_preview);
        Children.Add(row);

        var swatches = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var hex in Presets)
        {
            var h = hex;
            var swatch = new System.Windows.Shapes.Rectangle
            {
                Width           = 18,
                Height          = 18,
                Margin          = new Thickness(1),
                Cursor          = System.Windows.Input.Cursors.Hand,
                StrokeThickness = 1,
                Stroke          = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            };
            try { swatch.Fill = new SolidColorBrush(
                      (Color)ColorConverter.ConvertFromString(h)!); }
            catch { swatch.Fill = Brushes.Gray; }
            swatch.MouseDown += (_, _) => HexColor = h;
            swatches.Children.Add(swatch);
        }
        Children.Add(swatches);

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        try
        {
            _preview.Fill = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_hexBox.Text.Trim())!);
        }
        catch { _preview.Fill = Brushes.Gray; }
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapInputDialog — single-line text prompt
// ────────────────────────────────────────────────────────────────────────────

public class MapInputDialog : MapDialog
{
    private readonly TextBox _input;
    public string Value => _input.Text.Trim();

    public MapInputDialog(string title, string prompt, string defaultValue, Window? owner)
        : base(title, owner, 320, 120)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(Label(prompt));
        _input = MkTextBox(defaultValue);
        sp.Children.Add(_input);
        sp.Children.Add(Row(OkButton(), CancelButton()));
        Content = sp;
        Loaded += (_, _) => { _input.SelectAll(); _input.Focus(); };
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapEncounterDialog — Merge / Combat / Nothing
// ────────────────────────────────────────────────────────────────────────────

public class MapEncounterDialog : MapDialog
{
    public enum EncounterResult { Merge, Combat, Nothing }
    public EncounterResult Result { get; private set; } = EncounterResult.Nothing;

    public MapEncounterDialog(string entityNames, int node, Window? owner)
        : base("Encounter!", owner, 340, 140)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(Label($"Multiple entities on hex {node}:", bold: true));
        sp.Children.Add(Label(entityNames));
        sp.Children.Add(HSep());

        var mergeBtn  = Btn("Merge  ✦", 10);
        var combatBtn = Btn("Combat ⚔", 10);
        var nothingBtn = Btn("Pass",    10);
        mergeBtn.Click   += (_, _) => { Result = EncounterResult.Merge;   DialogResult = true; };
        combatBtn.Click  += (_, _) => { Result = EncounterResult.Combat;  DialogResult = true; };
        nothingBtn.Click += (_, _) => { Result = EncounterResult.Nothing; DialogResult = true; };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        row.Children.Add(mergeBtn);
        row.Children.Add(combatBtn);
        row.Children.Add(nothingBtn);
        sp.Children.Add(row);
        Content = sp;
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapItemPickDialog — pick one string from a list
// ────────────────────────────────────────────────────────────────────────────

public class MapItemPickDialog : MapDialog
{
    private readonly ListBox _list;
    public string? SelectedItem => (_list.SelectedItem as ListBoxItem)?.Content?.ToString();

    public MapItemPickDialog(string title, string prompt, IEnumerable<string> items, Window? owner)
        : base(title, owner, 320, 220)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(Label(prompt));
        _list = MapList(120);
        foreach (var item in items)
            _list.Items.Add(new ListBoxItem
            {
                Content    = item,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            });
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        sp.Children.Add(_list);
        sp.Children.Add(Row(OkButton(), CancelButton()));
        Content = sp;
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapAddEntityDialog — add a new character or group
// ────────────────────────────────────────────────────────────────────────────

public class MapAddEntityDialog : MapDialog
{
    private readonly TextBox         _name;
    private readonly IntegerBox      _node;
    private readonly ColorPickerBox  _color;
    private readonly CheckBox        _isGroup;
    private readonly CheckBox        _seafaring;

    public (string Name, int Node, string Color, bool IsGroup, bool Seafaring) Values
        => (_name.Text.Trim(), _node.IntValue, _color.HexColor,
            _isGroup.IsChecked == true, _seafaring.IsChecked == true);

    public MapAddEntityDialog(int maxNode, string playerName, Window? owner)
        : base("Add Character / Group", owner, 340, 320)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };

        sp.Children.Add(Label("Name:"));
        _name = MkTextBox();
        sp.Children.Add(_name);

        if (!string.IsNullOrEmpty(playerName))
        {
            var fillBtn = Btn($"Use character name: {playerName}", 4);
            fillBtn.Click += (_, _) => _name.Text = playerName;
            fillBtn.Margin = new Thickness(0, 0, 0, 6);
            sp.Children.Add(fillBtn);
        }

        sp.Children.Add(Label("Starting hex node:"));
        _node = IntInput(1, 1, maxNode);
        sp.Children.Add(_node);

        sp.Children.Add(Label("Color:"));
        _color = new ColorPickerBox("#e74c3c");
        sp.Children.Add(_color);

        _isGroup   = Check("This is a group",   false);
        _seafaring = Check("Seafaring",          false);
        sp.Children.Add(_isGroup);
        sp.Children.Add(_seafaring);

        sp.Children.Add(Row(OkButton("Add"), CancelButton()));
        Content = sp;
        Loaded += (_, _) => { _name.SelectAll(); _name.Focus(); };
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapEditEntityDialog — edit an individual (non-group) entity
// ────────────────────────────────────────────────────────────────────────────

public class MapEditEntityDialog : MapDialog
{
    private readonly TextBox        _name;
    private readonly ColorPickerBox _color;
    private readonly CheckBox       _seafaring;
    private readonly TextBox        _flavor;

    public (string Name, string Color, bool Seafaring, string FlavorText) Values
        => (_name.Text.Trim(), _color.HexColor,
            _seafaring.IsChecked == true, _flavor.Text.Trim());

    public MapEditEntityDialog(MapEntity entity, Window? owner)
        : base($"Edit — {entity.Name}", owner, 340, 360)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };

        sp.Children.Add(Label("Name:"));
        _name = MkTextBox(entity.Name);
        sp.Children.Add(_name);

        sp.Children.Add(Label("Color:"));
        _color = new ColorPickerBox(entity.Color);
        sp.Children.Add(_color);

        _seafaring = Check("Seafaring", entity.Seafaring);
        sp.Children.Add(_seafaring);

        sp.Children.Add(Label("Flavor text:"));
        _flavor = new TextBox
        {
            Text            = entity.FlavorText,
            TextWrapping    = TextWrapping.Wrap,
            AcceptsReturn   = true,
            Height          = 72,
            Background      = new SolidColorBrush(Color.FromRgb(13, 26, 11)),
            Foreground      = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(58, 90, 42)),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(4, 2, 4, 2),
            FontSize        = 11,
            Margin          = new Thickness(0, 0, 0, 6),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        sp.Children.Add(_flavor);

        sp.Children.Add(Row(OkButton("Save"), CancelButton()));
        Content = sp;
        Loaded += (_, _) => { _name.SelectAll(); _name.Focus(); };
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapEditGroupDialog — edit a group entity (dual-pane member editor)
// ────────────────────────────────────────────────────────────────────────────

public class MapEditGroupDialog : MapDialog
{
    private readonly TextBox  _name;
    private readonly CheckBox _seafaring;
    private readonly TextBox  _flavor;
    private readonly ListBox  _membersList;
    private readonly ListBox  _availableList;

    private readonly List<(string Id, string Name)> _members   = [];
    private readonly List<(string Id, string Name)> _available = [];

    public bool ShouldDisband { get; private set; }

    public (string Name, bool Seafaring, string FlavorText, IReadOnlyList<string> Members) Values
        => (_name.Text.Trim(), _seafaring.IsChecked == true, _flavor.Text.Trim(),
            _members.Select(m => m.Id).ToList());

    public MapEditGroupDialog(MapEntity group, MapEntityManager em, Window? owner)
        : base($"Edit Group — {group.Name}", owner, 440, 460)
    {
        // Populate member/available lists
        var memberSet = group.Members.ToHashSet();
        foreach (var mid in group.Members)
        {
            var m = em.Get(mid);
            if (m is not null) _members.Add((m.Id, m.Name));
        }
        foreach (var e in em.All)
        {
            if (!memberSet.Contains(e.Id) && !e.IsGroup && e.Id != group.Id)
                _available.Add((e.Id, e.Name));
        }

        var sp = new StackPanel { Margin = new Thickness(12) };

        sp.Children.Add(Label("Group name:"));
        _name = MkTextBox(group.Name);
        sp.Children.Add(_name);

        _seafaring = Check("Seafaring", group.Seafaring);
        sp.Children.Add(_seafaring);

        sp.Children.Add(Label("Flavor text:"));
        _flavor = new TextBox
        {
            Text            = group.FlavorText,
            TextWrapping    = TextWrapping.Wrap,
            AcceptsReturn   = true,
            Height          = 56,
            Background      = new SolidColorBrush(Color.FromRgb(13, 26, 11)),
            Foreground      = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(58, 90, 42)),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(4, 2, 4, 2),
            FontSize        = 11,
            Margin          = new Thickness(0, 0, 0, 6),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        sp.Children.Add(_flavor);

        sp.Children.Add(Label("Members  ←→  Available", bold: true));

        var dualPanel = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        dualPanel.ColumnDefinitions.Add(new ColumnDefinition());
        dualPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        dualPanel.ColumnDefinitions.Add(new ColumnDefinition());

        _membersList   = MapList(100);
        _availableList = MapList(100);
        PopulateLists();

        var btnCol = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var removeBtn = Btn("← Rem", 4);
        var addBtn    = Btn("Add →", 4);
        removeBtn.Click += OnRemoveFromGroup;
        addBtn.Click    += OnAddToGroup;
        removeBtn.Margin = new Thickness(2, 4, 2, 4);
        addBtn.Margin    = new Thickness(2, 4, 2, 4);
        btnCol.Children.Add(removeBtn);
        btnCol.Children.Add(addBtn);

        Grid.SetColumn(_membersList,   0);
        Grid.SetColumn(btnCol,         1);
        Grid.SetColumn(_availableList, 2);
        dualPanel.Children.Add(_membersList);
        dualPanel.Children.Add(btnCol);
        dualPanel.Children.Add(_availableList);
        sp.Children.Add(dualPanel);

        var disbandBtn = Btn("Disband Group", 4);
        disbandBtn.Background = new SolidColorBrush(Color.FromRgb(60, 18, 18));
        disbandBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 160));
        disbandBtn.Click += (_, _) =>
        {
            ShouldDisband = true;
            DialogResult  = true;
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 4, 0, 0),
        };
        row.Children.Add(OkButton("Save"));
        row.Children.Add(CancelButton());
        row.Children.Add(disbandBtn);
        sp.Children.Add(row);

        Content = sp;
        Loaded += (_, _) => { _name.SelectAll(); _name.Focus(); };
    }

    private void PopulateLists()
    {
        _membersList.Items.Clear();
        foreach (var (id, name) in _members)
            _membersList.Items.Add(new ListBoxItem
            {
                Content = name, Tag = id,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            });

        _availableList.Items.Clear();
        foreach (var (id, name) in _available)
            _availableList.Items.Add(new ListBoxItem
            {
                Content = name, Tag = id,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            });
    }

    private void OnRemoveFromGroup(object s, RoutedEventArgs e)
    {
        if (_membersList.SelectedItem is not ListBoxItem li) return;
        string id = (string)li.Tag!;
        var entry = _members.FirstOrDefault(m => m.Id == id);
        if (entry == default) return;
        _members.Remove(entry);
        _available.Add(entry);
        PopulateLists();
    }

    private void OnAddToGroup(object s, RoutedEventArgs e)
    {
        if (_availableList.SelectedItem is not ListBoxItem li) return;
        string id = (string)li.Tag!;
        var entry = _available.FirstOrDefault(m => m.Id == id);
        if (entry == default) return;
        _available.Remove(entry);
        _members.Add(entry);
        PopulateLists();
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapCombatDialog — select winners and fate of losers
// ────────────────────────────────────────────────────────────────────────────

public class MapCombatDialog : MapDialog
{
    private readonly List<(string Id, CheckBox Cb)> _checks = [];
    private readonly RadioButton _moveRadio;
    private readonly RadioButton _removeRadio;

    public IReadOnlyList<string> WinnerIds
        => _checks.Where(t => t.Cb.IsChecked == true).Select(t => t.Id).ToList();

    public string LoserAction => _moveRadio.IsChecked == true ? "move" : "remove";

    public MapCombatDialog(IReadOnlyList<MapEntity> entities, int node, Window? owner)
        : base($"Combat — hex {node}", owner, 340, 280)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(Label("Select winners (check those who won):", bold: true));

        foreach (var entity in entities)
        {
            var cb = Check(entity.Name, false);
            cb.Margin = new Thickness(8, 0, 0, 4);
            _checks.Add((entity.Id, cb));
            sp.Children.Add(cb);
        }

        sp.Children.Add(HSep());
        sp.Children.Add(Label("Loser fate:"));

        _moveRadio   = new RadioButton
        {
            Content    = "Move to adjacent hex",
            IsChecked  = true,
            Foreground = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            FontSize   = 11,
            Margin     = new Thickness(0, 0, 0, 4),
        };
        _removeRadio = new RadioButton
        {
            Content    = "Remove from map",
            Foreground = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            FontSize   = 11,
            Margin     = new Thickness(0, 0, 0, 6),
        };
        sp.Children.Add(_moveRadio);
        sp.Children.Add(_removeRadio);

        sp.Children.Add(Row(OkButton("Resolve"), CancelButton()));
        Content = sp;
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapAddLocationDialog — add or edit a map location
// ────────────────────────────────────────────────────────────────────────────

public class MapAddLocationDialog : MapDialog
{
    private readonly TextBox        _name;
    private readonly IntegerBox     _node;
    private readonly ComboBox       _type;
    private readonly TextBox        _desc;
    private readonly ColorPickerBox _color;

    public string InitialName  { init => _name.Text  = value; get => _name.Text;  }
    public string InitialDesc  { init => _desc.Text  = value; get => _desc.Text;  }
    public string InitialColor { init => _color.HexColor = value; get => _color.HexColor; }
    public string InitialType
    {
        init
        {
            int idx = _type.Items.IndexOf(value);
            _type.SelectedIndex = idx >= 0 ? idx : 0;
        }
        get => (_type.SelectedItem as string) ?? "";
    }

    public (string Name, int Node, string Color, string Description, string LocationType) Values
        => (_name.Text.Trim(), _node.IntValue, _color.HexColor,
            _desc.Text.Trim(), (_type.SelectedItem as string) ?? "");

    public MapAddLocationDialog(int maxNode, int defaultNode, Window? owner)
        : base("Add Location", owner, 340, 400)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };

        sp.Children.Add(Label("Name:"));
        _name = MkTextBox();
        sp.Children.Add(_name);

        sp.Children.Add(Label("Type:"));
        _type = Combo(MapLocationTypes.All.Prepend(""));
        sp.Children.Add(_type);

        sp.Children.Add(Label("Hex node:"));
        _node = IntInput(defaultNode, 1, maxNode);
        sp.Children.Add(_node);

        sp.Children.Add(Label("Description:"));
        _desc = new TextBox
        {
            TextWrapping    = TextWrapping.Wrap,
            AcceptsReturn   = true,
            Height          = 72,
            Background      = new SolidColorBrush(Color.FromRgb(13, 26, 11)),
            Foreground      = new SolidColorBrush(Color.FromRgb(176, 200, 160)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(58, 90, 42)),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(4, 2, 4, 2),
            FontSize        = 11,
            Margin          = new Thickness(0, 0, 0, 6),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        sp.Children.Add(_desc);

        sp.Children.Add(Label("Color:"));
        _color = new ColorPickerBox("#f39c12");
        sp.Children.Add(_color);

        sp.Children.Add(Row(OkButton("Add"), CancelButton()));
        Content = sp;
        Loaded += (_, _) => { _name.SelectAll(); _name.Focus(); };
    }
}

// ────────────────────────────────────────────────────────────────────────────
// MapGridSettingsDialog — live-preview hex grid configuration
// ────────────────────────────────────────────────────────────────────────────

public class MapGridSettingsDialog : MapDialog
{
    private readonly DoubleBox  _size;
    private readonly DoubleBox  _originX;
    private readonly DoubleBox  _originY;
    private readonly IntegerBox _cols;
    private readonly IntegerBox _rows;
    private readonly ComboBox   _orientation;

    /// <summary>Fires on every value change so the canvas can update its preview in real time.</summary>
    public event Action<GridSettings>? SettingsChanged;

    private GridSettings CurrentSettings => new GridSettings(
        _size.DblValue,
        (_originX.DblValue, _originY.DblValue),
        (_orientation.SelectedItem as string) ?? "flat",
        _cols.IntValue,
        _rows.IntValue);

    public MapGridSettingsDialog(HexGrid grid, Window? owner)
        : base("Grid Settings", owner, 320, 290)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };

        sp.Children.Add(Label("Hex size (px):"));
        _size = DblInput(grid.Size, 1.0);
        _size.Width = 90;
        sp.Children.Add(_size);

        sp.Children.Add(Label("Grid origin (X, Y):"));
        var originRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        _originX = DblInput(grid.Origin.X, 0);
        _originY = DblInput(grid.Origin.Y, 0);
        _originX.Width = 80;
        _originY.Width = 80;
        _originX.Margin = new Thickness(0, 0, 6, 0);
        originRow.Children.Add(_originX);
        originRow.Children.Add(_originY);
        sp.Children.Add(originRow);

        sp.Children.Add(Label("Columns × Rows:"));
        var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        _cols = IntInput(grid.Cols, 1, 200);
        _rows = IntInput(grid.Rows, 1, 200);
        _cols.Width = 70;
        _rows.Width = 70;
        _cols.Margin = new Thickness(0, 0, 6, 0);
        sizeRow.Children.Add(_cols);
        sizeRow.Children.Add(_rows);
        sp.Children.Add(sizeRow);

        sp.Children.Add(Label("Orientation:"));
        _orientation = Combo(["flat", "pointy"], grid.Orientation);
        sp.Children.Add(_orientation);

        // Wire live preview
        foreach (var tb in new TextBox[] { _size, _originX, _originY, _cols, _rows })
            tb.TextChanged += (_, _) => SettingsChanged?.Invoke(CurrentSettings);
        _orientation.SelectionChanged += (_, _) => SettingsChanged?.Invoke(CurrentSettings);

        sp.Children.Add(Row(OkButton("Apply"), CancelButton()));
        Content = sp;
    }
}
