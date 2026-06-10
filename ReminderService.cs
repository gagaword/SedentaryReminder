using System.Threading;

namespace SedentaryReminder;

/// <summary>
/// 久坐计时核心。使用轻量的 System.Threading.Timer，
/// 不占用 UI 线程，常驻开销极小。
/// </summary>
public sealed class ReminderService : IDisposable
{
    private readonly object _gate = new();
    private Timer? _timer;
    private TimeSpan _interval;
    private DateTime _nextFireUtc;

    /// <summary>到达设定间隔时触发（在线程池线程上）。</summary>
    public event EventHandler? Elapsed;

    public bool IsRunning { get; private set; }

    /// <summary>距下次提醒的剩余时间；未在计时时为 null。</summary>
    public TimeSpan? Remaining
    {
        get
        {
            lock (_gate)
            {
                if (_timer is null) return null;
                var left = _nextFireUtc - DateTime.UtcNow;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }
    }

    public void Start(TimeSpan interval)
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _interval = interval;
            _nextFireUtc = DateTime.UtcNow + interval;
            _timer = new Timer(OnTick, null, interval, interval);
            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            IsRunning = false;
        }
    }

    private void OnTick(object? state)
    {
        // 周期计时器会继续跑，刷新下一轮的到点时间
        lock (_gate) { _nextFireUtc = DateTime.UtcNow + _interval; }
        Elapsed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}
