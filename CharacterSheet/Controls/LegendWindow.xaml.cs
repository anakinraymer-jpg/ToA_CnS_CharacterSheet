using System.Windows;
using System.Windows.Input;

namespace CharacterSheet.Controls;

public partial class LegendWindow : Window
{
    public LegendWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
