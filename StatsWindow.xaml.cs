using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SedentaryReminder;

/// <summary>
/// 统计窗口：今日数据卡片 + 近 7 天久坐时长柱状图。
/// 与设置窗口一样按需创建、关闭即释放。
/// </summary>
public partial class StatsWindow : FluentWindow
{
    private const double BarAreaHeight = 120;
    private static readonly Brush BarBrush =
        new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)); // 与休息遮罩同色系

    public StatsWindow(StatsStore stats)
    {
        InitializeComponent();

        var today = stats.Today;
        SittingValue.Text = FormatMinutes(today.SittingMinutes);
        RemindersValue.Text = today.Reminders.ToString();
        DoneValue.Text = today.BreaksCompleted.ToString();
        SkippedValue.Text = today.BreaksSkipped.ToString();

        BuildChart(stats.LastDays(7));
    }

    private void BuildChart(IReadOnlyList<DayStats> days)
    {
        int max = Math.Max(1, days.Max(d => d.SittingMinutes));

        for (int i = 0; i < days.Count; i++)
        {
            ChartGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var d = days[i];
            var cell = new Grid { VerticalAlignment = VerticalAlignment.Bottom };
            cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(BarAreaHeight) });
            cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var value = new TextBlock
            {
                Text = d.SittingMinutes > 0 ? FormatMinutes(d.SittingMinutes) : "",
                FontSize = 11,
                Opacity = 0.8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            };
            Grid.SetRow(value, 0);

            var bar = new Border
            {
                Width = 24,
                // 有数据的日子至少给 4px，避免柱体完全不可见
                Height = d.SittingMinutes > 0
                    ? Math.Max(4, BarAreaHeight * d.SittingMinutes / max)
                    : 0,
                Background = BarBrush,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            Grid.SetRow(bar, 1);

            var label = new TextBlock
            {
                Text = DayLabel(d, isToday: i == days.Count - 1),
                FontSize = 11,
                Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
            };
            Grid.SetRow(label, 2);

            cell.Children.Add(value);
            cell.Children.Add(bar);
            cell.Children.Add(label);
            Grid.SetColumn(cell, i);
            ChartGrid.Children.Add(cell);
        }
    }

    private static string DayLabel(DayStats d, bool isToday)
    {
        if (isToday) return "今天";
        return DateTime.TryParseExact(d.Date, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt.ToString("M/d")
            : d.Date;
    }

    private static string FormatMinutes(int minutes) =>
        minutes >= 60 ? $"{minutes / 60}h{minutes % 60:00}m" : $"{minutes}m";
}
