namespace CharacterSheet.Map;

/// <summary>
/// A single hex cell identified by axial coordinates and a sequential number.
/// </summary>
public record HexCell(int Q, int R, int Number);

/// <summary>
/// The four warp quad control points (top-left, top-right, bottom-left, bottom-right).
/// </summary>
public record WarpCorners(
    (double X, double Y) TL,
    (double X, double Y) TR,
    (double X, double Y) BL,
    (double X, double Y) BR);

/// <summary>
/// Axial-coordinate hex grid with optional bilinear warp. Ported from hex_grid.py.
///
/// Warp mode: call SetWarpCorners with four pixel positions to enable bilinear
/// warping. Each parametric hex position is normalised into [0,1]² relative to
/// the parametric bounding box, then bilinearly interpolated to the user-defined
/// quad. Individual hex-polygon corners are warped the same way, keeping the
/// tessellation gap-free. Call ResetWarp() to return to the parametric grid.
/// </summary>
public class HexGrid
{
    private static readonly double Sqrt3 = Math.Sqrt(3);

    // ── Configuration ──────────────────────────────────────────────────
    public double Size        { get; private set; }
    public (double X, double Y) Origin { get; private set; }
    public string Orientation { get; private set; }   // "flat" | "pointy"
    public int    Cols        { get; private set; }
    public int    Rows        { get; private set; }

    // ── Internal state ─────────────────────────────────────────────────
    private readonly Dictionary<(int Q, int R), HexCell> _cells      = [];
    private readonly Dictionary<int, (int Q, int R)>     _numToCoord = [];

    private WarpCorners? _warpCorners;
    private (double MinX, double MaxX, double MinY, double MaxY)? _warpBbox;

    public HexGrid(
        double size        = 40.0,
        (double X, double Y) origin = default,
        string orientation = "flat",
        int    cols        = 20,
        int    rows        = 15)
    {
        Size        = size;
        Origin      = origin;
        Orientation = orientation;
        Cols        = cols;
        Rows        = rows;
        Rebuild();
    }

    // ── Grid construction ──────────────────────────────────────────────

    private void Rebuild()
    {
        _cells.Clear();
        _numToCoord.Clear();
        int n = 1;

        if (Orientation == "flat")
        {
            for (int col = 0; col < Cols; col++)
            {
                int rOffset = -(col / 2);
                for (int row = 0; row < Rows; row++)
                {
                    int q = col, r = row + rOffset;
                    var cell = new HexCell(q, r, n);
                    _cells[(q, r)]  = cell;
                    _numToCoord[n]  = (q, r);
                    n++;
                }
            }
        }
        else // pointy
        {
            for (int row = 0; row < Rows; row++)
            {
                int qOffset = -(row / 2);
                for (int col = 0; col < Cols; col++)
                {
                    int q = col + qOffset, r = row;
                    var cell = new HexCell(q, r, n);
                    _cells[(q, r)]  = cell;
                    _numToCoord[n]  = (q, r);
                    n++;
                }
            }
        }

        _warpCorners = null;
        _warpBbox    = null;
        ComputeWarpBbox();
    }

    public void Reconfigure(
        double? size        = null,
        (double X, double Y)? origin = null,
        string? orientation = null,
        int?    cols        = null,
        int?    rows        = null)
    {
        if (size        != null) Size        = size.Value;
        if (origin      != null) Origin      = origin.Value;
        if (orientation != null) Orientation = orientation;
        if (cols        != null) Cols        = cols.Value;
        if (rows        != null) Rows        = rows.Value;
        Rebuild();
    }

    // ── Warp support ───────────────────────────────────────────────────

    private void ComputeWarpBbox()
    {
        if (_cells.Count == 0)
        {
            _warpBbox = (0, 1, 0, 1);
            return;
        }
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var (q, r) in _cells.Keys)
        {
            var (x, y) = ParametricHexToPixel(q, r);
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        _warpBbox = (minX, maxX, minY, maxY);
    }

    private (double X, double Y) ApplyWarp(double cx, double cy)
    {
        if (_warpCorners is null || _warpBbox is null) return (cx, cy);
        var (minX, maxX, minY, maxY) = _warpBbox.Value;
        var (tl, tr, bl, br)         = (_warpCorners.TL, _warpCorners.TR,
                                         _warpCorners.BL, _warpCorners.BR);
        double dx = maxX - minX == 0 ? 1.0 : maxX - minX;
        double dy = maxY - minY == 0 ? 1.0 : maxY - minY;
        double u  = (cx - minX) / dx;
        double v  = (cy - minY) / dy;
        double wx = (1 - u) * (1 - v) * tl.X + u * (1 - v) * tr.X
                  + (1 - u) *       v  * bl.X + u *       v  * br.X;
        double wy = (1 - u) * (1 - v) * tl.Y + u * (1 - v) * tr.Y
                  + (1 - u) *       v  * bl.Y + u *       v  * br.Y;
        return (wx, wy);
    }

    public WarpCorners GetWarpCorners()
    {
        if (_warpCorners != null) return _warpCorners;
        if (_warpBbox is null) ComputeWarpBbox();
        var (minX, maxX, minY, maxY) = _warpBbox!.Value;
        return new WarpCorners((minX, minY), (maxX, minY), (minX, maxY), (maxX, maxY));
    }

    public void SetWarpCorners(WarpCorners corners)
    {
        if (_warpBbox is null) ComputeWarpBbox();
        _warpCorners = corners;
    }

    public void ResetWarp() => _warpCorners = null;

    public bool IsWarped => _warpCorners != null;

    // ── Coordinate conversion ──────────────────────────────────────────

    private (double X, double Y) ParametricHexToPixel(int q, int r)
    {
        double ox = Origin.X, oy = Origin.Y, s = Size;
        if (Orientation == "flat")
            return (s * 1.5 * q + ox,
                    s * (Sqrt3 * r + Sqrt3 / 2 * q) + oy);
        else
            return (s * (Sqrt3 * q + Sqrt3 / 2 * r) + ox,
                    s * 1.5 * r + oy);
    }

    /// <summary>Returns the warped pixel centre of a hex.</summary>
    public (double X, double Y) HexToPixel(int q, int r) =>
        ApplyWarp(ParametricHexToPixel(q, r).X, ParametricHexToPixel(q, r).Y);

    /// <summary>
    /// Returns the (q, r) of the nearest cell to a pixel position, or null if
    /// no cell is in the grid.
    /// </summary>
    public (int Q, int R)? PixelToNearest(double px, double py) =>
        IsWarped ? PixelToNearestBruteforce(px, py)
                 : PixelToNearestParametric(px, py);

    private (int Q, int R)? PixelToNearestParametric(double px, double py)
    {
        double ox = Origin.X, oy = Origin.Y, s = Size;
        px -= ox; py -= oy;
        double fq, fr;
        if (Orientation == "flat")
        {
            fq = (2.0 / 3.0 * px) / s;
            fr = (-1.0 / 3.0 * px + Sqrt3 / 3.0 * py) / s;
        }
        else
        {
            fq = (Sqrt3 / 3.0 * px - 1.0 / 3.0 * py) / s;
            fr = (2.0 / 3.0 * py) / s;
        }
        var coord = CubeRound(fq, fr);
        return _cells.ContainsKey(coord) ? coord : null;
    }

    private (int Q, int R)? PixelToNearestBruteforce(double px, double py)
    {
        (int Q, int R)? best = null;
        double bestD2 = double.PositiveInfinity;
        foreach (var (coord, _) in _cells)
        {
            var (cx, cy) = HexToPixel(coord.Q, coord.R);
            double d2 = (px - cx) * (px - cx) + (py - cy) * (py - cy);
            if (d2 < bestD2) { bestD2 = d2; best = coord; }
        }
        return best;
    }

    private static (int Q, int R) CubeRound(double fq, double fr)
    {
        double fs = -fq - fr;
        int q = (int)Math.Round(fq), r = (int)Math.Round(fr), s = (int)Math.Round(fs);
        double dq = Math.Abs(q - fq), dr = Math.Abs(r - fr), ds = Math.Abs(s - fs);
        if (dq > dr && dq > ds) q = -r - s;
        else if (dr > ds)       r = -q - s;
        return (q, r);
    }

    // ── Geometry helpers ───────────────────────────────────────────────

    /// <summary>
    /// Returns the 6 warped pixel corners of the hex polygon.
    /// Each corner is warped from its parametric position independently,
    /// keeping the tessellation gap-free even under heavy warp.
    /// </summary>
    public (double X, double Y)[] Corners(int q, int r)
    {
        var (cx, cy) = ParametricHexToPixel(q, r);
        double angleOffset = Orientation == "flat" ? 0 : 30;
        var pts = new (double X, double Y)[6];
        for (int i = 0; i < 6; i++)
        {
            double rad = Math.PI / 180.0 * (60 * i + angleOffset);
            pts[i] = ApplyWarp(cx + Size * Math.Cos(rad), cy + Size * Math.Sin(rad));
        }
        return pts;
    }

    /// <summary>Returns all valid (q, r) neighbours of the given hex.</summary>
    public IReadOnlyList<(int Q, int R)> Neighbors(int q, int r)
    {
        (int dq, int dr)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1)];
        var result = new List<(int, int)>(6);
        foreach (var (dq, dr) in dirs)
        {
            var n = (q + dq, r + dr);
            if (_cells.ContainsKey(n)) result.Add(n);
        }
        return result;
    }

    // ── Lookup helpers ─────────────────────────────────────────────────

    public bool AreAdjacent(int n1, int n2)
    {
        if (!_numToCoord.TryGetValue(n1, out var c1)) return false;
        if (!_numToCoord.TryGetValue(n2, out var c2)) return false;
        return Neighbors(c1.Q, c1.R).Contains(c2);
    }

    public HexCell? Cell(int q, int r) =>
        _cells.TryGetValue((q, r), out var c) ? c : null;

    public HexCell? CellByNumber(int n)
    {
        if (!_numToCoord.TryGetValue(n, out var coord)) return null;
        return _cells.TryGetValue(coord, out var c) ? c : null;
    }

    public (double X, double Y)? PixelOf(int n)
    {
        if (!_numToCoord.TryGetValue(n, out var coord)) return null;
        return HexToPixel(coord.Q, coord.R);
    }

    public IReadOnlyList<int> NeighborNumbers(int n)
    {
        if (!_numToCoord.TryGetValue(n, out var coord)) return [];
        return [.. Neighbors(coord.Q, coord.R)
                    .Select(c => _cells[c].Number)];
    }

    public IReadOnlyList<HexCell> AllCells =>
        [.. _cells.Values.OrderBy(c => c.Number)];

    public int MaxNumber => _cells.Count;
}
