using System.Windows;
using H.NotifyIcon;
using Microsoft.Toolkit.Uwp.Notifications;

namespace SedentaryReminder;

public partial class App : Application
{
    private const string AppUserModelId = "SedentaryReminder.App";
    private const string MutexName = "SedentaryReminder.App.SingleInstance";

    private Mutex? _instanceMutex;
    private bool _isDuplicateInstance;
    private TaskbarIcon? _tray;
    private ReminderService? _reminder;
    private AppSettings _settings = new();
    private StatsStore _stats = new();
    private SettingsWindow? _settingsWindow;
    private StatsWindow? _statsWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // 单实例：拿不到命名互斥锁说明已有实例在运行，提示后退出
        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            _isDuplicateInstance = true;
            MessageBox.Show("久坐提醒已经在运行中了，请留意系统托盘右下角的图标。",
                "久坐提醒", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 兜底：UI 线程异常记录到日志，避免后台工具直接消失
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = true;
        };

        // Toast 通知需要一个 AppUserModelID（未打包应用）
        Native.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

        _settings = AppSettings.Load();
        _stats = StatsStore.Load();

        _tray = (TaskbarIcon)FindResource("TrayIcon");
        _tray.ForceCreate();

        _reminder = new ReminderService();
        _reminder.Elapsed += OnReminderElapsed;
        ApplySettingsToReminder();

        UpdateMenuState();

        // 启动峰值过后回收一次，压低常驻内存
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Native.TrimWorkingSet();
            }));
    }

    private void ApplySettingsToReminder()
    {
        if (_reminder is null) return;
        if (_settings.Enabled)
            _reminder.Start(TimeSpan.FromMinutes(_settings.IntervalMinutes));
        else
            _reminder.Stop();
    }

    private ForcedBreakController? _break;

    private void OnReminderElapsed(object? sender, EventArgs e)
    {
        // 计时线程触发，切回 UI 线程处理
        Dispatcher.Invoke(() =>
        {
            _stats.RecordReminder(_settings.IntervalMinutes);
            if (_settings.ForceMode)
                ShowForcedBreak();
            else
                ShowToast();
        });
    }

    private void ShowToast()
    {
        new ToastContentBuilder()
            .AddText("该起来活动一下了 🧍")
            .AddText($"你已经坐了 {_settings.IntervalMinutes} 分钟，起身走两步、喝口水吧。")
            .Show();
    }

    private void ShowForcedBreak()
    {
        if (_break is not null) return; // 已在休息中

        _reminder?.Stop(); // 休息期间暂停计时
        _break = new ForcedBreakController(_settings.RestSeconds);
        _break.Finished += (_, skipped) =>
        {
            _break = null;
            _stats.RecordBreak(skipped);
            ApplySettingsToReminder(); // 休息结束，开始下一轮计时
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => { GC.Collect(); GC.Collect(); Native.TrimWorkingSet(); }));
        };
        _break.Show();
    }

    private void OnTrayLeftClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void OnOpenSettings(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // 按需创建，关闭即释放，避免常驻占用
        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            // 窗口回收后再 GC，恢复低内存常驻
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => { GC.Collect(); GC.Collect(); Native.TrimWorkingSet(); }));
        };
        _settingsWindow.Saved += (_, newSettings) =>
        {
            _settings = newSettings;
            AppSettings.Save(_settings);
            AutoStartHelper.SetEnabled(_settings.AutoStart);
            ApplySettingsToReminder();
            UpdateMenuState();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnOpenStats(object sender, RoutedEventArgs e)
    {
        if (_statsWindow is not null)
        {
            _statsWindow.Activate();
            return;
        }

        // 与设置窗口同样按需创建、关闭即释放
        _statsWindow = new StatsWindow(_stats);
        _statsWindow.Closed += (_, _) =>
        {
            _statsWindow = null;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => { GC.Collect(); GC.Collect(); Native.TrimWorkingSet(); }));
        };
        _statsWindow.Show();
        _statsWindow.Activate();
    }

    private void OnResetTimer(object sender, RoutedEventArgs e)
    {
        if (_settings.Enabled)
            _reminder?.Start(TimeSpan.FromMinutes(_settings.IntervalMinutes));
    }

    private void OnTogglePause(object sender, RoutedEventArgs e)
    {
        _settings.Enabled = !_settings.Enabled;
        AppSettings.Save(_settings);
        ApplySettingsToReminder();
        UpdateMenuState();
    }

    private void UpdateMenuState()
    {
        if (StatusMenuItemRef is { } status)
            status.Header = _settings.Enabled ? "状态：运行中" : "状态：已暂停";
        if (PauseMenuItemRef is { } pause)
            pause.Header = _settings.Enabled ? "暂停提醒" : "恢复提醒";
    }

    // ContextMenu 在资源里，名称引用通过 LogicalTree 查找
    private System.Windows.Controls.MenuItem? StatusMenuItemRef =>
        _tray?.ContextMenu?.Items.OfType<System.Windows.Controls.MenuItem>()
            .FirstOrDefault(m => (m.Header as string)?.StartsWith("状态") == true);

    private System.Windows.Controls.MenuItem? PauseMenuItemRef =>
        _tray?.ContextMenu?.Items.OfType<System.Windows.Controls.MenuItem>()
            .FirstOrDefault(m => (m.Header as string) is "暂停提醒" or "恢复提醒");

    private void OnExitClick(object sender, RoutedEventArgs e) => Shutdown();

    private static void LogCrash(Exception ex)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SedentaryReminder");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // 记录失败也不能再抛
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        // 重复实例退出时不能清理 Toast 注册等共享状态，否则会影响正在运行的实例
        if (_isDuplicateInstance) return;

        _reminder?.Stop();
        _reminder?.Dispose();
        _tray?.Dispose();
        ToastNotificationManagerCompat.Uninstall();
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
    }
}
