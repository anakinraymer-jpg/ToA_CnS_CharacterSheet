using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CharacterSheet.Controls;

public sealed class MapHost : HwndHost
{
    private readonly string _mapDir;
    private Process? _process;
    private IntPtr _hwndHost;
    private IntPtr _childHwnd;

    public bool IsRunning => _process is { HasExited: false };

    public event Action? EmbedReady;
    public event Action<string>? EmbedFailed;

    public MapHost(string mapDir) => _mapDir = mapDir;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hwndHost = CreateWindowEx(0, "static", "",
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 800, 600,
            hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        _ = EmbedAsync();
        return new HandleRef(this, _hwndHost);
    }

    protected override void DestroyWindowCore(HandleRef hwnd) => Stop();

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SIZE && _childHwnd != IntPtr.Zero)
        {
            GetClientRect(hwnd, out RECT r);
            MoveWindow(_childHwnd, 0, 0, r.right, r.bottom, true);
        }
        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
        }
        _process?.Dispose();
        _process = null;
        _childHwnd = IntPtr.Zero;
    }

    private async Task EmbedAsync()
    {
        try
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo("python", "main.py")
                {
                    WorkingDirectory = _mapDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            _process.Start();

            // Wait for the PyQt6 main window to appear
            try { _process.WaitForInputIdle(10000); } catch { }

            IntPtr child = IntPtr.Zero;
            for (int i = 0; i < 60 && child == IntPtr.Zero; i++)
            {
                await Task.Delay(250);
                if (_process.HasExited) break;
                _process.Refresh();
                child = _process.MainWindowHandle;
            }

            if (child == IntPtr.Zero)
            {
                Dispatcher.Invoke(() => EmbedFailed?.Invoke("Map window did not appear. Check that Python and PyQt6 are installed."));
                return;
            }

            // Hide during reparent to avoid visual glitch
            ShowWindow(child, SW_HIDE);

            // Strip title bar and border
            int style = GetWindowLong(child, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_BORDER);
            SetWindowLong(child, GWL_STYLE, style);

            int exStyle = GetWindowLong(child, GWL_EXSTYLE);
            exStyle &= ~(WS_EX_DLGMODALFRAME | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE);
            SetWindowLong(child, GWL_EXSTYLE, exStyle);

            // Reparent into our host window
            SetParent(child, _hwndHost);
            _childHwnd = child;

            // Resize to fill the host
            GetClientRect(_hwndHost, out RECT r);
            MoveWindow(_childHwnd, 0, 0, r.right, r.bottom, true);

            ShowWindow(_childHwnd, SW_SHOW);

            Dispatcher.Invoke(() => EmbedReady?.Invoke());
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => EmbedFailed?.Invoke(ex.Message));
        }
    }

    // ── Win32 constants ───────────────────────────────────────────────────────

    const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000;
    const int WS_CLIPCHILDREN = 0x02000000, WS_CLIPSIBLINGS = 0x04000000;
    const int WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_BORDER = 0x00800000;
    const int WS_SYSMENU = 0x00080000, WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000;
    const int WS_EX_DLGMODALFRAME = 0x00000001, WS_EX_WINDOWEDGE = 0x00000100;
    const int WS_EX_CLIENTEDGE = 0x00000200, WS_EX_STATICEDGE = 0x00020000;
    const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    const int WM_SIZE = 0x0005;
    const int SW_HIDE = 0, SW_SHOW = 5;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")] static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
