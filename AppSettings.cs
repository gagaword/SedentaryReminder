using System.IO;
using System.Text.Json;

namespace SedentaryReminder;

public sealed class AppSettings
{
    public int IntervalMinutes { get; set; } = 45;
    public bool AutoStart { get; set; } = false;
    public bool Enabled { get; set; } = true;

    /// <summary>强制模式：到点全屏遮罩提醒，而非系统 Toast。</summary>
    public bool ForceMode { get; set; } = false;

    /// <summary>强制模式下的休息倒计时秒数。</summary>
    public int RestSeconds { get; set; } = 30;

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SedentaryReminder");

    private static string ConfigPath => Path.Combine(ConfigDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s is not null)
                {
                    s.IntervalMinutes = Math.Clamp(s.IntervalMinutes, 1, 600);
                    s.RestSeconds = Math.Clamp(s.RestSeconds, 5, 600);
                    return s;
                }
            }
        }
        catch
        {
            // 配置损坏时回退默认值
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // 写入失败不致命
        }
    }
}
