using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NexOverlay.Windows;

namespace NexOverlay.App;

public partial class HandleWindow : Window
{
    private static readonly IntPtr HwndTopmost = new(-1);

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public event EventHandler? HoverTriggered;

    private bool _hoverConsumed;
    private int _zOrderGeneration;

    public HandleWindow()
    {
        InitializeComponent();

        WindowStartupLocation = WindowStartupLocation.Manual;

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
                _hoverConsumed = false;
        };
    }

    public void ShowForMonitor(
        MonitorBounds monitor,
        HandleMode mode)
    {
        SetMode(mode);

        Left =
            monitor.X +
            (monitor.Width - Width) / 2.0;

        Top =
            monitor.Y + 24;

        Opacity = 1;
        Topmost = true;
        _hoverConsumed = false;

        if (!IsVisible)
            Show();

        UpdateLayout();

        ReinforceTopmost();
    }

    public void ReinforceTopmost()
    {
        _zOrderGeneration++;
        var generation = _zOrderGeneration;

        ForceTopmost();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (generation == _zOrderGeneration && IsVisible)
                    ForceTopmost();
            }));

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(90)
        };

        var pulses = 0;

        timer.Tick += (_, _) =>
        {
            if (generation != _zOrderGeneration || !IsVisible)
            {
                timer.Stop();
                return;
            }

            ForceTopmost();

            pulses++;

            if (pulses >= 4)
                timer.Stop();
        };

        timer.Start();
    }

    public bool IsCursorPhysicallyInside()
    {
        try
        {
            var hwnd =
                new WindowInteropHelper(this).Handle;

            if (hwnd == IntPtr.Zero ||
                !IsWindow(hwnd) ||
                !IsWindowVisible(hwnd))
            {
                return false;
            }

            if (!GetWindowRect(
                    hwnd,
                    out var rect))
            {
                return false;
            }

            if (!GetCursorPos(
                    out var cursor))
            {
                return false;
            }

            return
                cursor.X >= rect.Left &&
                cursor.X < rect.Right &&
                cursor.Y >= rect.Top &&
                cursor.Y < rect.Bottom;
        }
        catch
        {
            return false;
        }
    }

    public bool IsHealthy(MonitorBounds monitor)
    {
        try
        {
            if (!IsVisible || Opacity < 0.5)
                return false;

            if (ActualWidth < 20 || ActualHeight < 10)
                return false;

            var hwnd =
                new WindowInteropHelper(this).Handle;

            if (hwnd == IntPtr.Zero)
                return false;

            if (!IsWindow(hwnd))
                return false;

            if (!IsWindowVisible(hwnd))
                return false;

            if (!GetWindowRect(hwnd, out var rect))
                return false;

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            if (width < 20 || height < 10)
                return false;

            var monitorRight =
                monitor.X + monitor.Width;

            var monitorBottom =
                monitor.Y + monitor.Height;

            var intersects =
                rect.Right > monitor.X &&
                rect.Left < monitorRight &&
                rect.Bottom > monitor.Y &&
                rect.Top < monitorBottom;

            return intersects;
        }
        catch
        {
            return false;
        }
    }

    public void ForceTopmost()
    {
        var hwnd =
            new WindowInteropHelper(this).Handle;

        if (hwnd == IntPtr.Zero)
            return;

        _ = SetWindowPos(
            hwnd,
            HwndTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove |
            SwpNoSize |
            SwpNoActivate |
            SwpShowWindow);
    }

    public void SetMode(HandleMode mode)
    {
        if (mode == HandleMode.Open)
        {
            HandleBorder.BorderBrush =
                new SolidColorBrush(
                    Color.FromRgb(
                        221,
                        235,
                        255));

            HandleDot.Background =
                new SolidColorBrush(
                    Color.FromRgb(
                        199,
                        222,
                        255));

            LeftLine.Background =
                new SolidColorBrush(
                    Color.FromArgb(
                        92,
                        255,
                        255,
                        255));

            HandleLine.Background =
                new SolidColorBrush(
                    Color.FromArgb(
                        92,
                        255,
                        255,
                        255));

            return;
        }

        HandleBorder.BorderBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    255,
                    150,
                    174));

        HandleDot.Background =
            new SolidColorBrush(
                Color.FromRgb(
                    255,
                    105,
                    135));

        LeftLine.Background =
            new SolidColorBrush(
                Color.FromArgb(
                    110,
                    255,
                    126,
                    153));

        HandleLine.Background =
            new SolidColorBrush(
                Color.FromArgb(
                    110,
                    255,
                    126,
                    153));
    }

    private void Root_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (_hoverConsumed)
            return;

        _hoverConsumed = true;

        HoverTriggered?.Invoke(
            this,
            EventArgs.Empty);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(
        out Point point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        IntPtr hWnd,
        out Rect rect);
}