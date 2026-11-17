using System.Drawing.Drawing2D;

namespace AjazzBattery;

internal sealed class SettingsForm : Form
{
    private const int FormWidth = 480;
    private const int FormHeight = 604;

    private readonly TrayContext _context;
    private readonly NumberField _interval;
    private readonly NumberField _threshold;
    private readonly NumberField _clockInterval;
    private readonly FlatToggle _notifications;
    private readonly FlatToggle _autostart;
    private readonly FlatToggle _taskbarIcon;
    private readonly FlatToggle _percentOnIcon;
    private readonly FlatToggle _clockSync;

    private static readonly Rectangle CardOne = new(20, 84, 440, 148);
    private static readonly Rectangle CardTwo = new(20, 244, 440, 148);
    private static readonly Rectangle CardThree = new(20, 404, 440, 99);

    public SettingsForm(TrayContext context)
    {
        _context = context;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Window;
        ClientSize = new Size(FormWidth, FormHeight);
        Text = "Настройки — AJAZZ Battery";
        MaximizeBox = false;
        MinimizeBox = false;

        _interval = CreateNumberField(CardOne, 0, 10, 3600);
        _threshold = CreateNumberField(CardOne, 1, 3, 60);
        _notifications = CreateToggle(CardOne, 2);
        _autostart = CreateToggle(CardTwo, 0);
        _taskbarIcon = CreateToggle(CardTwo, 1);
        _percentOnIcon = CreateToggle(CardTwo, 2);
        _clockSync = CreateToggle(CardThree, 0);
        _clockSync.CheckedChanged += (_, _) => UpdateClockIntervalState();
        _clockInterval = CreateNumberField(CardThree, 1, 5, 1440);

        var save = new FlatButton
        {
            Text = "Сохранить",
            Kind = ButtonKind.Accent,
            Bounds = new Rectangle(330, 528, 130, 32),
            Font = Theme.Body(9.75f),
        };
        save.Click += (_, _) => SaveAndClose();

        var cancel = new FlatButton
        {
            Text = "Отмена",
            Bounds = new Rectangle(222, 528, 100, 32),
            Font = Theme.Body(9.75f),
        };
        cancel.Click += (_, _) => Close();

        var close = new FlatButton
        {
            Text = "✕",
            Kind = ButtonKind.Subtle,
            Bounds = new Rectangle(428, 16, 28, 28),
            CornerRadius = 14f,
            Font = Theme.Body(9f),
        };
        close.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            _interval, _threshold, _clockInterval,
            _notifications, _autostart, _taskbarIcon, _percentOnIcon, _clockSync,
            save, cancel, close,
        });

        LoadFromConfig();
        Theme.Changed += OnThemeChanged;
    }

    public bool IsDirty => _interval.Value != _context.Config.PollSeconds
                           || _threshold.Value != _context.Config.LowBatteryThreshold
                           || _autostart.Checked != _context.Config.RunAtStartup
                           || _taskbarIcon.Checked != _context.Config.ShowTaskbarIcon
                           || _percentOnIcon.Checked != _context.Config.ShowPercentOnIcon
                           || _clockSync.Checked != _context.Config.SyncDockClock
                           || _notifications.Checked != _context.Config.Notifications;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= 0x00020000;
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            area.Left + (area.Width - Width) / 2,
            area.Top + (area.Height - Height) / 2);
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

    private static float RowHeight(Rectangle card, int rows) => card.Height / (float)rows;

    private static int RowCount(Rectangle card) => card.Height > 120 ? 3 : 2;

    private NumberField CreateNumberField(Rectangle card, int rowIndex, int minimum, int maximum)
    {
        float rowHeight = RowHeight(card, RowCount(card));
        return new NumberField
        {
            Minimum = minimum,
            Maximum = maximum,
            Bounds = new Rectangle(card.Right - 112, (int)(card.Y + rowIndex * rowHeight + (rowHeight - 28) / 2), 96, 28),
            Font = Theme.Body(9.75f),
        };
    }

    private FlatToggle CreateToggle(Rectangle card, int rowIndex)
    {
        float rowHeight = RowHeight(card, RowCount(card));
        return new FlatToggle
        {
            Bounds = new Rectangle(card.Right - 52, (int)(card.Y + rowIndex * rowHeight + (rowHeight - 20) / 2), 40, 20),
        };
    }

    private void LoadFromConfig()
    {
        var config = _context.Config;
        _interval.Value = Math.Clamp(config.PollSeconds, _interval.Minimum, _interval.Maximum);
        _threshold.Value = Math.Clamp(config.LowBatteryThreshold, _threshold.Minimum, _threshold.Maximum);
        _clockInterval.Value = Math.Clamp(config.ClockSyncMinutes, _clockInterval.Minimum, _clockInterval.Maximum);
        _notifications.Checked = config.Notifications;
        _autostart.Checked = config.RunAtStartup;
        _taskbarIcon.Checked = config.ShowTaskbarIcon;
        _percentOnIcon.Checked = config.ShowPercentOnIcon;
        _clockSync.Checked = config.SyncDockClock;
        UpdateClockIntervalState();
    }

    private void UpdateClockIntervalState()
    {
        _clockInterval.Enabled = _clockSync.Checked;
        _clockInterval.Invalidate();
    }

    private void SaveAndClose()
    {
        var config = _context.Config;
        config.PollSeconds = _interval.Value;
        config.LowBatteryThreshold = _threshold.Value;
        config.ClockSyncMinutes = _clockInterval.Value;
        config.Notifications = _notifications.Checked;
        config.ShowPercentOnIcon = _percentOnIcon.Checked;
        config.ShowTaskbarIcon = _taskbarIcon.Checked;

        var autostartChanged = config.RunAtStartup != _autostart.Checked;
        config.RunAtStartup = _autostart.Checked;
        config.SyncDockClock = _clockSync.Checked;
        config.Save();

        if (autostartChanged)
        {
            _context.ApplyAutostart(config.RunAtStartup);
        }

        _context.OnSettingsChanged();
        Close();
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

        using (var titleFont = Theme.Display(19f, FontStyle.Bold))
        using (var titleBrush = new SolidBrush(Theme.TextPrimary))
        {
            g.DrawString("Настройки", titleFont, titleBrush, 22, 14);
        }

        using (var subFont = Theme.Body(9.75f))
        using (var subBrush = new SolidBrush(Theme.TextSecondary))
        {
            g.DrawString("Заряд мыши AJAZZ AJ159 APEX", subFont, subBrush, 24, 50);
        }

        DrawCard(g, CardOne, 3);
        DrawCard(g, CardTwo, 3);
        DrawCard(g, CardThree, 2);

        DrawRowText(g, CardOne, 0, "Интервал опроса, с");
        DrawRowText(g, CardOne, 1, "Порог низкого заряда, %");
        DrawRowText(g, CardOne, 2, "Уведомлять о низком заряде");

        DrawRowText(g, CardTwo, 0, "Запускать при входе в Windows");
        DrawRowText(g, CardTwo, 1, "Иконка с процентом на панели задач");
        DrawRowText(g, CardTwo, 2, "Процент вместо батарейки в трее");

        DrawRowText(g, CardThree, 0, "Синхронизировать часы док-станции");
        DrawRowText(g, CardThree, 1, "Интервал синхронизации, мин");

        using var footerFont = Theme.Body(8.25f);
        using var footerBrush = new SolidBrush(Theme.TextSecondary);
        g.DrawString("Файл настроек: %APPDATA%\\AJAZZBattery\\config.json", footerFont, footerBrush, 24, FormHeight - 26);
    }

    private static void DrawCard(Graphics g, Rectangle card, int rows)
    {
        using var path = Theme.RoundedRect(card, 8f);
        using (var fill = new SolidBrush(Theme.Card))
        {
            g.FillPath(fill, path);
        }

        using (var pen = new Pen(Theme.Stroke, 1f))
        {
            g.DrawPath(pen, path);
        }

        float rowHeight = card.Height / (float)rows;
        for (int i = 1; i < rows; i++)
        {
            using var pen = new Pen(Theme.Stroke, 1f);
            g.DrawLine(pen, card.X + 20, card.Y + i * rowHeight, card.Right - 20, card.Y + i * rowHeight);
        }
    }

    private static void DrawRowText(Graphics g, Rectangle card, int rowIndex, string text)
    {
        float rowHeight = RowHeight(card, RowCount(card));
        using var font = Theme.Body(9.75f);
        using var brush = new SolidBrush(Theme.TextPrimary);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        var rect = new RectangleF(card.X + 20, card.Y + rowIndex * rowHeight, card.Width - 150, rowHeight);
        g.DrawString(text, font, brush, rect, format);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Theme.Changed -= OnThemeChanged;
        }

        base.Dispose(disposing);
    }
}
