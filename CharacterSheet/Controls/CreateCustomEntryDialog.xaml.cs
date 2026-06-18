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
        BtnAddGrant.Click += (_, _) => TryAddGrant();

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

    private void RemoveGrant(int index)
    {
        if (index < 0 || index >= _grants.Count) return;
        _grants.RemoveAt(index);
        RefreshGrantsList();
    }

    private void RefreshGrantsList()
    {
        GrantsContainer.Children.Clear();
        for (int i = 0; i < _grants.Count; i++)
        {
            int captured = i;
            var row = new System.Windows.Controls.DockPanel { Margin = new System.Windows.Thickness(0, 0, 0, 3) };

            var removeBtn = new System.Windows.Controls.Button
            {
                Content  = "×",
                Width    = 18,
                Height   = 18,
                FontSize = 11,
                Padding  = new System.Windows.Thickness(0),
                Margin   = new System.Windows.Thickness(4, 0, 0, 0),
                Style    = (System.Windows.Style)FindResource("RemoveButton"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };
            removeBtn.Click += (_, _) => RemoveGrant(captured);
            System.Windows.Controls.DockPanel.SetDock(removeBtn, System.Windows.Controls.Dock.Right);

            var label = new System.Windows.Controls.TextBlock
            {
                Text       = FormatGrant(_grants[captured]),
                FontFamily = new System.Windows.Media.FontFamily("Palatino Linotype"),
                FontSize   = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xD4, 0xA9, 0x6A)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };

            row.Children.Add(removeBtn);
            row.Children.Add(label);
            GrantsContainer.Children.Add(row);
        }
    }

    private static string FormatGrant((string Name, int FloorRating) g) =>
        $"{g.Name} — floor {g.FloorRating}";

    private void ShowGrantError(string msg)
    {
        TbGrantError.Text = msg;
        TbGrantError.Visibility = Visibility.Visible;
    }
}
