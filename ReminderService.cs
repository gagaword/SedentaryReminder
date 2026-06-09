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

    /// <summary>到达设定间隔时触发（在线程池线程上）。</summary>
    public event EventHandler? Elapsed;

    public bool IsRunning { get; private set; }

    public void Start(TimeSpan interval)
    {
        lock (_gate)
        {
            _timer?.Dispose();
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

    private void OnTick(object? state) => Elapsed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => Stop();
}
