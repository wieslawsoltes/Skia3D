using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Controls;

public sealed class AnimationCurveEditorControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<AnimationCurveSeries>?> SeriesProperty =
        AvaloniaProperty.Register<AnimationCurveEditorControl, IReadOnlyList<AnimationCurveSeries>?>(nameof(Series));

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<AnimationCurveEditorControl, double>(nameof(Duration), 1.0);

    public static readonly StyledProperty<double> PlayheadTimeProperty =
        AvaloniaProperty.Register<AnimationCurveEditorControl, double>(nameof(PlayheadTime));

    public static readonly StyledProperty<double> SelectedTimeProperty =
        AvaloniaProperty.Register<AnimationCurveEditorControl, double>(nameof(SelectedTime), double.NaN);

    public IReadOnlyList<AnimationCurveSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public double PlayheadTime
    {
        get => GetValue(PlayheadTimeProperty);
        set => SetValue(PlayheadTimeProperty, value);
    }

    public double SelectedTime
    {
        get => GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        var plot = bounds.Deflate(new Thickness(6));
        context.FillRectangle(new SolidColorBrush(Color.Parse("#161B21")), bounds);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#2F3741"))), plot);

        DrawGrid(context, plot);
        DrawCurves(context, plot);
        DrawMarker(context, plot, PlayheadTime, Color.Parse("#E8EEF5"), 1.2);
        if (!double.IsNaN(SelectedTime))
        {
            DrawMarker(context, plot, SelectedTime, Color.Parse("#6ED0E0"), 1.0);
        }
    }

    private void DrawGrid(DrawingContext context, Rect plot)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#24303A")), 1);
        for (int i = 1; i < 6; i++)
        {
            var x = plot.X + plot.Width * i / 6.0;
            context.DrawLine(pen, new Point(x, plot.Y), new Point(x, plot.Bottom));
        }

        for (int i = 1; i < 4; i++)
        {
            var y = plot.Y + plot.Height * i / 4.0;
            context.DrawLine(pen, new Point(plot.X, y), new Point(plot.Right, y));
        }
    }

    private void DrawCurves(DrawingContext context, Rect plot)
    {
        var series = Series;
        if (series == null || series.Count == 0)
        {
            return;
        }

        var duration = Math.Max(0.001, Duration);
        var valueMin = float.PositiveInfinity;
        var valueMax = float.NegativeInfinity;

        for (int i = 0; i < series.Count; i++)
        {
            var points = series[i].Points;
            for (int j = 0; j < points.Count; j++)
            {
                valueMin = MathF.Min(valueMin, points[j].Value);
                valueMax = MathF.Max(valueMax, points[j].Value);
            }
        }

        if (!float.IsFinite(valueMin) || !float.IsFinite(valueMax))
        {
            return;
        }

        if (Math.Abs(valueMax - valueMin) < 1e-5)
        {
            valueMin -= 1f;
            valueMax += 1f;
        }

        for (int i = 0; i < series.Count; i++)
        {
            var points = series[i].Points;
            if (points.Count < 2)
            {
                continue;
            }

            var pen = new Pen(new SolidColorBrush(ToColor(series[i].Color)), 1.4);
            for (int j = 1; j < points.Count; j++)
            {
                var a = points[j - 1];
                var b = points[j];
                var ax = plot.X + plot.Width * Math.Clamp(a.Time / duration, 0, 1);
                var ay = plot.Bottom - plot.Height * Math.Clamp((a.Value - valueMin) / (valueMax - valueMin), 0, 1);
                var bx = plot.X + plot.Width * Math.Clamp(b.Time / duration, 0, 1);
                var by = plot.Bottom - plot.Height * Math.Clamp((b.Value - valueMin) / (valueMax - valueMin), 0, 1);
                context.DrawLine(pen, new Point(ax, ay), new Point(bx, by));
            }
        }
    }

    private void DrawMarker(DrawingContext context, Rect plot, double time, Color color, double thickness)
    {
        var duration = Math.Max(0.001, Duration);
        var x = plot.X + plot.Width * Math.Clamp(time / duration, 0, 1);
        context.DrawLine(new Pen(new SolidColorBrush(color), thickness), new Point(x, plot.Y), new Point(x, plot.Bottom));
    }

    private static Color ToColor(uint argb)
    {
        return Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
    }
}
