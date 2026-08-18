using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NexOverlay.App;

public partial class CenterWindow : Window
{
    private const double HomeHeight = 300;
    private const double ModuleHeight = 650;

    private SnippetsView? _snippetsView;

    public CenterWindow()
    {
        InitializeComponent();
    }

    public void ResetAnimationState()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;

        RootScale.ScaleX = 0.96;
        RootScale.ScaleY = 0.96;
        RootTranslate.Y = 10;
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
                From = 10,
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

    public void ResetToHome()
    {
        ModuleHost.Children.Clear();
        ModuleHost.Visibility = Visibility.Collapsed;
        ModuleHost.Opacity = 0;

        HomePanel.Visibility = Visibility.Visible;
        HomePanel.Opacity = 1;

        SetCenteredHeight(HomeHeight);
    }

    private void ModuleCard_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Border card)
            return;

        AnimateCard(card, 1.035, -3, 150);

        card.Background = new SolidColorBrush(
            Color.FromArgb(235, 18, 23, 31));

        card.BorderBrush = GetAccent(
            card.Tag?.ToString(),
            115);
    }

    private void ModuleCard_OnMouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Border card)
            return;

        AnimateCard(card, 1, 0, 170);

        card.Background = new SolidColorBrush(
            Color.FromArgb(220, 16, 20, 27));

        card.BorderBrush = new SolidColorBrush(
            Color.FromArgb(44, 255, 255, 255));
    }

    private void ModuleCard_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Border card)
            AnimateCard(card, 0.975, 0, 70);
    }

    private void ModuleCard_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border card)
            return;

        AnimateCard(card, 1.035, -3, 95);

        if (card.Tag?.ToString() == "Snippets")
            OpenSnippets();
    }

    private static void AnimateCard(
        Border card,
        double scaleTo,
        double yTo,
        int durationMs)
    {
        if (card.RenderTransform is not TransformGroup group)
            return;

        if (group.Children[0] is not ScaleTransform scale)
            return;

        if (group.Children[1] is not TranslateTransform translate)
            return;

        var easing = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                To = scaleTo,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing
            });

        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                To = scaleTo,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing
            });

        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                To = yTo,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing
            });
    }

    private static Brush GetAccent(
        string? module,
        byte alpha)
    {
        var color = module switch
        {
            "Notes" =>
                Color.FromArgb(alpha, 169, 207, 255),

            "Snippets" =>
                Color.FromArgb(alpha, 189, 179, 255),

            "Files" =>
                Color.FromArgb(alpha, 166, 240, 229),

            "Clips" =>
                Color.FromArgb(alpha, 255, 184, 216),

            _ =>
                Color.FromArgb(alpha, 255, 255, 255)
        };

        return new SolidColorBrush(color);
    }

    private void OpenSnippets()
    {
        if (_snippetsView is null)
        {
            _snippetsView = new SnippetsView();
            _snippetsView.BackRequested += (_, _) => ReturnHome();
        }

        var homeFade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(110)
        };

        homeFade.Completed += (_, _) =>
        {
            HomePanel.Visibility = Visibility.Collapsed;

            ModuleHost.Children.Clear();
            ModuleHost.Children.Add(_snippetsView);

            ModuleHost.Visibility = Visibility.Visible;
            ModuleHost.Opacity = 0;

            SetCenteredHeight(ModuleHeight);

            ModuleHost.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(175),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                });
        };

        HomePanel.BeginAnimation(
            OpacityProperty,
            homeFade);
    }

    private void ReturnHome()
    {
        var moduleFade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(110)
        };

        moduleFade.Completed += (_, _) =>
        {
            ModuleHost.Children.Clear();
            ModuleHost.Visibility = Visibility.Collapsed;

            SetCenteredHeight(HomeHeight);

            HomePanel.Visibility = Visibility.Visible;
            HomePanel.Opacity = 0;

            HomePanel.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(175),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                });
        };

        ModuleHost.BeginAnimation(
            OpacityProperty,
            moduleFade);
    }

    private void SetCenteredHeight(double newHeight)
    {
        var centerY = Top + Height / 2.0;

        Height = newHeight;
        Top = centerY - newHeight / 2.0;
    }
}