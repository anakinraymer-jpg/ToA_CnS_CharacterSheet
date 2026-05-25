using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CharacterSheet.Data;

namespace CharacterSheet.Controls;

/// <summary>
/// A TextBox that, when focused / clicked, opens a searchable popup list.
/// <para>
/// <b>EntryKey</b> (preferred for dynamic lists): set to <c>"CoreAbility"</c> or
/// <c>"Flaw"</c> to resolve the list from <see cref="CustomEntryStore"/> at
/// open-time.  Also shows a "＋ Add custom…" button in the popup.
/// </para>
/// <para>
/// <b>EntrySource</b> (static lists): set to any <c>IReadOnlyList&lt;SkillEntry&gt;</c>
/// for a fixed list without custom-creation support.
/// </para>
/// <para>When neither is set the control falls back to <see cref="CustomEntryStore.AllSkills"/>.</para>
/// </summary>
public partial class SkillPickerBox : UserControl
{
    // ── Dependency properties ────────────────────────────────────────────

    public static readonly DependencyProperty SelectedSkillProperty =
        DependencyProperty.Register(
            nameof(SelectedSkill), typeof(string), typeof(SkillPickerBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedSkillChanged));

    public string SelectedSkill
    {
        get => (string)GetValue(SelectedSkillProperty);
        set => SetValue(SelectedSkillProperty, value);
    }

    /// <summary>
    /// Dynamic key that resolves the entry list at open-time from
    /// <see cref="CustomEntryStore"/>.  Valid values: "CoreAbility", "Flaw".
    /// When set, also shows "＋ Add custom…" in the popup.
    /// </summary>
    public static readonly DependencyProperty EntryKeyProperty =
        DependencyProperty.Register(
            nameof(EntryKey), typeof(string), typeof(SkillPickerBox),
            new FrameworkPropertyMetadata(null, OnEntryKeyChanged));

    public string? EntryKey
    {
        get => (string?)GetValue(EntryKeyProperty);
        set => SetValue(EntryKeyProperty, value);
    }

    /// <summary>
    /// Optional static source list.  Ignored when <see cref="EntryKey"/> is set.
    /// </summary>
    public static readonly DependencyProperty EntrySourceProperty =
        DependencyProperty.Register(
            nameof(EntrySource), typeof(IReadOnlyList<SkillEntry>), typeof(SkillPickerBox),
            new FrameworkPropertyMetadata(null));

    public IReadOnlyList<SkillEntry>? EntrySource
    {
        get => (IReadOnlyList<SkillEntry>?)GetValue(EntrySourceProperty);
        set => SetValue(EntrySourceProperty, value);
    }

    // ── Fields ───────────────────────────────────────────────────────────

    private TextBox?   _input;
    private Popup?     _popup;
    private ListBox?   _list;
    private Button?    _createBtn;
    private Separator? _createSep;

    private bool _suppressTextChange;
    private bool _popupOpen;

    // ── Constructor ──────────────────────────────────────────────────────

    public SkillPickerBox()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Initialisation ───────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _input     = (TextBox)FindName("PART_Input");
        _popup     = (Popup)FindName("PART_Popup");
        _list      = (ListBox)FindName("PART_List");
        _createBtn = FindName("PART_CreateBtn") as Button;
        _createSep = FindName("PART_Separator") as Separator;

        RefreshList(string.Empty);

        _input.GotFocus         += OnInputGotFocus;
        _input.TextChanged      += OnInputTextChanged;
        _input.PreviewKeyDown   += OnInputKeyDown;
        _input.MouseDown        += OnInputMouseDown;

        _list.MouseLeftButtonUp += OnListItemClicked;
        _list.PreviewKeyDown    += OnListKeyDown;

        if (_createBtn != null)
            _createBtn.Click += OnCreateBtnClicked;

        // Attach window-level click-outside handler
        var window = Window.GetWindow(this);
        if (window != null)
            window.PreviewMouseDown += OnWindowMouseDown;

        UpdateCreateButtonVisibility();
        SyncInputFromProperty();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
            window.PreviewMouseDown -= OnWindowMouseDown;
    }

    // ── DP callbacks ─────────────────────────────────────────────────────

    private static void OnSelectedSkillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SkillPickerBox)d).SyncInputFromProperty();

    private static void OnEntryKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SkillPickerBox)d).UpdateCreateButtonVisibility();

    // ── Entry source resolution ───────────────────────────────────────────

    /// <summary>
    /// Returns the live list to display, resolving EntryKey first (dynamic,
    /// always fresh), then EntrySource (static), then AllSkills (default).
    /// </summary>
    private IReadOnlyList<SkillEntry> ResolveSource() => EntryKey switch
    {
        "CoreAbility" => CustomEntryStore.AllCoreAbilities,
        "Flaw"        => CustomEntryStore.AllFlaws,
        _             => EntrySource ?? CustomEntryStore.AllSkills,
    };

    // ── Create-button visibility ──────────────────────────────────────────

    private void UpdateCreateButtonVisibility()
    {
        bool show = EntryKey is "CoreAbility" or "Flaw";
        if (_createBtn != null) _createBtn.Visibility = show ? Visibility.Visible  : Visibility.Collapsed;
        if (_createSep != null) _createSep.Visibility = show ? Visibility.Visible  : Visibility.Collapsed;
    }

    // ── Sync input from DP ───────────────────────────────────────────────

    private void SyncInputFromProperty()
    {
        if (_input == null) return;
        _suppressTextChange = true;
        _input.Text = SelectedSkill ?? string.Empty;
        _suppressTextChange = false;
        UpdateTooltip();
    }

    // ── Tooltip update ───────────────────────────────────────────────────

    private void UpdateTooltip()
    {
        if (_input == null) return;
        var desc = ResolveSource()
            .FirstOrDefault(e => e.Name.Equals(SelectedSkill, StringComparison.OrdinalIgnoreCase))
            ?.Description;

        if (string.IsNullOrWhiteSpace(desc))
        {
            _input.ToolTip = null;
            return;
        }

        _input.ToolTip = new ToolTip
        {
            Content = new TextBlock
            {
                Text            = desc,
                TextWrapping    = TextWrapping.Wrap,
                MaxWidth        = 300,
                FontFamily      = new System.Windows.Media.FontFamily("Palatino Linotype"),
                FontSize        = 11,
                Foreground      = System.Windows.Media.Brushes.WhiteSmoke,
            },
            Background      = new System.Windows.Media.SolidColorBrush(
                                  (System.Windows.Media.Color)
                                  System.Windows.Media.ColorConverter.ConvertFromString("#CC1A0D02")),
            BorderBrush     = new System.Windows.Media.SolidColorBrush(
                                  (System.Windows.Media.Color)
                                  System.Windows.Media.ColorConverter.ConvertFromString("#5A2E0E")),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(8, 6, 8, 6),
            HasDropShadow   = true,
        };
    }

    // ── Popup open / close ───────────────────────────────────────────────

    private void OpenPopup()
    {
        if (_popup == null || _list == null || _popupOpen) return;
        RefreshList(_input?.Text ?? string.Empty);
        _popup.IsOpen = true;
        _popupOpen    = true;
    }

    private void ClosePopup()
    {
        if (_popup == null || !_popupOpen) return;
        _popup.IsOpen = false;
        _popupOpen    = false;
    }

    // ── List filtering ───────────────────────────────────────────────────

    private void RefreshList(string filter)
    {
        if (_list == null) return;
        _list.Items.Clear();
        foreach (var entry in ResolveSource())
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _list.Items.Add(entry.Name);
            }
        }
    }

    // ── Input events ─────────────────────────────────────────────────────

    private void OnInputGotFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_input?.Text))
            OpenPopup();
    }

    private void OnInputMouseDown(object sender, MouseButtonEventArgs e)
    {
        OpenPopup();
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChange || _input == null) return;

        var typed  = _input.Text;
        var source = ResolveSource();
        var exact  = source.FirstOrDefault(s =>
            s.Name.Equals(typed, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
        {
            CommitSkill(exact.Name);
            return;
        }

        RefreshList(typed);
        if (!_popupOpen && !string.IsNullOrWhiteSpace(typed))
            OpenPopup();

        if (!string.IsNullOrWhiteSpace(SelectedSkill))
        {
            _suppressTextChange = true;
            SelectedSkill = string.Empty;
            _suppressTextChange = false;
        }
        UpdateTooltip();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (_list == null) return;

        switch (e.Key)
        {
            case Key.Down:
                if (!_popupOpen) OpenPopup();
                if (_list.Items.Count > 0)
                {
                    _list.Focus();
                    _list.SelectedIndex = 0;
                    ((ListBoxItem)_list.ItemContainerGenerator
                        .ContainerFromIndex(0))?.Focus();
                }
                e.Handled = true;
                break;

            case Key.Escape:
                ClosePopup();
                SyncInputFromProperty();
                e.Handled = true;
                break;

            case Key.Enter:
                if (_popupOpen && _list.Items.Count > 0)
                {
                    CommitSkill((string)_list.Items[0]);
                    e.Handled = true;
                }
                break;
        }
    }

    // ── List events ──────────────────────────────────────────────────────

    private void OnListItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (_list?.SelectedItem is string name)
            CommitSkill(name);
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (_list?.SelectedItem is string name)
                    CommitSkill(name);
                e.Handled = true;
                break;

            case Key.Escape:
                ClosePopup();
                _input?.Focus();
                SyncInputFromProperty();
                e.Handled = true;
                break;

            case Key.Up when _list?.SelectedIndex == 0:
                ClosePopup();
                _input?.Focus();
                e.Handled = true;
                break;
        }
    }

    // ── Create custom entry ───────────────────────────────────────────────

    private void OnCreateBtnClicked(object sender, RoutedEventArgs e)
    {
        ClosePopup();

        var title = EntryKey switch
        {
            "CoreAbility" => "Create Custom Core Ability",
            "Flaw"        => "Create Custom Flaw",
            _             => "Create Custom Entry",
        };

        var dlg = new CreateCustomEntryDialog(title) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        switch (EntryKey)
        {
            case "CoreAbility": CustomEntryStore.AddCoreAbility(dlg.EntryName, dlg.EntryDescription); break;
            case "Flaw":        CustomEntryStore.AddFlaw(dlg.EntryName,        dlg.EntryDescription); break;
        }

        // Select and commit the newly created entry
        CommitSkill(dlg.EntryName);
    }

    // ── Window-level click-outside handler ───────────────────────────────

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_popupOpen) return;
        if (ContainsElement(e.OriginalSource as DependencyObject)) return;
        if (_popup?.Child != null &&
            (_popup.Child == e.OriginalSource ||
             (_popup.Child as FrameworkElement)?.IsAncestorOf(e.OriginalSource as DependencyObject) == true))
            return;

        ClosePopup();
        SyncInputFromProperty();
    }

    private bool ContainsElement(DependencyObject? element)
    {
        while (element != null)
        {
            if (element == this) return true;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    // ── Commit ───────────────────────────────────────────────────────────

    private void CommitSkill(string name)
    {
        ClosePopup();
        SelectedSkill = name;
        _input?.Focus();
    }
}
