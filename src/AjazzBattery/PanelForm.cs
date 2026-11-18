using System.Drawing.Drawing2D;

namespace AjazzBattery;

internal sealed class PanelForm : Form
{
    private const int PanelWidth = 360;
    private const int PanelHeight = 400;

    private readonly TrayContext _context;
    private readonly FlatButton _refreshButton;
    private readonly FlatButton _settingsButton;
    private readonly FlatButton _moreButton;
    private readonly FlatButton _closeButton;

    private int? _percent;
    private bool _charging;
    private bool _found;
    private DateTime? _updated;
    private string _subtitle = "Приёмник 2.4 ГГц";

    private readonly System.Windows.Forms.Timer _focusTimer = new() { Interval = 250 };
    private DateTime _focusGrace = DateTime.MinValue;
    private bool _dragging;
    private Point _dragStart;

    public PanelForm(TrayContext context)
    {
        _context = context;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Window;
        DoubleBuffered = true;
        ClientSize = new Size(PanelWidth, PanelHeight);
        Text = "AJAZZ Battery";

        _refreshButton = new FlatButton
        {
            Text = "Обновить",
            Kind = ButtonKind.Accent,
            Bounds = new Rectangle(20, 312, 150, 34),
            Font = Theme.Body(9.75f),
        };
        _refreshButton.Click += (_, _) => _context.Poll(userInitiated: true);

        _settingsButton = new FlatButton
        {
            Text = "Настройки",
            Bounds = new Rectangle(178, 312, 112, 34),
            Font = Theme.Body(9.75f),
        };
        _settingsButton.Click += (_, _) => { SuppressHide(); _context.ShowSettings(); };

        _moreButton = new FlatButton
        {
            Text = "⋯",
            Bounds = new Rectangle(298, 312, 42, 34),
            Font = Theme.Body(11f),
        };
        _moreButton.Click += (_, _) => _context.ShowMenuAt(_moreButton);

        _closeButton = new FlatButton
        {
            Text = "✕",
            Kind = ButtonKind.Subtle,
            Bounds = new Rectangle(316, 12, 28, 28),
            CornerRadius = 14f,
            Font = Theme.Body(9f),
        };
        _closeButton.Click += (_, _) => Hide();

        Controls.AddRange(new Control[] { _refreshButton, _settingsButton, _moreButton, _closeButton });

        Theme.Changed += OnThemeChanged;
        _focusTimer.Tick += OnFocusTimerTick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeUi.ApplyBorderColor(this, Theme.Window);
        if (!NativeUi.TryRoundCorners(this))
        {
            using var path = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), 8f);
            Region = new Region(path);
        }
    }

    private void OnThemeChanged()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BackColor = Theme.Window;
        NativeUi.ApplyBorderColor(this, Theme.Window);
        Invalidate(true);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public DateTime LastAutoHide { get; private set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool SuppressAutoHide { get; set; }

    public void SuppressHide() => SuppressAutoHide = true;

    public void AllowHide() => SuppressAutoHide = false;

    public void UpdateReading(BatteryReading reading, bool charging)
    {
        _found = reading.Found;
        _percent = reading.Found ? reading.Percent : null;
        _charging = charging && reading.Found;
        _updated = reading.Timestamp;
        _subtitle = reading.Found && !string.IsNullOrWhiteSpace(reading.Product)
            ? $"{reading.Product} · на связи"
            : "Приёмник не найден";
        Invalidate();
    }

    public void ShowNearTray()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
        Show();
        NativeUi.ForceForeground(this);
        Activate();
        BringToFront();

        // клик по виджету активирует панель задач, из-за чего панель тут же теряет фокус —
        // даём ей «карантин» и один раз перехватываем фокус обратно
        _focusGrace = DateTime.UtcNow.AddMilliseconds(600);
        _focusTimer.Stop();
        _focusTimer.Start();
    }

    public void ToggleNearTray()
    {
        if (Visible)
        {
            Hide();
        }
        else
        {
            ShowNearTray();
        }
    }

    private void OnFocusTimerTick(object? sender, EventArgs e)
    {
        _focusTimer.Stop();
        if (!Visible || SuppressAutoHide || _context.IsSettingsOpen)
        {
            return;
        }

        if (!NativeUi.IsForeground(this))
        {
            NativeUi.ForceForeground(this);
            Activate();
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (DateTime.UtcNow < _focusGrace)
        {
            return;
        }

        if (!SuppressAutoHide && !_context.IsSettingsOpen)
        {
            LastAutoHide = DateTime.UtcNow;
            Hide();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && e.Y < 64)
        {
            _dragging = true;
            _dragStart = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var background = new SolidBrush(Theme.Window))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        using (var titleFont = Theme.Body(13.5f, FontStyle.Bold))
        using (var titleBrush = new SolidBrush(Theme.TextPrimary))
        {
            g.DrawString("AJAZZ AJ159 APEX", titleFont, titleBrush, 20, 15);
        }

        using (var subFont = Theme.Body(9.5f))
        using (var subBrush = new SolidBrush(Theme.TextSecondary))
        {
            g.DrawString(_subtitle, subFont, subBrush, 20, 42);
        }

        DrawGauge(g);
        DrawInfoCard(g);

        using (var footerFont = Theme.Body(8.25f))
        using (var footerBrush = new SolidBrush(Theme.TextSecondary))
        {
            g.DrawString($"AJAZZ Battery · опрос каждые {_context.Config.PollSeconds} с · порог {_context.Config.LowBatteryThreshold}%",
                footerFont, footerBrush, 20, 362);
        }
    }

    private void DrawGauge(Graphics g)
    {
        var body = new RectangleF(20, 76, PanelWidth - 40, 96);
        using var path = Theme.RoundedRect(body, 12f);

        using (var fill = new SolidBrush(Theme.Card))
        {
            g.FillPath(fill, path);
        }

        if (_percent.HasValue)
        {
            var color = Theme.ForPercent(_percent.Value);
            var state = g.Save();
            g.SetClip(path);
            float width = (body.Width - 6) * Math.Clamp(_percent.Value / 100f, 0f, 1f);
            var inner = new RectangleF(body.X + 3, body.Y + 3, Math.Max(0f, width), body.Height - 6);
            using (var innerPath = Theme.RoundedRect(inner, 9f))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, innerPath);
            }

            g.Restore(state);
        }

        using (var border = new Pen(Theme.Stroke, 1.5f))
        {
            g.DrawPath(border, path);
        }

        string text = _percent.HasValue ? $"{_percent.Value}%" : "—";
        bool overFill = _percent is >= 50;
        var textColor = overFill ? Color.FromArgb(12, 30, 12) : Theme.TextPrimary;
        using var font = Theme.Body(27f, FontStyle.Bold);
        using var brush2 = new SolidBrush(textColor);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush2, body, format);

        if (_charging)
        {
            var bolt = new RectangleF(body.Right - 64, body.Y + 24, 30, 48);
            float fillEnd = body.X + 3 + (body.Width - 6) * Math.Clamp((_percent ?? 0) / 100f, 0f, 1f);
            bool boltOverFill = bolt.X + bolt.Width / 2 <= fillEnd;
            DrawBolt(g, bolt, boltOverFill ? Color.FromArgb(12, 30, 12) : Theme.Accent);
        }
    }

    private static void DrawBolt(Graphics g, RectangleF box, Color color)
    {
        var points = new[]
        {
            new PointF(box.X + box.Width * 0.62f, box.Y),
            new PointF(box.X + box.Width * 0.16f, box.Y + box.Height * 0.56f),
            new PointF(box.X + box.Width * 0.45f, box.Y + box.Height * 0.56f),
            new PointF(box.X + box.Width * 0.34f, box.Y + box.Height),
            new PointF(box.X + box.Width * 0.86f, box.Y + box.Height * 0.42f),
            new PointF(box.X + box.Width * 0.55f, box.Y + box.Height * 0.42f),
        };

        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, points);
    }

    private void DrawInfoCard(Graphics g)
    {
        var card = new RectangleF(20, 188, PanelWidth - 40, 108);
        using (var path = Theme.RoundedRect(card, 8f))
        {
            using var fill = new SolidBrush(Theme.Card);
            g.FillPath(fill, path);
            using var pen = new Pen(Theme.Stroke, 1f);
            g.DrawPath(pen, path);
        }

        string state;
        Color stateColor;
        if (!_found)
        {
            state = "нет связи";
            stateColor = Theme.ForPercent(10);
        }
        else if (_charging)
        {
            state = "заряжается";
            stateColor = Theme.Accent;
        }
        else if (_percent >= 100)
        {
            state = "заряжено";
            stateColor = Theme.ForPercent(90);
        }
        else
        {
            state = "на связи";
            stateColor = Theme.ForPercent(80);
        }

        DrawRow(g, card, 0, "Состояние", state, stateColor);
        DrawRow(g, card, 1, "Обновлено", _updated.HasValue ? _updated.Value.ToString("HH:mm:ss") : "—", Theme.TextPrimary);
        DrawRow(g, card, 2, "Автоопрос", $"{_context.Config.PollSeconds} с", Theme.TextPrimary);
    }

    private static void DrawRow(Graphics g, RectangleF card, int index, string label, string value, Color valueColor)
    {
        float rowHeight = card.Height / 3f;
        float y = card.Y + index * rowHeight;

        if (index > 0)
        {
            using var pen = new Pen(Theme.Stroke, 1f);
            g.DrawLine(pen, card.X + 16, y, card.Right - 16, y);
        }

        using var labelFont = Theme.Body(9.75f);
        using var valueFont = Theme.Body(9.75f);
        using var labelBrush = new SolidBrush(Theme.TextSecondary);
        using var valueBrush = new SolidBrush(valueColor);
        using var left = new StringFormat { LineAlignment = StringAlignment.Center };
        using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        var row = new RectangleF(card.X + 16, y, card.Width - 32, rowHeight);
        g.DrawString(label, labelFont, labelBrush, row, left);
        g.DrawString(value, valueFont, valueBrush, row, right);
    }
}




