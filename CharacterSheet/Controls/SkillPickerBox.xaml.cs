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
    // ── Multi-allowed / paren sets ────────────────────────────────────────

    /// <summary>Skills that may be selected more than once (each with a unique specifier in parens).</summary>
    private static readonly HashSet<string> s_multiAllowedSkills =
        new(StringComparer.OrdinalIgnoreCase)
        { "Knowledge", "Languages", "Profession", "Resist (Type)", "Scholar" };

    /// <summary>
    /// Flaws that prompt for a bracket specialization when selected,
    /// e.g. "Addict [Ale]".  These are also multi-allowed so that two
    /// different specializations of the same flaw can coexist.
    /// </summary>
    private static readonly HashSet<string> s_bracketFlaws =
        new(StringComparer.OrdinalIgnoreCase)
        { "Addict", "Employed", "Grudge", "Injured", "Phobia" };

    /// <summary>Flaws that may be selected more than once (each with a unique specialization).</summary>
    private static readonly HashSet<string> s_multiAllowedFlaws =
        new(StringComparer.OrdinalIgnoreCase)
        { "Addict", "Employed", "Grudge", "Injured", "Phobia" };

    private static readonly HashSet<string> s_emptySet =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Skills that automatically get "()" appended when selected, placing
    /// the cursor inside so the user can type a specifier.
    /// </summary>
    private static readonly HashSet<string> s_parenSkills =
        new(StringComparer.OrdinalIgnoreCase)
        { "Knowledge", "Languages", "Profession", "Resist (Type)", "Scholar" };

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

    /// <summary>
    /// Names already selected on sibling pickers.  Entries whose name appears
    /// here are hidden from the list, unless they are in the multi-allowed set
    /// for this picker's entry type.  The picker's own current value is always
    /// visible (self-exclusion guard).
    /// </summary>
    public static readonly DependencyProperty ExcludedValuesProperty =
        DependencyProperty.Register(
            nameof(ExcludedValues), typeof(IEnumerable<string>), typeof(SkillPickerBox),
            new FrameworkPropertyMetadata(null, OnExcludedValuesChanged));

    public IEnumerable<string>? ExcludedValues
    {
        get => (IEnumerable<string>?)GetValue(ExcludedValuesProperty);
        set => SetValue(ExcludedValuesProperty, value);
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

    private static void OnExcludedValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (SkillPickerBox)d;
        // Refresh visible list immediately when sibling selections change
        if (box._popupOpen)
            box.RefreshList(box._input?.Text ?? string.Empty);
    }

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

    // ── Multi-allowed / paren helpers ─────────────────────────────────────

    /// <summary>Returns the set of names that may appear on multiple lines for this picker's type.</summary>
    private HashSet<string> GetMultiAllowed() => EntryKey switch
    {
        "CoreAbility" => s_emptySet,
        "Flaw"        => s_multiAllowedFlaws,
        _             => s_multiAllowedSkills,
    };

    /// <summary>True when selecting <paramref name="name"/> should append "()" for a type specifier.</summary>
    private bool ShouldAddParens(string name)
        => string.IsNullOrEmpty(EntryKey) && s_parenSkills.Contains(name);

    /// <summary>
    /// True when selecting <paramref name="name"/> as a flaw should trigger the
    /// bracket-specialization dialog, e.g. "Addict" → "Addict [Ale]".
    /// </summary>
    private bool ShouldAskSpecialization(string name)
        => EntryKey == "Flaw" && s_bracketFlaws.Contains(name);

    /// <summary>
    /// True when <paramref name="text"/> is a parenthesized form of a paren-skill,
    /// e.g. "Knowledge(History)" or "Knowledge(" (mid-typing).
    /// </summary>
    private static bool IsParenForm(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var pi = text.IndexOf('(');
        if (pi <= 0) return false;
        var baseName = text[..pi].TrimEnd();
        return s_parenSkills.Contains(baseName);
    }

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
        var source = ResolveSource();

        // Exact match first
        var desc = source
            .FirstOrDefault(e => e.Name.Equals(SelectedSkill, StringComparison.OrdinalIgnoreCase))
            ?.Description;

        // Fallback: strip specifier to find base skill (e.g., "Knowledge(History)" → "Knowledge")
        if (desc == null && !string.IsNullOrEmpty(SelectedSkill))
        {
            var pi = SelectedSkill.IndexOf('(');
            if (pi > 0)
            {
                var baseName = SelectedSkill[..pi].TrimEnd();
                desc = source
                    .FirstOrDefault(e => e.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    ?.Description;
            }
        }

        // Fallback: strip bracket specifier to find base flaw (e.g., "Addict [Ale]" → "Addict")
        if (desc == null && !string.IsNullOrEmpty(SelectedSkill))
        {
            var bi = SelectedSkill.IndexOf('[');
            if (bi > 0)
            {
                var baseName = SelectedSkill[..bi].TrimEnd();
                desc = source
                    .FirstOrDefault(e => e.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    ?.Description;
            }
        }

        if (string.IsNullOrWhiteSpace(desc))
        {
            _input.ToolTip = null;
            return;
        }

        _input.ToolTip = desc;
    }

    // ── Popup open / close ───────────────────────────────────────────────

    private void OpenPopup()
    {
        if (_popup == null || _list == null || _popupOpen) return;
        var filter = _input?.Text ?? string.Empty;
        // A parenthesized value (e.g. "Knowledge(History)") won't match list entries;
        // show the full list instead so the user can pick a different specialisation.
        if (IsParenForm(filter)) filter = string.Empty;
        RefreshList(filter);
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

        // Build exclusion set from sibling selections, then remove self so the
        // picker can still show and re-select its own current value.
        HashSet<string>? excluded = null;
        if (ExcludedValues != null)
        {
            excluded = new HashSet<string>(ExcludedValues, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(SelectedSkill))
            {
                excluded.Remove(SelectedSkill!);
                // Also remove the base name in case self is a paren form like "Knowledge(History)"
                var pi = SelectedSkill!.IndexOf('(');
                if (pi > 0) excluded.Remove(SelectedSkill![..pi].TrimEnd());
            }
        }

        var multiAllowed = GetMultiAllowed();

        foreach (var entry in ResolveSource())
        {
            // Skip entries that are already in use on another line
            // (unless the entry type is explicitly multi-allowed)
            if (excluded != null &&
                excluded.Contains(entry.Name) &&
                !multiAllowed.Contains(entry.Name))
                continue;

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

        // If the user is editing inside a parenthesized specifier (e.g. "Knowledge(H"),
        // accept it directly as the selected value without searching the list.
        if (IsParenForm(typed))
        {
            SelectedSkill = typed;          // SyncInputFromProperty handles suppress internally
            if (_popupOpen) RefreshList(string.Empty);
            return;
        }

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

        // For bracket-specialization flaws (Addict, Employed, etc.) open the prompt
        // and append "[specialization]" to the name if the user provides one.
        if (ShouldAskSpecialization(name))
        {
            var dlg = new SpecializationDialog(name) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.Specialization))
                name = $"{name} [{dlg.Specialization}]";
            // Cancelled or blank → commit the plain flaw name as-is
        }

        if (ShouldAddParens(name))
        {
            // Set "Name()" and park cursor between the parens
            SelectedSkill = name + "()";   // SyncInputFromProperty sets _input.Text
            _input?.Focus();
            if (_input != null)
                _input.CaretIndex = _input.Text.Length - 1;   // before ')'
        }
        else
        {
            SelectedSkill = name;
            _input?.Focus();
        }
    }
}
