using SkiaSharp;
using System.Numerics;

namespace Skia3D.Core;

public sealed class Material
{
    public SKColor BaseColor { get; set; } = new SKColor(255, 255, 255);

    public float Ambient { get; set; } = 0.15f;

    public float Diffuse { get; set; } = 0.85f;

    public float Specular { get; set; } = 0.2f;

    public float Shininess { get; set; } = 16f;

    public bool UseVertexColor { get; set; } = true;

    public bool DoubleSided { get; set; }

    public static Material Default() => new();
}

public sealed class Light
{
    public static Light Directional(Vector3 direction, SKColor color, float intensity = 1f)
    {
        return new Light
        {
            Direction = direction,
            Color = color,
            Intensity = intensity,
            Type = LightType.Directional
        };
    }

    public static Light Point(Vector3 position, SKColor color, float intensity = 1f, float range = 10f)
    {
        return new Light
        {
            Position = position,
            Color = color,
            Intensity = intensity,
            Range = range,
            Type = LightType.Point
        };
    }

    public LightType Type { get; init; } = LightType.Directional;

    public Vector3 Direction { get; init; } = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.6f));

    public Vector3 Position { get; init; } = Vector3.Zero;

    public SKColor Color { get; init; } = new SKColor(255, 255, 255);

    public float Intensity { get; init; } = 1f;

    public float Range { get; init; } = 10f;
}

public enum LightType
{
    Directional,
    Point
}
