using System;
using NexOverlay.Windows;

namespace NexOverlay.App;

public partial class MainWindow
{
    private GlobalHotkeyService? _stablePrimaryInput;

    private void InitializeStablePrimaryInput()
    {
        if (_stablePrimaryInput is not null)
            return;

        _stablePrimaryInput =
            new GlobalHotkeyService();

        _stablePrimaryInput.Triggered +=
            StablePrimaryInput_OnTriggered;

        _stablePrimaryInput.Start();
    }

    private void StablePrimaryInput_OnTriggered(
        object? sender,
        EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted ||
            Dispatcher.HasShutdownFinished)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(HandlePrimaryHotkey));
    }

    private void DisposeStablePrimaryInput()
    {
        var service =
            _stablePrimaryInput;

        if (service is null)
            return;

        _stablePrimaryInput = null;

        service.Triggered -=
            StablePrimaryInput_OnTriggered;

        service.Dispose();
    }
}