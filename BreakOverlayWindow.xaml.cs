using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SedentaryReminder;

/// <summary>
/// 强制休息全屏遮罩（每块屏幕一个实例）。倒计时数字 / ESC / 关闭由
/// <see cref="ForcedBreakController"/> 统一编排；外圈进度环在本窗口内自行平滑动画。
/// </summary>
public partial class BreakOverlayWindow : Window
{
    private const double Cx = 150, Cy = 150, Radius = 132;

    private readonly Native.RECT _bounds;

    internal BreakOverlayWindow(Native.RECT bounds)
    {
        InitializeComponent();
        _bounds = bounds;
    }

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(BreakOverlayWindow),
            new PropertyMetadata(1.0, (d, _) => ((BreakOverlayWindow)d).UpdateArc()));

    /// <summary>进度 1→0，驱动进度环消减。</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);

        // 先移到目标显示器，再交给系统最大化铺满整块屏（DPI 正确、内容真正居中）
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST,
            _bounds.Left, _bounds.Top, 100, 100, Native.SWP_SHOWWINDOW);
        WindowState = WindowState.Maximized;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_GETMINMAXINFO)
        {
            Native.ApplyFullMonitorMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void SetCountdown(int seconds) => CountdownText.Text = seconds.ToString();

    public void SetEscProgress(double fraction)
    {
        EscHint.Text = fraction <= 0
            ? "长按 ESC 3 秒可提前退出"
            : $"松开取消 · 正在退出 {(int)(fraction * 100)}%";
    }

    /// <summary>启动进度环：在总时长内从满到空平滑动画。</summary>
    public void StartRing(int totalSeconds)
    {
        UpdateArc();
        var anim = new DoubleAnimation(1.0, 0.0,
            new Duration(TimeSpan.FromSeconds(Math.Max(1, totalSeconds))));
        BeginAnimation(ProgressProperty, anim);
    }

    private void UpdateArc()
    {
        if (ProgressArc is null) return;

        double sweep = 360.0 * Progress;
        if (sweep <= 0.05)
        {
            ProgressArc.Data = null;
            return;
        }
        if (sweep >= 359.95) sweep = 359.95;

        Point start = PointOnCircle(-90);
        Point end = PointOnCircle(-90 + sweep);
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(Radius, Radius), 0,
            sweep > 180, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        ProgressArc.Data = geometry;
    }

    private static Point PointOnCircle(double angleDeg)
    {
        double a = angleDeg * Math.PI / 180.0;
        return new Point(Cx + Radius * Math.Cos(a), Cy + Radius * Math.Sin(a));
    }
}
