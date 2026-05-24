using System.Windows;
using System.Windows.Input;

namespace CharacterSheet.Controls;

/// <summary>
/// Small modal dialog for adding a new equipment or skill row.
/// <para>Equipment mode: plain TextBox for the name.</para>
/// <para>Skill mode: SkillPickerBox so the user can pick from the built-in list
/// or type a custom skill name.</para>
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
            TbTitle.Text      = "Add Skill";
            TbName.Visibility = Visibility.Collapsed;
            SpName.Visibility = Visibility.Visible;
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

    /// <summary>The name entered by the user (EquipName or SkillName).</summary>
    public string EntryName =>
        _isSkill ? (SpName.SelectedSkill ?? "") : (TbName.Text ?? "");

    /// <summary>The description / notes entered (EquipSub or SkillSub).</summary>
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

    private void TryConfirm()
    {
        if (string.IsNullOrWhiteSpace(EntryName)) return;
        DialogResult = true;
    }
}
