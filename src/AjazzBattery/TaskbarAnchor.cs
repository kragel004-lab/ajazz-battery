namespace AjazzBattery;

/// <summary>
/// Невидимое окно, которое даёт кнопку на панели задач с иконкой-процентом.
/// Клик по кнопке открывает основную панель.
/// </summary>
internal sealed class TaskbarAnchor : Form
{
    private readonly TrayContext _context;
    private readonly System.Windows.Forms.Timer _armTimer;
    private bool _armed;

    public TaskbarAnchor(TrayContext context)
    {
        _context = context;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(-32000, -32000, 1, 1);
        ShowIcon = true;
        Text = "AJAZZ Battery";
        MinimizeBox = false;
        MaximizeBox = false;
        ControlBox = false;
        BackColor = Color.Black;
        Opacity = 0.01;

        // при первом показе окно активируется само — не считаем это кликом по кнопке
        _armTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _armTimer.Tick += (_, _) =>
        {
            _armTimer.Stop();
            _armed = true;
        };
        _armTimer.Start();
    }

    public void UpdateState(Icon icon, string tooltip)
    {
        if (IsDisposed)
        {
            return;
        }

        Icon = icon;
        Text = tooltip;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (!_armed)
        {
            return;
        }

        _context.OnTaskbarButtonClicked();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _context.ExitThread();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _armTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
