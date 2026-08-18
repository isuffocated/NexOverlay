using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using NexOverlay.Windows;
using NexOverlay.Storage.Paths;
using NexOverlay.Storage.Snippets;
using NexOverlay.Storage.Notes;
using NexOverlay.Storage.Files;
using NexOverlay.Storage.Clipboard;

namespace NexOverlay.App;

public partial class OverlayWindow : Window
{
    private readonly DesktopCaptureService _captureService = new();
    private readonly SnippetRepository _snippetRepository;
    private readonly NoteRepository _noteRepository;
    private readonly WorkspaceFileRepository _fileRepository;

    private SnippetsView? _snippetsView;
    private NotesView? _notesView;
    private FilesView? _filesView;
    private MonitorBounds _monitor;

    private bool _isOpen;
    private bool _isAnimating;
    private bool _moduleLayoutActive;
    private LayoutZone _layoutZone =
        LayoutZone.None;
    private readonly ParticleNetworkBackground _particleBackground =
        new();

    private readonly DropShadowEffect _recentFocusGlow =
        CreateFocusGlow(
            Color.FromRgb(
                169,
                207,
                255));

    private readonly DropShadowEffect _centerFocusGlow =
        CreateFocusGlow(
            Color.FromRgb(
                189,
                179,
                255));

    private readonly DropShadowEffect _workspaceFocusGlow =
        CreateFocusGlow(
            Color.FromRgb(
                166,
                240,
                229));
    private Border? _recentGlowLayer;
    private Border? _workspaceGlowLayer;

    private Border? _notesGlowLayer;
    private Border? _snippetsGlowLayer;
    private Border? _filesGlowLayer;
    private Border? _clipsGlowLayer;
    private string? _activeModuleName;
    private int _globalSearchRevision;

    private Border? _activeModuleGlowLayer;

    private readonly ClipboardRepository _clipRepository;
    private readonly ClipboardMonitorService _clipboardMonitor;

    private ClipsView? _clipsView;
    private readonly OnboardingStateService _onboardingState =
        new();

    private WelcomeWizardView? _welcomeWizard;
    private Border? _tutorialSpotlight;
    private TranslateTransform? _tutorialCardTranslate;
    private TranslateTransform? _tutorialSpotlightTranslate;
    private readonly BackgroundThemeService _backgroundThemeService =
        new();

    private AuroraMeshBackground? _auroraBackground;

    private BackgroundTheme _backgroundTheme =
        BackgroundTheme.Aurora;
    private StackPanel? _utilityButtonDock;
    public bool IsOpen => _isOpen;
    public bool IsAnimating => _isAnimating;

    public DateTimeOffset LastMouseInputUtc { get; private set; } =
        DateTimeOffset.MinValue;

    public OverlayWindow()
    {
        InitializeComponent();
#if DEBUG
        DebugTutorialButton.Visibility =
            Visibility.Visible;
#endif
        var clipboardPaths =
            new AppDataPathService();

        _clipRepository =
            new ClipboardRepository(
                clipboardPaths);

        _clipRepository.InitializeAsync()
            .GetAwaiter()
            .GetResult();

        _clipboardMonitor =
            new ClipboardMonitorService();

        _clipboardMonitor.TextCaptured +=
            ClipboardMonitor_OnTextCaptured;

        InitializeIndependentGlowLayers();

        InitializeGlobalSearchAndModuleGlow();
        _particleBackground.Opacity =
            0.92;

        OverlayRoot.Children.Insert(
            Math.Min(3, OverlayRoot.Children.Count),
            _particleBackground);
PreviewMouseMove += (_, _) =>
        {
            LastMouseInputUtc =
                DateTimeOffset.UtcNow;
        };

        PreviewMouseDown += (_, _) =>
        {
            LastMouseInputUtc =
                DateTimeOffset.UtcNow;

            InputDiagnostics.Log(
                $"MOUSE_DOWN visible={IsVisible} enabled={IsEnabled} hitTest={IsHitTestVisible}");
        };

        var paths =
            new AppDataPathService();

        _snippetRepository =
            new SnippetRepository(
                paths);

        _noteRepository =
            new NoteRepository(
                paths);
        _fileRepository =
            new WorkspaceFileRepository(
                paths);
    
        InitializeBackgroundTheme();}

    public void OpenOnMonitor(MonitorBounds monitor)
    {
        if (_isOpen || _isAnimating)
            return;

        _isAnimating = true;
        _monitor = monitor;

        ResetToHomeImmediately();
        ResetAnimations();


        ResetAdaptiveLayoutImmediately();
        _ = RefreshSnippetSummaryAsync();
        _ = RefreshNoteSummaryAsync();
        _ = RefreshFileSummaryAsync();

        DesktopImage.Source =
            _captureService.Capture(monitor);

        WindowStartupLocation =
            WindowStartupLocation.Manual;

        Left = monitor.X;
        Top = monitor.Y;
        Width = monitor.Width;
        Height = monitor.Height;

        Show();

        _particleBackground.Start();

        AnimateOpen();
    }

    public void CloseOverlay(Action completed)
    {
        if (!_isOpen || _isAnimating)
        {
            ForceHide();
            completed();
            return;
        }

        _isAnimating = true;

        var ease = new CubicEase
        {
            EasingMode = EasingMode.EaseIn
        };

        RecentPanel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(100)
            });

        WorkspacePanel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(100)
            });

        CenterPanel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(120)
            });

        CenterScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                To = 0.97,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = ease
            });

        CenterScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                To = 0.97,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = ease
            });

        var windowFade = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromMilliseconds(60),
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = ease
        };

        windowFade.Completed += (_, _) =>
        {
            ForceHide();
            completed();
        };

        BeginAnimation(
            OpacityProperty,
            windowFade);
    }

    public void RepairInputSurface()
    {
        if (!IsVisible)
            return;

        IsEnabled = true;
        IsHitTestVisible = true;
        Focusable = true;
        Topmost = true;

        OverlayRoot.IsEnabled = true;
        OverlayRoot.IsHitTestVisible = true;

        CompositionRoot.IsEnabled = true;
        CompositionRoot.IsHitTestVisible = true;

        if (HomePanel.Visibility == Visibility.Visible)
        {
            HomePanel.IsEnabled = true;
            HomePanel.IsHitTestVisible = true;
        }

        if (ModuleHost.Visibility == Visibility.Visible)
        {
            ModuleHost.IsEnabled = true;
            ModuleHost.IsHitTestVisible = true;
        }
    }

    public void ForceHide()
    {
        ResetAnimations();

        _isOpen = false;
        _isAnimating = false;

        Opacity = 0;

        _particleBackground.Stop();

        Hide();
    }

    private void AnimateOpen()
    {
        var ease = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        var windowFade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = ease
        };

        BeginAnimation(
            OpacityProperty,
            windowFade);

        AnimatePanel(
            CenterPanel,
            CenterScale,
            CenterTranslate,
            12,
            0,
            35);

        AnimateSidePanel(
            RecentPanel,
            RecentScale,
            RecentTranslate,
            -18,
            0,
            80);

        AnimateSidePanel(
            WorkspacePanel,
            WorkspaceScale,
            WorkspaceTranslate,
            18,
            0,
            105);

        var completion = new DoubleAnimation
        {
            From = 0,
            To = 1,
            BeginTime = TimeSpan.FromMilliseconds(260),
            Duration = TimeSpan.FromMilliseconds(1)
        };

        completion.Completed += (_, _) =>
        {
            _isOpen = true;
            _isAnimating = false;
        };

        CompositionRoot.BeginAnimation(
            OpacityProperty,
            completion);
    
        MaybeShowWelcomeWizard();}

    private static void AnimatePanel(
        UIElement panel,
        ScaleTransform scale,
        TranslateTransform translate,
        double fromY,
        double toY,
        int delayMs)
    {
        var ease = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        panel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = ease
            });

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            });

        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            });

        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                From = fromY == 0 ? 12 : fromY,
                To = toY,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            });
    }

    private static void AnimateSidePanel(
        UIElement panel,
        ScaleTransform scale,
        TranslateTransform translate,
        double fromX,
        double toX,
        int delayMs)
    {
        var ease = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        panel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = ease
            });

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            });

        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            });

        translate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation
            {
                From = fromX,
                To = toX,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            });
    }

    private void ResetAnimations()
    {
        BeginAnimation(OpacityProperty, null);

        RecentPanel.BeginAnimation(
            OpacityProperty,
            null);

        CenterPanel.BeginAnimation(
            OpacityProperty,
            null);

        WorkspacePanel.BeginAnimation(
            OpacityProperty,
            null);

        CompositionRoot.BeginAnimation(
            OpacityProperty,
            null);

        RecentScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            null);

        RecentScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            null);

        WorkspaceScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            null);

        WorkspaceScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            null);

        CenterScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            null);

        CenterScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            null);

        RecentTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            null);

        WorkspaceTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            null);

        CenterTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            null);

        Opacity = 0;

        RecentPanel.Opacity = 0;
        CenterPanel.Opacity = 0;
        WorkspacePanel.Opacity = 0;

        CompositionRoot.Opacity = 1;

        RecentScale.ScaleX = 0.96;
        RecentScale.ScaleY = 0.96;

        WorkspaceScale.ScaleX = 0.96;
        WorkspaceScale.ScaleY = 0.96;

        CenterScale.ScaleX = 0.96;
        CenterScale.ScaleY = 0.96;

        RecentTranslate.X = -18;
        WorkspaceTranslate.X = 18;
        CenterTranslate.Y = 12;
    }
    private enum LayoutZone
    {
        None,
        Left,
        Center,
        Right
    }

    private void RecentPanel_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        SetLayoutZone(LayoutZone.Left);
    }

    private void CenterPanel_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        SetLayoutZone(LayoutZone.Center);
    }

    private void WorkspacePanel_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        SetLayoutZone(LayoutZone.Right);
    }

    private void LayoutZone_OnMouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (RecentPanel.IsMouseOver)
        {
            SetLayoutZone(LayoutZone.Left);
            return;
        }

        if (CenterPanel.IsMouseOver)
        {
            SetLayoutZone(LayoutZone.Center);
            return;
        }

        if (WorkspacePanel.IsMouseOver)
        {
            SetLayoutZone(LayoutZone.Right);
            return;
        }

        SetLayoutZone(LayoutZone.None);
    }

    private void SetModuleLayoutActive(
        bool active)
    {
        _moduleLayoutActive = active;
        _layoutZone = LayoutZone.None;

        ApplyAdaptiveLayout();
        ApplyIndependentFocusGlow(
            LayoutZone.None);

        ApplyActiveModuleGlow(
            active);
    }

    private void SetLayoutZone(
        LayoutZone zone)
    {
        if (_layoutZone == zone)
            return;

        _layoutZone = zone;

        ApplyAdaptiveLayout();
        ApplyIndependentFocusGlow(zone);
    }

    private void ApplyAdaptiveLayout()
    {
        double recentWidth;
        double recentHeight;
        double recentOpacity;

        double workspaceWidth;
        double workspaceHeight;
        double workspaceOpacity;

        if (_moduleLayoutActive)
        {
            recentWidth = 275;
            recentHeight = 335;
            recentOpacity = 0.72;

            workspaceWidth = 275;
            workspaceHeight = 335;
            workspaceOpacity = 0.72;

            switch (_layoutZone)
            {
                case LayoutZone.Left:
                    recentWidth = 368;
                    recentHeight = 465;
                    recentOpacity = 1.0;

                    workspaceWidth = 245;
                    workspaceHeight = 315;
                    workspaceOpacity = 0.55;
                    break;

                case LayoutZone.Center:
                    recentWidth = 245;
                    recentHeight = 315;
                    recentOpacity = 0.58;

                    workspaceWidth = 245;
                    workspaceHeight = 315;
                    workspaceOpacity = 0.58;
                    break;

                case LayoutZone.Right:
                    recentWidth = 245;
                    recentHeight = 315;
                    recentOpacity = 0.55;

                    workspaceWidth = 368;
                    workspaceHeight = 465;
                    workspaceOpacity = 1.0;
                    break;
            }
        }
        else
        {
            recentWidth = 340;
            recentHeight = 390;
            recentOpacity = 1.0;

            workspaceWidth = 340;
            workspaceHeight = 390;
            workspaceOpacity = 1.0;

            switch (_layoutZone)
            {
                case LayoutZone.Left:
                    recentWidth = 370;
                    recentHeight = 455;
                    recentOpacity = 1.0;

                    workspaceWidth = 300;
                    workspaceHeight = 355;
                    workspaceOpacity = 0.76;
                    break;

                case LayoutZone.Center:
                    recentWidth = 300;
                    recentHeight = 355;
                    recentOpacity = 0.80;

                    workspaceWidth = 300;
                    workspaceHeight = 355;
                    workspaceOpacity = 0.80;
                    break;

                case LayoutZone.Right:
                    recentWidth = 300;
                    recentHeight = 355;
                    recentOpacity = 0.76;

                    workspaceWidth = 370;
                    workspaceHeight = 455;
                    workspaceOpacity = 1.0;
                    break;
            }
        }

        AnimateLayoutWidth(
            RecentPanel,
            recentWidth);

        AnimateLayoutHeight(
            RecentPanel,
            recentHeight);

        AnimateLayoutOpacity(
            RecentPanel,
            recentOpacity);

        AnimateLayoutWidth(
            WorkspacePanel,
            workspaceWidth);

        AnimateLayoutHeight(
            WorkspacePanel,
            workspaceHeight);

        AnimateLayoutOpacity(
            WorkspacePanel,
            workspaceOpacity);
    }

    private void ResetAdaptiveLayoutImmediately()
    {
        _moduleLayoutActive = false;
        _layoutZone = LayoutZone.None;

        RecentPanel.BeginAnimation(
            WidthProperty,
            null);

        RecentPanel.BeginAnimation(
            HeightProperty,
            null);

        WorkspacePanel.BeginAnimation(
            WidthProperty,
            null);

        WorkspacePanel.BeginAnimation(
            HeightProperty,
            null);

        RecentPanel.BeginAnimation(
            OpacityProperty,
            null);

        WorkspacePanel.BeginAnimation(
            OpacityProperty,
            null);

        RecentScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            null);

        RecentScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            null);

        CenterScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            null);

        CenterScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            null);

        WorkspaceScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            null);

        WorkspaceScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            null);

        RecentScale.ScaleX = 1.0;
        RecentScale.ScaleY = 1.0;

        CenterScale.ScaleX = 1.0;
        CenterScale.ScaleY = 1.0;

        WorkspaceScale.ScaleX = 1.0;
        WorkspaceScale.ScaleY = 1.0;

        RecentPanel.Width = 340;
        RecentPanel.Height = 390;

        CenterPanel.Width = 860;
        HomePanel.Width = 860;
        ModuleHost.Width = 860;

        WorkspacePanel.Width = 340;
        WorkspacePanel.Height = 390;

        RecentPanel.Opacity = 1.0;
        WorkspacePanel.Opacity = 1.0;
    }

    private static void AnimateFrameworkWidth(
        FrameworkElement element,
        double width)
    {
        element.BeginAnimation(
            WidthProperty,
            new DoubleAnimation
            {
                To = width,
                Duration =
                    TimeSpan.FromMilliseconds(190),
                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    },
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateFrameworkSize(
        FrameworkElement element,
        double width,
        double height)
    {
        AnimateFrameworkWidth(
            element,
            width);

        element.BeginAnimation(
            HeightProperty,
            new DoubleAnimation
            {
                To = height,
                Duration =
                    TimeSpan.FromMilliseconds(190),
                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    },
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateLayoutScale(
        ScaleTransform transform,
        double scale)
    {
        var duration =
            TimeSpan.FromMilliseconds(185);

        var easing =
            new CubicEase
            {
                EasingMode =
                    EasingMode.EaseOut
            };

        transform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                To = scale,
                Duration = duration,
                EasingFunction = easing,
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);

        transform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                To = scale,
                Duration = duration,
                EasingFunction = easing,
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateLayoutWidth(
        FrameworkElement element,
        double width)
    {
        element.BeginAnimation(
            WidthProperty,
            new DoubleAnimation
            {
                To = width,
                Duration =
                    TimeSpan.FromMilliseconds(185),
                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    },
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }
    private static void AnimateLayoutHeight(
        FrameworkElement element,
        double height)
    {
        element.BeginAnimation(
            HeightProperty,
            new DoubleAnimation
            {
                To = height,
                Duration =
                    TimeSpan.FromMilliseconds(185),
                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    },
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateLayoutOpacity(
        UIElement element,
        double opacity)
    {
        element.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                To = opacity,
                Duration =
                    TimeSpan.FromMilliseconds(160),
                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    },
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }
    private static DropShadowEffect CreateFocusGlow(
        Color color)
    {
        return
            new DropShadowEffect
            {
                Color = color,
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0,
                RenderingBias =
                    RenderingBias.Performance
            };
    }

    private void ApplyFocusGlow(
        LayoutZone zone)
    {
        AnimateFocusGlow(
            _recentFocusGlow,
            zone == LayoutZone.Left
                ? 0.68
                : 0.0,
            zone == LayoutZone.Left
                ? 30
                : 18);

        AnimateFocusGlow(
            _centerFocusGlow,
            zone == LayoutZone.Center
                ? 0.58
                : 0.0,
            zone == LayoutZone.Center
                ? 32
                : 18);

        AnimateFocusGlow(
            _workspaceFocusGlow,
            zone == LayoutZone.Right
                ? 0.68
                : 0.0,
            zone == LayoutZone.Right
                ? 30
                : 18);
    }

    private static void AnimateFocusGlow(
        DropShadowEffect effect,
        double opacity,
        double blurRadius)
    {
        var easing =
            new CubicEase
            {
                EasingMode =
                    EasingMode.EaseOut
            };

        effect.BeginAnimation(
            DropShadowEffect.OpacityProperty,
            new DoubleAnimation
            {
                To = opacity,
                Duration =
                    TimeSpan.FromMilliseconds(180),
                EasingFunction = easing,
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);

        effect.BeginAnimation(
            DropShadowEffect.BlurRadiusProperty,
            new DoubleAnimation
            {
                To = blurRadius,
                Duration =
                    TimeSpan.FromMilliseconds(220),
                EasingFunction = easing,
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }
    private void InitializeIndependentGlowLayers()
    {
        _recentGlowLayer =
            CreatePanelGlowLayer(
                Color.FromRgb(
                    169,
                    207,
                    255),
                20,
                26);

        RecentPanel.Children.Insert(
            0,
            _recentGlowLayer);

        _workspaceGlowLayer =
            CreatePanelGlowLayer(
                Color.FromRgb(
                    166,
                    240,
                    229),
                20,
                26);

        WorkspacePanel.Children.Insert(
            0,
            _workspaceGlowLayer);

        if (NotesCard.Parent is Grid moduleGrid)
        {
            _notesGlowLayer =
                CreateModuleGlowLayer(
                    0,
                    Color.FromRgb(
                        169,
                        207,
                        255));

            _snippetsGlowLayer =
                CreateModuleGlowLayer(
                    2,
                    Color.FromRgb(
                        189,
                        179,
                        255));

            _filesGlowLayer =
                CreateModuleGlowLayer(
                    4,
                    Color.FromRgb(
                        166,
                        240,
                        229));

            _clipsGlowLayer =
                CreateModuleGlowLayer(
                    6,
                    Color.FromRgb(
                        255,
                        184,
                        216));

            moduleGrid.Children.Insert(
                0,
                _notesGlowLayer);

            moduleGrid.Children.Insert(
                1,
                _snippetsGlowLayer);

            moduleGrid.Children.Insert(
                2,
                _filesGlowLayer);

            moduleGrid.Children.Insert(
                3,
                _clipsGlowLayer);
        }
    }

    private static Border CreatePanelGlowLayer(
        Color color,
        double margin,
        double cornerRadius)
    {
        return
            new Border
            {
                Margin =
                    new Thickness(margin),
                CornerRadius =
                    new CornerRadius(cornerRadius),
                BorderThickness =
                    new Thickness(1),
                BorderBrush =
                    new SolidColorBrush(color),
                IsHitTestVisible = false,
                Opacity = 0,
                Effect =
                    new DropShadowEffect
                    {
                        Color = color,
                        BlurRadius = 38,
                        ShadowDepth = 0,
                        Opacity = 0.95,
                        RenderingBias =
                            RenderingBias.Performance
                    }
            };
    }

    private static Border CreateModuleGlowLayer(
        int gridColumn,
        Color color)
    {
        var fill =
            new SolidColorBrush(
                Color.FromArgb(
                    115,
                    color.R,
                    color.G,
                    color.B));

        var border =
            new Border
            {
                Margin =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(17),

                Background =
                    fill,

                BorderThickness =
                    new Thickness(0),

                IsHitTestVisible = false,

                Opacity = 0,

                Effect =
                    new DropShadowEffect
                    {
                        Color = color,
                        BlurRadius = 27,
                        ShadowDepth = 0,
                        Opacity = 0.96,
                        RenderingBias =
                            RenderingBias.Performance
                    }
            };

        Grid.SetColumn(
            border,
            gridColumn);

        // Behind the actual module card. The opaque card hides
        // the colored body and only the soft outer halo remains.
        Panel.SetZIndex(
            border,
            -10);

        return border;
    }

    private void ApplyIndependentFocusGlow(
        LayoutZone zone)
    {
        AnimateGlowLayer(
            _recentGlowLayer,
            zone == LayoutZone.Left);

        AnimateGlowLayer(
            _workspaceGlowLayer,
            zone == LayoutZone.Right);
    }

    private void ClearAllModuleCardGlows()
    {
        _activeModuleName =
            null;

        foreach (var layer in new UIElement?[]
        {
            _notesGlowLayer,
            _snippetsGlowLayer,
            _filesGlowLayer,
            _clipsGlowLayer
        })
        {
            if (layer is null)
                continue;

            layer.BeginAnimation(
                OpacityProperty,
                null);

            layer.Opacity =
                0;
        }
    }
    private void AnimateModuleGlow(
        string? module,
        bool active)
    {
        var target =
            module switch
            {
                "Notes" =>
                    _notesGlowLayer,

                "Snippets" =>
                    _snippetsGlowLayer,

                "Files" =>
                    _filesGlowLayer,

                "Clips" =>
                    _clipsGlowLayer,

                _ =>
                    null
            };

        var shouldGlow =
            active ||
            string.Equals(
                module,
                _activeModuleName,
                StringComparison.Ordinal);

        AnimateGlowLayer(
            target,
            shouldGlow);
    }

    private static void AnimateGlowLayer(
        UIElement? element,
        bool active)
    {
        if (element is null)
            return;

        element.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                To =
                    active
                        ? 1.0
                        : 0.0,

                Duration =
                    TimeSpan.FromMilliseconds(
                        active
                            ? 110
                            : 170),

                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    },

                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }
    private sealed record GlobalSearchSuggestion(
        string Title,
        string Meta,
        string Module,
        string Query);

    private void InitializeGlobalSearchAndModuleGlow()
    {
        _activeModuleGlowLayer =
            new Border
            {
                Margin =
                    new Thickness(28),

                HorizontalAlignment =
                    HorizontalAlignment.Stretch,

                VerticalAlignment =
                    VerticalAlignment.Stretch,

                CornerRadius =
                    new CornerRadius(28),

                BorderThickness =
                    new Thickness(0),

                Background =
                    new SolidColorBrush(
                        Color.FromArgb(
                            105,
                            189,
                            179,
                            255)),

                IsHitTestVisible = false,

                Visibility =
                    Visibility.Collapsed,

                Opacity = 0,

                Effect =
                    new DropShadowEffect
                    {
                        Color =
                            Color.FromRgb(
                                189,
                                179,
                                255),

                        BlurRadius = 30,
                        ShadowDepth = 0,
                        Opacity = 0.92,

                        RenderingBias =
                            RenderingBias.Performance
                    }
            };

        Panel.SetZIndex(
            _activeModuleGlowLayer,
            -20);

        Panel.SetZIndex(
            HomePanel,
            2);

        Panel.SetZIndex(
            ModuleHost,
            2);

        CenterPanel.Children.Insert(
            0,
            _activeModuleGlowLayer);
    }

    private void ApplyActiveModuleGlow(
        bool active)
    {
        if (_activeModuleGlowLayer is null)
            return;

        if (!active)
        {
            _activeModuleGlowLayer.BeginAnimation(
                OpacityProperty,
                null);

            _activeModuleGlowLayer.Opacity =
                0;

            _activeModuleGlowLayer.Visibility =
                Visibility.Collapsed;

            return;
        }

        var color =
            GetModuleGlowColor(
                _activeModuleName);

        _activeModuleGlowLayer.Background =
            new SolidColorBrush(
                Color.FromArgb(
                    105,
                    color.R,
                    color.G,
                    color.B));

        _activeModuleGlowLayer.BorderBrush =
            null;

        _activeModuleGlowLayer.BorderThickness =
            new Thickness(0);

        if (_activeModuleGlowLayer.Effect
            is DropShadowEffect effect)
        {
            effect.Color =
                color;

            effect.BlurRadius =
                30;

            effect.Opacity =
                0.92;
        }

        _activeModuleGlowLayer.Visibility =
            Visibility.Visible;

        _activeModuleGlowLayer.Opacity =
            0;

        AnimateGlowLayer(
            _activeModuleGlowLayer,
            true);
    }

    private static Color GetModuleGlowColor(
        string? module)
    {
        return
            module switch
            {
                "Notes" =>
                    Color.FromRgb(
                        169,
                        207,
                        255),

                "Snippets" =>
                    Color.FromRgb(
                        189,
                        179,
                        255),

                "Files" =>
                    Color.FromRgb(
                        166,
                        240,
                        229),

                "Clips" =>
                    Color.FromRgb(
                        255,
                        184,
                        216),

                _ =>
                    Color.FromRgb(
                        189,
                        179,
                        255)
            };
    }

    private void GlobalSearchBox_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        AnimateGlowLayer(
            SearchGlowLayer,
            true);
    }

    private void GlobalSearchBox_OnMouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (!GlobalSearchBox.IsKeyboardFocusWithin)
        {
            AnimateGlowLayer(
                SearchGlowLayer,
                false);
        }
    }

    private void GlobalSearchBox_OnGotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        AnimateGlowLayer(
            SearchGlowLayer,
            true);
    }

    private void GlobalSearchBox_OnLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (!GlobalSearchBox.IsMouseOver)
        {
            AnimateGlowLayer(
                SearchGlowLayer,
                false);
        }
    }

    private async void GlobalSearchBox_OnTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        GlobalSearchPlaceholder.Visibility =
            string.IsNullOrEmpty(
                GlobalSearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

        var query =
            GlobalSearchBox.Text.Trim();

        var revision =
            ++_globalSearchRevision;

        if (query.Length == 0)
        {
            SearchSuggestionsPanel.Visibility =
                Visibility.Collapsed;

            SearchSuggestionsList.ItemsSource =
                null;

            return;
        }

        await Task.Delay(70);

        if (revision !=
            _globalSearchRevision)
        {
            return;
        }

        var results =
            await BuildGlobalSearchSuggestionsAsync(
                query);

        if (revision !=
            _globalSearchRevision)
        {
            return;
        }

        SearchSuggestionsList.ItemsSource =
            results;

        SearchSuggestionsPanel.Visibility =
            results.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private async Task<IReadOnlyList<GlobalSearchSuggestion>>
        BuildGlobalSearchSuggestionsAsync(
            string query)
    {
        var result =
            new List<GlobalSearchSuggestion>(10);

        AddActionSuggestions(
            result,
            query);

        AddModuleSuggestions(
            result,
            query);

        try
        {
            var snippetTask =
                _snippetRepository.GetAllAsync();

            var noteTask =
                _noteRepository.GetAllAsync();

            var fileTask =
                _fileRepository.GetAllAsync();

            var clipTask =
                _clipRepository.GetAllAsync();

            await Task.WhenAll(
                snippetTask,
                noteTask,
                fileTask,
                clipTask);

            foreach (var item in await snippetTask)
            {
                if (!MatchesQuery(
                        query,
                        item.Title,
                        item.Category,
                        item.Content))
                {
                    continue;
                }

                result.Add(
                    new GlobalSearchSuggestion(
                        item.Title,
                        "RESULT / SNIPPET",
                        "Snippets",
                        query));

                if (result.Count >= 9)
                    break;
            }

            if (result.Count < 9)
            {
                foreach (var item in await noteTask)
                {
                    if (!MatchesQuery(
                            query,
                            item.Title,
                            item.Content))
                    {
                        continue;
                    }

                    result.Add(
                        new GlobalSearchSuggestion(
                            item.Title,
                            "RESULT / NOTE",
                            "Notes",
                            query));

                    if (result.Count >= 9)
                        break;
                }
            }

            if (result.Count < 9)
            {
                foreach (var item in await fileTask)
                {
                    if (!MatchesQuery(
                            query,
                            item.Name,
                            item.Path))
                    {
                        continue;
                    }

                    result.Add(
                        new GlobalSearchSuggestion(
                            item.Name,
                            "RESULT / FILE",
                            "Files",
                            query));

                    if (result.Count >= 9)
                        break;
                }
            }

            if (result.Count < 9)
            {
                foreach (var item in await clipTask)
                {
                    if (!MatchesQuery(
                            query,
                            item.Content))
                    {
                        continue;
                    }

                    result.Add(
                        new GlobalSearchSuggestion(
                            item.Preview,
                            "RESULT / CLIP",
                            "Clips",
                            query));

                    if (result.Count >= 9)
                        break;
                }
            }
        }
        catch
        {
            // OPEN/ACTION suggestions stay available even if
            // one storage search fails.
        }

        return
            result
                .GroupBy(
                    item =>
                        $"{item.Module}|{item.Title}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(9)
                .ToList();
    }

    private static void AddActionSuggestions(
        List<GlobalSearchSuggestion> result,
        string query)
    {
        var normalized =
            query.Trim().ToLowerInvariant();

        if (normalized.Length == 0)
            return;

        var actions =
            new[]
            {
                (
                    "NEW NOTE",
                    "Action.NewNote",
                    new[] { "new note", "create note" }
                ),

                (
                    "NEW SNIPPET",
                    "Action.NewSnippet",
                    new[] { "new snippet", "create snippet" }
                ),

                (
                    "ADD FILE",
                    "Action.AddFile",
                    new[] { "add file", "link file" }
                ),

                (
                    "OPEN CLIPS",
                    "Clips",
                    new[] { "clips", "clipboard", "history" }
                )
            };

        foreach (var action in actions)
        {
            var matches =
                action.Item3.Any(
                    alias =>
                        alias.Contains(
                            normalized,
                            StringComparison.OrdinalIgnoreCase) ||
                        normalized.Contains(
                            alias.Split(' ')[0],
                            StringComparison.OrdinalIgnoreCase));

            if (!matches)
                continue;

            result.Add(
                new GlobalSearchSuggestion(
                    action.Item1,
                    "ACTION",
                    action.Item2,
                    string.Empty));
        }
    }
    private static void AddModuleSuggestions(
        List<GlobalSearchSuggestion> result,
        string query)
    {
        var normalized =
            query
                .Trim()
                .ToLowerInvariant();

        var modules =
            new[]
            {
                ("Notes", "notes", "NOTE"),
                ("Snippets", "snippets", "SNIP"),
                ("Files", "files", "FILE"),
                ("Clips", "clips", "CLIP")
            };

        foreach (var module in modules)
        {
            var starts =
                module.Item2.StartsWith(
                    normalized,
                    StringComparison.OrdinalIgnoreCase);

            var contains =
                module.Item2.Contains(
                    normalized,
                    StringComparison.OrdinalIgnoreCase);

            var distance =
                LevenshteinDistance(
                    normalized,
                    module.Item2);

            var fuzzy =
                normalized.Length >= 3 &&
                distance <= 2;

            if (!starts &&
                !contains &&
                !fuzzy)
            {
                continue;
            }

            result.Add(
                new GlobalSearchSuggestion(
                    module.Item1.ToUpperInvariant(),
                    fuzzy &&
                    !starts &&
                    !contains
                        ? "OPEN / DID YOU MEAN?"
                        : "OPEN / MODULE",
                    module.Item1,
                    string.Empty));
        }
    }

    private static bool MatchesQuery(
        string query,
        params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value) &&
                value.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int LevenshteinDistance(
        string left,
        string right)
    {
        if (left.Length == 0)
            return right.Length;

        if (right.Length == 0)
            return left.Length;

        var previous =
            new int[right.Length + 1];

        var current =
            new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var cost =
                    left[i - 1] ==
                    right[j - 1]
                        ? 0
                        : 1;

                current[j] =
                    Math.Min(
                        Math.Min(
                            current[j - 1] + 1,
                            previous[j] + 1),
                        previous[j - 1] + cost);
            }

            (previous, current) =
                (current, previous);
        }

        return previous[right.Length];
    }

    private void GlobalSearchBox_OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            GlobalSearchBox.Clear();

            SearchSuggestionsPanel.Visibility =
                Visibility.Collapsed;

            Keyboard.ClearFocus();

            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        if (SearchSuggestionsList.Items.Count <= 0)
            return;

        if (SearchSuggestionsList.Items[0]
            is not GlobalSearchSuggestion suggestion)
        {
            return;
        }

        OpenSearchSuggestion(
            suggestion);

        e.Handled = true;
    }

    private void SearchSuggestion_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not GlobalSearchSuggestion suggestion)
        {
            return;
        }

        OpenSearchSuggestion(
            suggestion);
    }

    private void OpenSearchSuggestion(
        GlobalSearchSuggestion suggestion)
    {
        SearchSuggestionsPanel.Visibility =
            Visibility.Collapsed;

        GlobalSearchBox.Clear();

        switch (suggestion.Module)
        {
            case "Notes":
                OpenNotes();

                _notesView?.SetExternalSearch(
                    suggestion.Query);

                break;

            case "Snippets":
                OpenSnippets();

                _snippetsView?.SetExternalSearch(
                    suggestion.Query);

                break;

            case "Files":
                OpenFiles();

                _filesView?.SetExternalSearch(
                    suggestion.Query);

                break;

            case "Clips":
                OpenClips();

                _clipsView?.SetExternalSearch(
                    suggestion.Query);

                break;

            case "Action.NewNote":
                OpenNotes();

                _notesView?.BeginNewFromCommandPalette();

                break;

            case "Action.NewSnippet":
                OpenSnippets();

                _snippetsView?.BeginNewFromCommandPalette();

                break;

            case "Action.AddFile":
                OpenFiles();

                _filesView?.BeginAddFileFromCommandPalette();

                break;
        }
    }
    private async void ClipboardMonitor_OnTextCaptured(
        object? sender,
        string text)
    {
        try
        {
            await _clipRepository.CaptureAsync(
                text);

            if (_clipsView is not null)
            {
                await _clipsView.ReloadFromExternalAsync();
            }
        }
        catch
        {
            // Clipboard history must never destabilize the overlay.
        }
    }
    private async System.Threading.Tasks.Task RefreshSnippetSummaryAsync()
    {
        try
        {
            await _snippetRepository.InitializeAsync();

            var count =
                await _snippetRepository.CountAsync();

            var recent =
                await _snippetRepository.GetRecentAsync(3);

            SnippetCountText.Text =
                count.ToString();

            var titleControls =
                new[]
                {
                    RecentTitle1,
                    RecentTitle2,
                    RecentTitle3
                };

            var metaControls =
                new[]
                {
                    RecentMeta1,
                    RecentMeta2,
                    RecentMeta3
                };

            for (var i = 0; i < 3; i++)
            {
                if (i < recent.Count)
                {
                    titleControls[i].Text =
                        recent[i].Title;

                    metaControls[i].Text =
                        $"snippet В· {recent[i].Category}";
                }
                else
                {
                    titleControls[i].Text =
                        i == 0
                            ? "EMPTY"
                            : string.Empty;

                    metaControls[i].Text =
                        string.Empty;
                }
            }
        }
        catch
        {
            SnippetCountText.Text = "вЂ”";
        }
    }

    private void ModuleHitButton_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Button button)
            return;

        var card =
            GetModuleCard(
                button.Tag?.ToString());

        if (card is null)
            return;
        AnimateModuleGlow(
            button.Tag?.ToString(),
            true);

        AnimateCard(
            card,
            1.035,
            -3,
            150);

        card.Background =
            new SolidColorBrush(
                Color.FromArgb(
                    235,
                    18,
                    23,
                    31));

        card.BorderBrush =
            GetAccent(
                button.Tag?.ToString(),
                115);
    }

    private void ModuleHitButton_OnMouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Button button)
            return;

        var card =
            GetModuleCard(
                button.Tag?.ToString());

        if (card is null)
            return;
        AnimateModuleGlow(
            button.Tag?.ToString(),
            false);

        AnimateCard(
            card,
            1,
            0,
            170);

        card.Background =
            new SolidColorBrush(
                Color.FromArgb(
                    220,
                    16,
                    20,
                    27));

        card.BorderBrush =
            new SolidColorBrush(
                Color.FromArgb(
                    44,
                    255,
                    255,
                    255));
    }

    private Border? GetModuleCard(
        string? module)
    {
        return module switch
        {
            "Notes" => NotesCard,
            "Snippets" => SnippetsCard,
            "Files" => FilesCard,
            "Clips" => ClipsCard,
            _ => null
        };
    }

    private void ModuleButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        switch (button.Tag?.ToString())
        {
            case "Snippets":
                OpenSnippets();
                break;

            case "Notes":
                OpenNotes();
                break;

            case "Files":


                OpenFiles();

                break;
            case "Clips":
                OpenClips();
                break;
        }
    }

    private void ModuleCard_OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Border card)
            return;

        AnimateCard(
            card,
            1.035,
            -3,
            150);

        card.Background =
            new SolidColorBrush(
                Color.FromArgb(
                    235,
                    18,
                    23,
                    31));

        card.BorderBrush =
            GetAccent(
                card.Tag?.ToString(),
                115);
    }

    private void ModuleCard_OnMouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Border card)
            return;

        AnimateCard(
            card,
            1,
            0,
            170);

        card.Background =
            new SolidColorBrush(
                Color.FromArgb(
                    220,
                    16,
                    20,
                    27));

        card.BorderBrush =
            new SolidColorBrush(
                Color.FromArgb(
                    44,
                    255,
                    255,
                    255));
    }

    private void ModuleCard_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Border card)
        {
            AnimateCard(
                card,
                0.975,
                0,
                70);
        }
    }

    private void ModuleCard_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border card)
            return;

        AnimateCard(
            card,
            1.035,
            -3,
            95);

        if (card.Tag?.ToString() == "Snippets")
            OpenSnippets();
    }

    private static void AnimateCard(
        Border card,
        double scale,
        double translateY,
        int durationMs)
    {
        // RenderTransforms sourced from a shared Style/Resource can be frozen.
        // Never animate such an instance directly.
        TransformGroup group;
        ScaleTransform scaleTransform;
        TranslateTransform translateTransform;

        if (card.RenderTransform is TransformGroup existingGroup &&
            !existingGroup.IsFrozen &&
            existingGroup.Children.Count >= 2 &&
            existingGroup.Children[0] is ScaleTransform existingScale &&
            !existingScale.IsFrozen &&
            existingGroup.Children[1] is TranslateTransform existingTranslate &&
            !existingTranslate.IsFrozen)
        {
            group = existingGroup;
            scaleTransform = existingScale;
            translateTransform = existingTranslate;
        }
        else
        {
            scaleTransform =
                new ScaleTransform(
                    1.0,
                    1.0);

            translateTransform =
                new TranslateTransform(
                    0.0,
                    0.0);

            group =
                new TransformGroup();

            group.Children.Add(
                scaleTransform);

            group.Children.Add(
                translateTransform);

            card.RenderTransform =
                group;

            card.RenderTransformOrigin =
                new Point(
                    0.5,
                    0.5);
        }

        var duration =
            new Duration(
                TimeSpan.FromMilliseconds(
                    durationMs));

        var easing =
            new QuadraticEase
            {
                EasingMode =
                    EasingMode.EaseOut
            }; 

        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(
                scaleTransform.ScaleX,
                scale,
                duration)
            {
                EasingFunction = easing,
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);

        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(
                scaleTransform.ScaleY,
                scale,
                duration)
            {
                EasingFunction = easing,
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);

        translateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(
                translateTransform.Y,
                translateY,
                duration)
            {
                EasingFunction = easing,
                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static Brush GetAccent(
        string? module,
        byte alpha)
    {
        var color = module switch
        {
            "Notes" =>
                Color.FromArgb(
                    alpha,
                    169,
                    207,
                    255),

            "Snippets" =>
                Color.FromArgb(
                    alpha,
                    189,
                    179,
                    255),

            "Files" =>
                Color.FromArgb(
                    alpha,
                    166,
                    240,
                    229),

            "Clips" =>
                Color.FromArgb(
                    alpha,
                    255,
                    184,
                    216),

            _ =>
                Color.FromArgb(
                    alpha,
                    255,
                    255,
                    255)
        };

        return new SolidColorBrush(color);
    }

    private async System.Threading.Tasks.Task RefreshFileSummaryAsync()
    {
        try
        {
            await _fileRepository.InitializeAsync();

            var count =
                await _fileRepository.CountAsync();

            FileCountText.Text =
                count.ToString();
        }
        catch
        {
            FileCountText.Text =
                "0";
        }
    }

    private void OpenClips()
    {
        _activeModuleName =
            "Clips";

        AnimateModuleGlow(
            "Clips",
            true);

        if (_clipsView is null)
        {
            _clipsView =
                new ClipsView();

            _clipsView.BackRequested +=
                (_, _) => ReturnHome();
        }

        ModuleHost.Children.Clear();

        ModuleHost.Children.Add(
            _clipsView);

        ShowCurrentModule();
    }
    private void OpenFiles()
    {
        _activeModuleName = "Files";
        AnimateModuleGlow("Files", true);
        if (_filesView is null)
        {
            _filesView =
                new FilesView();

            _filesView.BackRequested +=
                (_, _) => ReturnHome();

            _filesView.DataChanged +=
                async (_, _) =>
                    await RefreshFileSummaryAsync();
        }

        ModuleHost.Children.Clear();

        ModuleHost.Children.Add(
            _filesView);

        ShowCurrentModule();
    }
    private async System.Threading.Tasks.Task RefreshNoteSummaryAsync()
    {
        try
        {
            await _noteRepository.InitializeAsync();

            var count =
                await _noteRepository.CountAsync();

            NoteCountText.Text =
                count.ToString();
        }
        catch
        {
            NoteCountText.Text =
                "0";
        }
    }

    private void OpenNotes()
    {
        _activeModuleName = "Notes";
        AnimateModuleGlow("Notes", true);
        if (_notesView is null)
        {
            _notesView =
                new NotesView();

            _notesView.BackRequested +=
                (_, _) => ReturnHome();

            _notesView.DataChanged +=
                async (_, _) =>
                    await RefreshNoteSummaryAsync();
        }

        ModuleHost.Children.Clear();

        ModuleHost.Children.Add(
            _notesView);

        ShowCurrentModule();
    }
    private void OpenSnippets()
    {
        _activeModuleName = "Snippets";
        AnimateModuleGlow("Snippets", true);
        if (_snippetsView is null)
        {
            _snippetsView =
                new SnippetsView();

            _snippetsView.BackRequested +=
                (_, _) => ReturnHome();

            _snippetsView.DataChanged +=
                async (_, _) =>
                    await RefreshSnippetSummaryAsync();
        }

        ModuleHost.Children.Clear();

        ModuleHost.Children.Add(
            _snippetsView);

        ShowCurrentModule();
    }

    private void ShowCurrentModule()
    {
        SetModuleLayoutActive(true);
        var homeFade =
            new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration =
                    TimeSpan.FromMilliseconds(100)
            };

        homeFade.Completed += (_, _) =>
        {
            HomePanel.Visibility =
                Visibility.Collapsed;

            ModuleHost.Visibility =
                Visibility.Visible;

            ModuleHost.Opacity = 0;

            ModuleHost.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration =
                        TimeSpan.FromMilliseconds(170),
                    EasingFunction =
                        new CubicEase
                        {
                            EasingMode =
                                EasingMode.EaseOut
                        }
                });
        };

        HomePanel.BeginAnimation(
            OpacityProperty,
            homeFade);
    }
    private void InitializeBackgroundTheme()
    {
        _backgroundTheme =
            _backgroundThemeService.Load();

        _auroraBackground =
            new AuroraMeshBackground
            {
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,

                VerticalAlignment =
                    VerticalAlignment.Stretch,

                Opacity = 1,

                IsHitTestVisible = false
            };

        var compositionIndex =
            OverlayRoot.Children.IndexOf(
                CompositionRoot);

        if (compositionIndex < 0)
        {
            compositionIndex =
                OverlayRoot.Children.Count;
        }

        OverlayRoot.Children.Insert(
            compositionIndex,
            _auroraBackground);

#if DEBUG
        BackgroundThemeButton.Margin = new Thickness(0);

        DebugTutorialButton.Margin = new Thickness(0);

        DebugTutorialButton.Visibility =
            Visibility.Visible;
#else
        BackgroundThemeButton.Margin = new Thickness(0);

        DebugTutorialButton.Visibility =
            Visibility.Collapsed;
#endif

        ApplyBackgroundTheme(
            animate: false);
    
        BuildFinalUtilityButtonDock();}

    private void BuildFinalUtilityButtonDock()
    {
        if (_utilityButtonDock is not null)
            return;

        if (BackgroundThemeButton.Parent is Panel backgroundParent)
        {
            backgroundParent.Children.Remove(
                BackgroundThemeButton);
        }

        if (DebugTutorialButton.Parent is Panel tutorialParent)
        {
            tutorialParent.Children.Remove(
                DebugTutorialButton);
        }

        BackgroundThemeButton.Margin =
            new Thickness(0);

        BackgroundThemeButton.Width = 188;
        BackgroundThemeButton.Height = 46;
        BackgroundThemeButton.FontSize = 13;
        BackgroundThemeButton.FontWeight =
            FontWeights.SemiBold;

        DebugTutorialButton.Margin =
            new Thickness(
                24,
                0,
                0,
                0);

        DebugTutorialButton.Width = 188;
        DebugTutorialButton.Height = 46;
        DebugTutorialButton.FontSize = 13;
        DebugTutorialButton.FontWeight =
            FontWeights.SemiBold;

        _utilityButtonDock =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        58),

                IsHitTestVisible = true
            };

        _utilityButtonDock.Children.Add(
            BackgroundThemeButton);

#if DEBUG
        DebugTutorialButton.Visibility =
            Visibility.Visible;

        _utilityButtonDock.Children.Add(
            DebugTutorialButton);
#else
        DebugTutorialButton.Visibility =
            Visibility.Collapsed;
#endif

        Panel.SetZIndex(
            _utilityButtonDock,
            620);

        OverlayRoot.Children.Add(
            _utilityButtonDock);

        _ = Dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    ApplyFinalHomeTypography();
                }),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyFinalHomeTypography()
    {
        if (_utilityButtonDock is not null)
        {
            BackgroundThemeButton.ApplyTemplate();
            DebugTutorialButton.ApplyTemplate();

            SetButtonTemplateTypography(
                BackgroundThemeButton,
                13.5);

            SetButtonTemplateTypography(
                DebugTutorialButton,
                13.5);
        }

        foreach (var text in
                 FindOverlayVisualChildren<TextBlock>(
                     OverlayRoot))
        {
            var value =
                text.Text?.Trim();

            if (string.IsNullOrWhiteSpace(
                    value))
            {
                continue;
            }

            if (value.Equals(
                    "NOTES",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Equals(
                    "SNIPS",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Equals(
                    "FILES",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Equals(
                    "CLIPS",
                    StringComparison.OrdinalIgnoreCase))
            {
                text.FontSize = 13;
                text.FontWeight =
                    FontWeights.SemiBold;

                continue;
            }

            if (value.Equals(
                    "RECENT",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Equals(
                    "WORKSPACE",
                    StringComparison.OrdinalIgnoreCase))
            {
                text.FontSize =
                    Math.Max(
                        text.FontSize,
                        11.5);

                text.FontWeight =
                    FontWeights.SemiBold;

                continue;
            }

            if (text.FontSize <= 8.5)
            {
                text.FontSize = 10;
            }
            else if (text.FontSize <= 10)
            {
                text.FontSize = 11;
            }
        }

        if (GlobalSearchBox is not null)
        {
            GlobalSearchBox.TextAlignment =
                TextAlignment.Left;

            GlobalSearchBox.HorizontalContentAlignment =
                HorizontalAlignment.Stretch;

            GlobalSearchBox.VerticalContentAlignment =
                VerticalAlignment.Center;

            GlobalSearchBox.Padding =
                new Thickness(
                    14,
                    0,
                    10,
                    0);

            GlobalSearchBox.FontSize = 16;
        }
    }

    private static void SetButtonTemplateTypography(
        Button button,
        double fontSize)
    {
        foreach (var text in
                 FindOverlayVisualChildren<TextBlock>(
                     button))
        {
            text.FontSize = fontSize;
            text.FontWeight =
                FontWeights.SemiBold;
        }
    }

    private static IEnumerable<T>
        FindOverlayVisualChildren<T>(
            DependencyObject root)
        where T : DependencyObject
    {
        if (root is null)
            yield break;

        var count =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (var i = 0; i < count; i++)
        {
            var child =
                VisualTreeHelper.GetChild(
                    root,
                    i);

            if (child is T typed)
                yield return typed;

            foreach (var descendant in
                     FindOverlayVisualChildren<T>(
                         child))
            {
                yield return descendant;
            }
        }
    }
    private void BackgroundThemeButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        _backgroundTheme =
            _backgroundTheme ==
            BackgroundTheme.Aurora
                ? BackgroundTheme.Particles
                : BackgroundTheme.Aurora;

        _backgroundThemeService.Save(
            _backgroundTheme);

        ApplyBackgroundTheme(
            animate: true);
    }

    private void ApplyBackgroundTheme(
        bool animate)
    {
        var useAurora =
            _backgroundTheme ==
            BackgroundTheme.Aurora;

        if (_auroraBackground is not null)
        {
            if (useAurora)
            {
                _auroraBackground.Visibility =
                    Visibility.Visible;

                _auroraBackground.Start();
            }
            else
            {
                _auroraBackground.Stop();

                _auroraBackground.Visibility =
                    Visibility.Collapsed;
            }
        }

        if (_particleBackground is not null)
        {
            if (useAurora)
            {
                _particleBackground.Stop();

                _particleBackground.Visibility =
                    Visibility.Collapsed;
            }
            else
            {
                _particleBackground.Visibility =
                    Visibility.Visible;

                _particleBackground.Start();
            }
        }

        BackgroundThemeButton.Tag =
            useAurora
                ? "BACKGROUND: AURORA"
                : "BACKGROUND: PARTICLES";

        if (!animate)
            return;

        BackgroundThemeButton.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0.45,
                To = 1,

                Duration =
                    TimeSpan.FromMilliseconds(
                        220),

                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    }
            },
            HandoffBehavior.SnapshotAndReplace);
    }
    private void DebugTutorialButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
#if DEBUG
        ClearTutorialVisualState();

        _onboardingState.Reset();

        if (_welcomeWizard is not null)
        {
            OverlayRoot.Children.Remove(
                _welcomeWizard);

            _welcomeWizard =
                null;
        }

        MaybeShowWelcomeWizard();
#endif
    }
    private void MaybeShowWelcomeWizard()
    {
        if (_onboardingState.IsCompleted)
            return;

        if (_welcomeWizard is null)
        {
            _welcomeWizard =
                new WelcomeWizardView
                {
                    Width = 540,
                    HorizontalAlignment =
                        HorizontalAlignment.Right,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            70,
                            0)
                };

            _welcomeWizard.Completed +=
                WelcomeWizard_OnFinished;

            _welcomeWizard.Skipped +=
                WelcomeWizard_OnFinished;

            _welcomeWizard.StepChanged +=
                WelcomeWizard_OnStepChanged;

            Panel.SetZIndex(
                _welcomeWizard,
                700);

            OverlayRoot.Children.Add(
                _welcomeWizard);
        }

        _welcomeWizard.Visibility =
            Visibility.Visible;

        _welcomeWizard.Opacity =
            1;

        EnsureTutorialSpotlight();

        Dispatcher.BeginInvoke(
            new Action(
                () =>
                    ApplyTutorialStep(
                        _welcomeWizard.CurrentStepIndex)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void WelcomeWizard_OnStepChanged(
        object? sender,
        int stepIndex)
    {
        ApplyTutorialStep(
            stepIndex);
    }

    private void EnsureTutorialSpotlight()
    {
        if (_tutorialSpotlight is not null)
            return;

        _tutorialSpotlightTranslate =
            new TranslateTransform();

        _tutorialSpotlight =
            new Border
            {
                HorizontalAlignment =
                    HorizontalAlignment.Left,

                VerticalAlignment =
                    VerticalAlignment.Top,

                BorderThickness =
                    new Thickness(1.35),

                CornerRadius =
                    new CornerRadius(20),

                BorderBrush =
                    new SolidColorBrush(
                        Color.FromArgb(
                            238,
                            169,
                            216,
                            255)),

                Background =
                    new SolidColorBrush(
                        Color.FromArgb(
                            5,
                            169,
                            216,
                            255)),

                IsHitTestVisible = false,

                Visibility =
                    Visibility.Collapsed,

                RenderTransform =
                    _tutorialSpotlightTranslate,

                Effect =
                    new DropShadowEffect
                    {
                        Color =
                            Color.FromRgb(
                                169,
                                216,
                                255),

                        BlurRadius = 28,
                        ShadowDepth = 0,
                        Opacity = 0.68,
                        RenderingBias =
                            RenderingBias.Performance
                    }
            };

        Panel.SetZIndex(
            _tutorialSpotlight,
            650);

        OverlayRoot.Children.Add(
            _tutorialSpotlight);
    }

    private void ApplyTutorialStep(
        int stepIndex)
    {
        if (_welcomeWizard is null)
            return;

        ClearTutorialTransientState();

        switch (stepIndex)
        {
            case 0:
                ReturnHomeForTutorial();

                PositionTutorialCard(
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center,
                    new Thickness(
                        0,
                        0,
                        70,
                        0));

                HighlightTutorialTarget(
                    CenterPanel,
                    18);

                break;

            case 1:
                ReturnHomeForTutorial();

                PositionTutorialCard(
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center,
                    new Thickness(
                        0,
                        0,
                        70,
                        0));

                HighlightTutorialTarget(
                    RecentPanel,
                    12);

                break;

            case 2:
                ReturnHomeForTutorial();

                PositionTutorialCard(
                    HorizontalAlignment.Left,
                    VerticalAlignment.Center,
                    new Thickness(
                        70,
                        0,
                        0,
                        0));

                HighlightTutorialTarget(
                    WorkspacePanel,
                    12);

                break;

            case 3:
                ReturnHomeForTutorial();

                PositionTutorialCard(
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center,
                    new Thickness(
                        0,
                        0,
                        70,
                        0));

                HighlightTutorialTarget(
                    HomePanel,
                    18);

                AnimateModuleGlow(
                    "Notes",
                    true);

                AnimateModuleGlow(
                    "Snippets",
                    true);

                AnimateModuleGlow(
                    "Files",
                    true);

                AnimateModuleGlow(
                    "Clips",
                    true);

                break;

            case 4:
                OpenClips();

                PositionTutorialCard(
                    HorizontalAlignment.Left,
                    VerticalAlignment.Center,
                    new Thickness(
                        55,
                        0,
                        0,
                        0));

                _ = Dispatcher.InvokeAsync(
                    async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(
                            180);

                        HighlightTutorialTarget(
                            ModuleHost,
                            18);

                        _clipsView?.SetPinTutorialHighlight(
                            true);
                    },
                    System.Windows.Threading.DispatcherPriority.Loaded);

                break;

            case 5:
                ReturnHomeForTutorial();

                PositionTutorialCard(
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center,
                    new Thickness(
                        0,
                        0,
                        70,
                        0));

                _ = Dispatcher.InvokeAsync(
                    async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(
                            170);

                        HighlightTutorialTarget(
                            GlobalSearchBox,
                            12);

                        GlobalSearchBox.Focus();

                        await System.Threading.Tasks.Task.Delay(
                            180);

                        GlobalSearchBox.Text =
                            "clip";

                        GlobalSearchBox.CaretIndex =
                            GlobalSearchBox.Text.Length;
                    },
                    System.Windows.Threading.DispatcherPriority.Loaded);

                break;
        }
    }

    private void ReturnHomeForTutorial()
    {
        if (_moduleLayoutActive)
        {
            ReturnHome();
        }

        SearchSuggestionsPanel.Visibility =
            Visibility.Collapsed;

        if (!string.IsNullOrEmpty(
                GlobalSearchBox.Text))
        {
            GlobalSearchBox.Clear();
        }

        _clipsView?.SetPinTutorialHighlight(
            false);
    }

    private void PositionTutorialCard(
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        Thickness margin)
    {
        if (_welcomeWizard is null)
            return;

        OverlayRoot.UpdateLayout();
        _welcomeWizard.UpdateLayout();

        _welcomeWizard.HorizontalAlignment =
            HorizontalAlignment.Left;

        _welcomeWizard.VerticalAlignment =
            VerticalAlignment.Top;

        _welcomeWizard.Margin =
            new Thickness(0);

        _tutorialCardTranslate ??=
            new TranslateTransform();

        _welcomeWizard.RenderTransform =
            _tutorialCardTranslate;

        var width =
            _welcomeWizard.ActualWidth > 1
                ? _welcomeWizard.ActualWidth
                : 540;

        var height =
            _welcomeWizard.ActualHeight > 1
                ? _welcomeWizard.ActualHeight
                : 315;

        var targetX =
            Math.Max(
                0,
                (OverlayRoot.ActualWidth -
                 width) / 2);

        var targetY =
            Math.Max(
                24,
                OverlayRoot.ActualHeight -
                height -
                54);

        AnimateTransformValue(
            _tutorialCardTranslate,
            TranslateTransform.XProperty,
            targetX,
            280);

        AnimateTransformValue(
            _tutorialCardTranslate,
            TranslateTransform.YProperty,
            targetY,
            280);
    }

    private void HighlightTutorialTarget(
        FrameworkElement target,
        double padding)
    {
        EnsureTutorialSpotlight();

        if (_tutorialSpotlight is null ||
            _tutorialSpotlightTranslate is null ||
            !target.IsVisible)
        {
            return;
        }

        target.UpdateLayout();
        OverlayRoot.UpdateLayout();

        var transform =
            target.TransformToAncestor(
                OverlayRoot);

        var bounds =
            transform.TransformBounds(
                new Rect(
                    new Point(
                        0,
                        0),
                    target.RenderSize));

        var targetX =
            bounds.X -
            padding;

        var targetY =
            bounds.Y -
            padding;

        var targetWidth =
            Math.Max(
                1,
                bounds.Width +
                padding * 2);

        var targetHeight =
            Math.Max(
                1,
                bounds.Height +
                padding * 2);

        _tutorialSpotlight.Visibility =
            Visibility.Visible;

        if (_tutorialSpotlight.Width <= 1 ||
            double.IsNaN(
                _tutorialSpotlight.Width))
        {
            _tutorialSpotlight.Width =
                targetWidth;
        }

        if (_tutorialSpotlight.Height <= 1 ||
            double.IsNaN(
                _tutorialSpotlight.Height))
        {
            _tutorialSpotlight.Height =
                targetHeight;
        }

        AnimateTransformValue(
            _tutorialSpotlightTranslate,
            TranslateTransform.XProperty,
            targetX,
            360);

        AnimateTransformValue(
            _tutorialSpotlightTranslate,
            TranslateTransform.YProperty,
            targetY,
            360);

        AnimateElementValue(
            _tutorialSpotlight,
            FrameworkElement.WidthProperty,
            targetWidth,
            360);

        AnimateElementValue(
            _tutorialSpotlight,
            FrameworkElement.HeightProperty,
            targetHeight,
            360);

        _tutorialSpotlight.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0.72,
                To = 1.0,
                Duration =
                    TimeSpan.FromMilliseconds(
                        1150),

                AutoReverse = true,

                RepeatBehavior =
                    RepeatBehavior.Forever,

                EasingFunction =
                    new SineEase
                    {
                        EasingMode =
                            EasingMode.EaseInOut
                    }
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateTransformValue(
        Animatable target,
        DependencyProperty property,
        double to,
        int durationMs)
    {
        target.BeginAnimation(
            property,
            new DoubleAnimation
            {
                To = to,

                Duration =
                    TimeSpan.FromMilliseconds(
                        durationMs),

                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseInOut
                    },

                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateElementValue(
        UIElement target,
        DependencyProperty property,
        double to,
        int durationMs)
    {
        target.BeginAnimation(
            property,
            new DoubleAnimation
            {
                To = to,

                Duration =
                    TimeSpan.FromMilliseconds(
                        durationMs),

                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseInOut
                    },

                FillBehavior =
                    FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }
    private void ClearTutorialTransientState()
    {
        if (_tutorialSpotlight is not null)
        {
            _tutorialSpotlight.BeginAnimation(
                OpacityProperty,
                null);

            _tutorialSpotlight.Visibility =
                Visibility.Collapsed;
        }

        _clipsView?.SetPinTutorialHighlight(
            false);

        AnimateModuleGlow(
            "Notes",
            false);

        AnimateModuleGlow(
            "Snippets",
            false);

        AnimateModuleGlow(
            "Files",
            false);

        AnimateModuleGlow(
            "Clips",
            false);

        if (GlobalSearchBox.Text ==
            "clip")
        {
            GlobalSearchBox.Clear();
        }

        SearchSuggestionsPanel.Visibility =
            Visibility.Collapsed;
    }

    private void ClearTutorialVisualState()
    {
        ClearTutorialTransientState();

        if (_moduleLayoutActive)
        {
            ReturnHome();
        }
    }
    private void WelcomeWizard_OnFinished(
        object? sender,
        EventArgs e)
    {
        ClearTutorialVisualState();

        _onboardingState.MarkCompleted();

        if (_welcomeWizard is not null)
        {
            _welcomeWizard.Visibility =
                Visibility.Collapsed;
        }
    }
    private void ReturnHome()
    {
        ClearAllModuleCardGlows();
        var previousActiveModule =
            _activeModuleName;

        _activeModuleName =
            null;

        AnimateModuleGlow(
            previousActiveModule,
            false);
        SetModuleLayoutActive(false);
        var moduleFade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(100)
        };

        moduleFade.Completed += (_, _) =>
        {
            ModuleHost.Visibility =
                Visibility.Collapsed;

            HomePanel.Visibility =
                Visibility.Visible;

            HomePanel.Opacity = 0;

            HomePanel.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(170),
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

    private void ResetToHomeImmediately()
    {
        ModuleHost.BeginAnimation(
            OpacityProperty,
            null);

        HomePanel.BeginAnimation(
            OpacityProperty,
            null);

        ModuleHost.Visibility =
            Visibility.Collapsed;

        ModuleHost.Opacity = 0;

        HomePanel.Visibility =
            Visibility.Visible;

        HomePanel.Opacity = 1;
    }
}