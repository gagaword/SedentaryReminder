using System.IO;
using System.Text.Json;

namespace SedentaryReminder;

/// <summary>单日统计数据。</summary>
public sealed class DayStats
{
    /// <summary>日期，格式 yyyy-MM-dd。</summary>
    public string Date { get; set; } = "";
    public int SittingMinutes { get; set; }
    public int Reminders { get; set; }
    public int BreaksCompleted { get; set; }
    public int BreaksSkipped { get; set; }
}

/// <summary>
/// 每日统计：久坐时长、提醒次数、完成/跳过休息次数。
/// JSON 持久化到 %AppData%\SedentaryReminder\stats.json，仅保留最近 60 天。
/// 所有方法只在 UI 线程调用（提醒事件已切回 UI 线程），无需加锁。
/// </summary>
public sealed class StatsStore
{
    private const int KeepDays = 60;

    private readonly Dictionary<string, DayStats> _days = new();

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SedentaryReminder");

    private static string StatsPath => Path.Combine(ConfigDir, "stats.json");

    /// <summary>今日记录（不存在则创建，但在首次写入前不会落盘）。</summary>
    public DayStats Today
    {
        get
        {
            var key = DateTime.Now.ToString("yyyy-MM-dd");
            if (!_days.TryGetValue(key, out var d))
            {
                d = new DayStats { Date = key };
                _days[key] = d;
            }
            return d;
        }
    }

    /// <summary>到点提醒触发一次：累计提醒次数与本轮久坐分钟数。</summary>
    public void RecordReminder(int sittingMinutes)
    {
        var today = Today;
        today.Reminders++;
        today.SittingMinutes += sittingMinutes;
        Save();
    }

    /// <summary>强制休息结束：按是否被 ESC 提前退出分别计数。</summary>
    public void RecordBreak(bool skipped)
    {
        var today = Today;
        if (skipped) today.BreaksSkipped++;
        else today.BreaksCompleted++;
        Save();
    }

    /// <summary>最近 n 天的记录（含今天），从旧到新排列，缺失的日期补零。</summary>
    public IReadOnlyList<DayStats> LastDays(int n)
    {
        var list = new List<DayStats>(n);
        var today = DateTime.Now.Date;
        for (int i = n - 1; i >= 0; i--)
        {
            var key = today.AddDays(-i).ToString("yyyy-MM-dd");
            list.Add(_days.TryGetValue(key, out var d) ? d : new DayStats { Date = key });
        }
        return list;
    }

    public static StatsStore Load()
    {
        var store = new StatsStore();
        try
        {
            if (File.Exists(StatsPath))
            {
                var json = File.ReadAllText(StatsPath);
                var days = JsonSerializer.Deserialize<List<DayStats>>(json);
                if (days is not null)
                {
                    foreach (var d in days)
                    {
                        if (!string.IsNullOrEmpty(d.Date))
                            store._days[d.Date] = d;
                    }
                }
            }
        }
        catch
        {
            // 数据损坏时从空白开始，不影响主功能
        }
        return store;
    }

    private void Save()
    {
        try
        {
            Prune();
            Directory.CreateDirectory(ConfigDir);
            var ordered = _days.Values.OrderBy(d => d.Date).ToList();
            var json = JsonSerializer.Serialize(ordered,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StatsPath, json);
        }
        catch
        {
            // 写入失败不致命
        }
    }

    private void Prune()
    {
        var cutoff = DateTime.Now.Date.AddDays(-KeepDays).ToString("yyyy-MM-dd");
        // yyyy-MM-dd 字典序与日期序一致，可直接字符串比较
        var stale = _days.Keys.Where(k => string.Compare(k, cutoff, StringComparison.Ordinal) < 0).ToList();
        foreach (var k in stale) _days.Remove(k);
    }
}
