using System.Text.Json;

namespace AjazzBattery;

internal sealed class AppConfig
{
    public int PollSeconds { get; set; } = 60;
    public int LowBatteryThreshold { get; set; } = 20;
    public bool Notifications { get; set; } = true;
    public bool RunAtStartup { get; set; } = false;
    public bool ShowPercentOnIcon { get; set; } = true;
    public bool ShowTaskbarIcon { get; set; } = true;
    public bool SyncDockClock { get; set; } = false;
    public int ClockSyncMinutes { get; set; } = 60;

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AJAZZBattery");

    private static readonly string FilePath = Path.Combine(Folder, "config.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FolderPath => Folder;

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, Options);
                if (loaded is not null)
                {
                    return loaded.Normalized();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Не удалось прочитать настройки: {ex.Message}");
        }

        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Normalized(), Options));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Не удалось сохранить настройки: {ex.Message}");
        }
    }

    private AppConfig Normalized()
    {
        PollSeconds = Math.Clamp(PollSeconds, 10, 3600);
        LowBatteryThreshold = Math.Clamp(LowBatteryThreshold, 3, 60);
        ClockSyncMinutes = Math.Clamp(ClockSyncMinutes, 5, 1440);
        return this;
    }
}
