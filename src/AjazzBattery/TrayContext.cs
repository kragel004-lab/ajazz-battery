using System.Diagnostics;
using System.Drawing;
using Microsoft.Win32;

namespace AjazzBattery;

internal sealed class TrayContext : ApplicationContext
{
    private const string RunKeyName = "AJAZZBattery";

    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly List<(ToolStripMenuItem Item, string Key)> _localizedItems = new();
    private readonly Control _marshaler;
    private readonly EventWaitHandle _showEvent;

    private PanelForm? _panel;
    private SettingsForm? _settings;
    private TaskbarWidget? _widget;
    private Icon? _currentIcon;
    private BatteryReading? _last;
    private bool _polling;
    private bool _lowNotified;
    private bool _disposed;
    private DateTime _lastClockSync = DateTime.MinValue;

    // Определение зарядки: донгл не отдаёт флаг «заряжается», поэтому ловим рост заряда.
    private int? _previousPercent;
    private DateTime _lastRise = DateTime.MinValue;
    private bool _charging;

    // только для отладки/скриншотов: AJAZZ_FORCE_CHARGING=1
    private static readonly bool ForceCharging =
        Environment.GetEnvironmentVariable("AJAZZ_FORCE_CHARGING") == "1";

    public AppConfig Config { get; }

    public bool IsSettingsOpen => _settings is { IsDisposed: false, Visible: true };

    public TrayContext()
    {
        Config = AppConfig.Load();
        Loc.Language = Config.Language;
        _marshaler = new Control();
        _marshaler.CreateControl();

        _statusItem = new ToolStripMenuItem(Loc.T("tray.searching")) { ForeColor = Theme.TextPrimary };
        _startupItem = new ToolStripMenuItem(Loc.T("tray.autostart"))
        {
            CheckOnClick = true,
            Checked = Config.RunAtStartup,
        };
        _startupItem.CheckedChanged += (_, _) => ToggleAutostart(_startupItem.Checked);

        _menu = BuildMenu();

        _currentIcon = BatteryGlyph.Create(null, Config.ShowPercentOnIcon);
        _tray = new NotifyIcon
        {
            Visible = true,
            Icon = _currentIcon,
            Text = Loc.T("tray.searchingTooltip"),
            ContextMenuStrip = _menu,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                TogglePanel();
            }
        };
        _tray.DoubleClick += (_, _) => TogglePanel();

        _timer = new System.Windows.Forms.Timer { Interval = Config.PollSeconds * 1000 };
        _timer.Tick += (_, _) => Poll(userInitiated: false);
        _timer.Start();

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\AJAZZBattery.Show");
        ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => ShowPanelOnUi(), null, Timeout.Infinite, executeOnlyOnce: false);

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Theme.Changed += UpdateChrome;

        Logger.Info(Loc.T("log.launch"));
        ApplyTaskbarPresence();
        Poll(userInitiated: false);

        if (Config.SyncDockClock)
        {
            SyncClockAsync(silent: true);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            // RenderMode задаём до Renderer: установка режима пересоздаёт рендерер по умолчанию
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            BackColor = Theme.Card,
            ForeColor = Theme.TextPrimary,
            Font = Theme.Body(9.75f),
            Padding = new Padding(4),
        };

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateItem("menu.refresh", () => Poll(userInitiated: true)));
        menu.Items.Add(CreateItem("menu.settings", ShowSettings));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateItem("menu.syncClock", () => SyncClockAsync(silent: false)));
        menu.Items.Add(CreateItem("menu.clearScreen", ClearDockScreen));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(CreateItem("menu.openLog", OpenLog));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateItem("menu.exit", ExitThread));

        menu.Opened += (_, _) => _panel?.SuppressHide();
        menu.Closed += (_, _) => _panel?.AllowHide();
        return menu;
    }

    private ToolStripMenuItem CreateItem(string key, Action action)
    {
        var item = new ToolStripMenuItem(Loc.T(key)) { ForeColor = Theme.TextPrimary };
        item.Click += (_, _) => action();
        _localizedItems.Add((item, key));
        return item;
    }

    /// <summary>Перестроить подписи меню и трея после смены языка.</summary>
    private void RefreshTexts(BatteryReading? reading)
    {
        _startupItem.Text = Loc.T("tray.autostart");
        foreach (var (item, key) in _localizedItems)
        {
            item.Text = Loc.T(key);
        }

        ApplyStatusText(reading);
    }

    private void ApplyStatusText(BatteryReading? reading)
    {
        if (reading is null)
        {
            _statusItem.Text = Loc.T("tray.searching");
            _tray.Text = Loc.T("tray.searchingTooltip");
            return;
        }

        if (reading.Found)
        {
            var suffix = _charging ? Loc.T("tray.charging")
                : reading.Percent >= 100 ? Loc.T("tray.charged")
                : string.Empty;
            _tray.Text = Trim(Loc.T("tray.tooltip", reading.Percent, suffix));
            _statusItem.Text = Loc.T("tray.status", reading.Percent, suffix);
        }
        else
        {
            _tray.Text = Loc.T("tray.notFound");
            _statusItem.Text = Loc.T("tray.noLink", Shorten(reading.Error, 40));
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            Theme.Refresh();
        }
    }

    private void UpdateChrome()
    {
        _menu.BackColor = Theme.Card;
        _menu.ForeColor = Theme.TextPrimary;
        foreach (ToolStripItem item in _menu.Items)
        {
            item.ForeColor = Theme.TextPrimary;
        }
    }

    private void ApplyTaskbarPresence()
    {
        if (Config.ShowTaskbarIcon)
        {
            if (_widget is null || _widget.IsDisposed)
            {
                _widget = new TaskbarWidget(this);
                _widget.Show();
                if (_last is not null)
                {
                    _widget.UpdateValue(_last.Found ? _last.Percent : null, _charging);
                }
            }
        }
        else if (_widget is not null)
        {
            _widget.Hide();
            _widget.Dispose();
            _widget = null;
        }
    }

    public void TogglePanelFromWidget()
    {
        if (_panel is { Visible: true })
        {
            _panel.Hide();
            return;
        }

        // клик по виджету сначала снимает фокус с панели (она прячется сама) — не открываем её снова
        if (_panel is not null && (DateTime.UtcNow - _panel.LastAutoHide).TotalMilliseconds < 400)
        {
            return;
        }

        TogglePanel();
    }

    private void ShowPanelOnUi() => _marshaler.BeginInvoke(new Action(TogglePanel));

    private void TogglePanel()
    {
        _panel ??= new PanelForm(this);
        if (_last is not null)
        {
            _panel.UpdateReading(_last, _charging);
        }

        _panel.ToggleNearTray();
    }

    public void ShowMenuAt(Control anchor) => _menu.Show(Cursor.Position);

    public void ShowSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.Activate();
            return;
        }

        _panel?.SuppressHide();
        _settings = new SettingsForm(this);
        _settings.FormClosed += (_, _) =>
        {
            _settings = null;
            _panel?.AllowHide();
        };
        _settings.Show();
        _settings.Activate();
    }

    public async void Poll(bool userInitiated)
    {
        if (_polling)
        {
            return;
        }

        _polling = true;
        try
        {
            var reading = await Task.Run(AjazzReader.Read);
            Apply(reading, userInitiated);
        }
        catch (Exception ex)
        {
            Logger.Error(Loc.T("log.pollError", ex));
            Apply(BatteryReading.NotConnected(ex.Message), userInitiated);
        }
        finally
        {
            _polling = false;
        }
    }

    private void Apply(BatteryReading reading, bool userInitiated)
    {
        var previous = _last;
        _last = reading;
        UpdateChargingState(reading);

        if (previous is null || previous.Percent != reading.Percent || previous.Found != reading.Found ||
            userInitiated || previous.Found != reading.Found)
        {
            UpdateIcons(reading);
        }

        ApplyStatusText(reading);
        if (!reading.Found)
        {
            Logger.Warn(Loc.T("log.deviceNotFound", reading.Error));
        }

        _panel?.UpdateReading(reading, _charging);
        HandleLowBattery(reading);
        MaybeSyncClock();
    }

    private void UpdateChargingState(BatteryReading reading)
    {
        if (ForceCharging)
        {
            _charging = reading.Found;
            return;
        }

        if (!reading.Found)
        {
            _charging = false;
            _previousPercent = null;
            return;
        }

        if (_previousPercent is int previous)
        {
            if (reading.Percent > previous)
            {
                _charging = true;
                _lastRise = reading.Timestamp;
            }
            else if (reading.Percent < previous)
            {
                _charging = false;
            }
        }

        if (reading.Percent >= 100)
        {
            _charging = false;
        }
        else if (_charging && (reading.Timestamp - _lastRise).TotalMinutes > 15)
        {
            _charging = false;
        }

        _previousPercent = reading.Percent;
    }

    private void UpdateIcons(BatteryReading reading)
    {
        var icon = BatteryGlyph.Create(reading.Found ? reading.Percent : null, Config.ShowPercentOnIcon);
        _tray.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;

        if (_widget is { IsDisposed: false })
        {
            _widget.UpdateValue(reading.Found ? reading.Percent : null, _charging);
        }
    }

    private void HandleLowBattery(BatteryReading reading)
    {
        if (!reading.Found || !Config.Notifications || _charging)
        {
            return;
        }

        if (reading.Percent <= Config.LowBatteryThreshold && !_lowNotified)
        {
            _lowNotified = true;
            _tray.ShowBalloonTip(8000,
                Loc.T("notify.lowTitle"),
                Loc.T("notify.lowText", reading.Percent),
                ToolTipIcon.Warning);
            Logger.Info(Loc.T("log.lowNotified", reading.Percent));
        }
        else if (reading.Percent > Config.LowBatteryThreshold + 3)
        {
            _lowNotified = false;
        }
    }

    private void MaybeSyncClock()
    {
        if (!Config.SyncDockClock)
        {
            return;
        }

        if (DateTime.Now - _lastClockSync < TimeSpan.FromMinutes(Config.ClockSyncMinutes))
        {
            return;
        }

        SyncClockAsync(silent: true);
    }

    private async void SyncClockAsync(bool silent)
    {
        _lastClockSync = DateTime.Now;
        bool ok = await Task.Run(() => AjazzReader.SyncClock());
        Logger.Info(Loc.T("log.clockSync", ok ? Loc.T("log.sent") : Loc.T("log.failed")));
        if (!silent)
        {
            _tray.ShowBalloonTip(4000, Loc.T("notify.clockTitle"),
                ok ? Loc.T("notify.clockOk") : Loc.T("notify.clockFail"),
                ok ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
    }

    private async void ClearDockScreen()
    {
        bool ok = await Task.Run(() => AjazzReader.ClearDockScreen());
        Logger.Info(Loc.T("log.screenClear", ok ? Loc.T("log.sent") : Loc.T("log.failed")));
        _tray.ShowBalloonTip(5000, Loc.T("notify.screenTitle"),
            ok ? Loc.T("notify.screenOk") : Loc.T("notify.screenFail"),
            ok ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    public void ApplyAutostart(bool enable) => ToggleAutostart(enable);

    private void ToggleAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null)
            {
                return;
            }

            if (enable)
            {
                key.SetValue(RunKeyName, $"\"{Application.ExecutablePath}\" --tray");
            }
            else
            {
                key.DeleteValue(RunKeyName, throwOnMissingValue: false);
            }

            if (_startupItem.Checked != enable)
            {
                _startupItem.Checked = enable;
            }

            Logger.Info(Loc.T("log.autostart", enable ? Loc.T("log.autostartOn") : Loc.T("log.autostartOff")));
        }
        catch (Exception ex)
        {
            Logger.Error(Loc.T("log.autostartFailed", ex.Message));
        }
    }

    public void OnSettingsChanged()
    {
        Loc.Language = Config.Language;
        _timer.Interval = Math.Clamp(Config.PollSeconds, 10, 3600) * 1000;
        UpdateIcons(_last ?? BatteryReading.NotConnected(Loc.T("tray.noData")));
        ApplyTaskbarPresence();
        RefreshTexts(_last);
        _panel?.ApplyLanguage();
        _panel?.Invalidate(true);
        Logger.Info(Loc.T("log.settingsApplied"));
    }

    private static void OpenLog()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Logger.LogFilePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Warn(Loc.T("log.openLogFailed", ex.Message));
        }
    }

    private static string Shorten(string text, int max) =>
        string.IsNullOrWhiteSpace(text) ? Loc.T("tray.noData") : text.Length <= max ? text : text[..max] + "…";

    private static string Trim(string text) => text.Length <= 127 ? text : text[..127];

    protected override void ExitThreadCore()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Logger.Info(Loc.T("log.exit"));
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Theme.Changed -= UpdateChrome;
        _timer.Stop();
        _timer.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _currentIcon?.Dispose();
        _panel?.Dispose();
        _settings?.Dispose();
        _widget?.Dispose();
        base.ExitThreadCore();
    }
}





