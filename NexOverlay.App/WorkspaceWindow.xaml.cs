using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NexOverlay.App;

public partial class WorkspaceWindow : Window
{
    public WorkspaceWindow()
    {
        InitializeComponent();
    }

    public void ResetAnimationState()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;

        RootScale.ScaleX = 0.96;
        RootScale.ScaleY = 0.96;
        RootTranslate.Y = 12;
    }

    public void AnimateIn(int delayMs)
    {
        ResetAnimationState();

        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });

        RootScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(230),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });

        RootScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(230),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });

        RootTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                From = 12,
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(230),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });
    }

    public void AnimateOut(Action completed)
    {
        var fade = new DoubleAnimation
        {
            From = Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(130)
        };

        fade.Completed += (_, _) => completed();

        BeginAnimation(
            OpacityProperty,
            fade);
    }
}
