using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CharacterSheet.Controls;

/// <summary>
/// A circular bubble that displays a numeric skill rating.
/// • Shows 0 (inactive/grey)  when HasSkill is false.
/// • Shows 3–18 (active/parchment) when HasSkill is true.
/// • Left-click  increments the value (up to 18).
/// • Right-click decrements the value (down to 3).
/// The model enforces all clamping; this control just fires the
/// Value change and lets the binding push the clamped value back.
/// </summary>
public class DieBubble : Control
{
    static DieBubble()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DieBubble),
            new FrameworkPropertyMetadata(typeof(DieBubble)));
    }

    // ── Dependency properties ─────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(DieBubble),
            new FrameworkPropertyMetadata(0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnVisualChanged));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty HasSkillProperty =
        DependencyProperty.Register(nameof(HasSkill), typeof(bool), typeof(DieBubble),
            new FrameworkPropertyMetadata(false, OnVisualChanged));

    public bool HasSkill
    {
        get => (bool)GetValue(HasSkillProperty);
        set => SetValue(HasSkillProperty, value);
    }

    public static readonly DependencyProperty CanIncreaseProperty =
        DependencyProperty.Register(nameof(CanIncrease), typeof(bool), typeof(DieBubble),
            new FrameworkPropertyMetadata(true));

    /// <summary>
    /// When false, left-click is ignored so the rating cannot be increased.
    /// Bind to CharacterState.CanSpendPoint to gate on Available Points > 0.
    /// </summary>
    public bool CanIncrease
    {
        get => (bool)GetValue(CanIncreaseProperty);
        set => SetValue(CanIncreaseProperty, value);
    }

    // ── Brushes ───────────────────────────────────────────────────────

    private static readonly SolidColorBrush ActiveStroke   = Frozen("#5A2E0E");
    private static readonly SolidColorBrush ActiveFill     = Frozen("#F2E4C0");
    private static readonly SolidColorBrush ActiveFillHov  = Frozen("#E8D4A8");
    private static readonly SolidColorBrush ActiveText     = Frozen("#1A0D02");
    private static readonly SolidColorBrush InactiveStroke = Frozen("#885A2E0E");
    private static readonly SolidColorBrush InactiveFill   = Frozen("#E8E2D8");
    private static readonly SolidColorBrush InactiveText   = Frozen("#885A2E0E");

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // ── Template parts ────────────────────────────────────────────────

    private Ellipse?   _ellipse;
    private TextBlock? _label;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _ellipse = GetTemplateChild("PART_Ellipse") as Ellipse;
        _label   = GetTemplateChild("PART_Label")   as TextBlock;
        UpdateVisual(hover: false);
    }

    // ── Mouse interactions ────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (HasSkill && CanIncrease && Value < 18)
            Value++;
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (HasSkill && Value > 3)
            Value--;
        e.Handled = true;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (HasSkill) UpdateVisual(hover: true);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateVisual(hover: false);
    }

    // ── Visual update ─────────────────────────────────────────────────

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DieBubble)d).UpdateVisual(hover: false);

    private void UpdateVisual(bool hover)
    {
        if (_ellipse == null || _label == null) return;

        if (HasSkill)
        {
            _ellipse.Stroke    = ActiveStroke;
            _ellipse.Fill      = hover ? ActiveFillHov : ActiveFill;
            _label.Text        = Value.ToString();
            _label.Foreground  = ActiveText;
        }
        else
        {
            _ellipse.Stroke    = InactiveStroke;
            _ellipse.Fill      = InactiveFill;
            _label.Text        = "0";
            _label.Foreground  = InactiveText;
        }
    }
}
