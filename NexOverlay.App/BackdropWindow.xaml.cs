using System;
using System.Windows;
using System.Windows.Media.Animation;
using NexOverlay.Windows;

namespace NexOverlay.App;

public partial class BackdropWindow : Window
{
    private readonly DesktopCaptureService _captureService = new();

    public BackdropWindow()
    {
        InitializeComponent();
    }

    public void Prepare(MonitorBounds monitor)
    {
        BeginAnimation(
            OpacityProperty,
            null);

        Opacity = 0;

        WindowStartupLocation =
            WindowStartupLocation.Manual;

        Left = monitor.X;
        Top = monitor.Y;
        Width = monitor.Width;
        Height = monitor.Height;

        DesktopImage.Source =
            _captureService.Capture(monitor);
    }

    public void AnimateIn()
    {
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });
    }

    public void AnimateOut(Action completed)
    {
        var animation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseIn
            }
        };

        animation.Completed += (_, _) =>
            completed();

        BeginAnimation(
            OpacityProperty,
            animation);
    }

    public void ResetImmediately()
    {
        BeginAnimation(
            OpacityProperty,
            null);

        Opacity = 0;
        Hide();
    }
}