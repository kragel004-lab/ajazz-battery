using System.Diagnostics;
using Microsoft.Win32;

namespace AjazzBattery;

internal static class Uninstaller
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AJAZZBattery";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static int Run(bool silent)
    {
        if (!silent)
        {
            var answer = MessageBox.Show(
                "Удалить AJAZZ Battery?\n\nПриложение будет закрыто, ярлыки и настройки удалены.",
                "Удаление AJAZZ Battery",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return 0;
            }
        }

        var exePath = Application.ExecutablePath;
        var directory = Path.GetDirectoryName(exePath) ?? string.Empty;

        foreach (var process in Process.GetProcessesByName("AjazzBattery"))
        {
            if (process.Id == Environment.ProcessId)
            {
                continue;
            }

            try
            {
                process.Kill();
                process.WaitForExit(2000);
            }
            catch
            {
                // процесс мог уже закрыться
            }
        }

        DeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "AJAZZ Battery.lnk"));
        DeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "AJAZZ Battery.lnk"));

        // проверяем до удаления записи: иначе не отличить установленную копию от портативной
        bool installedHere = IsInstalledCopy(directory);

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Не удалось удалить запись деинсталлятора: {ex.Message}");
        }

        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            run?.DeleteValue("AJAZZBattery", throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Не удалось удалить автозапуск: {ex.Message}");
        }

        // папку удаляем только если это установленная копия (иначе можно снести портативную)
        if (installedHere)
        {
            DeleteDirectory(AppConfig.FolderPath);
            ScheduleDirectoryRemoval(directory);
        }
        else if (!silent)
        {
            MessageBox.Show(
                "Это портативная копия: ярлыки и настройки удалены, папку с программой удалите вручную.",
                "AJAZZ Battery",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        return 0;
    }

    private static bool IsInstalledCopy(string directory)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            var location = key?.GetValue("InstallLocation") as string;
            return location is not null &&
                   string.Equals(Path.GetFullPath(location).TrimEnd('\\'), Path.GetFullPath(directory).TrimEnd('\\'),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void ScheduleDirectoryRemoval(string directory)
    {
        try
        {
            var info = new ProcessStartInfo("cmd.exe", $"/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"{directory}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(info);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Не удалось запланировать удаление папки: {ex.Message}");
        }
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // не критично
        }
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // не критично
        }
    }
}
