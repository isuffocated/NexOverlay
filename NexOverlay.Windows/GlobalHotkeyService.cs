using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NexOverlay.Windows;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int VkCapital = 0x14;
    private const int VkSpace = 0x20;

    private Thread? _thread;
    private volatile bool _running;
    private bool _latched;
    private bool _disposed;

    public event EventHandler? Triggered;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_thread is { IsAlive: true })
            return;

        _running = true;

        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "NexOverlay.StablePrimaryInput"
        };

        _thread.Start();
    }

    private void PollLoop()
    {
        while (_running)
        {
            var down =
                IsDown(VkCapital) &&
                IsDown(VkSpace);

            if (down)
            {
                if (!_latched)
                {
                    _latched = true;

                    try
                    {
                        Triggered?.Invoke(
                            this,
                            EventArgs.Empty);
                    }
                    catch
                    {
                        // Never let a subscriber kill the polling thread.
                    }
                }
            }
            else
            {
                _latched = false;
            }

            Thread.Sleep(20);
        }
    }

    private static bool IsDown(int key)
    {
        return (GetAsyncKeyState(key) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _running = false;

        var thread = _thread;
        _thread = null;

        if (thread is not null &&
            thread.IsAlive &&
            thread.ManagedThreadId != Environment.CurrentManagedThreadId)
        {
            thread.Join(300);
        }

        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}