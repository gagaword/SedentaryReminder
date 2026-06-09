using System.Windows;
using Wpf.Ui.Controls;

namespace SedentaryReminder;

public partial class SettingsWindow : FluentWindow
{
    /// <summary>用户点击“保存”时触发，携带新的设置。</summary>
    public event EventHandler<AppSettings>? Saved;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();

        IntervalBox.Value = current.IntervalMinutes;
        AutoStartSwitch.IsChecked = current.AutoStart;
        EnabledSwitch.IsChecked = current.Enabled;
        ForceModeSwitch.IsChecked = current.ForceMode;
        RestSecondsBox.Value = current.RestSeconds;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var minutes = (int)Math.Clamp(IntervalBox.Value ?? 45, 1, 600);
        var rest = (int)Math.Clamp(RestSecondsBox.Value ?? 30, 5, 600);
        var settings = new AppSettings
        {
            IntervalMinutes = minutes,
            AutoStart = AutoStartSwitch.IsChecked == true,
            Enabled = EnabledSwitch.IsChecked == true,
            ForceMode = ForceModeSwitch.IsChecked == true,
            RestSeconds = rest,
        };

        Saved?.Invoke(this, settings);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
