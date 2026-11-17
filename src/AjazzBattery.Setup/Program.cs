using System.Diagnostics;

namespace AjazzBattery.Setup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var silent = args.Any(a => a is "/S" or "--silent" or "-s");
        var target = GetArgument(args, "--dir") ?? Installer.DefaultTarget;
        var noAutostart = args.Any(a => a == "--no-autostart");
        var noDesktop = args.Any(a => a == "--no-desktop");

        if (silent)
        {
            try
            {
                Installer.Install(target, autostart: !noAutostart, desktopShortcut: !noDesktop,
                    progress: (_, _) => { });
                var exe = Path.Combine(Path.GetFullPath(target), Installer.AppFileName);
                if (!args.Any(a => a == "--no-run"))
                {
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
        return 0;
    }

    private static string? GetArgument(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
