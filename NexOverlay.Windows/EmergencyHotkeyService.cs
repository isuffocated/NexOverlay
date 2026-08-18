using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NexOverlay.Windows;

public sealed class EmergencyHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0012;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;

    private const uint VkSpace = 0x20;
    private const int HotkeyId = 0x4E58;

    private Thread? _thread;
    private readonly ManualResetEventSlim _ready = new(false);

    private volatile bool _disposed;
    private uint _threadId;

    public event EventHandler? Triggered;

    public bool IsRegistered { get; private set; }

    public void Register()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        if (_thread is not null)
            return;

        _thread =
            new Thread(MessageLoop)
            {
                IsBackground = true,
                Name = "NexOverlay.EmergencyHotkey"
            };

        _thread.Start();

        _ready.Wait(
            TimeSpan.FromSeconds(2));
    }

    private void MessageLoop()
    {
        _threadId =
            GetCurrentThreadId();

        // Force creation of this thread's message queue.
        _ = PeekMessage(
            out _,
            IntPtr.Zero,
            0,
            0,
            0);

        IsRegistered =
            RegisterHotKey(
                IntPtr.Zero,
                HotkeyId,
                ModControl |
                ModAlt |
                ModNoRepeat,
                VkSpace);

        _ready.Set();

        try
        {
            while (GetMessage(
                out var message,
                IntPtr.Zero,
                0,
                0) > 0)
            {
                if (message.Message == WmHotkey &&
                    message.WParam.ToInt32() == HotkeyId)
                {
                    try
                    {
                        Triggered?.Invoke(
                            this,
                            EventArgs.Empty);
                    }
                    catch
                    {
                        // Keep emergency channel alive.
                    }
                }
            }
        }
        finally
        {
            if (IsRegistered)
            {
                _ = UnregisterHotKey(
                    IntPtr.Zero,
                    HotkeyId);
            }

            IsRegistered = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_threadId != 0)
        {
            _ = PostThreadMessage(
                _threadId,
                WmQuit,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        var thread = _thread;
        _thread = null;

        if (thread is not null &&
            thread.IsAlive)
        {
            thread.Join(500);
        }

        _ready.Dispose();

        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Message
    {
        public IntPtr HWnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Pt;
        public uint LPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(
        out Win32Message lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(
        out Win32Message lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax,
        uint wRemoveMsg);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool PostThreadMessage(
        uint idThread,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}