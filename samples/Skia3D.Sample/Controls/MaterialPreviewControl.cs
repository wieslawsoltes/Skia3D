using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Skia3D.Core;

namespace Skia3D.Sample.Controls;

public sealed class MaterialPreviewControl : Control
{
    public static readonly StyledProperty<Material?> MaterialProperty =
        AvaloniaProperty.Register<MaterialPreviewControl, Material?>(nameof(Material));

    public static readonly StyledProperty<int> StampProperty =
        AvaloniaProperty.Register<MaterialPreviewControl, int>(nameof(Stamp));

    public Material? Material
    {
        get => GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    public int Stamp
    {
        get => GetValue(StampProperty);
        set => SetValue(StampProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        _ = Stamp;

        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        var material = Material ?? Skia3D.Core.Material.Default();
        var baseColor = ToColor(material.BaseColor);
        var panel = bounds.Deflate(new Thickness(6));

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(baseColor, 0),
                new GradientStop(Color.FromArgb(baseColor.A, (byte)(baseColor.R * 0.35), (byte)(baseColor.G * 0.35), (byte)(baseColor.B * 0.35)), 1)
            }
        };

        context.FillRectangle(new SolidColorBrush(Color.Parse("#151A1F")), bounds);
        context.FillRectangle(gradient, panel);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#2F3741"))), panel);

        var title = new FormattedText(
            material.ShadingModel == MaterialShadingModel.MetallicRoughness
                ? $"PBR  M:{material.Metallic:0.##}  R:{material.Roughness:0.##}"
                : $"Phong  S:{material.Specular:0.##}  Sh:{material.Shininess:0.##}",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            11,
            new SolidColorBrush(Color.Parse("#E8EEF5")));

        context.DrawText(title, new Point(panel.X + 8, panel.Y + 8));
    }

    private static Color ToColor(SkiaSharp.SKColor color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }
}
