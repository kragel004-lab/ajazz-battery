using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AjazzBattery.Setup;

internal static class Installer
{
    public const string AppFileName = "AjazzBattery.exe";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AJAZZBattery";
    public const string RunKeyName = "AJAZZBattery";
    public const string Version = "1.2.0";

    public static string DefaultTarget =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AJAZZ Battery");

    public static void Install(string target, bool autostart, bool desktopShortcut, Action<string, double> progress)
    {
        progress(Loc.T("setup.progress.prepare"), 0.02);
        target = Path.GetFullPath(target);
        Directory.CreateDirectory(target);

        progress(Loc.T("setup.progress.stopping"), 0.06);
        StopRunningInstances(target);

        progress(Loc.T("setup.progress.extracting"), 0.1);
        ExtractPayload(Path.Combine(target, AppFileName), value => progress(Loc.T("setup.progress.extracting"), 0.1 + value * 0.7));

        progress(Loc.T("setup.progress.shortcuts"), 0.84);
        var exe = Path.Combine(target, AppFileName);
        Shortcut.Create(StartMenuShortcutPath(), exe, "--tray", "AJAZZ Battery", exe);
        if (desktopShortcut)
        {
            Shortcut.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "AJAZZ Battery.lnk"),
                exe, "--tray", "AJAZZ Battery", exe);
        }

        progress(Loc.T("setup.progress.registering"), 0.92);
        WriteUninstallEntry(target, exe);
        ApplyAutostart(autostart, exe);

        progress(Loc.T("setup.progress.done"), 1.0);
    }

    public static void ApplyAutostart(bool enable, string exe)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key is null)
        {
            return;
        }

        if (enable)
        {
            key.SetValue(RunKeyName, $"\"{exe}\" --tray");
        }
        else
        {
            key.DeleteValue(RunKeyName, throwOnMissingValue: false);
        }
    }

    public static string StartMenuShortcutPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "AJAZZ Battery.lnk");

    private static void WriteUninstallEntry(string target, string exe)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        if (key is null)
        {
            return;
        }

        key.SetValue("DisplayName", Loc.T("setup.displayName"));
        key.SetValue("DisplayVersion", Version);
        key.SetValue("Publisher", Loc.T("setup.publisher"));
        key.SetValue("DisplayIcon", exe);
        key.SetValue("InstallLocation", target);
        key.SetValue("UninstallString", $"\"{exe}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        try
        {
            var sizeKb = new FileInfo(exe).Length / 1024;
            key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
        }
        catch
        {
            // размер не критичен
        }
    }

    private static void ExtractPayload(string destination, Action<double> onProgress)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("payload.exe")
                           ?? throw new InvalidOperationException(Loc.T("setup.error.noPayload"));
        using var output = File.Create(destination);

        var buffer = new byte[1024 * 1024];
        long total = stream.Length;
        long copied = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            copied += read;
            onProgress(total == 0 ? 1 : (double)copied / total);
        }
    }

    private static void StopRunningInstances(string target)
    {
        foreach (var process in Process.GetProcessesByName("AjazzBattery"))
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (path is not null && path.StartsWith(target, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch
            {
                // процесс мог уже завершиться
            }
        }
    }
}

internal static class Shortcut
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    public static void Create(string linkPath, string target, string arguments, string description, string iconPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(target);
        link.SetArguments(arguments);
        link.SetDescription(description);
        link.SetWorkingDirectory(Path.GetDirectoryName(target)!);
        link.SetIconLocation(iconPath, 0);
        ((IPersistFile)link).Save(linkPath, true);
        Marshal.ReleaseComObject(link);
    }
}

