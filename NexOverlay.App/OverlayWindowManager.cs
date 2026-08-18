using System;
using System.Windows.Threading;
using NexOverlay.Windows;

namespace NexOverlay.App;

public sealed class OverlayWindowManager
{
    private readonly BackdropWindow _backdropWindow = new();
    private readonly RecentWindow _recentWindow = new();
    private readonly CenterWindow _centerWindow = new();
    private readonly WorkspaceWindow _workspaceWindow = new();

    private bool _open;
    private bool _closing;

    private int _lifecycleGeneration;

    public bool IsOpen => _open;
    public bool IsClosing => _closing;

    public void Open(MonitorBounds monitor)
    {
        if (_open || _closing)
            return;

        _lifecycleGeneration++;
        var generation = _lifecycleGeneration;

        _centerWindow.ResetToHome();

        PositionWindows(monitor);

        // Capture MUST happen before the backdrop window is shown.
        _backdropWindow.Prepare(monitor);

        _backdropWindow.Show();
        _recentWindow.Show();
        _centerWindow.Show();
        _workspaceWindow.Show();

        _backdropWindow.AnimateIn();

        _centerWindow.AnimateIn(40);
        _recentWindow.AnimateIn(85);
        _workspaceWindow.AnimateIn(110);

        _open = true;

        // Ignore stale callbacks from an older lifecycle.
        _ = generation;
    }

    public void Close(Action completed)
    {
        if (_closing)
            return;

        if (!_open)
        {
            completed();
            return;
        }

        _closing = true;

        _lifecycleGeneration++;
        var generation = _lifecycleGeneration;

        var remaining = 4;
        var finalized = false;

        var fallback = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        void FinalizeClose()
        {
            if (finalized)
                return;

            if (generation != _lifecycleGeneration)
                return;

            finalized = true;

            fallback.Stop();

            ForceHideWindows();

            _open = false;
            _closing = false;

            completed();
        }

        void OneCompleted()
        {
            if (generation != _lifecycleGeneration)
                return;

            remaining--;

            if (remaining <= 0)
                FinalizeClose();
        }

        fallback.Tick += (_, _) =>
        {
            FinalizeClose();
        };

        fallback.Start();

        _recentWindow.AnimateOut(OneCompleted);
        _centerWindow.AnimateOut(OneCompleted);
        _workspaceWindow.AnimateOut(OneCompleted);
        _backdropWindow.AnimateOut(OneCompleted);
    }

    public void ForceClose()
    {
        _lifecycleGeneration++;

        _open = false;
        _closing = false;

        ForceHideWindows();
    }

    private void ForceHideWindows()
    {
        _recentWindow.BeginAnimation(
            System.Windows.UIElement.OpacityProperty,
            null);

        _centerWindow.BeginAnimation(
            System.Windows.UIElement.OpacityProperty,
            null);

        _workspaceWindow.BeginAnimation(
            System.Windows.UIElement.OpacityProperty,
            null);

        _recentWindow.Opacity = 0;
        _centerWindow.Opacity = 0;
        _workspaceWindow.Opacity = 0;

        _recentWindow.Hide();
        _centerWindow.Hide();
        _workspaceWindow.Hide();

        _backdropWindow.ResetImmediately();
    }

    private void PositionWindows(MonitorBounds monitor)
    {
        var centerX =
            monitor.X + monitor.Width / 2.0;

        var centerY =
            monitor.Y + monitor.Height / 2.0;

        _centerWindow.Left =
            centerX - _centerWindow.Width / 2.0;

        _centerWindow.Top =
            centerY - _centerWindow.Height / 2.0;

        var sideGap = Math.Clamp(
            monitor.Width * 0.028,
            34,
            72);

        _recentWindow.Left =
            _centerWindow.Left -
            sideGap -
            _recentWindow.Width;

        _recentWindow.Top =
            centerY - _recentWindow.Height / 2.0;

        _workspaceWindow.Left =
            _centerWindow.Left +
            _centerWindow.Width +
            sideGap;

        _workspaceWindow.Top =
            centerY - _workspaceWindow.Height / 2.0;
    }
}