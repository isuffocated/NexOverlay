using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace NexOverlay.Windows;

public sealed class DesktopCaptureService
{
    private const int SrcCopy = 0x00CC0020;

    public BitmapSource Capture(MonitorBounds bounds)
    {
        var desktopDc = GetDC(IntPtr.Zero);

        if (desktopDc == IntPtr.Zero)
            throw new InvalidOperationException("GetDC failed.");

        var memoryDc = CreateCompatibleDC(desktopDc);

        if (memoryDc == IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, desktopDc);
            throw new InvalidOperationException("CreateCompatibleDC failed.");
        }

        var bitmap = CreateCompatibleBitmap(
            desktopDc,
            bounds.Width,
            bounds.Height);

        if (bitmap == IntPtr.Zero)
        {
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, desktopDc);
            throw new InvalidOperationException("CreateCompatibleBitmap failed.");
        }

        var oldBitmap = SelectObject(memoryDc, bitmap);

        try
        {
            if (!BitBlt(
                    memoryDc,
                    0,
                    0,
                    bounds.Width,
                    bounds.Height,
                    desktopDc,
                    bounds.X,
                    bounds.Y,
                    SrcCopy))
            {
                throw new InvalidOperationException("BitBlt failed.");
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
                SelectObject(memoryDc, oldBitmap);

            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, desktopDc);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(
        IntPtr hWnd,
        IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(
        IntPtr hDc,
        int width,
        int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(
        IntPtr hDc,
        IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hDestDc,
        int x,
        int y,
        int width,
        int height,
        IntPtr hSourceDc,
        int sourceX,
        int sourceY,
        int rasterOperation);
}
