using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CharacterSheet.Controls;

/// <summary>
/// A split-circle Hero Points tracker.
///
/// Top hemisphere  ("Total Points")  — MaxPoints (≥ 50).
///   Left-click : +1 to max.   Right-click : −1 from max.
///   Flaws add/remove 5 automatically via MainWindow.
///
/// Bottom hemisphere ("Available Points") — CurrentPoints ([0, MaxPoints]).
///   Left-click : spend a point (−1).  Right-click : restore a point (+1).
///   Skill costs are deducted automatically via MainWindow.
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
        ctrl.CoerceValue(CurrentPointsProperty);   // keep current ≤ new max
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
    private Border?    _topArea;
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
        _topArea    = (Border)FindName("PART_TopArea");
        _bottomArea = (Border)FindName("PART_BottomArea");

        UpdateDisplay();

        // Top hemisphere: manual total adjustment
        if (_topArea != null)
        {
            _topArea.MouseLeftButtonDown  += (_, ev) => { MaxPoints++;  ev.Handled = true; };
            _topArea.MouseRightButtonDown += (_, ev) => { MaxPoints--;  ev.Handled = true; };
        }

        // Bottom hemisphere: spend / restore available points
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
