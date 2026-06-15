using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CharacterSheet.Map;

/// <summary>
/// WPF rendering canvas for the hex map. Ported from HexMapCanvas (canvas.py).
///
/// All map layers are drawn onto DrawingContext in OnRender inside the current
/// view Matrix (pan + zoom). Mouse input handles selection, movement, panning
/// (middle-drag), zoom (wheel), and corner-handle warp-dragging.
/// </summary>
public class HexMapCanvas : FrameworkElement
{
    // ── Events (replacing Qt signals) ──────────────────────────────────
    public event Action<MapEntity?>?     EntitySelected;
    public event Action<MapEntity, int>? MoveRequested;
    public event Action<double, double>? OriginClicked;
    public event Action<MapEntity>?      RightClickedEntity;

    // ── Data sources ───────────────────────────────────────────────────
    private readonly HexGrid            _grid;
    private readonly MapEntityManager   _em;
    private readonly MapLocationManager _lm;
    private readonly MapTerrainMap      _tm;

    // ── View state ─────────────────────────────────────────────────────
    private Matrix       _viewMatrix   = Matrix.Identity;
    private ImageSource? _bgImage;
    private double       _ppd          = 1.0;   // pixels-per-dip for FormattedText

    // ── Selection & targeting ──────────────────────────────────────────
    private MapEntity?   _selected;
    private HashSet<int> _validTargets = [];

    // ── Public flags ───────────────────────────────────────────────────
    public bool       FogEnabled  { get; set; }
    public HashSet<int> FogRevealed { get; } = [];
    public int        GridOpacity { get; set; } = 180;

    // ── Internal flags ─────────────────────────────────────────────────
    private bool            _teleport;
    private bool            _originMode;
    private int             _highlightLoc = -1;
    private HashSet<string> _movedIds     = [];

    // ── Pan / corner-drag state ────────────────────────────────────────
    private Point?                _panStart;
    private int                   _dragCorner  = -1;
    private Point?                _dragStart;             // screen
    private (double X, double Y)? _dragOrigin;            // scene

    // ── Corner warp visuals ────────────────────────────────────────────
    private static readonly Color[] CornerColors =
    [
        Color.FromRgb(0, 229, 255),   // TL cyan
        Color.FromRgb(255, 64, 255),  // TR magenta
        Color.FromRgb(255, 255, 0),   // BL yellow
        Color.FromRgb(0, 255, 128),   // BR lime
    ];
    private static readonly string[] CornerLabels = ["TL", "TR", "BL", "BR"];
    private const double HandleR = 10;
    private const double HitR    = 16;

    // Static background brush (dark grey matching Python 30,30,30)
    private static readonly Brush BgBrush =
        new SolidColorBrush(Color.FromRgb(30, 30, 30));

    // ── Constructor ────────────────────────────────────────────────────

    public HexMapCanvas(
        HexGrid grid, MapEntityManager em,
        MapLocationManager lm, MapTerrainMap tm)
    {
        _grid = grid; _em = em; _lm = lm; _tm = tm;
        Focusable    = true;
        ClipToBounds = true;
    }

    // ── Public API ─────────────────────────────────────────────────────

    public void LoadImage(string path)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource   = new Uri(path, UriKind.Absolute);
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();
        _bgImage = bi;
        // Fit once we have layout; do it immediately if already laid out
        if (ActualWidth > 0) FitView();
        else SizeChanged += OnFirstSizeChanged;
        InvalidateVisual();
    }

    private void OnFirstSizeChanged(object s, SizeChangedEventArgs e)
    {
        SizeChanged -= OnFirstSizeChanged;
        FitView();
    }

    private void FitView()
    {
        if (_bgImage == null || ActualWidth == 0 || ActualHeight == 0) return;
        double sx = ActualWidth  / _bgImage.Width;
        double sy = ActualHeight / _bgImage.Height;
        double sc = Math.Min(sx, sy);
        double ox = (ActualWidth  - _bgImage.Width  * sc) / 2;
        double oy = (ActualHeight - _bgImage.Height * sc) / 2;
        _viewMatrix = new Matrix(sc, 0, 0, sc, ox, oy);
        InvalidateVisual();
    }

    public void Refresh() => InvalidateVisual();

    public void MarkMoved(string id)  { _movedIds.Add(id);    InvalidateVisual(); }
    public void ClearMoved()          { _movedIds.Clear();     InvalidateVisual(); }
    public void RevealHex(int node)   { FogRevealed.Add(node); InvalidateVisual(); }
    public void SetFogEnabled(bool v) { FogEnabled = v;        InvalidateVisual(); }
    public void SetGridOpacity(int v) { GridOpacity = Math.Clamp(v, 0, 255); InvalidateVisual(); }

    public void SetHighlightedLocation(int node)
    {
        _highlightLoc = node;
        InvalidateVisual();
    }

    public void SetTeleport(bool enabled)
    {
        _teleport = enabled;
        if (_selected != null) SetSelected(_selected);
    }

    public void SetSelected(MapEntity? entity)
    {
        _selected = entity;
        _validTargets.Clear();

        if (entity != null && !_movedIds.Contains(entity.Id))
        {
            if (_teleport)
            {
                foreach (var c in _grid.AllCells)
                    if (c.Number != entity.Node) _validTargets.Add(c.Number);
            }
            else
            {
                foreach (var n in _grid.NeighborNumbers(entity.Node))
                    _validTargets.Add(n);
            }

            if (!entity.Seafaring)
                _validTargets.RemoveWhere(n =>
                    MapTerrain.WaterTerrains.Contains(_tm.Get(n) ?? ""));
        }

        InvalidateVisual();
        EntitySelected?.Invoke(entity);
    }

    public void SetOriginClickMode(bool enabled)
    {
        _originMode = enabled;
        Cursor = enabled ? Cursors.Cross : Cursors.Arrow;
    }

    // ── Rendering ─────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        try { _ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip; }
        catch { _ppd = 1.0; }

        // Dark background
        dc.DrawRectangle(BgBrush, null, new Rect(RenderSize));

        // All map content drawn in scene coordinates
        dc.PushTransform(new MatrixTransform(_viewMatrix));
        DrawBackground(dc);
        DrawTerrain(dc);
        DrawGrid(dc);
        DrawLocations(dc);
        DrawEntities(dc);
        DrawFog(dc);
        DrawCornerHandles(dc);
        dc.Pop();
    }

    private void DrawBackground(DrawingContext dc)
    {
        if (_bgImage == null) return;
        dc.DrawImage(_bgImage, new Rect(0, 0, _bgImage.Width, _bgImage.Height));
    }

    // z-order 0.5 fill · 0.8 abbr
    private void DrawTerrain(DrawingContext dc)
    {
        double abbrSz = Math.Max(5, _grid.Size * 0.16);

        foreach (var cell in _grid.AllCells)
        {
            var t = _tm.Get(cell.Number);
            if (t is null) continue;

            var fill    = ParseColor(MapTerrain.GetColor(t));
            fill.A      = MapTerrain.GetAlpha(t);
            dc.DrawGeometry(new SolidColorBrush(fill), null,
                            MakePoly(_grid.Corners(cell.Q, cell.R)));

            var (cx, cy) = _grid.HexToPixel(cell.Q, cell.R);
            var ft = MakeText(MapTerrain.GetAbbr(t), abbrSz,
                              new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)));
            dc.DrawText(ft, new Point(cx - ft.Width / 2,
                                      cy - _grid.Size * 0.48));
        }
    }

    // z-order 1 grid · 2 numbers
    private void DrawGrid(DrawingContext dc)
    {
        int  op    = GridOpacity;
        byte opB   = B(op);
        byte opP   = B(op + 60);
        byte opD3  = B(op / 3);
        byte opD4  = B(op / 4);
        byte txtA  = B((int)(op * 1.2));

        var pNorm   = Pen(Color.FromArgb(opB,  80,  80,  80),  1);
        var pTgt    = Pen(Color.FromArgb(opP,  80,  200, 80),  2);
        var pSel    = Pen(Color.FromArgb(opP,  255, 200, 0),   3);
        var pLoc    = Pen(Color.FromArgb(opP,  180, 100, 255), 2);
        Brush bTgt  = new SolidColorBrush(Color.FromArgb(opD3, 80,  200, 80));
        Brush bSel  = new SolidColorBrush(Color.FromArgb(opD3, 255, 200, 0));
        Brush bLoc  = new SolidColorBrush(Color.FromArgb(opD4, 180, 100, 255));
        Brush bTxt  = new SolidColorBrush(Color.FromArgb(txtA, 210, 210, 210));
        double fSz  = Math.Max(6, _grid.Size * 0.28);
        int?   sel  = _selected?.Node;

        foreach (var cell in _grid.AllCells)
        {
            var geo      = MakePoly(_grid.Corners(cell.Q, cell.R));
            var (cx, cy) = _grid.HexToPixel(cell.Q, cell.R);

            bool isSel = cell.Number == sel;
            bool isTgt = _validTargets.Contains(cell.Number);
            bool isLoc = cell.Number == _highlightLoc;

            System.Windows.Media.Pen   pen;
            Brush? fill;
            if      (isSel) { pen = pSel;  fill = bSel;  }
            else if (isTgt) { pen = pTgt;  fill = bTgt;  }
            else if (isLoc) { pen = pLoc;  fill = bLoc;  }
            else            { pen = pNorm; fill = null;   }

            dc.DrawGeometry(fill, pen, geo);

            var ft = MakeText(cell.Number.ToString(), fSz, bTxt);
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    // z-order 3 diamonds · 4 names
    private void DrawLocations(DrawingContext dc)
    {
        double nameSz  = Math.Max(5, _grid.Size * 0.14);
        var    whitePen = Pen(Colors.White, 1.2);

        foreach (var loc in _lm.All)
        {
            var pos = _grid.PixelOf(loc.Node);
            if (pos is null) continue;
            var (cx, cy) = pos.Value;
            double h     = Math.Max(6.0, _grid.Size * 0.18);

            (double X, double Y)[] diamond =
            [
                (cx,     cy - h),
                (cx + h, cy),
                (cx,     cy + h),
                (cx - h, cy),
            ];
            var col = ParseColor(loc.Color);
            dc.DrawGeometry(new SolidColorBrush(col), whitePen, MakePoly(diamond));

            var ft = MakeText(loc.Name, nameSz, new SolidColorBrush(col));
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy + h + 2));
        }
    }

    // z-order 5 circle · 6 label · 7 tick · 8 star
    private void DrawEntities(DrawingContext dc)
    {
        double lblSz  = Math.Max(7, _grid.Size * 0.28);
        double tickSz = Math.Max(5, _grid.Size * 0.18);
        double starSz = Math.Max(6, _grid.Size * 0.24);
        var    boldTf = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                                     FontWeights.Bold, FontStretches.Normal);

        foreach (var entity in _em.TopLevel)
        {
            var pos = _grid.PixelOf(entity.Node);
            if (pos is null) continue;
            var (cx, cy) = pos.Value;
            double r     = _grid.Size * 0.38;
            bool isSel   = _selected?.Id == entity.Id;
            bool isMov   = _movedIds.Contains(entity.Id);

            var fillC = ParseColor(entity.Color);
            if (isMov) fillC.A = 110;

            System.Windows.Media.Pen border;
            if      (isSel) border = Pen(Colors.Yellow,                    3);
            else if (isMov) border = Pen(Color.FromRgb(90, 90, 90),       1);
            else            border = Pen(Colors.White,                     2);

            dc.DrawEllipse(new SolidColorBrush(fillC), border, new Point(cx, cy), r, r);

            var lblC = isMov ? Color.FromRgb(160, 160, 160) : Colors.White;
            var ft   = new FormattedText(entity.Label,
                           CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                           boldTf, lblSz, new SolidColorBrush(lblC), _ppd);
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));

            if (isMov)
            {
                var chk = MakeText("✓", tickSz,
                          new SolidColorBrush(Color.FromArgb(200, 120, 220, 120)));
                dc.DrawText(chk, new Point(cx + r * 0.4 - chk.Width  / 2,
                                           cy - r       - chk.Height * 0.6));
            }

            if (entity.IsPlayer)
            {
                var star = MakeText("★", starSz,
                           new SolidColorBrush(Color.FromArgb(230, 255, 210, 50)));
                dc.DrawText(star, new Point(cx - star.Width  / 2,
                                            cy - r - star.Height * 0.85));
            }
        }
    }

    // z-order 8 fog · 9 border/tint · 9.5 number
    private void DrawFog(DrawingContext dc)
    {
        if (!FogEnabled) return;

        int  op   = GridOpacity;
        byte opP  = B(op + 60);
        byte opD3 = B(op / 3);
        byte txtA = B((int)(op * 1.2));

        Brush fogFill  = new SolidColorBrush(Color.FromArgb(245, 0, 0, 0));
        Brush tgtFill  = new SolidColorBrush(Color.FromArgb(opD3, 80, 200, 80));
        var   pWhite   = Pen(Color.FromArgb(opP, 255, 255, 255), 1);
        var   pTgt     = Pen(Color.FromArgb(opP, 80,  200, 80),  2);
        Brush txtBrush = new SolidColorBrush(Color.FromArgb(txtA, 255, 255, 255));
        double fSz     = Math.Max(6, _grid.Size * 0.28);

        foreach (var cell in _grid.AllCells)
        {
            if (FogRevealed.Contains(cell.Number)) continue;

            var geo      = MakePoly(_grid.Corners(cell.Q, cell.R));
            var (cx, cy) = _grid.HexToPixel(cell.Q, cell.R);
            bool isTgt   = _validTargets.Contains(cell.Number);

            dc.DrawGeometry(fogFill, null, geo);

            if (isTgt) dc.DrawGeometry(tgtFill, pTgt,   geo);
            else       dc.DrawGeometry(null,    pWhite,  geo);

            var ft = MakeText(cell.Number.ToString(), fSz, txtBrush);
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    // z-order 10 handles · 11 labels
    private void DrawCornerHandles(DrawingContext dc)
    {
        var  c    = _grid.GetWarpCorners();
        var  pts  = new[] { c.TL, c.TR, c.BL, c.BR };
        var  wPen = Pen(Colors.White, 1.5);

        for (int i = 0; i < 4; i++)
        {
            var (cx, cy) = pts[i];
            dc.DrawEllipse(new SolidColorBrush(CornerColors[i]), wPen,
                           new Point(cx, cy), HandleR, HandleR);

            var ft = MakeText(CornerLabels[i], 7, Brushes.Black);
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    // ── Mouse handling ─────────────────────────────────────────────────

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var screen = e.GetPosition(this);
        var sp     = ToScene(screen);

        if (e.ChangedButton == MouseButton.Middle)
        {
            _panStart = screen;
            Cursor    = Cursors.SizeAll;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_originMode && e.ChangedButton == MouseButton.Left)
        {
            SetOriginClickMode(false);
            OriginClicked?.Invoke(sp.X, sp.Y);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            int ci = CornerHit(sp.X, sp.Y);
            if (ci >= 0)
            {
                _dragCorner = ci;
                _dragStart  = screen;
                var cur     = _grid.GetWarpCorners();
                _dragOrigin = new[] { cur.TL, cur.TR, cur.BL, cur.BR }[ci];
                Cursor      = Cursors.SizeAll;
                CaptureMouse();
                e.Handled   = true;
                return;
            }
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            var coord = _grid.PixelToNearest(sp.X, sp.Y);
            if (coord.HasValue)
            {
                var cell = _grid.Cell(coord.Value.Q, coord.Value.R);
                if (cell != null)
                {
                    var here = _em.AtNode(cell.Number);
                    if (here.Count > 0)
                    {
                        RightClickedEntity?.Invoke(here[0]);
                        e.Handled = true;
                        return;
                    }
                }
            }
            SetSelected(null);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            var coord = _grid.PixelToNearest(sp.X, sp.Y);
            if (coord is null) return;
            var cell = _grid.Cell(coord.Value.Q, coord.Value.R);
            if (cell is null) return;
            int clicked = cell.Number;

            if (_selected != null)
            {
                if (_validTargets.Contains(clicked))
                    MoveRequested?.Invoke(_selected, clicked);
                else
                {
                    var here = _em.AtNode(clicked);
                    SetSelected(here.Count > 0 ? here[0] : null);
                }
            }
            else
            {
                var here = _em.AtNode(clicked);
                if (here.Count > 0) SetSelected(here[0]);
            }
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var screen = e.GetPosition(this);

        if (_dragCorner >= 0 && _dragStart.HasValue && _dragOrigin.HasValue)
        {
            var sp0 = ToScene(_dragStart.Value);
            var sp1 = ToScene(screen);
            var (ox, oy) = _dragOrigin.Value;
            var newPt = (ox + sp1.X - sp0.X, oy + sp1.Y - sp0.Y);

            var cur = _grid.GetWarpCorners();
            _grid.SetWarpCorners(_dragCorner switch
            {
                0 => cur with { TL = newPt },
                1 => cur with { TR = newPt },
                2 => cur with { BL = newPt },
                3 => cur with { BR = newPt },
                _ => cur,
            });
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_panStart.HasValue && e.MiddleButton == MouseButtonState.Pressed)
        {
            var delta = screen - _panStart.Value;
            _panStart = screen;
            var m = _viewMatrix;
            m.Translate(delta.X, delta.Y);
            _viewMatrix = m;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.ChangedButton == MouseButton.Left && _dragCorner >= 0)
        {
            _dragCorner = -1;
            _dragStart  = null;
            _dragOrigin = null;
            Cursor      = Cursors.Arrow;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Middle && _panStart.HasValue)
        {
            _panStart = null;
            Cursor    = Cursors.Arrow;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        double factor   = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        var    mousePos = e.GetPosition(this);

        // Scale around the mouse position
        var scenePos = ToScene(mousePos);
        var m = _viewMatrix;
        m.Scale(factor, factor);
        _viewMatrix = m;

        // Correct so the scene point stays under the mouse
        var moved  = _viewMatrix.Transform(new Point(scenePos.X, scenePos.Y));
        var offset = mousePos - moved;
        m = _viewMatrix;
        m.Translate(offset.X, offset.Y);
        _viewMatrix = m;

        InvalidateVisual();
        e.Handled = true;
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private Point ToScene(Point screen)
    {
        if (!_viewMatrix.HasInverse) return screen;
        var inv = _viewMatrix;
        inv.Invert();
        return inv.Transform(screen);
    }

    private int CornerHit(double sx, double sy)
    {
        var c   = _grid.GetWarpCorners();
        var pts = new[] { c.TL, c.TR, c.BL, c.BR };
        for (int i = 0; i < 4; i++)
        {
            double dx = sx - pts[i].X, dy = sy - pts[i].Y;
            if (dx * dx + dy * dy <= HitR * HitR) return i;
        }
        return -1;
    }

    private FormattedText MakeText(string text, double size, Brush brush)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
               new Typeface("Segoe UI"), size, brush, _ppd);

    private static StreamGeometry MakePoly((double X, double Y)[] pts)
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();
        ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), isFilled: true, isClosed: true);
        for (int i = 1; i < pts.Length; i++)
            ctx.LineTo(new Point(pts[i].X, pts[i].Y), isStroked: true, isSmoothJoin: false);
        return geo;
    }

    private static System.Windows.Media.Pen Pen(Color color, double thickness)
        => new(new SolidColorBrush(color), thickness);

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { return Colors.Gray; }
    }

    // Clamp to 0–255 and return as byte
    private static byte B(int v) => (byte)Math.Clamp(v, 0, 255);
}
