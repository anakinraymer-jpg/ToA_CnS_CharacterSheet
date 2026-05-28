using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CharacterSheet.Controls;

/// <summary>
/// One section of the hit / wound tracker bar.
/// Click to fill; click again to clear.
/// </summary>
public partial class HitSegment : UserControl
{
    // ── Dependency properties ────────────────────────────────────────────

    public static readonly DependencyProperty IsMarkedProperty =
        DependencyProperty.Register(
            nameof(IsMarked), typeof(bool), typeof(HitSegment),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnVisualChanged));

    public bool IsMarked
    {
        get => (bool)GetValue(IsMarkedProperty);
        set => SetValue(IsMarkedProperty, value);
    }

    public static readonly DependencyProperty NumberProperty =
        DependencyProperty.Register(
            nameof(Number), typeof(int), typeof(HitSegment),
            new FrameworkPropertyMetadata(1, OnVisualChanged));

    public int Number
    {
        get => (int)GetValue(NumberProperty);
        set => SetValue(NumberProperty, value);
    }

    // ── Brushes ──────────────────────────────────────────────────────────

    private static readonly Brush            BgEmpty   = Brushes.Transparent;
    private static readonly SolidColorBrush BgFilled  = Frozen("#5A2E0E");
    private static readonly SolidColorBrush BgHoverE  = Frozen("#305A2E0E"); // subtle warm tint on empty hover
    private static readonly SolidColorBrush BgHoverF  = Frozen("#6A3A12");   // brighter on filled hover
    private static readonly SolidColorBrush FgEmpty   = Frozen("#5A2E0E");   // dark brown — readable on parchment
    private static readonly SolidColorBrush FgFilled  = Frozen("#D4A96A");   // gold on dark fill

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // ── Fields ───────────────────────────────────────────────────────────

    private Border?    _border;
    private TextBlock? _number;
    private bool       _hovering;

    // ── Constructor ──────────────────────────────────────────────────────

    public HitSegment()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _border = (Border)FindName("PART_Border");
        _number = (TextBlock)FindName("PART_Number");

        UpdateDisplay();

        MouseLeftButtonDown += (_, ev) => { IsMarked = !IsMarked; ev.Handled = true; };
        MouseEnter          += (_, _)  => { _hovering = true;  UpdateDisplay(); };
        MouseLeave          += (_, _)  => { _hovering = false; UpdateDisplay(); };
    }

    // ── Callbacks ────────────────────────────────────────────────────────

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HitSegment)d).UpdateDisplay();

    // ── Display ──────────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        if (_border == null || _number == null) return;

        _number.Text       = Number.ToString();
        _number.Foreground = IsMarked ? FgFilled : FgEmpty;

        _border.Background = _hovering
            ? (IsMarked ? BgHoverF : BgHoverE)
            : (IsMarked ? BgFilled : BgEmpty);
    }
}
