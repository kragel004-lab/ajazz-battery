using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AjazzBattery;

/// <summary>
/// Виджет на панели задач: значок мыши + процент заряда.
/// Слоистое окно с прозрачностью — без кнопки в панели задач и без окна в Alt+Tab.
/// </summary>
internal sealed class TaskbarWidget : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int UlwAlpha = 0x00000002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SwpNoSize = 0x0001;

    private static readonly IntPtr HwndTopMost = new(-1);

    private readonly TrayContext _context;
    private readonly System.Windows.Forms.Timer _layoutTimer;

    private int? _percent;
    private bool _charging;
    private float _scale = 1f;

    private Point _lastPoint = new(int.MinValue, int.MinValue);
    private Size _lastSize = Size.Empty;
    private string _lastText = string.Empty;
    private bool _contentDrawn;
    private int _ticks;
    private bool _hover;

    public TaskbarWidget(TrayContext context)
    {
        _context = context;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Size = new Size(96, 32);

        _layoutTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _layoutTimer.Tick += (_, _) =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                ApplyLayout();
            }
        };

        Theme.Changed += OnThemeChanged;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExLayered | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void UpdateValue(int? percent, bool charging)
    {
        if (_percent == percent && _charging == charging)
        {
            return;
        }

        _percent = percent;
        _charging = charging;
        if (IsHandleCreated)
        {
            ApplyLayout();
        }
    }

    private void OnThemeChanged()
    {
        _lastText = string.Empty;
        if (!IsDisposed && IsHandleCreated)
        {
            ApplyLayout();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _layoutTimer.Start();
        ApplyLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyLayout();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Cursor = Cursors.Hand;
        _hover = true;
        ForceRedraw();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        ForceRedraw();
    }

    private void ForceRedraw()
    {
        _contentDrawn = false;
        if (!IsDisposed && IsHandleCreated)
        {
            ApplyLayout();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _context.TogglePanelFromWidget();
            // после клика заново подтверждаем, что виджет сверху
            BeginInvoke(new Action(ApplyLayout));
        }
    }

    private void ApplyLayout()
    {
        var taskbar = TaskbarInfo.Acquire();
        if (taskbar is null || !taskbar.IsUsable)
        {
            if (Visible)
            {
                Logger.Info($"Виджет скрыт: taskbar={taskbar is not null}, usable={taskbar?.IsUsable}");
            }

            Visible = false;
            _contentDrawn = false;
            _lastPoint = new Point(int.MinValue, int.MinValue);
            return;
        }

        _scale = DeviceDpi / 96f;
        string text = _percent.HasValue ? $"{_percent.Value}%" : "—";
        _ticks++;

        // раз в ~30 секунд перерисовываем принудительно: лечит любые сбои композиции
        bool needRender = !_contentDrawn || text != _lastText || _lastSize == Size.Empty || _ticks % 10 == 0;

        if (!needRender)
        {
            var moved = taskbar.PlaceWidget(_lastSize.Width, _lastSize.Height);
            if (!Visible)
            {
                Visible = true;
            }

            // каждый тик подтверждаем верхний z-порядок: панель задач перерисовывается и может перекрыть виджет
            SetWindowPos(Handle, HwndTopMost, moved.X, moved.Y, _lastSize.Width, _lastSize.Height,
                SwpNoActivate | SwpShowWindow | (uint)SwpNoSize);
            _lastPoint = moved;
            return;
        }

        using var bitmap = Render(text);
        var point = taskbar.PlaceWidget(bitmap.Width, bitmap.Height);
        Visible = true;
        _contentDrawn = UpdateLayered(bitmap, point);
        _lastText = text;
        _lastSize = bitmap.Size;
        _lastPoint = point;
    }

    private Bitmap Render(string text)
    {
        using var font = Theme.Body(10f * _scale, FontStyle.Bold);
        using var probe = new Bitmap(1, 1);
        using var probeGraphics = Graphics.FromImage(probe);
        var textSize = probeGraphics.MeasureString(text, font, new PointF(0, 0), StringFormat.GenericTypographic);

        int padding = (int)(10 * _scale);
        int mouseWidth = (int)(14 * _scale);
        int gap = (int)(8 * _scale);
        int boltWidth = _charging ? (int)(12 * _scale) : 0;
        int height = (int)(30 * _scale);
        int width = padding + mouseWidth + gap + (int)Math.Ceiling(textSize.Width) + 8 + boltWidth + padding;

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // alpha = 1: пиксели почти прозрачные, но кликабельные — иначе клики «сквозь» виджет
        g.Clear(Color.FromArgb(1, 0, 0, 0));

        if (_hover)
        {
            var hoverRect = new RectangleF(0, 0, width, height);
            using var hoverPath = Theme.RoundedRect(hoverRect, 6f * _scale);
            using var hoverBrush = new SolidBrush(Theme.IsLight
                ? Color.FromArgb(26, 0, 0, 0)
                : Color.FromArgb(30, 255, 255, 255));
            g.FillPath(hoverBrush, hoverPath);
        }

        var foreground = Theme.IsLight
            ? Color.FromArgb(235, 26, 26, 26)
            : Color.FromArgb(240, 255, 255, 255);

        DrawMouse(g, new RectangleF(padding, (height - 20 * _scale) / 2f, mouseWidth, 20 * _scale), foreground);

        using (var brush = new SolidBrush(foreground))
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        {
            var textRect = new RectangleF(padding + mouseWidth + gap, 0, textSize.Width + 10, height);
            g.DrawString(text, font, brush, textRect, format);

            if (_charging)
            {
                DrawBolt(g, new RectangleF(textRect.Right + 1 * _scale, (height - 12 * _scale) / 2f, 8 * _scale, 12 * _scale), foreground);
            }
        }

        return bitmap;
    }

    private void DrawMouse(Graphics g, RectangleF box, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.2f, 1.4f * _scale));
        using var body = Theme.RoundedRect(box, box.Width / 2f);
        g.DrawPath(pen, body);

        float centerX = box.X + box.Width / 2f;
        // колесо
        g.DrawLine(pen, centerX, box.Y + box.Height * 0.1f, centerX, box.Y + box.Height * 0.32f);
        // разделитель кнопок
        g.DrawLine(pen, box.X + box.Width * 0.12f, box.Y + box.Height * 0.32f,
            box.X + box.Width * 0.88f, box.Y + box.Height * 0.32f);
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

    private bool UpdateLayered(Bitmap bitmap, Point location)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memoryDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = SelectObject(memoryDc, hBitmap);

        try
        {
            var destination = new Point(location.X, location.Y);
            var size = new Size(bitmap.Width, bitmap.Height);
            var source = new Point(0, 0);
            var blend = new BlendFunction
            {
                BlendOp = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = 1,
            };

            bool ok = UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, UlwAlpha);
            if (ok)
            {
                // панель задач тоже topmost — поднимаемся выше неё
                SetWindowPos(Handle, HwndTopMost, location.X, location.Y, size.Width, size.Height,
                    SwpNoActivate | SwpShowWindow);
            }
            else
            {
                Logger.Warn($"Виджет не отрисован: {Marshal.GetLastWin32Error()}");
            }

            return ok;
        }
        finally
        {
            SelectObject(memoryDc, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Theme.Changed -= OnThemeChanged;
            _layoutTimer.Stop();
            _layoutTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
        IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}




