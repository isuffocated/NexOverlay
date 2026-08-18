using System;
using System.Windows;
using System.Windows.Media;

namespace NexOverlay.App;

public sealed class AuroraMeshBackground :
    FrameworkElement
{
    private readonly DrawingVisual _visual =
        new();

    private readonly DateTime _started =
        DateTime.UtcNow;

    private bool _running;

    public AuroraMeshBackground()
    {
        AddVisualChild(_visual);
        IsHitTestVisible = false;

Loaded +=
            (_, _) => Start();

        Unloaded +=
            (_, _) => Stop();

        IsVisibleChanged +=
            (_, _) =>
            {
                if (IsVisible)
                    Start();
                else
                    Stop();
            };
    }

    protected override int VisualChildrenCount =>
        _visual is null
            ? 0
            : 1;

    protected override Visual GetVisualChild(
        int index)
    {
        if (index != 0 ||
            _visual is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index));
        }

        return _visual;
    }

    public void Start()
    {
        if (_running ||
            Visibility != Visibility.Visible)
        {
            return;
        }

        _running = true;

        CompositionTarget.Rendering +=
            OnRendering;

        Draw();
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;

        CompositionTarget.Rendering -=
            OnRendering;
    }

    private void OnRendering(
        object? sender,
        EventArgs e)
    {
        if (!IsVisible ||
            ActualWidth <= 1 ||
            ActualHeight <= 1)
        {
            return;
        }

        Draw();
    }

    private void Draw()
    {
        var width =
            ActualWidth;

        var height =
            ActualHeight;

        if (width <= 1 ||
            height <= 1)
        {
            return;
        }

        var t =
            (DateTime.UtcNow - _started)
            .TotalSeconds;

        using var dc =
            _visual.RenderOpen();

        // Extremely subtle overall veil so the gradients blend
        // into the already-darkened desktop capture.
        dc.DrawRectangle(
            new SolidColorBrush(
                Color.FromArgb(
                    8,
                    0,
                    0,
                    0)),
            null,
            new Rect(
                0,
                0,
                width,
                height));

        DrawBlob(
            dc,
            width,
            height,
            0.18 +
            Math.Sin(t * 0.055) * 0.07,
            0.28 +
            Math.Cos(t * 0.043) * 0.08,
            0.52,
            0.58,
            Color.FromRgb(
                72,
                135,
                255),
            0.16);

        DrawBlob(
            dc,
            width,
            height,
            0.73 +
            Math.Cos(t * 0.047) * 0.08,
            0.24 +
            Math.Sin(t * 0.061) * 0.07,
            0.50,
            0.60,
            Color.FromRgb(
                145,
                104,
                255),
            0.14);

        DrawBlob(
            dc,
            width,
            height,
            0.68 +
            Math.Sin(t * 0.039) * 0.11,
            0.72 +
            Math.Cos(t * 0.052) * 0.08,
            0.56,
            0.54,
            Color.FromRgb(
                54,
                208,
                196),
            0.12);

        DrawBlob(
            dc,
            width,
            height,
            0.31 +
            Math.Cos(t * 0.035) * 0.09,
            0.78 +
            Math.Sin(t * 0.045) * 0.07,
            0.42,
            0.48,
            Color.FromRgb(
                255,
                110,
                184),
            0.075);
    }

    private static void DrawBlob(
        DrawingContext dc,
        double width,
        double height,
        double x,
        double y,
        double widthFactor,
        double heightFactor,
        Color color,
        double opacity)
    {
        var cx =
            width * x;

        var cy =
            height * y;

        var radiusX =
            width * widthFactor;

        var radiusY =
            height * heightFactor;

        var transparent =
            Color.FromArgb(
                0,
                color.R,
                color.G,
                color.B);

        var center =
            Color.FromArgb(
                (byte)Math.Clamp(
                    opacity * 255,
                    0,
                    255),
                color.R,
                color.G,
                color.B);

        var brush =
            new RadialGradientBrush
            {
                Center =
                    new Point(
                        0.5,
                        0.5),

                GradientOrigin =
                    new Point(
                        0.5,
                        0.5),

                RadiusX = 0.5,
                RadiusY = 0.5,

                GradientStops =
                {
                    new GradientStop(
                        center,
                        0),

                    new GradientStop(
                        Color.FromArgb(
                            (byte)(center.A * 0.48),
                            color.R,
                            color.G,
                            color.B),
                        0.38),

                    new GradientStop(
                        transparent,
                        1)
                }
            };

        dc.PushTransform(
            new TranslateTransform(
                cx,
                cy));

        dc.PushTransform(
            new ScaleTransform(
                radiusX,
                radiusY));

        dc.DrawEllipse(
            brush,
            null,
            new Point(
                0,
                0),
            1,
            1);

        dc.Pop();
        dc.Pop();
    }
}