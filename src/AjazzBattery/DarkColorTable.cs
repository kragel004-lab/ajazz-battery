using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AjazzBattery;

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Card;
    public override Color ImageMarginGradientBegin => Theme.Card;
    public override Color ImageMarginGradientMiddle => Theme.Card;
    public override Color ImageMarginGradientEnd => Theme.Card;
    public override Color ImageMarginRevealedGradientBegin => Theme.Card;
    public override Color ImageMarginRevealedGradientMiddle => Theme.Card;
    public override Color ImageMarginRevealedGradientEnd => Theme.Card;
    public override Color MenuBorder => Theme.Stroke;
    public override Color MenuItemBorder => Theme.Stroke;
    public override Color MenuItemSelected => Theme.ControlFillHover;
    public override Color MenuItemSelectedGradientBegin => Theme.ControlFillHover;
    public override Color MenuItemSelectedGradientEnd => Theme.ControlFillHover;
    public override Color MenuItemPressedGradientBegin => Theme.ControlFillPressed;
    public override Color MenuItemPressedGradientMiddle => Theme.ControlFillPressed;
    public override Color MenuItemPressedGradientEnd => Theme.ControlFillPressed;
    public override Color SeparatorDark => Theme.Stroke;
    public override Color SeparatorLight => Theme.Stroke;
    public override Color CheckBackground => Theme.Accent;
    public override Color CheckSelectedBackground => Theme.Accent;
    public override Color CheckPressedBackground => Theme.Accent;
}

/// <summary>
/// Рендерер тёмных меню: та же палитра <see cref="DarkColorTable"/>, но галочка
/// рисуется акцентом, а не системной картинкой, — иначе на тёмном фоне её не видно.
/// </summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // размер галочки задаём сами и центрируем её в поле значков:
        // системная картинка на тёмном фоне почти не видна
        var area = e.ImageRectangle;
        if (area.Width < 6 || area.Height < 6)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        const float size = 13f;
        float cx = area.X + area.Width / 2f;
        float cy = area.Y + area.Height / 2f;
        using var pen = new Pen(Theme.Accent, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        g.DrawLines(pen, new[]
        {
            new PointF(cx - size * 0.42f, cy + 0.5f),
            new PointF(cx - size * 0.10f, cy + size * 0.40f),
            new PointF(cx + size * 0.46f, cy - size * 0.40f),
        });
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.TextPrimary : Theme.TextSecondary;
        base.OnRenderItemText(e);
    }
}
