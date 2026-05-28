using System.Windows;
using System.Windows.Input;

namespace CharacterSheet.Controls;

/// <summary>
/// Minimal modal that prompts for a bracket specialization.
/// Opens after the user picks a flaw that accepts one (Addict, Employed, etc.).
/// </summary>
public partial class SpecializationDialog : Window
{
    public SpecializationDialog(string flawName)
    {
        InitializeComponent();
        TbTitle.Text = flawName;

        BtnOk.Click     += (_, _) => TryConfirm();
        BtnCancel.Click += (_, _) => { DialogResult = false; };

        Loaded += (_, _) => TbSpecialization.Focus();
    }

    /// <summary>The trimmed text the user typed.  Empty when cancelled or blank.</summary>
    public string Specialization { get; private set; } = "";

    private void TryConfirm()
    {
        Specialization = TbSpecialization.Text?.Trim() ?? "";
        DialogResult   = true;
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
}
