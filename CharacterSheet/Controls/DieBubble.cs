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

    public static readonly DependencyProperty HasBonusProperty =
        DependencyProperty.Register(nameof(HasBonus), typeof(bool), typeof(DieBubble),
            new FrameworkPropertyMetadata(false, OnVisualChanged));

    /// <summary>
    /// When true a +3 bonus is applied to the displayed value (capped at 18)
    /// and a leaf indicator is shown inside the bubble.
    /// The underlying Value property is not modified — the bonus is display-only.
    /// </summary>
    public bool HasBonus
    {
        get => (bool)GetValue(HasBonusProperty);
        set => SetValue(HasBonusProperty, value);
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

    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(int), typeof(DieBubble),
            new FrameworkPropertyMetadata(3));

    /// <summary>Minimum value right-click can reach.  Bind to SkillData.MinRating.</summary>
    public int MinValue
    {
        get => (int)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(int), typeof(DieBubble),
            new FrameworkPropertyMetadata(1));

    /// <summary>
    /// How much each click changes Value.  Bind to SkillData.RatingStep.
    /// 1 = normal; 2 = Loremaster Knowledge (1 AP per 2 rating points).
    /// </summary>
    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(DieBubble),
            new FrameworkPropertyMetadata(false));

    /// <summary>
    /// When true all mouse interaction is disabled (display-only).
    /// Bind to SkillData.IsLocked for skills like Create Device that mirror another skill.
    /// </summary>
    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    // ── Brushes ───────────────────────────────────────────────────────

    private static readonly SolidColorBrush ActiveStroke   = Frozen("#5A2E0E");
    private static readonly SolidColorBrush ActiveFill     = Frozen("#F2E4C0");
    private static readonly SolidColorBrush ActiveFillHov  = Frozen("#E8D4A8");
    private static readonly SolidColorBrush ActiveText     = Frozen("#1A0D02");
    private static readonly SolidColorBrush BonusText      = Frozen("#1A4A10");  // dark green for bonus value
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
    private TextBlock? _leafLabel;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _ellipse   = GetTemplateChild("PART_Ellipse")   as Ellipse;
        _label     = GetTemplateChild("PART_Label")     as TextBlock;
        _leafLabel = GetTemplateChild("PART_LeafLabel") as TextBlock;
        UpdateVisual(hover: false);
    }

    // ── Mouse interactions ────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsLocked && HasSkill && CanIncrease && Value + Step <= 18)
            Value += Step;
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (!IsLocked && HasSkill && Value - Step >= MinValue)
            Value -= Step;
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
            _ellipse.Stroke = ActiveStroke;
            _ellipse.Fill   = hover ? ActiveFillHov : ActiveFill;

            if (HasBonus)
            {
                _label.Text       = Math.Min(18, Value + 3).ToString();
                _label.Foreground = BonusText;
            }
            else
            {
                _label.Text       = Value.ToString();
                _label.Foreground = ActiveText;
            }
        }
        else
        {
            _ellipse.Stroke   = InactiveStroke;
            _ellipse.Fill     = InactiveFill;
            _label.Text       = "0";
            _label.Foreground = InactiveText;
        }

        if (_leafLabel != null)
            _leafLabel.Visibility = HasSkill && HasBonus
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
