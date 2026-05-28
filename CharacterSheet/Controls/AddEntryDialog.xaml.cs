using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CharacterSheet.Data;

namespace CharacterSheet.Controls;

/// <summary>
/// Modal dialog for adding a new equipment or skill row.
/// <para><b>Pick Existing</b> — choose from the running list (built-in skills + any
/// previously-created custom entries).</para>
/// <para><b>Create Custom</b> — type a name and description; the entry is saved
/// permanently to <see cref="CustomEntryStore"/> before the dialog closes.</para>
/// </summary>
public partial class AddEntryDialog : Window
{
    private readonly bool _isSkill;

    // The full list used for Pick mode (updated once at construction)
    private readonly System.Collections.Generic.IReadOnlyList<SkillEntry> _allEntries;

    // Names already in use on existing rows (null = no filtering)
    private readonly HashSet<string>? _excluded;

    // Skills that may appear on multiple rows and are therefore never excluded
    private static readonly HashSet<string> s_multiAllowed =
        new(StringComparer.OrdinalIgnoreCase)
        { "Knowledge", "Languages", "Profession", "Resist (Type)", "Scholar" };

    /// <param name="isSkill">True for the skill list, false for equipment.</param>
    /// <param name="excludedNames">
    /// Skill names already selected on existing rows.  These are hidden from
    /// the pick list unless they are in the multi-allowed set.
    /// Pass <c>null</c> (or omit) to show the full list.
    /// </param>
    public AddEntryDialog(bool isSkill, IEnumerable<string>? excludedNames = null)
    {
        InitializeComponent();
        _isSkill = isSkill;

        TbTitle.Text = isSkill ? "Add Skill" : "Add Equipment";
        _allEntries  = isSkill ? CustomEntryStore.AllSkills : CustomEntryStore.AllEquipment;

        if (isSkill && excludedNames != null)
            _excluded = new HashSet<string>(excludedNames, StringComparer.OrdinalIgnoreCase);

        BtnAdd.Click    += (_, _) => TryConfirm();
        BtnCancel.Click += (_, _) => { DialogResult = false; };

        Loaded += OnDialogLoaded;

        // Armor section is only relevant for equipment
        if (isSkill)
            PanelArmor.Visibility = System.Windows.Visibility.Collapsed;
    }

    // ── Results ───────────────────────────────────────────────────────────

    /// <summary>Name of the chosen / created entry (EquipName or SkillName).</summary>
    public string EntryName        { get; private set; } = "";

    /// <summary>Description of the chosen / created entry (EquipSub or SkillSub).</summary>
    public string EntryDescription { get; private set; } = "";

    /// <summary>Armor value for the new equipment item (always 0 for skills).</summary>
    public int EntryArmorValue     { get; private set; } = 0;

    // ── Loaded ────────────────────────────────────────────────────────────

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        TbFilter.TextChanged       += OnFilterChanged;
        LbEntries.MouseDoubleClick += (_, _) => TryConfirm();

        PopulateList(string.Empty);

        // If nothing to pick, jump straight to Create mode
        if (_allEntries.Count == 0)
            RbCreate.IsChecked = true;      // fires OnModeChanged → focuses TbName
        else
            TbFilter.Focus();
    }

    // ── Mode switch ───────────────────────────────────────────────────────

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;   // skip during InitializeComponent

        bool pick = RbPick.IsChecked == true;
        PanelPick.Visibility   = pick ? Visibility.Visible  : Visibility.Collapsed;
        PanelCreate.Visibility = pick ? Visibility.Collapsed : Visibility.Visible;

        if (pick)
            TbFilter.Focus();
        else
            TbName.Focus();
    }

    // ── Pick helpers ──────────────────────────────────────────────────────

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
        => PopulateList(TbFilter.Text);

    private void PopulateList(string filter)
    {
        LbEntries.Items.Clear();
        foreach (var entry in _allEntries)
        {
            // Hide already-selected skills unless they are multi-allowed
            if (_excluded != null &&
                _excluded.Contains(entry.Name) &&
                !s_multiAllowed.Contains(entry.Name))
                continue;

            if (string.IsNullOrWhiteSpace(filter) ||
                entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                LbEntries.Items.Add(entry);
            }
        }
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Enter when !e.IsRepeat:
                TryConfirm();
                e.Handled = true;
                break;
            case Key.Escape:
                DialogResult = false;
                e.Handled = true;
                break;
            // Down from filter box → move selection into list
            case Key.Down when RbPick.IsChecked == true && LbEntries.Items.Count > 0:
                if (LbEntries.SelectedIndex < 0)
                    LbEntries.SelectedIndex = 0;
                ((ListBoxItem?)LbEntries.ItemContainerGenerator
                    .ContainerFromIndex(LbEntries.SelectedIndex))?.Focus();
                e.Handled = true;
                break;
        }
    }

    // ── Confirm ───────────────────────────────────────────────────────────

    private void TryConfirm()
    {
        if (RbPick.IsChecked == true)
        {
            // Pick mode: a list item must be selected
            if (LbEntries.SelectedItem is not SkillEntry entry) return;
            EntryName        = entry.Name;
            EntryDescription = entry.Description ?? "";
        }
        else
        {
            // Create mode: name is required; save to the persistent store
            var name = TbName.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return;
            var desc = TbDescription.Text?.Trim() ?? "";

            if (_isSkill)
                CustomEntryStore.AddSkill(name, desc);
            else
                CustomEntryStore.AddEquipment(name, desc);

            EntryName        = name;
            EntryDescription = desc;
        }

        // Read armor value (equipment only; ignored for skills)
        if (!_isSkill)
            EntryArmorValue = int.TryParse(TbArmorValue.Text?.Trim(), out int av)
                ? Math.Max(0, av)
                : 0;

        DialogResult = true;
    }
}
