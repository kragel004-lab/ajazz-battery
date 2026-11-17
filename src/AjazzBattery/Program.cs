using System.Runtime.InteropServices;
using System.Text;

namespace AjazzBattery;

internal static class Program
{
    private const string MutexName = @"Local\AJAZZBattery.SingleInstance";
    private const string ShowEventName = @"Local\AJAZZBattery.Show";

    [STAThread]
    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;

        switch (command)
        {
            case "--check":
            case "-c":
            case "check":
                return RunCheck();
            case "--diag":
            case "diag":
                return RunDiagnose();
            case "--set-time":
            case "--clock":
                return RunClockSync();
            case "--clear-screen":
                return RunClearScreen();
            case "--dump-icons":
                return RunDumpIcons(args.Length > 1 ? args[1] : "icons");
            case "--uninstall":
                return Uninstaller.Run(silent: args.Any(a => a is "/S" or "--silent"));
            case "--help":
            case "-h":
            case "-?":
            case "/?":
                return RunHelp();
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
        if (!created)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var showEvent))
            {
                showEvent.Set();
                showEvent.Dispose();
            }

            return 0;
        }

        using var context = new TrayContext();
        Application.Run(context);
        GC.KeepAlive(mutex);
        return 0;
    }

    private static int RunCheck()
    {
        EnsureConsole();
        var reading = AjazzReader.Read();
        if (!reading.Found)
        {
            Console.WriteLine($"Мышь не найдена: {reading.Error}");
            return 1;
        }

        Console.WriteLine($"Устройство : {reading.Product}");
        Console.WriteLine($"Интерфейс  : {reading.Path}");
        Console.WriteLine($"Заряд      : {reading.Percent}%");
        Console.WriteLine($"Сырой ответ: {string.Join(' ', reading.Raw.Select(b => b.ToString("x2")))}");
        return 0;
    }

    private static int RunDiagnose()
    {
        EnsureConsole();
        Console.WriteLine(AjazzReader.Diagnose());
        return 0;
    }

    private static int RunClockSync()
    {
        EnsureConsole();
        bool ok = AjazzReader.SyncClock();
        Console.WriteLine(ok ? "Время отправлено на док-станцию." : "Не удалось отправить время: нет связи.");
        return ok ? 0 : 1;
    }

    private static int RunClearScreen()
    {
        EnsureConsole();
        bool ok = AjazzReader.ClearDockScreen();
        Console.WriteLine(ok ? "Команда очистки экрана отправлена." : "Не удалось отправить команду: нет связи.");
        return ok ? 0 : 1;
    }

    private static int RunDumpIcons(string folder)
    {
        EnsureConsole();
        Directory.CreateDirectory(folder);
        int?[] percents = { 82, 45, 12, null };
        foreach (var showText in new[] { false, true })
        {
            foreach (var size in new[] { 16, 24, 32 })
            {
                const int scale = 8;
                using var sheet = new Bitmap(percents.Length * (size * scale + 16) + 16, size * scale + 32);
                using (var g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.FromArgb(28, 30, 33));
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    int x = 16;
                    foreach (var percent in percents)
                    {
                        using var bitmap = BatteryGlyph.Render(size, percent, showText);
                        g.DrawImage(bitmap, new Rectangle(x, 16, size * scale, size * scale));
                        x += size * scale + 16;
                    }
                }

                var file = Path.Combine(folder, $"icon_{(showText ? "text" : "battery")}_{size}.png");
                sheet.Save(file);
                Console.WriteLine(file);
            }
        }

        return 0;
    }

    private static int RunHelp()
    {
        EnsureConsole();
        Console.WriteLine("AJAZZ Battery — заряд мыши AJAZZ AJ159 APEX в трее Windows");
        Console.WriteLine();
        Console.WriteLine("  AjazzBattery.exe               запустить трей-приложение");
        Console.WriteLine("  AjazzBattery.exe --check       показать заряд и сырой ответ устройства");
        Console.WriteLine("  AjazzBattery.exe --diag        диагностика: интерфейсы и ответы HID");
        Console.WriteLine("  AjazzBattery.exe --set-time    синхронизировать часы док-станции");
        Console.WriteLine("  AjazzBattery.exe --clear-screen очистить картинку с экрана дока");
        return 0;
    }

    private static void EnsureConsole()
    {
        if (!AttachConsole(-1))
        {
            AllocConsole();
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(stdout);
            var stderr = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetError(stderr);
        }
        catch
        {
            // если консоль недоступна — просто молчим
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}
