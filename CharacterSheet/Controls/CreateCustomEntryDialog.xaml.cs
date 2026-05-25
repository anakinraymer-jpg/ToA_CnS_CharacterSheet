using System.Windows;
using System.Windows.Input;

namespace CharacterSheet.Controls;

/// <summary>
/// Minimal dialog: just a Name and Description field.
/// Used by <see cref="SkillPickerBox"/> when the user clicks
/// "＋ Add custom…" inside the dropdown popup.
/// </summary>
public partial class CreateCustomEntryDialog : Window
{
    public CreateCustomEntryDialog(string title)
    {
        InitializeComponent();
        TbTitle.Text = title;

        BtnAdd.Click    += (_, _) => TryConfirm();
        BtnCancel.Click += (_, _) => { DialogResult = false; };

        Loaded += (_, _) => TbName.Focus();
    }

    // ── Results ───────────────────────────────────────────────────────────

    public string EntryName        => TbName.Text?.Trim()        ?? "";
    public string EntryDescription => TbDescription.Text?.Trim() ?? "";

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
        }
    }

    // ── Confirm ───────────────────────────────────────────────────────────

    private void TryConfirm()
    {
        if (string.IsNullOrWhiteSpace(EntryName)) return;
        DialogResult = true;
    }
}
