using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;

namespace AjazzBattery;

/// <summary>Палитра и шрифты в стиле Windows 11: тёмная/светлая тема, системный акцент.</summary>
internal static class Theme
{
    public static event Action? Changed;

    public static bool IsLight { get; private set; }

    public static Color Window { get; private set; }
    public static Color Card { get; private set; }
    public static Color Stroke { get; private set; }
    public static Color TextPrimary { get; private set; }
    public static Color TextSecondary { get; private set; }
    public static Color Accent { get; private set; }
    public static Color OnAccent { get; private set; }
    public static Color ControlFill { get; private set; }
    public static Color ControlFillHover { get; private set; }
    public static Color ControlFillPressed { get; private set; }
    public static Color ControlStroke { get; private set; }
    public static Color TrackOff { get; private set; }
    public static Color KnobOff { get; private set; }

    public static readonly string UiFamily = ResolveFamily("Segoe UI Variable Text", "Segoe UI");

    static Theme()
    {
        ApplyPalette();
    }

    public static Font Body(float size = 9.75f, FontStyle style = FontStyle.Regular) =>
        new(UiFamily, size, style, GraphicsUnit.Point);

    public static Font Display(float size = 20f, FontStyle style = FontStyle.Regular) =>
        new(ResolveFamily("Segoe UI Variable Display", "Segoe UI"), size, style, GraphicsUnit.Point);

    public static Color ForPercent(int percent) => percent switch
    {
        >= 50 => Color.FromArgb(108, 203, 95),
        >= 20 => Color.FromArgb(245, 194, 66),
        _ => Color.FromArgb(228, 89, 90),
    };

    public static Color Unknown => Color.FromArgb(150, 158, 166);

    public static void Refresh()
    {
        ApplyPalette();
        Changed?.Invoke();
    }

    private static void ApplyPalette()
    {
        IsLight = ReadAppsUseLightTheme();
        var accent = ReadSystemAccent();

        if (IsLight)
        {
            Window = Color.FromArgb(243, 243, 243);
            Card = Color.FromArgb(255, 255, 255);
            Stroke = Color.FromArgb(226, 226, 226);
            TextPrimary = Color.FromArgb(26, 26, 26);
            TextSecondary = Color.FromArgb(93, 93, 93);
            ControlFill = Color.FromArgb(251, 251, 251);
            ControlFillHover = Color.FromArgb(245, 245, 245);
            ControlFillPressed = Color.FromArgb(238, 238, 238);
            ControlStroke = Color.FromArgb(214, 214, 214);
            TrackOff = Color.FromArgb(214, 214, 214);
            KnobOff = Color.FromArgb(112, 112, 112);
            Accent = accent ?? Color.FromArgb(0, 95, 184);
            OnAccent = Color.White;
        }
        else
        {
            Window = Color.FromArgb(32, 32, 32);
            Card = Color.FromArgb(43, 43, 43);
            Stroke = Color.FromArgb(61, 61, 61);
            TextPrimary = Color.FromArgb(255, 255, 255);
            TextSecondary = Color.FromArgb(200, 200, 200);
            ControlFill = Color.FromArgb(45, 45, 45);
            ControlFillHover = Color.FromArgb(51, 51, 51);
            ControlFillPressed = Color.FromArgb(38, 38, 38);
            ControlStroke = Color.FromArgb(70, 70, 70);
            TrackOff = Color.FromArgb(63, 63, 63);
            KnobOff = Color.FromArgb(178, 178, 178);
            Accent = accent ?? Color.FromArgb(76, 194, 255);
            OnAccent = Luminance(Accent) > 0.5 ? Color.FromArgb(16, 16, 16) : Color.White;
        }

        if (IsLight && Luminance(Accent) > 0.72)
        {
            Accent = Darken(Accent, 0.35f);
        }

        OnAccent = Luminance(Accent) > 0.5 ? Color.FromArgb(16, 16, 16) : Color.White;
    }

    private static bool ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    private static Color? ReadSystemAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int raw)
            {
                int r = raw & 0xFF;
                int g = (raw >> 8) & 0xFF;
                int b = (raw >> 16) & 0xFF;
                return Color.FromArgb(r, g, b);
            }
        }
        catch
        {
            // акцент по умолчанию
        }

        return null;
    }

    private static string ResolveFamily(params string[] candidates)
    {
        try
        {
            using var installed = new InstalledFontCollection();
            foreach (var candidate in candidates)
            {
                foreach (var family in installed.Families)
                {
                    if (family.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return family.Name;
                    }
                }
            }
        }
        catch
        {
            // fallback ниже
        }

        return "Segoe UI";
    }

    private static double Luminance(Color color)
    {
        double Channel(int value) => value / 255.0;
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static Color Darken(Color color, float factor)
    {
        byte Mix(byte value) => (byte)Math.Clamp((int)(value * (1 - factor)), 0, 255);
        return Color.FromArgb(color.A, Mix(color.R), Mix(color.G), Mix(color.B));
    }

    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
