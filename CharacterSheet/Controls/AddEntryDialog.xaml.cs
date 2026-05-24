using System.Windows;
using System.Windows.Input;

namespace CharacterSheet.Controls;

/// <summary>
/// Small modal dialog for adding a new equipment item or skill entry.
/// Pass <paramref name="isSkill"/> = true to show the SkillPickerBox for the
/// name field; false shows a plain TextBox (equipment mode).
/// </summary>
public partial class AddEntryDialog : Window
{
    private readonly bool _isSkill;

    public AddEntryDialog(bool isSkill)
    {
        InitializeComponent();
        _isSkill = isSkill;

        if (isSkill)
        {
            TbTitle.Text       = "Add Skill";
            TbName.Visibility  = Visibility.Collapsed;
            SpName.Visibility  = Visibility.Visible;
            // Focus the picker's inner TextBox after load
            Loaded += (_, _) => SpName.Focus();
        }
        else
        {
            TbTitle.Text = "Add Equipment";
            Loaded += (_, _) => TbName.Focus();
        }

        BtnAdd.Click    += (_, _) => TryConfirm();
        BtnCancel.Click += (_, _) => { DialogResult = false; };
    }

    // ── Results ───────────────────────────────────────────────────────────

    public string EntryName =>
        _isSkill ? (SpName.SelectedSkill ?? "") : (TbName.Text ?? "");

    public string EntryDescription => TbDescription.Text ?? "";

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

    // ── Helpers ───────────────────────────────────────────────────────────

    private void TryConfirm()
    {
        if (string.IsNullOrWhiteSpace(EntryName)) return;   // require a name
        DialogResult = true;
    }
}
