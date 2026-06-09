using System.Runtime.InteropServices;

namespace SedentaryReminder;

internal static class Native
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);

    private const uint MB_ICONASTERISK = 0x40;

    /// <summary>播放一声柔和的系统提示音（跟随用户的声音方案）。</summary>
    internal static void PlaySoftAlert()
    {
        try { MessageBeep(MB_ICONASTERISK); } catch { /* 无声不致命 */ }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

    /// <summary>
    /// 把当前进程的工作集裁到最小，让任务管理器里的常驻内存回落。
    /// 页在被再次访问时按需换回，对空闲的后台工具几乎无副作用。
    /// </summary>
    internal static void TrimWorkingSet()
    {
        try
        {
            SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1));
        }
        catch
        {
            // 裁剪失败不致命
        }
    }

    // ---- 多显示器 + 窗口定位（强制全屏遮罩用） ----

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITORINFOF_PRIMARY = 0x1;
    private const uint MONITOR_DEFAULTTONEAREST = 0x2;

    /// <summary>
    /// 处理 WM_GETMINMAXINFO：让无边框窗口最大化时铺满整块显示器（含任务栏区域），
    /// 由系统按该屏 DPI 计算，避免手动定位导致的缩放错位。
    /// </summary>
    internal static void ApplyFullMonitorMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMon, ref mi)) return;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var r = mi.rcMonitor;
        mmi.ptMaxPosition = new POINT { X = 0, Y = 0 };
        mmi.ptMaxSize = new POINT { X = r.Width, Y = r.Height };
        mmi.ptMaxTrackSize = new POINT { X = r.Width, Y = r.Height };
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    /// <summary>枚举所有显示器的物理像素边界（含是否为主屏）。</summary>
    internal static List<(RECT Rect, bool Primary)> GetMonitors()
    {
        var list = new List<(RECT, bool)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr d) =>
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMon, ref mi))
                    list.Add((mi.rcMonitor, (mi.dwFlags & MONITORINFOF_PRIMARY) != 0));
                return true;
            }, IntPtr.Zero);
        return list;
    }
}
