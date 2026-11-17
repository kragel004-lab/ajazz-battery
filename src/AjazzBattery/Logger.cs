using System.Text;

namespace AjazzBattery;

internal static class Logger
{
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(AppConfig.FolderPath, "app.log");

    public static string LogFilePath => LogPath;

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppConfig.FolderPath);
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 512 * 1024)
                {
                    File.Move(LogPath, LogPath + ".1", overwrite: true);
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // журнал не должен ломать работу приложения
        }
    }
}
