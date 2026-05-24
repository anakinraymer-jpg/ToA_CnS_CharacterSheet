using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CharacterSheet.Controls;

public class DieBubble : Control
{
    private static readonly string[] DiceCycle = ["d4", "d6", "d8", "d10", "d12"];

    private static readonly Dictionary<string, Color> DieColors = new()
    {
        ["d4"]  = (Color)ColorConverter.ConvertFromString("#7A6A3A"),
        ["d6"]  = (Color)ColorConverter.ConvertFromString("#5A5A2A"),
        ["d8"]  = (Color)ColorConverter.ConvertFromString("#2A5A3A"),
        ["d10"] = (Color)ColorConverter.ConvertFromString("#2A3A6A"),
        ["d12"] = (Color)ColorConverter.ConvertFromString("#6A1A2A"),
    };

    static DieBubble()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DieBubble),
            new FrameworkPropertyMetadata(typeof(DieBubble)));
    }

    public static readonly DependencyProperty DieProperty =
        DependencyProperty.Register(nameof(Die), typeof(string), typeof(DieBubble),
            new FrameworkPropertyMetadata("d6",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnDieChanged));

    public string Die
    {
        get => (string)GetValue(DieProperty);
        set => SetValue(DieProperty, value);
    }

    private System.Windows.Shapes.Ellipse? _ellipse;
    private TextBlock? _label;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _ellipse = GetTemplateChild("PART_Ellipse") as System.Windows.Shapes.Ellipse;
        _label   = GetTemplateChild("PART_Label")   as TextBlock;
        UpdateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        int idx = Array.IndexOf(DiceCycle, Die);
        Die = DiceCycle[(idx + 1) % DiceCycle.Length];
        e.Handled = true;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (DieColors.TryGetValue(Die, out var c) && _ellipse != null)
        {
            _ellipse.Fill   = new SolidColorBrush(c);
            if (_label != null)
                _label.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F2E4C0"));
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateVisual();
    }

    private static void OnDieChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DieBubble)d).UpdateVisual();

    private void UpdateVisual()
    {
        if (_ellipse == null || _label == null) return;
        var die = Die ?? "d6";
        if (!DieColors.TryGetValue(die, out var c))
            c = DieColors["d6"];

        var brush = new SolidColorBrush(c);
        _ellipse.Stroke = brush;
        _ellipse.Fill   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F2E4C0"));
        _label.Text     = die;
        _label.Foreground = brush;
    }
}
