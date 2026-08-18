using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NexOverlay.Windows;

public sealed class NativeInputWatchdog : IDisposable
{
    private const int VkCapital = 0x14;
    private const int VkSpace = 0x20;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;

    private readonly object _sync = new();

    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    private bool _primaryLatched;
    private bool _emergencyLatched;

    private long _uiHeartbeatTicks;
    private int _uiState;
    private int _overlayVisible;

    private readonly string _logPath;

    public event EventHandler? PrimaryTriggered;
    public event EventHandler? EmergencyTriggered;

    public NativeInputWatchdog()
    {
        var directory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NexOverlay",
                "diagnostics");

        Directory.CreateDirectory(directory);

        _logPath =
            Path.Combine(
                directory,
                "fatal-input.log");
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        if (_thread is { IsAlive: true })
            return;

        _running = true;

        Interlocked.Exchange(
            ref _uiHeartbeatTicks,
            DateTimeOffset.UtcNow.UtcTicks);

        _thread =
            new Thread(Loop)
            {
                IsBackground = true,
                Name = "NexOverlay.NativeInput"
            };

        _thread.Start();

        Log("NATIVE_INPUT_START");
    }

    public void PulseUi(
        int state,
        bool overlayVisible)
    {
        Interlocked.Exchange(
            ref _uiHeartbeatTicks,
            DateTimeOffset.UtcNow.UtcTicks);

        Volatile.Write(
            ref _uiState,
            state);

        Volatile.Write(
            ref _overlayVisible,
            overlayVisible ? 1 : 0);
    }

    private void Loop()
    {
        var lastSummary =
            DateTimeOffset.MinValue;

        var lastUiStallLog =
            DateTimeOffset.MinValue;

        while (_running)
        {
            try
            {
                var now =
                    DateTimeOffset.UtcNow;

                var caps =
                    IsDown(VkCapital);

                var space =
                    IsDown(VkSpace);

                var ctrl =
                    IsDown(VkControl);

                var alt =
                    IsDown(VkMenu);

                var primaryDown =
                    caps &&
                    space;

                if (primaryDown)
                {
                    if (!_primaryLatched)
                    {
                        _primaryLatched = true;

                        Log(
                            $"PRIMARY_NATIVE state={Volatile.Read(ref _uiState)}");

                        SafeRaise(
                            PrimaryTriggered);
                    }
                }
                else
                {
                    _primaryLatched = false;
                }

                var emergencyDown =
                    ctrl &&
                    alt &&
                    space;

                if (emergencyDown)
                {
                    if (!_emergencyLatched)
                    {
                        _emergencyLatched = true;

                        Log(
                            $"EMERGENCY_NATIVE state={Volatile.Read(ref _uiState)}");

                        SafeRaise(
                            EmergencyTriggered);
                    }
                }
                else
                {
                    _emergencyLatched = false;
                }

                var uiAgeMs =
                    AgeMilliseconds(
                        Interlocked.Read(
                            ref _uiHeartbeatTicks),
                        now);

                if (uiAgeMs > 1800 &&
                    (now - lastUiStallLog)
                        .TotalMilliseconds > 1000)
                {
                    lastUiStallLog = now;

                    Log(
                        $"UI_STALL_NATIVE uiAgeMs={uiAgeMs:0} state={Volatile.Read(ref _uiState)} overlayVisible={Volatile.Read(ref _overlayVisible)}");
                }

                if ((now - lastSummary)
                        .TotalSeconds >= 2)
                {
                    lastSummary = now;

                    _ = GetCursorPos(
                        out var cursor);

                    Log(
                        $"NATIVE_HEALTH uiAgeMs={uiAgeMs:0} state={Volatile.Read(ref _uiState)} overlayVisible={Volatile.Read(ref _overlayVisible)} cursor={cursor.X},{cursor.Y} caps={caps} space={space} ctrl={ctrl} alt={alt}");
                }
            }
            catch (Exception ex)
            {
                Log(
                    $"NATIVE_INPUT_ERROR {ex.GetType().Name}: {ex.Message}");
            }

            Thread.Sleep(20);
        }
    }

    private void SafeRaise(
        EventHandler? handler)
    {
        try
        {
            handler?.Invoke(
                this,
                EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log(
                $"NATIVE_CALLBACK_ERROR {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void Log(string message)
    {
        try
        {
            lock (_sync)
            {
                File.AppendAllText(
                    _logPath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    private static double AgeMilliseconds(
        long ticks,
        DateTimeOffset now)
    {
        if (ticks <= 0)
            return double.PositiveInfinity;

        var value =
            new DateTimeOffset(
                ticks,
                TimeSpan.Zero);

        return (now - value)
            .TotalMilliseconds;
    }

    private static bool IsDown(int key)
    {
        return (
            GetAsyncKeyState(key) &
            0x8000) != 0;
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
            thread.IsAlive)
        {
            thread.Join(500);
        }

        Log("NATIVE_INPUT_STOP");

        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(
        int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(
        out Point point);
}