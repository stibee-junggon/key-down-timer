using System.IO;
using System.Text.Json;

namespace GameTimer;

public class TimerEntry
{
    public int Key { get; set; }
    public string Name { get; set; } = "";
    public int Seconds { get; set; }
    public int Sound { get; set; }
    public bool Enabled { get; set; } = true;
}

public class AppSettings
{
    public List<TimerEntry> Timers { get; set; } = new();
    public int Volume { get; set; } = 60;
    public double Opacity { get; set; } = 0.7;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public string StopwatchName { get; set; } = "스톱워치";
}

public static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameTimer", "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch
        {
            settings = new AppSettings();
        }

        settings.Timers ??= new List<TimerEntry>();
        settings.Timers.RemoveAll(t => t.Seconds < 1 || t.Key <= 0);
        foreach (var t in settings.Timers)
        {
            t.Name ??= "";
            if (t.Sound < 0 || t.Sound >= Chime.Names.Count) t.Sound = 0;
        }
        if (string.IsNullOrWhiteSpace(settings.StopwatchName)) settings.StopwatchName = "스톱워치";
        settings.Volume = Math.Clamp(settings.Volume, 0, 100);
        settings.Opacity = Math.Clamp(settings.Opacity, 0.2, 1.0);
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}
