using System.Windows;
using System.Windows.Input;

namespace CharacterSheet.Controls;

public partial class AddCaSkillDialog : Window
{
    public string SkillName   { get; private set; } = "";
    public int    FloorRating { get; private set; } = 9;

    public AddCaSkillDialog(Window? owner)
    {
        InitializeComponent();
        Owner           = owner;
        BtnAdd.Click    += (_, _) => TryConfirm();
        BtnCancel.Click += (_, _) => { DialogResult = false; };
        Loaded          += (_, _) => { TbSkillName.SelectAll(); TbSkillName.Focus(); };
    }

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
                e.Handled    = true;
                break;
        }
    }

    private void TryConfirm()
    {
        string name = TbSkillName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Please enter a skill name.");
            return;
        }
        if (!int.TryParse(TbRating.Text.Trim(), out int rating) || rating < 3 || rating > 18)
        {
            ShowError("Floor rating must be a whole number from 3 to 18.");
            return;
        }
        SkillName    = name;
        FloorRating  = rating;
        DialogResult = true;
    }

    private void ShowError(string msg)
    {
        TbError.Text       = msg;
        TbError.Visibility = Visibility.Visible;
    }
}
