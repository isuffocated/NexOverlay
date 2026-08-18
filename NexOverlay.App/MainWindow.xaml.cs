using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NexOverlay.Windows;

namespace NexOverlay.App;

public partial class MainWindow : Window
{
    private enum OverlayState
    {
        Idle,
        OpenArmed,
        Open,
        CloseArmed,
        Closing
    }

    private readonly MonitorService _monitorService =
        new();

    private readonly OverlayWindow _overlayWindow =
        new();

    private readonly NativeInputWatchdog _nativeInputWatchdog =
        new();
private HandleWindow? _handleWindow;
    private DispatcherTimer? _handleWatchdog;

    private OverlayState _state =
        OverlayState.Idle;

    private MonitorBounds _armedMonitor;
    private HandleMode _armedMode;

    private int _handleGeneration;

    private DispatcherTimer? _uiHeartbeatTimer;
    private Timer? _inputWatchdogTimer;

    private DateTimeOffset _lastUiHeartbeatUtc =
        DateTimeOffset.MinValue;

    private DateTimeOffset _lastSummaryLogUtc =
        DateTimeOffset.MinValue;

    private int _primaryHotkeyRecoveryInProgress;

    public MainWindow()
    {
        InitializeComponent();

        InitializeStablePrimaryInput();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {



        StartInputDiagnostics();

        InputDiagnostics.Log(
            "APP_LOADED nativeInput=true");

        Hide();
    }

    private void RecreatePrimaryHotkeyService()
    {
        // Legacy no-op. NativeInputWatchdog is the only runtime input source.
    }

    private void OnNativePrimaryTriggered(
        object? sender,
        EventArgs e)
    {
        Dispatcher.BeginInvoke(
            new Action(HandlePrimaryHotkey));
    }

    private void OnNativeEmergencyTriggered(
        object? sender,
        EventArgs e)
    {
        Dispatcher.BeginInvoke(
            new Action(HandleEmergencyHotkey));
    }

    private void OnEmergencyHotkeyTriggered(
        object? sender,
        EventArgs e)
    {
        InputDiagnostics.Log(
            $"EMERGENCY_TRIGGER thread={Environment.CurrentManagedThreadId} state={_state}");

        Dispatcher.BeginInvoke(
            new Action(HandleEmergencyHotkey));
    }

    private void HandleEmergencyHotkey()
    {
        switch (_state)
        {
            case OverlayState.Idle:
            case OverlayState.OpenArmed:
                StopHandleWatchdog();
                _handleWindow?.Hide();
                OpenOverlayDirectly();
                break;

            case OverlayState.Open:
            case OverlayState.CloseArmed:
            case OverlayState.Closing:
                StopHandleWatchdog();
                _handleWindow?.Hide();
                ForceCloseToIdle();
                break;
        }
    }

    private void OnHotkeyTriggered(
        object? sender,
        EventArgs e)
    {

        InputDiagnostics.Log(
            $"PRIMARY_TRIGGER thread={Environment.CurrentManagedThreadId} state={_state}");

        _nativeInputWatchdog.Log(
            $"PRIMARY_TRIGGER_NATIVE state={(int)_state}");

        Dispatcher.BeginInvoke(
            new Action(HandlePrimaryHotkey));
    }

    private void HandlePrimaryHotkey()
    {
        switch (_state)
        {
            case OverlayState.Idle:
                ArmOpen();
                break;

            case OverlayState.OpenArmed:
                ExecuteArmedActionDirectly();
                break;

            case OverlayState.Open:
                ArmClose();
                break;

            case OverlayState.CloseArmed:
                ExecuteArmedActionDirectly();
                break;

            case OverlayState.Closing:
                ForceCloseToIdle();
                break;
        }
    }

    private void ArmOpen()
    {
        if (_overlayWindow.IsOpen)
        {
            _state = OverlayState.Open;
            ArmClose();
            return;
        }

        var monitor =
            _monitorService.GetMonitorFromCursor();

        _state =
            OverlayState.OpenArmed;

        _nativeInputWatchdog.Log(
            "STATE OpenArmed");

        ShowHandleWithWatchdog(
            monitor,
            HandleMode.Open);
    }

    private void ArmClose()
    {
        if (!_overlayWindow.IsOpen)
        {
            ForceCloseToIdle();
            return;
        }

        var monitor =
            _monitorService.GetMonitorFromCursor();

        _state =
            OverlayState.CloseArmed;

        ShowHandleWithWatchdog(
            monitor,
            HandleMode.Close);
    }

    private void ShowHandleWithWatchdog(
        MonitorBounds monitor,
        HandleMode mode)
    {
        StopHandleWatchdog();

        _armedMonitor = monitor;
        _armedMode = mode;

        _handleGeneration++;
        var generation = _handleGeneration;

        var handle =
            EnsureHandle();

        handle.ShowForMonitor(
            monitor,
            mode);

        InputDiagnostics.Log(
            $"HANDLE_ARM mode={mode} monitor={monitor.X},{monitor.Y} {monitor.Width}x{monitor.Height}");

        var unhealthyPasses = 0;
        var cursorWasInside = false;

        _handleWatchdog =
            new DispatcherTimer(
                DispatcherPriority.Input)
            {
                Interval =
                    TimeSpan.FromMilliseconds(40)
            };

        _handleWatchdog.Tick += (_, _) =>
        {
            if (generation != _handleGeneration)
            {
                StopHandleWatchdog();
                return;
            }

            if (_state is not OverlayState.OpenArmed &&
                _state is not OverlayState.CloseArmed)
            {
                StopHandleWatchdog();
                return;
            }

            var current =
                _handleWindow;

            if (current is null ||
                !current.IsHealthy(monitor))
            {
                unhealthyPasses++;

                if (unhealthyPasses >= 2)
                {
                    InputDiagnostics.Log(
                        $"HANDLE_RECREATE mode={mode}");

                    RecreateHandle();

                    _handleWindow!.ShowForMonitor(
                        monitor,
                        mode);

                    unhealthyPasses = 0;
                    cursorWasInside = false;
                }

                return;
            }

            unhealthyPasses = 0;

            // Keep reinforcing z-order for the ENTIRE armed lifetime,
            // not just for the first few hundred milliseconds.
            current.ForceTopmost();

            var cursorInside =
                current.IsCursorPhysicallyInside();

            if (cursorInside &&
                !cursorWasInside)
            {
                InputDiagnostics.Log(
                    $"HANDLE_NATIVE_ENTER mode={mode}");

                StopHandleWatchdog();

                switch (_state)
                {
                    case OverlayState.OpenArmed:
                        OpenOverlay();
                        break;

                    case OverlayState.CloseArmed:
                        CloseOverlay();
                        break;
                }

                return;
            }

            cursorWasInside =
                cursorInside;
        };

        _handleWatchdog.Start();
    }

    private HandleWindow EnsureHandle()
    {
        if (_handleWindow is not null)
            return _handleWindow;

        _handleWindow =
            CreateHandleWindow();

        return _handleWindow;
    }

    private HandleWindow CreateHandleWindow()
    {
        var handle =
            new HandleWindow();

        handle.HoverTriggered +=
            OnHandleHover;

        return handle;
    }

    private void RecreateHandle()
    {
        var old =
            _handleWindow;

        _handleWindow = null;

        if (old is not null)
        {
            try
            {
                old.HoverTriggered -=
                    OnHandleHover;

                old.Hide();
                old.Close();
            }
            catch
            {
            }
        }

        _handleWindow =
            CreateHandleWindow();
    }

    private void StopHandleWatchdog()
    {
        if (_handleWatchdog is null)
            return;

        _handleWatchdog.Stop();
        _handleWatchdog = null;
    }

    private void OnHandleHover(
        object? sender,
        EventArgs e)
    {
        StopHandleWatchdog();

        switch (_state)
        {
            case OverlayState.OpenArmed:
                OpenOverlay();
                break;

            case OverlayState.CloseArmed:
                CloseOverlay();
                break;
        }
    }

    private void ExecuteArmedActionDirectly()
    {
        StopHandleWatchdog();
        _handleWindow?.Hide();

        switch (_state)
        {
            case OverlayState.OpenArmed:
                OpenOverlay();
                break;

            case OverlayState.CloseArmed:
                CloseOverlay();
                break;
        }
    }

    private void OpenOverlay()
    {
        StopHandleWatchdog();
        _handleWindow?.Hide();

        var monitor =
            _armedMode == HandleMode.Open
                ? _armedMonitor
                : _monitorService.GetMonitorFromCursor();

        _overlayWindow.OpenOnMonitor(
            monitor);

        _state =
            OverlayState.Open;

        _nativeInputWatchdog.Log(
            "STATE Open");
    }

    private void OpenOverlayDirectly()
    {
        if (_overlayWindow.IsOpen)
        {
            _state = OverlayState.Open;
            return;
        }

        var monitor =
            _monitorService.GetMonitorFromCursor();

        _overlayWindow.OpenOnMonitor(
            monitor);

        _state =
            OverlayState.Open;

        _nativeInputWatchdog.Log(
            "STATE Open");
    }

    private void CloseOverlay()
    {
        StopHandleWatchdog();
        _handleWindow?.Hide();

        _state =
            OverlayState.Closing;

        _nativeInputWatchdog.Log(
            "STATE Closing");

        _overlayWindow.CloseOverlay(() =>
        {
            _state =
                OverlayState.Idle;
        });
    }

    private void ForceCloseToIdle()
    {
        StopHandleWatchdog();

        _handleGeneration++;

        _handleWindow?.Hide();

        _overlayWindow.ForceHide();

        _state =
            OverlayState.Idle;
    }

    private void StartInputDiagnostics()
    {
        _lastUiHeartbeatUtc =
            DateTimeOffset.UtcNow;

        _uiHeartbeatTimer =
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    TimeSpan.FromMilliseconds(250)
            };

        _uiHeartbeatTimer.Tick += (_, _) =>
        {
            _lastUiHeartbeatUtc =
                DateTimeOffset.UtcNow;

            _nativeInputWatchdog.PulseUi(
                (int)_state,
                _overlayWindow.IsVisible);

            if (_overlayWindow.IsVisible)
            {
                _overlayWindow.RepairInputSurface();
            }
        };

        _uiHeartbeatTimer.Start();

        _inputWatchdogTimer =
            new Timer(
                _ => InputWatchdogTick(),
                null,
                TimeSpan.FromMilliseconds(750),
                TimeSpan.FromMilliseconds(750));
    }

    private void InputWatchdogTick()
    {
        try
        {
            var now =
                DateTimeOffset.UtcNow;

            var uiAgeMs =
                _lastUiHeartbeatUtc ==
                DateTimeOffset.MinValue
                    ? double.PositiveInfinity
                    : (now - _lastUiHeartbeatUtc)
                        .TotalMilliseconds;

            if (uiAgeMs > 2500)
            {
                InputDiagnostics.Log(
                    $"UI_STALL detected uiAgeMs={uiAgeMs:0}");
            }

            if ((now - _lastSummaryLogUtc)
                    .TotalSeconds < 2)
            {
                return;
            }

            _lastSummaryLogUtc = now;

            // NEVER touch WPF-owned objects from this Timer thread.
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    try
                    {
                        var uiNow =
                            DateTimeOffset.UtcNow;

                        var mouseAgeMs =
                            _overlayWindow.LastMouseInputUtc ==
                            DateTimeOffset.MinValue
                                ? -1
                                : (uiNow -
                                   _overlayWindow.LastMouseInputUtc)
                                  .TotalMilliseconds;

                        InputDiagnostics.Log(
                            $"HEALTH state={_state} uiAgeMs={uiAgeMs:0} overlayVisible={_overlayWindow.IsVisible} overlayOpen={_overlayWindow.IsOpen} overlayEnabled={_overlayWindow.IsEnabled} overlayHitTest={_overlayWindow.IsHitTestVisible} mouseAgeMs={mouseAgeMs:0}");

                        if (_overlayWindow.IsVisible)
                        {
                            _overlayWindow.RepairInputSurface();
                        }
                    }
                    catch (Exception ex)
                    {
                        InputDiagnostics.Log(
                            $"UI_DIAGNOSTIC_ERROR {ex.GetType().Name}: {ex.Message}");
                    }
                }));
        }
        catch (Exception ex)
        {
            InputDiagnostics.Log(
                $"WATCHDOG_ERROR {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void TryRecoverPrimaryHotkey(
        bool wasAlive,
        double ageMs)
    {
        // Legacy no-op.
        // NativeInputWatchdog is now the sole runtime keyboard source.
    }

    private void StopInputDiagnostics()
    {
        _uiHeartbeatTimer?.Stop();
        _uiHeartbeatTimer = null;

        _inputWatchdogTimer?.Dispose();
        _inputWatchdogTimer = null;

        InputDiagnostics.Log(
            "APP_CLOSING");
    }

    private void OnClosed(
        object? sender,
        EventArgs e)
    {
        DisposeStablePrimaryInput();
        StopInputDiagnostics();
        StopHandleWatchdog();



        _nativeInputWatchdog.Dispose();

        _overlayWindow.ForceHide();

        if (_handleWindow is not null)
        {
            try
            {
                _handleWindow.Close();
            }
            catch
            {
            }
        }
}
}
