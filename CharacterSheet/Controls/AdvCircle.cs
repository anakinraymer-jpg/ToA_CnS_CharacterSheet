using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CharacterSheet.Controls;

public class AdvCircle : Control
{
    static AdvCircle()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AdvCircle),
            new FrameworkPropertyMetadata(typeof(AdvCircle)));
    }

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(AdvCircle),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsCheckedChanged));

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    private static readonly Color BrownColor = (Color)ColorConverter.ConvertFromString("#5A2E0E");
    private static readonly Color RedColor   = (Color)ColorConverter.ConvertFromString("#7A1A0A");

    private Ellipse? _ellipse;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _ellipse = GetTemplateChild("PART_Ellipse") as Ellipse;
        UpdateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        IsChecked = !IsChecked;
        e.Handled = true;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (_ellipse != null && !IsChecked)
            _ellipse.Stroke = new SolidColorBrush(RedColor);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_ellipse != null && !IsChecked)
            _ellipse.Stroke = new SolidColorBrush(BrownColor);
    }

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AdvCircle)d).UpdateVisual();

    private void UpdateVisual()
    {
        if (_ellipse == null) return;
        if (IsChecked)
        {
            _ellipse.Fill   = new SolidColorBrush(RedColor);
            _ellipse.Stroke = new SolidColorBrush(RedColor);
        }
        else
        {
            _ellipse.Fill   = Brushes.Transparent;
            _ellipse.Stroke = new SolidColorBrush(BrownColor);
        }
    }
}
