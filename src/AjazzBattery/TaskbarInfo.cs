using System.Runtime.InteropServices;

namespace AjazzBattery;

/// <summary>Геометрия панели задач Windows и вычисление места для виджета.</summary>
internal sealed class TaskbarInfo
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    public Rectangle TaskbarRect { get; private init; }
    public Rectangle NotifyRect { get; private init; }
    public bool Horizontal { get; private init; }
    public bool TaskbarVisible { get; private init; }

    public bool IsUsable => TaskbarVisible && !IsFullscreenAppForeground();

    public static TaskbarInfo? Acquire()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero)
        {
            return null;
        }

        if (!GetWindowRect(taskbar, out var taskbarRect))
        {
            return null;
        }

        var notify = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        NativeRect notifyRect = default;
        if (notify != IntPtr.Zero)
        {
            GetWindowRect(notify, out notifyRect);
        }

        var bounds = taskbarRect.ToRectangle();
        var notifyBounds = notifyRect.ToRectangle();

        var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        bool onScreen = bounds.IntersectsWith(screen) && bounds.Width > 8 && bounds.Height > 8;
        bool hidden = bounds.Bottom <= screen.Top + 2 || bounds.Top >= screen.Bottom - 2 ||
                      bounds.Right <= screen.Left + 2 || bounds.Left >= screen.Right - 2;

        return new TaskbarInfo
        {
            TaskbarRect = bounds,
            NotifyRect = notifyBounds,
            Horizontal = bounds.Width >= bounds.Height,
            TaskbarVisible = onScreen && !hidden,
        };
    }

    public Point PlaceWidget(int width, int height)
    {
        int gap = 8;
        if (Horizontal)
        {
            int anchorX = NotifyRect.Width > 0 ? NotifyRect.Left : TaskbarRect.Right - 320;
            int x = Math.Max(TaskbarRect.Left + 4, anchorX - width - gap);
            int y = TaskbarRect.Top + (TaskbarRect.Height - height) / 2;
            return new Point(x, y);
        }

        int anchorY = NotifyRect.Height > 0 ? NotifyRect.Top : TaskbarRect.Bottom - 320;
        int verticalY = Math.Max(TaskbarRect.Top + 4, anchorY - height - gap);
        int verticalX = TaskbarRect.Left + (TaskbarRect.Width - width) / 2;
        return new Point(verticalX, verticalY);
    }

    private static bool IsFullscreenAppForeground()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(foreground, out var pid);
        if (pid == (uint)Environment.ProcessId)
        {
            return false;
        }

        var className = new System.Text.StringBuilder(128);
        GetClassName(foreground, className, className.Capacity);
        var name = className.ToString();
        if (name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")
        {
            return false;
        }

        long style = GetWindowLongPtr(foreground, GwlExStyle).ToInt64();
        if ((style & WsExToolWindow) != 0)
        {
            return false;
        }

        if (!GetWindowRect(foreground, out var rect))
        {
            return false;
        }

        var windowRect = rect.ToRectangle();
        var screen = Screen.FromHandle(foreground).Bounds;
        return windowRect.Left <= screen.Left && windowRect.Top <= screen.Top &&
               windowRect.Right >= screen.Right && windowRect.Bottom >= screen.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder buffer, int size);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
}
