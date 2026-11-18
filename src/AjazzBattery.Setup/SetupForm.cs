using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace AjazzBattery.Setup;

internal sealed class SetupForm : Form
{
    private const int FormWidth = 520;
    private const int FormHeight = 404;

    private readonly TextBox _pathBox;
    private readonly FlatButton _browseButton;
    private readonly FlatButton _primaryButton;
    private readonly FlatButton _secondaryButton;
    private readonly FlatToggle _autostart;
    private readonly FlatToggle _desktopShortcut;

    private double _progress;
    private string _status = Loc.T("setup.ready");
    private bool _installing;
    private bool _installed;
    private string _error = string.Empty;

    private static readonly Rectangle CardOne = new(24, 84, 472, 100);
    private static readonly Rectangle CardTwo = new(24, 196, 472, 92);

    public SetupForm()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Window;
        ClientSize = new Size(FormWidth, FormHeight);
        Text = Loc.T("setup.windowTitle");
        MaximizeBox = false;
        MinimizeBox = false;

        // цвета поля выставляются после создания handle (см. ApplyPathBoxTheme),
        // иначе нативный EDIT может остаться со светлым фоном
        _pathBox = new TextBox
        {
            Text = Installer.DefaultTarget,
            BorderStyle = BorderStyle.None,
            Font = Theme.Body(9.75f),
            Bounds = PathBoxBounds(),
        };

        _browseButton = new FlatButton
        {
            Text = "…",
            Bounds = new Rectangle(444, 128, 32, 26),
            Font = Theme.Body(10f),
        };
        _browseButton.Click += (_, _) => Browse();

        _autostart = new FlatToggle { Bounds = new Rectangle(436, 216, 40, 20), Checked = true };
        _desktopShortcut = new FlatToggle { Bounds = new Rectangle(436, 256, 40, 20), Checked = true };

        _primaryButton = new FlatButton
        {
            Text = Loc.T("setup.install"),
            Kind = ButtonKind.Accent,
            Bounds = new Rectangle(356, 344, 140, 34),
            Font = Theme.Body(9.75f),
        };
        _primaryButton.Click += (_, _) => OnPrimary();

        _secondaryButton = new FlatButton
        {
            Text = Loc.T("setup.cancel"),
            Bounds = new Rectangle(242, 344, 104, 34),
            Font = Theme.Body(9.75f),
        };
        _secondaryButton.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { _pathBox, _browseButton, _autostart, _desktopShortcut, _primaryButton, _secondaryButton });
        Theme.Changed += OnThemeChanged;
        ApplyTheme();
    }

    /// <summary>Прямоугольник поля пути внутри карточки (рамка рисуется в OnPaint).</summary>
    private static Rectangle PathBoxBounds() => new(54, 130, 356, 20);

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

        // handle уже есть — теперь цвета поля точно применились
        ApplyPathBoxTheme();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyPathBoxTheme();
        _pathBox.Select(_pathBox.TextLength, 0);
    }

    private void OnThemeChanged()
    {
        if (IsDisposed)
        {
            return;
        }

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        BackColor = Theme.Window;
        ApplyPathBoxTheme();
        if (IsHandleCreated)
        {
            NativeUi.ApplyBorderColor(this, Theme.Window);
        }

        Invalidate(true);
    }

    /// <summary>
    /// Красит поле пути: тёмная заливка <see cref="Theme.ControlFill"/>, текст <see cref="Theme.TextPrimary"/>.
    /// Нативный EDIT не перерисовывает фон сам, поэтому после смены цвета его нужно
    /// инвалидировать (родительская Invalidate на дочернее окно не действует).
    /// </summary>
    private void ApplyPathBoxTheme()
    {
        _pathBox.BackColor = Theme.ControlFill;
        _pathBox.ForeColor = Theme.TextPrimary;

        if (_pathBox.IsHandleCreated)
        {
            _pathBox.Invalidate();
            _pathBox.Update();
        }
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Loc.T("setup.browseTitle"),
            UseDescriptionForTitle = true,
            SelectedPath = _pathBox.Text,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = dialog.SelectedPath;
        }
    }

    private async void OnPrimary()
    {
        if (_installed)
        {
            StartApp();
            return;
        }

        if (_installing)
        {
            return;
        }

        var target = _pathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            _error = Loc.T("setup.needFolder");
            Invalidate();
            return;
        }

        _error = string.Empty;
        _installing = true;
        SetControlsEnabled(false);
        _primaryButton.Text = Loc.T("setup.installing");

        try
        {
            await Task.Run(() => Installer.Install(target, _autostart.Checked, _desktopShortcut.Checked, Report));
            _installed = true;
            _status = Loc.T("setup.installed");
            _primaryButton.Text = Loc.T("setup.run");
            _secondaryButton.Text = Loc.T("setup.close");
            _progress = 1;
        }
        catch (Exception ex)
        {
            _error = Loc.T("setup.installFailed", ex.Message);
            _primaryButton.Text = Loc.T("setup.retry");
        }
        finally
        {
            _installing = false;
            SetControlsEnabled(true);
            Invalidate();
        }
    }

    private void Report(string status, double progress)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string, double>(Report), status, progress);
            return;
        }

        _status = status;
        _progress = progress;
        Invalidate();
    }

    private void SetControlsEnabled(bool enabled)
    {
        _pathBox.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _autostart.Enabled = enabled;
        _desktopShortcut.Enabled = enabled;
        _secondaryButton.Enabled = enabled;
    }

    private void StartApp()
    {
        var exe = Path.Combine(_pathBox.Text.Trim(), Installer.AppFileName);
        if (!File.Exists(exe))
        {
            _error = Loc.T("setup.appFileMissing");
            Invalidate();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Close();
        }
        catch (Exception ex)
        {
            _error = Loc.T("setup.launchFailed", ex.Message);
            Invalidate();
        }
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

        using (var titleFont = Theme.Display(17f, FontStyle.Bold))
        using (var titleBrush = new SolidBrush(Theme.TextPrimary))
        {
            g.DrawString(Loc.T("setup.title"), titleFont, titleBrush, 22, 16);
        }

        using (var subFont = Theme.Body(9.75f))
        using (var subBrush = new SolidBrush(Theme.TextSecondary))
        {
            g.DrawString(Loc.T("setup.subtitle"), subFont, subBrush, 24, 50);
        }

        DrawCard(g, CardOne);
        DrawCard(g, CardTwo);

        using (var labelFont = Theme.Body(9.75f))
        using (var labelBrush = new SolidBrush(Theme.TextPrimary))
        {
            g.DrawString(Loc.T("setup.installDir"), labelFont, labelBrush, 44, 102);
            g.DrawString(Loc.T("setup.autostart"), labelFont, labelBrush, 44, 216);
            g.DrawString(Loc.T("setup.desktopShortcut"), labelFont, labelBrush, 44, 256);
        }

        // поле пути
        var field = new RectangleF(44, 124, 396, 30);
        using (var fieldPath = Theme.RoundedRect(field, 4f))
        {
            using var fill = new SolidBrush(Theme.ControlFill);
            g.FillPath(fill, fieldPath);
            using var stroke = new Pen(Theme.ControlStroke, 1f);
            g.DrawPath(stroke, fieldPath);
        }

        // прогресс
        if (_installing || _installed || _progress > 0)
        {
            var track = new RectangleF(24, 312, 472, 6);
            using (var trackPath = Theme.RoundedRect(track, 3f))
            using (var brush = new SolidBrush(Theme.ControlFill))
            {
                g.FillPath(brush, trackPath);
            }

            var filled = new RectangleF(track.X, track.Y, (float)(track.Width * Math.Clamp(_progress, 0, 1)), track.Height);
            if (filled.Width > 1)
            {
                using var fillPath = Theme.RoundedRect(filled, 3f);
                using var brush = new SolidBrush(Theme.Accent);
                g.FillPath(brush, fillPath);
            }
        }

        using (var statusFont = Theme.Body(8.75f))
        using (var statusBrush = new SolidBrush(_error.Length > 0 ? Color.FromArgb(228, 89, 90) : Theme.TextSecondary))
        {
            var text = (_error.Length > 0 ? _error : _status).Replace("\r", string.Empty);
            g.DrawString(text, statusFont, statusBrush, new RectangleF(24, 288, 472, 20));
        }
    }

    private static void DrawCard(Graphics g, Rectangle card)
    {
        using var path = Theme.RoundedRect(card, 8f);
        using (var fill = new SolidBrush(Theme.Card))
        {
            g.FillPath(fill, path);
        }

        using var pen = new Pen(Theme.Stroke, 1f);
        g.DrawPath(pen, path);
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

