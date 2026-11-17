using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AjazzBattery;

internal enum ButtonKind
{
    Standard,
    Accent,
    Subtle,
}

internal static class NativeUi
{
    private const int WmNclButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    public static bool TryRoundCorners(Form form)
    {
        try
        {
            int preference = 2; // DWMWCP_ROUND
            return DwmSetWindowAttribute(form.Handle, 33, ref preference, sizeof(int)) == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Убирает светлую рамку DWM и красит её в цвет фона окна.</summary>
    public static void ApplyBorderColor(Form form, Color color)
    {
        try
        {
            int colorRef = color.R | (color.G << 8) | (color.B << 16);
            DwmSetWindowAttribute(form.Handle, 34, ref colorRef, sizeof(int));
        }
        catch
        {
            // не критично
        }
    }

    public static void DragWindow(Form form, Control header)
    {
        header.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(form.Handle, WmNclButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        };
    }
}

/// <summary>Кнопка в стиле Windows 11: скругление 4 px, мягкая заливка, акцентный вариант.</summary>
internal sealed class FlatButton : Button
{
    private bool _hover;
    private bool _pressed;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ButtonKind Kind { get; set; } = ButtonKind.Standard;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float CornerRadius { get; set; } = 4f;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Small { get; set; }

    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        UseVisualStyleBackColor = false;
        Theme.Changed += () => { if (!IsDisposed) Invalidate(); };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Window);

        Color back;
        Color fore;
        Color border = Color.Transparent;

        switch (Kind)
        {
            case ButtonKind.Accent:
                back = _pressed ? Blend(Theme.Accent, Color.Black, 0.12f)
                    : _hover ? Blend(Theme.Accent, Color.White, 0.12f)
                    : Theme.Accent;
                fore = Theme.OnAccent;
                break;
            case ButtonKind.Subtle:
                back = _pressed ? Alpha(Theme.TextPrimary, 0.06f)
                    : _hover ? Alpha(Theme.TextPrimary, 0.09f)
                    : Color.Transparent;
                fore = Theme.TextPrimary;
                break;
            default:
                back = _pressed ? Theme.ControlFillPressed
                    : _hover ? Theme.ControlFillHover
                    : Theme.ControlFill;
                border = Theme.ControlStroke;
                fore = Theme.TextPrimary;
                break;
        }

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundedRect(bounds, CornerRadius);
        if (back.A > 0)
        {
            using var brush = new SolidBrush(back);
            g.FillPath(brush, path);
        }

        if (border.A > 0)
        {
            using var pen = new Pen(border, 1f);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    private static Color Alpha(Color color, float alpha) =>
        Color.FromArgb((int)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);
}

/// <summary>Переключатель в стиле Windows 11.</summary>
internal sealed class FlatToggle : Control
{
    private bool _checked;
    private float _position;

    public event EventHandler? CheckedChanged;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            _position = value ? 1f : 0f;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public FlatToggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(40, 20);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        Theme.Changed += () => { if (!IsDisposed) Invalidate(); };
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Window);

        var track = new RectangleF(0, 1, Width, Height - 2);
        using var path = Theme.RoundedRect(track, track.Height / 2f);

        if (_checked)
        {
            using var brush = new SolidBrush(Theme.Accent);
            g.FillPath(brush, path);
        }
        else
        {
            using var brush = new SolidBrush(Theme.TrackOff);
            g.FillPath(brush, path);
            using var pen = new Pen(Theme.ControlStroke, 1f);
            g.DrawPath(pen, path);
        }

        float knobSize = Height - 6;
        float left = 3;
        float right = Width - knobSize - 3;
        float x = _checked ? right : left;
        using var knob = new SolidBrush(_checked ? Color.FromArgb(255, 255, 255) : Theme.KnobOff);
        g.FillEllipse(knob, x, 3, knobSize, knobSize);
    }
}
