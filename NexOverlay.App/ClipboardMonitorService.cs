using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NexOverlay.App;

public sealed class ClipboardMonitorService :
    IDisposable
{
    private const int WM_CLIPBOARDUPDATE =
        0x031D;

    private static readonly IntPtr HWND_MESSAGE =
        new(-3);

    private readonly HwndSource _source;

    private bool _disposed;

    public event EventHandler<string>? TextCaptured;

    public ClipboardMonitorService()
    {
        var parameters =
            new HwndSourceParameters(
                "NexOverlay.ClipboardListener")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ParentWindow = HWND_MESSAGE
            };

        _source =
            new HwndSource(parameters);

        _source.AddHook(
            WndProc);

        if (!AddClipboardFormatListener(
                _source.Handle))
        {
            throw
                new InvalidOperationException(
                    "AddClipboardFormatListener failed.");
        }
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message !=
            WM_CLIPBOARDUPDATE)
        {
            return IntPtr.Zero;
        }

        _source.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(ReadClipboardText));

        return IntPtr.Zero;
    }

    private void ReadClipboardText()
    {
        if (_disposed)
            return;

        try
        {
            if (!Clipboard.ContainsText())
                return;

            var text =
                Clipboard.GetText();

            if (string.IsNullOrWhiteSpace(text))
                return;

            TextCaptured?.Invoke(
                this,
                text);
        }
        catch
        {
            // Clipboard can temporarily be locked by another process.
            // The next WM_CLIPBOARDUPDATE will retry naturally.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        RemoveClipboardFormatListener(
            _source.Handle);

        _source.RemoveHook(
            WndProc);

        _source.Dispose();
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(
        IntPtr hwnd);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(
        IntPtr hwnd);
}