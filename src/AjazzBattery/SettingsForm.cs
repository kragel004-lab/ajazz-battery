using System.Drawing.Drawing2D;

namespace AjazzBattery;

internal sealed class SettingsForm : Form
{
    private const int FormWidth = 480;
    private const int FormHeight = 652;
    private const int LanguageButtonHeight = 28;
    private const int LanguageButtonMinWidth = 150;
    private const int LanguageButtonMaxWidth = 190;
    private const string LanguageChevron = "▾";

    private readonly TrayContext _context;
    private readonly NumberField _interval;
    private readonly NumberField _threshold;
    private readonly NumberField _clockInterval;
    private readonly FlatToggle _notifications;
    private readonly FlatToggle _autostart;
    private readonly FlatToggle _taskbarIcon;
    private readonly FlatToggle _percentOnIcon;
    private readonly FlatToggle _clockSync;
    private readonly FlatButton _language;
    private readonly ContextMenuStrip _languageMenu;

    private string _languageCode = Loc.Auto;

    private static readonly Rectangle CardOne = new(20, 84, 440, 148);
    private static readonly Rectangle CardTwo = new(20, 244, 440, 148);
    private static readonly Rectangle CardThree = new(20, 404, 440, 99);
    private static readonly Rectangle CardFour = new(20, 512, 440, 52);

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
        Text = Loc.T("settings.windowTitle");
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

        // выбор языка — кнопка в стиле приложения и тёмное меню, как в трее
        _language = new FlatButton
        {
            Kind = ButtonKind.Standard,
            CornerRadius = 4f,
            Font = Theme.Body(9.75f),
            Text = LanguageLabel(_languageCode) + "  " + LanguageChevron,
        };
        _language.Click += (_, _) => ShowLanguageMenu();

        _languageMenu = new ContextMenuStrip
        {
            // RenderMode задаём до Renderer: установка режима пересоздаёт рендерер по умолчанию
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = true,
            BackColor = Theme.Card,
            ForeColor = Theme.TextPrimary,
            Font = Theme.Body(9.75f),
            Padding = new Padding(4),
        };

        foreach (var option in Loc.Languages)
        {
            var item = new ToolStripMenuItem(LanguageLabel(option.Code))
            {
                ForeColor = Theme.TextPrimary,
                CheckOnClick = false,
                Tag = option.Code,
            };
            item.Click += (_, _) => SelectLanguage(option.Code);
            _languageMenu.Items.Add(item);
        }

        _languageMenu.Opening += (_, _) => SyncLanguageChecks();
        _languageMenu.Closed += (_, _) => _language.Invalidate();

        var save = new FlatButton
        {
            Text = Loc.T("settings.save"),
            Kind = ButtonKind.Accent,
            Bounds = new Rectangle(330, 584, 130, 32),
            Font = Theme.Body(9.75f),
        };
        save.Click += (_, _) => SaveAndClose();

        var cancel = new FlatButton
        {
            Text = Loc.T("settings.cancel"),
            Bounds = new Rectangle(222, 584, 100, 32),
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
            _language, save, cancel, close,
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
                           || _notifications.Checked != _context.Config.Notifications
                           || !string.Equals(_languageCode, _context.Config.Language, StringComparison.OrdinalIgnoreCase);

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
        ApplyLanguageMenuTheme();
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

        _languageCode = Loc.Normalize(config.Language);
        UpdateLanguageButton();
        SyncLanguageChecks();
    }

    /// <summary>Подпись языка: у «авто» она локализована, остальные названы на своём языке.</summary>
    private static string LanguageLabel(string code)
    {
        if (string.Equals(code, Loc.Auto, StringComparison.OrdinalIgnoreCase))
        {
            return Loc.T("settings.languageAuto");
        }

        foreach (var option in Loc.Languages)
        {
            if (string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return option.Label;
            }
        }

        return code;
    }

    /// <summary>Текст кнопки = текущий язык, ширина — по подписи, выравнивание по правому краю карточки.</summary>
    private void UpdateLanguageButton()
    {
        _language.Text = LanguageLabel(_languageCode) + "  " + LanguageChevron;

        using (var font = Theme.Body(9.75f))
        {
            int measured = TextRenderer.MeasureText(_language.Text, font).Width;
            int width = Math.Clamp(measured + 18, LanguageButtonMinWidth, LanguageButtonMaxWidth);
            int y = CardFour.Y + (CardFour.Height - LanguageButtonHeight) / 2;
            // правый край — по правому краю числовых полей в других карточках
            _language.Bounds = new Rectangle(CardFour.Right - 16 - width, y, width, LanguageButtonHeight);
        }
    }

    private void SelectLanguage(string code)
    {
        _languageCode = Loc.Normalize(code);
        UpdateLanguageButton();
        SyncLanguageChecks();
        _language.Invalidate();
    }

    private void SyncLanguageChecks()
    {
        foreach (ToolStripItem entry in _languageMenu.Items)
        {
            if (entry is ToolStripMenuItem item)
            {
                item.Checked = string.Equals(item.Tag as string, _languageCode, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private void ShowLanguageMenu()
    {
        SyncLanguageChecks();
        var preferred = _languageMenu.GetPreferredSize(Size.Empty);
        var screen = _language.PointToScreen(new Point(
            Math.Max(0, _language.Width - preferred.Width),
            _language.Height + 3));
        _languageMenu.Show(screen);
    }

    private void ApplyLanguageMenuTheme()
    {
        _languageMenu.BackColor = Theme.Card;
        _languageMenu.ForeColor = Theme.TextPrimary;
        foreach (ToolStripItem entry in _languageMenu.Items)
        {
            entry.ForeColor = Theme.TextPrimary;
        }
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

        var languageChanged = !string.Equals(config.Language, _languageCode, StringComparison.OrdinalIgnoreCase);
        config.Language = _languageCode;
        config.Save();

        if (languageChanged)
        {
            Loc.Language = config.Language;
        }

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
            g.DrawString(Loc.T("settings.title"), titleFont, titleBrush, 22, 14);
        }

        using (var subFont = Theme.Body(9.75f))
        using (var subBrush = new SolidBrush(Theme.TextSecondary))
        {
            g.DrawString(Loc.T("settings.subtitle"), subFont, subBrush, 24, 50);
        }

        DrawCard(g, CardOne, 3);
        DrawCard(g, CardTwo, 3);
        DrawCard(g, CardThree, 2);
        DrawCard(g, CardFour, 1);

        DrawRowText(g, CardOne, 0, Loc.T("settings.pollInterval"));
        DrawRowText(g, CardOne, 1, Loc.T("settings.lowThreshold"));
        DrawRowText(g, CardOne, 2, Loc.T("settings.notifications"));

        DrawRowText(g, CardTwo, 0, Loc.T("settings.autostart"));
        DrawRowText(g, CardTwo, 1, Loc.T("settings.taskbarWidget"));
        DrawRowText(g, CardTwo, 2, Loc.T("settings.percentOnIcon"));

        DrawRowText(g, CardThree, 0, Loc.T("settings.clockSync"));
        DrawRowText(g, CardThree, 1, Loc.T("settings.clockInterval"));

        DrawCardLabel(g, CardFour, Loc.T("settings.language"));

        using var footerFont = Theme.Body(8.25f);
        using var footerBrush = new SolidBrush(Theme.TextSecondary);
        g.DrawString(Loc.T("settings.footer"), footerFont, footerBrush, 24, FormHeight - 26);
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

    /// <summary>Подпись на всю высоту карточки с одной строкой (язык).</summary>
    private static void DrawCardLabel(Graphics g, Rectangle card, string text)
    {
        using var font = Theme.Body(9.75f);
        using var brush = new SolidBrush(Theme.TextPrimary);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        var rect = new RectangleF(card.X + 20, card.Y, 200, card.Height);
        g.DrawString(text, font, brush, rect, format);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Theme.Changed -= OnThemeChanged;
            _languageMenu.Dispose();
        }

        base.Dispose(disposing);
    }
}

