using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CharacterSheet.Controls;

/// <summary>
/// A split-circle Hero Points tracker.
/// Top hemisphere: MaxPoints (≥ 50).  + / − buttons add or remove points.
/// Adding a point also increases CurrentPoints by one.
/// Bottom hemisphere: CurrentPoints ([0, MaxPoints]).
/// Left-click the bottom to spend a point; right-click to restore one.
/// </summary>
public partial class HeroPointCircle : UserControl
{
    // ── MaxPoints ────────────────────────────────────────────────────────────

    public static readonly DependencyProperty MaxPointsProperty =
        DependencyProperty.Register(
            nameof(MaxPoints), typeof(int), typeof(HeroPointCircle),
            new FrameworkPropertyMetadata(
                50,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnMaxPointsChanged,
                CoerceMaxPoints));

    public int MaxPoints
    {
        get => (int)GetValue(MaxPointsProperty);
        set => SetValue(MaxPointsProperty, value);
    }

    private static object CoerceMaxPoints(DependencyObject d, object baseValue)
        => Math.Max((int)baseValue, 50);

    private static void OnMaxPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (HeroPointCircle)d;
        // Re-coerce CurrentPoints so it can never exceed the new MaxPoints
        ctrl.CoerceValue(CurrentPointsProperty);
        ctrl.UpdateDisplay();
    }

    // ── CurrentPoints ────────────────────────────────────────────────────────

    public static readonly DependencyProperty CurrentPointsProperty =
        DependencyProperty.Register(
            nameof(CurrentPoints), typeof(int), typeof(HeroPointCircle),
            new FrameworkPropertyMetadata(
                50,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCurrentPointsChanged,
                CoerceCurrentPoints));

    public int CurrentPoints
    {
        get => (int)GetValue(CurrentPointsProperty);
        set => SetValue(CurrentPointsProperty, value);
    }

    private static object CoerceCurrentPoints(DependencyObject d, object baseValue)
    {
        var ctrl = (HeroPointCircle)d;
        return Math.Clamp((int)baseValue, 0, ctrl.MaxPoints);
    }

    private static void OnCurrentPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HeroPointCircle)d).UpdateDisplay();

    // ── Fields ───────────────────────────────────────────────────────────────

    private TextBlock? _max;
    private TextBlock? _current;
    private TextBlock? _plus;
    private TextBlock? _minus;
    private Border?    _bottomArea;

    // ── Constructor ──────────────────────────────────────────────────────────

    public HeroPointCircle()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _max        = (TextBlock)FindName("PART_Max");
        _current    = (TextBlock)FindName("PART_Current");
        _plus       = (TextBlock)FindName("PART_Plus");
        _minus      = (TextBlock)FindName("PART_Minus");
        _bottomArea = (Border)FindName("PART_BottomArea");

        UpdateDisplay();

        if (_plus != null)
            _plus.MouseLeftButtonDown += (_, ev) =>
            {
                MaxPoints++;
                CurrentPoints++;   // track the new point
                ev.Handled = true;
            };

        if (_minus != null)
            _minus.MouseLeftButtonDown += (_, ev) =>
            {
                MaxPoints--;           // coerced to ≥ 50; CurrentPoints coerced if needed
                ev.Handled = true;
            };

        if (_bottomArea != null)
        {
            _bottomArea.MouseLeftButtonDown  += (_, ev) => { CurrentPoints--; ev.Handled = true; };
            _bottomArea.MouseRightButtonDown += (_, ev) => { CurrentPoints++; ev.Handled = true; };
        }
    }

    // ── Display ──────────────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        if (_max     != null) _max.Text     = MaxPoints.ToString();
        if (_current != null) _current.Text = CurrentPoints.ToString();
    }
}
