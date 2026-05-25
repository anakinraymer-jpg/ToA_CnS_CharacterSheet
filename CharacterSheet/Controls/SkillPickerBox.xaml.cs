using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CharacterSheet.Data;

namespace CharacterSheet.Controls;

/// <summary>
/// A TextBox that, when focused and empty (or on explicit click), opens a
/// searchable popup list of Crown &amp; Skull skills.  Selecting a skill sets
/// <see cref="SelectedSkill"/> and closes the popup.  The TextBox shows the
/// skill name; a ToolTip on the TextBox shows the description while hovered.
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
    /// Optional override for the list shown in the picker.
    /// When null the control falls back to <see cref="CustomEntryStore.AllSkills"/>.
    /// Set this to <c>CoreAbilityList.All</c> (or any other list) to reuse
    /// the control for non-skill pickers.
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

    private TextBox?  _input;
    private Popup?    _popup;
    private ListBox?  _list;

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
        _input = (TextBox)FindName("PART_Input");
        _popup = (Popup)FindName("PART_Popup");
        _list  = (ListBox)FindName("PART_List");

        // Seed list with all skills
        RefreshList(string.Empty);

        // Wire TextBox events
        _input.GotFocus         += OnInputGotFocus;
        _input.TextChanged      += OnInputTextChanged;
        _input.PreviewKeyDown   += OnInputKeyDown;
        _input.MouseDown        += OnInputMouseDown;

        // Wire ListBox events
        _list.MouseLeftButtonUp += OnListItemClicked;
        _list.PreviewKeyDown    += OnListKeyDown;

        // Attach window-level click-outside handler
        var window = Window.GetWindow(this);
        if (window != null)
            window.PreviewMouseDown += OnWindowMouseDown;

        // Apply current value
        SyncInputFromProperty();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
            window.PreviewMouseDown -= OnWindowMouseDown;
    }

    // ── DP callback ──────────────────────────────────────────────────────

    private static void OnSelectedSkillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SkillPickerBox)d).SyncInputFromProperty();

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
        var source = EntrySource;
        var desc = source != null
            ? source.FirstOrDefault(e => e.Name.Equals(SelectedSkill, StringComparison.OrdinalIgnoreCase))?.Description
            : CustomEntryStore.GetSkillDescription(SelectedSkill);
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
            Background          = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)
                                      System.Windows.Media.ColorConverter.ConvertFromString("#CC1A0D02")),
            BorderBrush         = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)
                                      System.Windows.Media.ColorConverter.ConvertFromString("#5A2E0E")),
            BorderThickness     = new Thickness(1),
            Padding             = new Thickness(8, 6, 8, 6),
            HasDropShadow       = true,
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
        var source = EntrySource ?? CustomEntryStore.AllSkills;
        _list.Items.Clear();
        foreach (var skill in source)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                skill.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _list.Items.Add(skill.Name);
            }
        }
    }

    // ── Input events ─────────────────────────────────────────────────────

    private void OnInputGotFocus(object sender, RoutedEventArgs e)
    {
        // Open picker if field is empty or matches nothing typed
        if (string.IsNullOrWhiteSpace(_input?.Text))
            OpenPopup();
    }

    private void OnInputMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Clicking into the box always re-opens (even if already focused)
        OpenPopup();
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChange || _input == null) return;

        var typed = _input.Text;

        // If text matches an entry exactly → commit it
        var source = EntrySource ?? CustomEntryStore.AllSkills;
        var exact = source.FirstOrDefault(s =>
            s.Name.Equals(typed, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
        {
            CommitSkill(exact.Name);
            return;
        }

        // Otherwise filter the list and keep popup open
        RefreshList(typed);
        if (!_popupOpen && !string.IsNullOrWhiteSpace(typed))
            OpenPopup();

        // Clear SelectedSkill if text diverges from it
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
                // Revert input to last committed skill
                SyncInputFromProperty();
                e.Handled = true;
                break;

            case Key.Enter:
                // Pick first item if list is showing
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
                // Move focus back to input
                ClosePopup();
                _input?.Focus();
                e.Handled = true;
                break;
        }
    }

    // ── Window-level click-outside handler ───────────────────────────────

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_popupOpen) return;

        // If click is inside this control or its popup, leave it open
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
        SelectedSkill = name;          // DP → triggers OnSelectedSkillChanged → SyncInputFromProperty
        _input?.Focus();
    }
}
