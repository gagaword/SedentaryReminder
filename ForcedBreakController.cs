using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SedentaryReminder;

/// <summary>
/// 编排强制休息：为每块显示器创建全屏遮罩、跑统一倒计时、
/// 拦截关闭、抢回焦点，并支持长按 ESC 3 秒应急退出。
/// </summary>
public sealed class ForcedBreakController
{
    private const double EscHoldMs = 3000;

    private readonly List<BreakOverlayWindow> _windows = new();
    private readonly DispatcherTimer _countdown;
    private readonly DispatcherTimer _escHold;
    private int _remaining;
    private bool _closing;
    private bool _escDown;
    private double _escElapsed;
    private BreakOverlayWindow? _focusWindow;

    /// <summary>休息结束时触发；参数为 true 表示被长按 ESC 提前退出。</summary>
    public event EventHandler<bool>? Finished;

    public ForcedBreakController(int restSeconds)
    {
        _remaining = Math.Max(1, restSeconds);

        _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdown.Tick += OnCountdownTick;

        _escHold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _escHold.Tick += OnEscHoldTick;
    }

    public void Show()
    {
        foreach (var (rect, primary) in Native.GetMonitors())
        {
            var w = new BreakOverlayWindow(rect);
            w.SetCountdown(_remaining);
            w.KeyDown += OnKeyDown;
            w.KeyUp += OnKeyUp;
            w.Closing += OnWindowClosing;
            w.Deactivated += OnWindowDeactivated;
            _windows.Add(w);
            if (primary) _focusWindow = w;
        }

        if (_windows.Count == 0) // 极端兜底：拿不到显示器信息
        {
            Finished?.Invoke(this, false);
            return;
        }

        _focusWindow ??= _windows[0];
        Native.PlaySoftAlert(); // 轻提示音，拉回注意力
        foreach (var w in _windows)
        {
            w.Show();
            w.StartRing(_remaining); // 进度环在总时长内平滑消减
        }
        _focusWindow.Activate();
        _focusWindow.Focus();
        _countdown.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _remaining--;
        if (_remaining <= 0)
        {
            CloseAll(skipped: false);
            return;
        }
        foreach (var w in _windows) w.SetCountdown(_remaining);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !_escDown)
        {
            _escDown = true;
            _escElapsed = 0;
            _escHold.Start();
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _escDown = false;
            _escHold.Stop();
            foreach (var w in _windows) w.SetEscProgress(0);
        }
    }

    private void OnEscHoldTick(object? sender, EventArgs e)
    {
        _escElapsed += _escHold.Interval.TotalMilliseconds;
        var frac = Math.Min(1.0, _escElapsed / EscHoldMs);
        foreach (var w in _windows) w.SetEscProgress(frac);
        if (_escElapsed >= EscHoldMs) CloseAll(skipped: true);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_closing || _focusWindow is null) return;
        // 抢回焦点：保证 ESC 仍被捕获、遮罩保持在最前
        _focusWindow.Dispatcher.BeginInvoke(() =>
        {
            if (!_closing && _focusWindow is { IsActive: false })
                _focusWindow.Activate();
        });
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // 倒计时未走完 / 未长按 ESC 前，阻止任何关闭（含 Alt+F4）
        if (!_closing) e.Cancel = true;
    }

    private void CloseAll(bool skipped)
    {
        if (_closing) return;
        _closing = true;
        _countdown.Stop();
        _escHold.Stop();
        foreach (var w in _windows)
        {
            try { w.Close(); } catch { /* 忽略 */ }
        }
        _windows.Clear();
        Finished?.Invoke(this, skipped);
    }
}
