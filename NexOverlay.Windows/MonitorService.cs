using System;
using System.Runtime.InteropServices;

namespace NexOverlay.Windows;

public sealed class MonitorService
{
    private const uint MonitorDefaultToNearest = 2;

    public MonitorBounds GetMonitorFromCursor()
    {
        if (!GetCursorPos(out var point))
            throw new InvalidOperationException("Failed to get cursor position.");

        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);

        if (monitor == IntPtr.Zero)
            throw new InvalidOperationException("Failed to resolve monitor.");

        var info = new MonitorInfo
        {
            cbSize = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException("Failed to get monitor information.");

        return new MonitorBounds(
            info.rcMonitor.Left,
            info.rcMonitor.Top,
            info.rcMonitor.Right - info.rcMonitor.Left,
            info.rcMonitor.Bottom - info.rcMonitor.Top);
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(
        Point pt,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(
        IntPtr hMonitor,
        ref MonitorInfo lpmi);
}

public readonly record struct MonitorBounds(
    int X,
    int Y,
    int Width,
    int Height);