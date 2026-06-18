using System.Windows;
using System.Windows.Input;

namespace CharacterSheet.Controls;

public partial class CreateCustomEntryDialog : Window
{
    private readonly List<(string Name, int FloorRating)> _grants = new();

    public CreateCustomEntryDialog(string title, bool showSkillGrants = false)
    {
        InitializeComponent();
        TbTitle.Text = title;

        if (showSkillGrants)
            SkillGrantsSection.Visibility = Visibility.Visible;

        BtnAdd.Click         += (_, _) => TryConfirm();
        BtnCancel.Click      += (_, _) => { DialogResult = false; };
        BtnAddGrant.Click    += (_, _) => TryAddGrant();
        BtnRemoveGrant.Click += (_, _) => RemoveSelectedGrant();
        LbGrants.SelectionChanged += (_, _) =>
            BtnRemoveGrant.IsEnabled = LbGrants.SelectedItem != null;

        TbGrantFloor.KeyDown += (_, ke) => { if (ke.Key == Key.Enter) { TryAddGrant(); ke.Handled = true; } };

        Loaded += (_, _) => TbName.Focus();
    }

    // ── Results ───────────────────────────────────────────────────────────

    public string EntryName        => TbName.Text?.Trim()        ?? "";
    public string EntryDescription => TbDescription.Text?.Trim() ?? "";
    public IReadOnlyList<(string Name, int FloorRating)> SkillGrants => _grants;

    // ── Keyboard shortcuts ────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Enter when !e.IsRepeat:
                if (SkillGrantsSection.Visibility == Visibility.Visible &&
                    (GrantNamePicker.IsKeyboardFocusWithin || TbGrantFloor.IsFocused))
                    TryAddGrant();
                else
                    TryConfirm();
                e.Handled = true;
                break;
            case Key.Escape:
                DialogResult = false;
                e.Handled = true;
                break;
            case Key.Delete when LbGrants.SelectedItem != null && LbGrants.IsKeyboardFocusWithin:
                RemoveSelectedGrant();
                e.Handled = true;
                break;
        }
    }

    // ── Confirm ───────────────────────────────────────────────────────────

    private void TryConfirm()
    {
        if (string.IsNullOrWhiteSpace(EntryName)) return;
        DialogResult = true;
    }

    // ── Grant management ──────────────────────────────────────────────────

    private void TryAddGrant()
    {
        string name = GrantNamePicker.SelectedSkill?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowGrantError("Please select a skill.");
            return;
        }
        if (!int.TryParse(TbGrantFloor.Text.Trim(), out int rating) || rating < 3 || rating > 18)
        {
            ShowGrantError("Floor rating must be 3 - 18.");
            return;
        }
        if (_grants.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowGrantError("That skill is already in the list.");
            return;
        }
        TbGrantError.Visibility = Visibility.Collapsed;
        _grants.Add((name, rating));
        RefreshGrantsList();
        GrantNamePicker.SelectedSkill = "";
        GrantNamePicker.Focus();
    }

    private void RemoveSelectedGrant()
    {
        if (LbGrants.SelectedItem is not string selected) return;
        var idx = _grants.FindIndex(g => FormatGrant(g) == selected);
        if (idx >= 0)
        {
            _grants.RemoveAt(idx);
            RefreshGrantsList();
        }
    }

    private void RefreshGrantsList()
    {
        LbGrants.Items.Clear();
        foreach (var g in _grants)
            LbGrants.Items.Add(FormatGrant(g));
        BtnRemoveGrant.IsEnabled = false;
    }

    private static string FormatGrant((string Name, int FloorRating) g) =>
        $"{g.Name} — floor {g.FloorRating}";

    private void ShowGrantError(string msg)
    {
        TbGrantError.Text = msg;
        TbGrantError.Visibility = Visibility.Visible;
    }
}
