using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace NexOverlay.App;

public sealed class ParticleNetworkBackground : FrameworkElement
{
    private const int ParticleCount = 180;
    private const double ConnectionDistance = 108.0;
    private const double CellSize = ConnectionDistance;

    // 60 fps cap. CompositionTarget.Rendering can run at the
    // monitor refresh rate, including 144/165 Hz.
    private static readonly TimeSpan MinimumFrameInterval =
        TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly Random _random =
        new();

    private readonly List<Particle> _particles =
        new(ParticleCount);

    private readonly Dictionary<long, List<int>> _spatialGrid =
        new(256);

    private readonly DrawingVisual _visual =
        new();

    private readonly Brush _dotBrush;

    private readonly Pen _nearPen;
    private readonly Pen _midPen;
    private readonly Pen _farPen;

    private bool _running;
    private bool _hasRenderingTime;

    private TimeSpan _lastRenderingTime;

    public ParticleNetworkBackground()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;

        AddVisualChild(
            _visual);

        _dotBrush =
            Freeze(
                new SolidColorBrush(
                    Color.FromArgb(
                        205,
                        191,
                        226,
                        255)));

        _nearPen =
            CreatePen(
                82,
                180,
                220,
                255,
                0.78);

        _midPen =
            CreatePen(
                48,
                169,
                209,
                250,
                0.58);

        _farPen =
            CreatePen(
                22,
                157,
                196,
                238,
                0.44);

        SizeChanged +=
            (_, _) =>
            {
                EnsureParticles();
                DrawFrame();
            };
    }

    protected override int VisualChildrenCount =>
        1;

    protected override Visual GetVisualChild(
        int index)
    {
        if (index != 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _visual;
    }

    public void Start()
    {
        if (_running)
            return;

        _running = true;
        _hasRenderingTime = false;

        EnsureParticles();

        CompositionTarget.Rendering +=
            OnRendering;

        DrawFrame();
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;

        CompositionTarget.Rendering -=
            OnRendering;

        _hasRenderingTime = false;
    }

    private void OnRendering(
        object? sender,
        EventArgs e)
    {
        if (!_running ||
            ActualWidth <= 1 ||
            ActualHeight <= 1 ||
            e is not RenderingEventArgs rendering)
        {
            return;
        }

        if (!_hasRenderingTime)
        {
            _lastRenderingTime =
                rendering.RenderingTime;

            _hasRenderingTime = true;
            return;
        }

        var elapsed =
            rendering.RenderingTime -
            _lastRenderingTime;

        if (elapsed <
            MinimumFrameInterval)
        {
            return;
        }

        _lastRenderingTime =
            rendering.RenderingTime;

        var dt =
            Math.Clamp(
                elapsed.TotalSeconds,
                0.001,
                0.05);

        UpdateParticles(dt);
        RebuildSpatialGrid();
        DrawFrame();
    }

    private void EnsureParticles()
    {
        if (ActualWidth <= 1 ||
            ActualHeight <= 1)
        {
            return;
        }

        while (_particles.Count < ParticleCount)
        {
            var angle =
                _random.NextDouble() *
                Math.PI *
                2.0;

            var speed =
                18.0 +
                _random.NextDouble() *
                24.0;

            _particles.Add(
                new Particle
                {
                    X =
                        _random.NextDouble() *
                        ActualWidth,

                    Y =
                        _random.NextDouble() *
                        ActualHeight,

                    VX =
                        Math.Cos(angle) *
                        speed,

                    VY =
                        Math.Sin(angle) *
                        speed,

                    Radius =
                        1.45 +
                        _random.NextDouble() *
                        1.25
                });
        }

        RebuildSpatialGrid();
    }

    private void UpdateParticles(
        double dt)
    {
        var width =
            ActualWidth;

        var height =
            ActualHeight;

        foreach (var particle in _particles)
        {
            particle.X +=
                particle.VX *
                dt;

            particle.Y +=
                particle.VY *
                dt;

            if (particle.X < -10)
                particle.X = width + 10;
            else if (particle.X > width + 10)
                particle.X = -10;

            if (particle.Y < -10)
                particle.Y = height + 10;
            else if (particle.Y > height + 10)
                particle.Y = -10;
        }
    }

    private void RebuildSpatialGrid()
    {
        _spatialGrid.Clear();

        for (var i = 0; i < _particles.Count; i++)
        {
            var p =
                _particles[i];

            var cellX =
                (int)Math.Floor(
                    p.X /
                    CellSize);

            var cellY =
                (int)Math.Floor(
                    p.Y /
                    CellSize);

            var key =
                MakeCellKey(
                    cellX,
                    cellY);

            if (!_spatialGrid.TryGetValue(
                    key,
                    out var bucket))
            {
                bucket =
                    new List<int>(8);

                _spatialGrid.Add(
                    key,
                    bucket);
            }

            bucket.Add(i);
        }
    }

    private void DrawFrame()
    {
        using var dc =
            _visual.RenderOpen();

        if (_particles.Count == 0)
            return;

        var maxDistanceSquared =
            ConnectionDistance *
            ConnectionDistance;

        for (var i = 0; i < _particles.Count; i++)
        {
            var a =
                _particles[i];

            var cellX =
                (int)Math.Floor(
                    a.X /
                    CellSize);

            var cellY =
                (int)Math.Floor(
                    a.Y /
                    CellSize);

            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    var key =
                        MakeCellKey(
                            cellX + offsetX,
                            cellY + offsetY);

                    if (!_spatialGrid.TryGetValue(
                            key,
                            out var bucket))
                    {
                        continue;
                    }

                    foreach (var j in bucket)
                    {
                        if (j <= i)
                            continue;

                        var b =
                            _particles[j];

                        var dx =
                            a.X -
                            b.X;

                        var dy =
                            a.Y -
                            b.Y;

                        var distanceSquared =
                            dx * dx +
                            dy * dy;

                        if (distanceSquared >
                            maxDistanceSquared)
                        {
                            continue;
                        }

                        var distance =
                            Math.Sqrt(
                                distanceSquared);

                        var pen =
                            distance switch
                            {
                                < 42 =>
                                    _nearPen,

                                < 76 =>
                                    _midPen,

                                _ =>
                                    _farPen
                            };

                        dc.DrawLine(
                            pen,
                            new Point(a.X, a.Y),
                            new Point(b.X, b.Y));
                    }
                }
            }
        }

        foreach (var p in _particles)
        {
            dc.DrawEllipse(
                _dotBrush,
                null,
                new Point(
                    p.X,
                    p.Y),
                p.Radius,
                p.Radius);
        }
    }

    private static long MakeCellKey(
        int x,
        int y)
    {
        return
            ((long)x << 32) ^
            (uint)y;
    }

    private static Pen CreatePen(
        byte alpha,
        byte red,
        byte green,
        byte blue,
        double thickness)
    {
        var brush =
            Freeze(
                new SolidColorBrush(
                    Color.FromArgb(
                        alpha,
                        red,
                        green,
                        blue)));

        return
            Freeze(
                new Pen(
                    brush,
                    thickness));
    }

    private static T Freeze<T>(
        T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private sealed class Particle
    {
        public double X;
        public double Y;

        public double VX;
        public double VY;

        public double Radius;
    }
}