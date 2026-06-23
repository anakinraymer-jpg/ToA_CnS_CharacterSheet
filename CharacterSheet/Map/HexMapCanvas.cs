using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CharacterSheet.Map;

/// <summary>
/// WPF rendering canvas for the hex map.
///
/// Uses a DrawingVisual layer tree instead of FrameworkElement.OnRender so that
/// pan/zoom is a pure compositor transform — zero draw calls.  Individual layers
/// are redrawn only when their data actually changes.
///
/// Layer split:
///   _staticGridLayer   — all 1287 cells with normal border+number (rebuilt on grid/opacity change)
///   _gridOverlayLayer  — only selected/target/location cells (rebuilt on selection change, ~6 cells)
///   _fogLayer          — all unrevealed cells with combined fill+border (rebuilt on fog change)
///   _fogHighlightLayer — only valid-target cells in fog (rebuilt on selection change, ~6 cells)
///
/// This means clicking an entity only redraws ~12 cells total instead of ~5,000 draw commands.
/// </summary>
public class HexMapCanvas : FrameworkElement
{
    // ── Events ──────────────────────────────────────────────────────────
    public event Action<MapEntity?>?     EntitySelected;
    public event Action<MapEntity, int>? MoveRequested;
    public event Action<double, double>? OriginClicked;
    public event Action<MapEntity>?      RightClickedEntity;
    /// <summary>Fires when the user right-clicks an empty hex (no entity present).</summary>
    public event Action<int>?            RightClickedHex;
    /// <summary>Fires when the user left-clicks a hex while RevealMode is on.</summary>
    public event Action<int>?            HexRevealedInRevealMode;

    // ── Data sources ─────────────────────────────────────────────────
    private readonly HexGrid            _grid;
    private readonly MapEntityManager   _em;
    private readonly MapLocationManager _lm;
    private readonly MapTerrainMap      _tm;

    // ── View state ───────────────────────────────────────────────────
    private Matrix       _viewMatrix = Matrix.Identity;
    private ImageSource? _bgImage;
    private double       _ppd        = 1.0;   // pixels-per-dip for FormattedText

    // ── Selection & targeting ────────────────────────────────────────
    private MapEntity?   _selected;
    private HashSet<int> _validTargets = [];

    // ── Public flags ─────────────────────────────────────────────────
    public bool         FogEnabled  { get; set; }
    public HashSet<int> FogRevealed { get; } = [];
    public bool         RevealMode  { get; set; }
    public int          GridOpacity { get; set; } = 180;

    // ── Internal flags ───────────────────────────────────────────────
    private bool            _teleport;
    private int             _customMoveRange;   // 0 = off; >0 = BFS steps
    private bool            _originMode;
    private int             _highlightLoc = -1;
    private HashSet<string> _movedIds     = [];

    // ── Render caches (rebuilt only when grid layout changes) ────────
    private StreamGeometry[]?        _geoCache;
    private (double X, double Y)[]?  _centerCache;
    private FormattedText[]?         _numTextCache;
    private double                   _numTextSize = -1;
    private Dictionary<int, int>?    _nodeIndex;   // cell.Number → array index for O(1) lookup
    private static readonly Typeface EntityBoldTf = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    // ── Visual layer tree ────────────────────────────────────────────
    // _root carries the view transform via _rootTransform.  Setting .Matrix
    // is a pure compositor operation — zero draw calls on pan/zoom.
    private readonly MatrixTransform  _rootTransform = new();
    private readonly VisualCollection _visuals;
    private readonly ContainerVisual  _root;
    private readonly DrawingVisual    _bgLayer;
    private readonly DrawingVisual    _terrainLayer;
    private readonly DrawingVisual    _staticGridLayer;    // borders + numbers, all cells
    private readonly DrawingVisual    _gridOverlayLayer;   // fills + borders, highlighted cells only
    private readonly DrawingVisual    _locationLayer;
    private readonly DrawingVisual    _entityLayer;
    private readonly DrawingVisual    _fogLayer;           // fog fill+border, all unrevealed cells
    private readonly DrawingVisual    _fogHighlightLayer;  // green tint, valid-target cells in fog
    private readonly DrawingVisual    _handleLayer;

    protected override int    VisualChildrenCount        => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    // ── Pan / corner-drag state ──────────────────────────────────────
    private Point?                _panStart;
    private int                   _dragCorner = -1;
    private Point?                _dragStart;
    private (double X, double Y)? _dragOrigin;

    // ── Corner warp visuals ──────────────────────────────────────────
    private static readonly Color[] CornerColors =
    [
        Color.FromRgb(0, 229, 255),
        Color.FromRgb(255, 64, 255),
        Color.FromRgb(255, 255, 0),
        Color.FromRgb(0, 255, 128),
    ];
    private static readonly string[] CornerLabels = ["TL", "TR", "BL", "BR"];
    private const double HandleR = 10;
    private const double HitR    = 16;

    private static readonly Brush BgBrush =
        new SolidColorBrush(Color.FromRgb(30, 30, 30));

    // ── Constructor ──────────────────────────────────────────────────

    public HexMapCanvas(
        HexGrid grid, MapEntityManager em,
        MapLocationManager lm, MapTerrainMap tm)
    {
        _grid = grid; _em = em; _lm = lm; _tm = tm;
        Focusable    = true;
        ClipToBounds = true;

        _bgLayer           = new DrawingVisual();
        _terrainLayer      = new DrawingVisual();
        _staticGridLayer   = new DrawingVisual();
        _gridOverlayLayer  = new DrawingVisual();
        _locationLayer     = new DrawingVisual();
        _entityLayer       = new DrawingVisual();
        _fogLayer          = new DrawingVisual();
        _fogHighlightLayer = new DrawingVisual();
        _handleLayer       = new DrawingVisual();

        _root = new ContainerVisual();
        _root.Transform = _rootTransform;
        _root.Children.Add(_bgLayer);
        _root.Children.Add(_terrainLayer);
        _root.Children.Add(_staticGridLayer);
        _root.Children.Add(_gridOverlayLayer);
        _root.Children.Add(_locationLayer);
        _root.Children.Add(_entityLayer);
        _root.Children.Add(_fogLayer);
        _root.Children.Add(_fogHighlightLayer);
        _root.Children.Add(_handleLayer);

        _visuals = new VisualCollection(this) { _root };

        // Populate layers once the visual is connected to a window (DPI available)
        Loaded += async (_, _) => await PreWarmAsync();
    }

    // Pre-computes hex corner arrays on a background thread, then builds frozen
    // StreamGeometry objects on the UI thread and draws all layers.
    private async Task PreWarmAsync()
    {
        RefreshPpd();
        var cells = _grid.AllCells;
        int n = cells.Count;

        // Pure math — no WPF objects, safe on background thread
        var (cornersArr, centersArr) = await Task.Run(() =>
        {
            var corners = new (double X, double Y)[n][];
            var centers = new (double X, double Y)[n];
            for (int i = 0; i < n; i++)
            {
                corners[i] = _grid.Corners(cells[i].Q, cells[i].R);
                centers[i] = _grid.HexToPixel(cells[i].Q, cells[i].R);
            }
            return (corners, centers);
        });

        // Build + freeze StreamGeometry on UI thread (fast — no math left)
        var geos    = new StreamGeometry[n];
        var nodeIdx = new Dictionary<int, int>(n);
        for (int i = 0; i < n; i++)
        {
            var geo = MakePoly(cornersArr[i]);
            geo.Freeze();
            geos[i]               = geo;
            nodeIdx[cells[i].Number] = i;
        }
        _geoCache    = geos;
        _centerCache = centersArr;
        _nodeIndex   = nodeIdx;

        UpdateBgLayer();
        UpdateTerrainLayer();
        UpdateStaticGridLayer();
        UpdateGridOverlayLayer();
        UpdateLocationLayer();
        UpdateEntityLayer();
        UpdateFogLayer();
        UpdateFogHighlightLayer();
        UpdateHandleLayer();
    }

    // ── Public API ───────────────────────────────────────────────────

    public void LoadImage(string path)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource   = new Uri(path, UriKind.Absolute);
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();
        _bgImage = bi;
        if (ActualWidth > 0) FitView();
        else SizeChanged += OnFirstSizeChanged;
        UpdateBgLayer();
    }

    public async Task LoadImageAsync(string path)
    {
        var bi = await Task.Run(() =>
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource   = new Uri(path, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        });
        _bgImage = bi;
        if (ActualWidth > 0) FitView();
        else SizeChanged += OnFirstSizeChanged;
        UpdateBgLayer();
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
        ApplyViewTransform();
    }

    public void Refresh() => UpdateAllLayers();

    public void MarkMoved(string id)  { _movedIds.Add(id);    UpdateEntityLayer(); }
    public void ClearMoved()          { _movedIds.Clear();     UpdateEntityLayer(); }

    public void RevealHex(int node)
    {
        FogRevealed.Add(node);
        UpdateFogLayer();
        UpdateFogHighlightLayer();
        UpdateLocationLayer();
    }

    public void SetFogEnabled(bool v)
    {
        FogEnabled = v;
        UpdateFogLayer();
        UpdateFogHighlightLayer();
    }

    public void CenterOnNode(int node)
    {
        int idx = IndexOfNode(node);
        if (idx < 0 || _centerCache == null) return;
        var (wx, wy) = _centerCache[idx];
        var screenPt = _viewMatrix.Transform(new System.Windows.Point(wx, wy));
        double dx = ActualWidth  / 2 - screenPt.X;
        double dy = ActualHeight / 2 - screenPt.Y;
        var m = _viewMatrix;
        m.Translate(dx, dy);
        _viewMatrix = m;
        ApplyViewTransform();
    }

    public void SetGridOpacity(int v)
    {
        GridOpacity = Math.Clamp(v, 0, 255);
        UpdateStaticGridLayer();
        UpdateGridOverlayLayer();
        UpdateFogLayer();
        UpdateFogHighlightLayer();
    }

    /// <summary>Highlight a location hex on the grid — rebuilds only the tiny overlay layer.</summary>
    public void SetHighlightedLocation(int node)
    {
        _highlightLoc = node;
        UpdateGridOverlayLayer();
    }

    public void SetTeleport(bool enabled)
    {
        _teleport = enabled;
        if (_selected != null) SetSelected(_selected);
    }

    public void SetCustomMoveRange(int range)
    {
        _customMoveRange = range;
        if (_selected != null) SetSelected(_selected);
    }

    public void SetSelected(MapEntity? entity)
    {
        _selected = entity;
        _validTargets.Clear();

        if (entity != null && !_movedIds.Contains(entity.Id))
        {
            if (_customMoveRange > 0)
            {
                foreach (var n in _grid.ReachableNodes(entity.Node, _customMoveRange))
                    _validTargets.Add(n);
            }
            else if (_teleport)
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

        // Only rebuild the overlay layers — the large static grid/fog layers are unchanged.
        // In normal (non-teleport) mode this processes ~6 cells instead of 1,287.
        UpdateGridOverlayLayer();
        UpdateEntityLayer();
        UpdateFogHighlightLayer();
        EntitySelected?.Invoke(entity);
    }

    public MapEntity? SelectedEntity => _selected;

    public bool HasMoved(string id) => _movedIds.Contains(id);

    public int CountMoved(IEnumerable<string> ids) => ids.Count(_movedIds.Contains);

    public void ClearMovedId(string id) { _movedIds.Remove(id); UpdateEntityLayer(); }

    public void SetOriginClickMode(bool enabled)
    {
        _originMode = enabled;
        Cursor = enabled ? Cursors.Cross : Cursors.Arrow;
    }

    /// <summary>Clears geometry and text caches. Call when grid layout changes.</summary>
    public void InvalidateGeoCache()
    {
        _geoCache     = null;
        _centerCache  = null;
        _numTextCache = null;
        _numTextSize  = -1;
        _nodeIndex    = null;
    }

    // ── Layer management ─────────────────────────────────────────────

    private void ApplyViewTransform()
        => _rootTransform.Matrix = _viewMatrix;   // reuses existing MatrixTransform object

    private void UpdateAllLayers()
    {
        RefreshPpd();
        UpdateBgLayer();
        UpdateTerrainLayer();
        UpdateStaticGridLayer();
        UpdateGridOverlayLayer();
        UpdateLocationLayer();
        UpdateEntityLayer();
        UpdateFogLayer();
        UpdateFogHighlightLayer();
        UpdateHandleLayer();
    }

    private void RefreshPpd()
    {
        try { _ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip; }
        catch { _ppd = 1.0; }
    }

    private void UpdateBgLayer()
    {
        using var dc = _bgLayer.RenderOpen();
        if (_bgImage != null)
            dc.DrawImage(_bgImage, new Rect(0, 0, _bgImage.Width, _bgImage.Height));
    }

    private void UpdateTerrainLayer()      { using var dc = _terrainLayer.RenderOpen();      DrawTerrain(dc); }
    private void UpdateStaticGridLayer()   { using var dc = _staticGridLayer.RenderOpen();   DrawStaticGrid(dc); }
    private void UpdateGridOverlayLayer()  { using var dc = _gridOverlayLayer.RenderOpen();  DrawGridOverlay(dc); }
    private void UpdateLocationLayer()     { using var dc = _locationLayer.RenderOpen();     DrawLocations(dc); }
    private void UpdateEntityLayer()       { using var dc = _entityLayer.RenderOpen();       DrawEntities(dc); }
    private void UpdateFogLayer()          { using var dc = _fogLayer.RenderOpen();          DrawFog(dc); }
    private void UpdateFogHighlightLayer() { using var dc = _fogHighlightLayer.RenderOpen(); DrawFogHighlights(dc); }
    private void UpdateHandleLayer()       { using var dc = _handleLayer.RenderOpen();       DrawCornerHandles(dc); }

    // OnRender draws only the dark background fill.  All map content lives in the
    // DrawingVisual layer tree above, so pan/zoom never triggers this method.
    protected override void OnRender(DrawingContext dc)
        => dc.DrawRectangle(BgBrush, null, new Rect(RenderSize));

    // ── Geometry / text caches ───────────────────────────────────────

    private void EnsureGeoCache()
    {
        var cells = _grid.AllCells;
        if (_geoCache?.Length == cells.Count) return;
        int n         = cells.Count;
        _geoCache     = new StreamGeometry[n];
        _centerCache  = new (double, double)[n];
        _nodeIndex    = new Dictionary<int, int>(n);
        _numTextCache = null;
        for (int i = 0; i < n; i++)
        {
            var c   = cells[i];
            var geo = MakePoly(_grid.Corners(c.Q, c.R));
            geo.Freeze();
            _geoCache[i]        = geo;
            _centerCache[i]     = _grid.HexToPixel(c.Q, c.R);
            _nodeIndex[c.Number] = i;
        }
    }

    private void EnsureNumTextCache(double fSz)
    {
        if (_numTextCache != null && Math.Abs(_numTextSize - fSz) < 0.01) return;
        var cells = _grid.AllCells;
        int n     = cells.Count;
        _numTextCache = new FormattedText[n];
        for (int i = 0; i < n; i++)
            _numTextCache[i] = new FormattedText(
                cells[i].Number.ToString(),
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), fSz, Brushes.White, _ppd);
        _numTextSize = fSz;
    }

    private int IndexOfNode(int node)
        => _nodeIndex != null && _nodeIndex.TryGetValue(node, out int idx) ? idx : -1;

    // ── Draw methods ─────────────────────────────────────────────────

    private void DrawTerrain(DrawingContext dc)
    {
        EnsureGeoCache();
        double abbrSz = Math.Max(5, _grid.Size * 0.16);
        var    cells  = _grid.AllCells;

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var t    = _tm.Get(cell.Number);
            if (t is null) continue;

            var fill = ParseColor(MapTerrain.GetColor(t));
            fill.A   = MapTerrain.GetAlpha(t);
            dc.DrawGeometry(new SolidColorBrush(fill), null, _geoCache![i]);

            var (cx, cy) = _centerCache![i];
            var ft = MakeText(MapTerrain.GetAbbr(t), abbrSz,
                              new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)));
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - _grid.Size * 0.48));
        }
    }

    // Static grid: all cells with normal border + number.  No selection highlights.
    // Rebuilt only when the grid layout or opacity changes.
    private void DrawStaticGrid(DrawingContext dc)
    {
        int  op   = GridOpacity;
        byte opB  = B(op);
        byte txtA = B((int)(op * 1.2));

        var   pNorm = Pen(Color.FromArgb(opB, 80, 80, 80), 1);
        Brush bTxt  = new SolidColorBrush(Color.FromArgb(txtA, 210, 210, 210));
        double fSz  = Math.Max(6, _grid.Size * 0.28);

        EnsureGeoCache();
        EnsureNumTextCache(fSz);
        var cells = _grid.AllCells;

        for (int i = 0; i < cells.Count; i++)
        {
            dc.DrawGeometry(null, pNorm, _geoCache![i]);
            var ft = _numTextCache![i];
            ft.SetForegroundBrush(bTxt);
            var (cx, cy) = _centerCache![i];
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    // Overlay: only highlighted cells (selected / valid-target / location).
    // Drawn on top of the static grid.  In normal play processes ~6 cells.
    private void DrawGridOverlay(DrawingContext dc)
    {
        int  op   = GridOpacity;
        byte opP  = B(op + 60);
        byte opD3 = B(op / 3), opD4 = B(op / 4);
        byte txtA = B((int)(op * 1.2));

        var   pTgt  = Pen(Color.FromArgb(opP,  80, 200,  80), 2);
        var   pSel  = Pen(Color.FromArgb(opP, 255, 200,   0), 3);
        var   pLoc  = Pen(Color.FromArgb(opP, 180, 100, 255), 2);
        Brush bTgt  = new SolidColorBrush(Color.FromArgb(opD3,  80, 200,  80));
        Brush bSel  = new SolidColorBrush(Color.FromArgb(opD3, 255, 200,   0));
        Brush bLoc  = new SolidColorBrush(Color.FromArgb(opD4, 180, 100, 255));
        Brush bTxt  = new SolidColorBrush(Color.FromArgb(txtA, 210, 210, 210));
        double fSz  = Math.Max(6, _grid.Size * 0.28);

        EnsureGeoCache();
        EnsureNumTextCache(fSz);

        // Draw: fill + brighter border + number on top (number must be last so fill doesn't cover it)
        void Draw(int node, Brush fill, System.Windows.Media.Pen pen)
        {
            int i = IndexOfNode(node);
            if (i < 0) return;
            dc.DrawGeometry(fill, pen, _geoCache![i]);
            var ft = _numTextCache![i];
            ft.SetForegroundBrush(bTxt);
            var (cx, cy) = _centerCache![i];
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }

        // Priority order: targets (lowest), location, selected (highest — drawn last)
        foreach (var node in _validTargets)
            Draw(node, bTgt, pTgt);

        if (_highlightLoc >= 0)
            Draw(_highlightLoc, bLoc, pLoc);

        if (_selected != null)
            Draw(_selected.Node, bSel, pSel);
    }

    private void DrawLocations(DrawingContext dc)
    {
        double nameSz   = Math.Max(5, _grid.Size * 0.14);
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

    private void DrawEntities(DrawingContext dc)
    {
        double lblSz  = Math.Max(7, _grid.Size * 0.28);
        double tickSz = Math.Max(5, _grid.Size * 0.18);
        double starSz = Math.Max(6, _grid.Size * 0.24);

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
            if      (isSel) border = Pen(Colors.Yellow,              3);
            else if (isMov) border = Pen(Color.FromRgb(90, 90, 90), 1);
            else            border = Pen(Colors.White,               2);

            dc.DrawEllipse(new SolidColorBrush(fillC), border, new Point(cx, cy), r, r);

            var lblC = isMov ? Color.FromRgb(160, 160, 160) : Colors.White;
            var ft   = new FormattedText(entity.Label,
                           CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                           EntityBoldTf, lblSz, new SolidColorBrush(lblC), _ppd);
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

    // Static fog: all unrevealed cells with combined fill+border in a single DrawGeometry call.
    // Rebuilt only when FogRevealed changes, fog is toggled, or opacity changes.
    private void DrawFog(DrawingContext dc)
    {
        if (!FogEnabled) return;

        EnsureGeoCache();
        int  op   = GridOpacity;
        byte opP  = B(op + 60);
        byte txtA = B((int)(op * 1.2));
        double fSz = Math.Max(6, _grid.Size * 0.28);

        Brush fogFill  = new SolidColorBrush(Color.FromArgb(245, 0, 0, 0));
        var   pWhite   = Pen(Color.FromArgb(opP, 255, 255, 255), 1);
        Brush txtBrush = new SolidColorBrush(Color.FromArgb(txtA, 255, 255, 255));

        EnsureNumTextCache(fSz);
        var cells = _grid.AllCells;

        for (int i = 0; i < cells.Count; i++)
        {
            if (FogRevealed.Contains(cells[i].Number)) continue;
            dc.DrawGeometry(fogFill, pWhite, _geoCache![i]);   // fill+border combined
            var ft = _numTextCache![i];
            ft.SetForegroundBrush(txtBrush);
            var (cx, cy) = _centerCache![i];
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    // Fog highlights: green tint over valid-target cells still covered by fog.
    // Only processes _validTargets (typically 6 cells in normal mode) — nearly instant.
    private void DrawFogHighlights(DrawingContext dc)
    {
        if (!FogEnabled || _validTargets.Count == 0) return;

        EnsureGeoCache();
        int  op   = GridOpacity;
        byte opP  = B(op + 60);
        byte opD3 = B(op / 3);

        Brush tgtFill = new SolidColorBrush(Color.FromArgb(opD3, 80, 200, 80));
        var   pTgt    = Pen(Color.FromArgb(opP, 80, 200, 80), 2);

        foreach (var node in _validTargets)
        {
            if (FogRevealed.Contains(node)) continue;
            int i = IndexOfNode(node);
            if (i < 0) continue;
            dc.DrawGeometry(tgtFill, pTgt, _geoCache![i]);
        }
    }

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

    // ── Mouse handling ───────────────────────────────────────────────

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
                    RightClickedHex?.Invoke(cell.Number);
                    e.Handled = true;
                    return;
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

            if (RevealMode)
            {
                RevealHex(clicked);
                HexRevealedInRevealMode?.Invoke(clicked);
                e.Handled = true;
                return;
            }

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
            var sp0      = ToScene(_dragStart.Value);
            var sp1      = ToScene(screen);
            var (ox, oy) = _dragOrigin.Value;
            var newPt    = (ox + sp1.X - sp0.X, oy + sp1.Y - sp0.Y);

            var cur = _grid.GetWarpCorners();
            _grid.SetWarpCorners(_dragCorner switch
            {
                0 => cur with { TL = newPt },
                1 => cur with { TR = newPt },
                2 => cur with { BL = newPt },
                3 => cur with { BR = newPt },
                _ => cur,
            });
            // Grid layout changed — rebuild caches and redraw all geo-dependent layers
            InvalidateGeoCache();
            UpdateTerrainLayer();
            UpdateStaticGridLayer();
            UpdateGridOverlayLayer();
            UpdateFogLayer();
            UpdateFogHighlightLayer();
            UpdateHandleLayer();
            e.Handled = true;
            return;
        }

        if (_panStart.HasValue && e.MiddleButton == MouseButtonState.Pressed)
        {
            var delta   = screen - _panStart.Value;
            _panStart   = screen;
            var m       = _viewMatrix;
            m.Translate(delta.X, delta.Y);
            _viewMatrix = m;
            ApplyViewTransform();   // zero draw calls — compositor repositions layer tree
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

        ApplyViewTransform();   // zero draw calls
        e.Handled = true;
    }

    // ── Helpers ──────────────────────────────────────────────────────

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

    private static byte B(int v) => (byte)Math.Clamp(v, 0, 255);
}
