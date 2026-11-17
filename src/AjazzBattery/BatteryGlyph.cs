using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AjazzBattery;

/// <summary>Рисует иконку трея: батарейка с зарядом (или крупный процент) + многоразмерный .ico.</summary>
internal static class BatteryGlyph
{
    private static readonly int[] IconSizes = { 16, 20, 24, 32, 40, 48, 64 };

    public static Icon Create(int? percent, bool showPercentText)
    {
        var bitmaps = new List<Bitmap>();
        foreach (var size in IconSizes)
        {
            bitmaps.Add(Render(size, percent, showPercentText));
        }

        var icon = BuildIcon(bitmaps);
        foreach (var bitmap in bitmaps)
        {
            bitmap.Dispose();
        }

        return icon;
    }

    /// <summary>Рисует одну иконку заданного размера (для диагностики и предпросмотра).</summary>
    public static Bitmap Render(int size, int? percent, bool showPercentText)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);
        if (showPercentText)
        {
            DrawPercentText(g, size, percent);
        }
        else
        {
            DrawBattery(g, size, percent);
        }

        return bitmap;
    }

    private static void DrawBattery(Graphics g, int size, int? percent)
    {
        float s = size / 64f;
        float bodyLeft = 5 * s, bodyTop = 18 * s, bodyRight = 50 * s, bodyBottom = 46 * s;
        float radius = 6 * s;
        var color = percent.HasValue ? Theme.ForPercent(percent.Value) : Theme.Unknown;

        using var bodyPath = Theme.RoundedRect(new RectangleF(bodyLeft, bodyTop, bodyRight - bodyLeft, bodyBottom - bodyTop), radius);

        // пустая часть
        using (var emptyBrush = new SolidBrush(Color.FromArgb(percent.HasValue ? 70 : 45, 120, 130, 140)))
        {
            g.FillPath(emptyBrush, bodyPath);
        }

        if (percent.HasValue)
        {
            float fill = Math.Clamp(percent.Value / 100f, 0f, 1f);
            var state = g.Save();
            g.SetClip(bodyPath);
            var fillRect = new RectangleF(bodyLeft, bodyTop, (bodyRight - bodyLeft) * fill, bodyBottom - bodyTop);
            using (var fillBrush = new LinearGradientBrush(
                       new RectangleF(bodyLeft, bodyTop, Math.Max(1, bodyRight - bodyLeft), Math.Max(1, bodyBottom - bodyTop)),
                       ControlPaint.Light(color, 0.15f), color, LinearGradientMode.Vertical))
            {
                g.FillRectangle(fillBrush, fillRect);
            }

            g.Restore(state);
        }

        // контакт-«пипка»
        using (var nubBrush = new SolidBrush(Color.FromArgb(percent.HasValue ? 235 : 160, 200, 208, 216)))
        {
            var nub = new RectangleF(52 * s, 27 * s, 7 * s, 10 * s);
            g.FillRectangle(nubBrush, nub);
        }

        using var outline = new Pen(Color.FromArgb(90, 0, 0, 0), Math.Max(1f, 1.6f * s));
        g.DrawPath(outline, bodyPath);
    }

    private static void DrawPercentText(Graphics g, int size, int? percent)
    {
        string text = percent.HasValue ? percent.Value.ToString() : "?";
        var color = percent.HasValue ? Theme.ForPercent(percent.Value) : Theme.Unknown;
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var bounds = new RectangleF(0, 0, size, size);

        float fontSize = size * 0.8f;
        Font font = new("Segoe UI Semibold", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        var measured = g.MeasureString(text, font);
        while (measured.Width > size * 0.98f && fontSize > 4f)
        {
            font.Dispose();
            fontSize -= 0.5f;
            font = new Font("Segoe UI Semibold", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            measured = g.MeasureString(text, font);
        }

        using var brush = new SolidBrush(color);
        using (font)
        {
            g.DrawString(text, font, brush, bounds, format);
        }
    }

    private static Icon BuildIcon(List<Bitmap> images)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)images.Count);

            var payloads = new List<byte[]>();
            foreach (var image in images)
            {
                using var png = new MemoryStream();
                image.Save(png, ImageFormat.Png);
                payloads.Add(png.ToArray());
            }

            int offset = 6 + 16 * images.Count;
            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                writer.Write((byte)(image.Width >= 256 ? 0 : image.Width));
                writer.Write((byte)(image.Height >= 256 ? 0 : image.Height));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((short)1);
                writer.Write((short)32);
                writer.Write(payloads[i].Length);
                writer.Write(offset);
                offset += payloads[i].Length;
            }

            foreach (var payload in payloads)
            {
                writer.Write(payload);
            }
        }

        stream.Position = 0;
        return new Icon(stream);
    }
}
