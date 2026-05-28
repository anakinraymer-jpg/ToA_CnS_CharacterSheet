using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CharacterSheet.Controls;

/// <summary>
/// A shield-shaped counter.  Left-click increases the value; right-click
/// decreases it.  The value is clamped to [6, 18].
/// </summary>
public partial class ShieldCounter : UserControl
{
    // ── Dependency property ──────────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(ShieldCounter),
            new FrameworkPropertyMetadata(
                6,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged,
                CoerceValue));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
        => Math.Clamp((int)baseValue, 6, 18);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ShieldCounter)d).UpdateDisplay();

    // ── Bonus ────────────────────────────────────────────────────────────────
    // Armor bonus added on top of the base Value. Displayed as Value + Bonus.

    public static readonly DependencyProperty BonusProperty =
        DependencyProperty.Register(
            nameof(Bonus), typeof(int), typeof(ShieldCounter),
            new FrameworkPropertyMetadata(0, OnBonusChanged));

    public int Bonus
    {
        get => (int)GetValue(BonusProperty);
        set => SetValue(BonusProperty, value);
    }

    private static void OnBonusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ShieldCounter)d).UpdateDisplay();

    // ── Fields ───────────────────────────────────────────────────────────

    private Path?      _shield;
    private TextBlock? _text;

    private static readonly Brush BrushNormal  = Brushes.Transparent;
    private static readonly Brush BrushHover   = Frozen("#305A2E0E");   // subtle warm tint on hover
    private static readonly Brush BrushPressed = Frozen("#505A2E0E");   // slightly stronger on press

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // ── Constructor ──────────────────────────────────────────────────────

    public ShieldCounter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _shield = (Path)FindName("PART_Shield");
        _text   = (TextBlock)FindName("PART_Text");

        UpdateDisplay();

        MouseLeftButtonDown  += OnLeftClick;
        MouseRightButtonDown += OnRightClick;
        MouseEnter           += (_, _) => { if (_shield != null) _shield.Fill = BrushHover;   };
        MouseLeave           += (_, _) => { if (_shield != null) _shield.Fill = BrushNormal;  };
    }

    // ── Click handlers ───────────────────────────────────────────────────

    private void OnLeftClick(object sender, MouseButtonEventArgs e)
    {
        if (_shield != null) _shield.Fill = BrushPressed;
        Value++;
        // Stay on hover tint — cursor is still over the control
        if (_shield != null) _shield.Fill = BrushHover;
        e.Handled = true;
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_shield != null) _shield.Fill = BrushPressed;
        Value--;
        if (_shield != null) _shield.Fill = BrushHover;
        e.Handled = true;
    }

    // ── Display ──────────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        if (_text != null) _text.Text = (Value + Bonus).ToString();
    }
}
