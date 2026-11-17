using System.Drawing.Drawing2D;

namespace AjazzBattery;

/// <summary>Числовое поле в стиле Windows 11: скруглённый фон + аккуратный ввод.</summary>
internal sealed class NumberField : Control
{
    private readonly TextBox _box;

    public event EventHandler? ValueChanged;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; set; } = 100;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => int.TryParse(_box.Text, out var parsed) ? Math.Clamp(parsed, Minimum, Maximum) : Minimum;
        set
        {
            var text = Math.Clamp(value, Minimum, Maximum).ToString();
            if (_box.Text != text)
            {
                _box.Text = text;
            }
        }
    }

    public NumberField()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(96, 28);
        Cursor = Cursors.IBeam;

        _box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            TextAlign = HorizontalAlignment.Center,
            Font = Theme.Body(9.75f),
        };
        _box.TextChanged += (_, _) => ValueChanged?.Invoke(this, EventArgs.Empty);
        _box.KeyPress += OnBoxKeyPress;
        _box.GotFocus += (_, _) => Invalidate();
        _box.LostFocus += (_, _) =>
        {
            _box.Text = Value.ToString();
            Invalidate();
        };
        _box.Leave += (_, _) => Value = Value;

        Controls.Add(_box);
        Theme.Changed += ApplyTheme;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (IsDisposed)
        {
            return;
        }

        _box.BackColor = Theme.ControlFill;
        _box.ForeColor = Theme.TextPrimary;
        Invalidate();
    }

    private void OnBoxKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
        {
            return;
        }

        if (!char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        _box.Focus();
        base.OnClick(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_box is null)
        {
            return;
        }

        _box.Bounds = new Rectangle(10, (Height - _box.PreferredHeight) / 2 + 1, Width - 20, _box.PreferredHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Card);

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundedRect(bounds, 4f);
        using (var fill = new SolidBrush(Theme.ControlFill))
        {
            g.FillPath(fill, path);
        }

        if (_box.Focused)
        {
            using var accent = new Pen(Theme.Accent, 1.6f);
            g.DrawPath(accent, path);
        }
        else
        {
            using var stroke = new Pen(Theme.ControlStroke, 1f);
            g.DrawPath(stroke, path);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Theme.Changed -= ApplyTheme;
        }

        base.Dispose(disposing);
    }
}

