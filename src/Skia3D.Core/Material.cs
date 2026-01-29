using SkiaSharp;
using System.Numerics;

namespace Skia3D.Core;

public enum MaterialShadingModel
{
    Phong,
    MetallicRoughness
}

public sealed class Material
{
    public MaterialShadingModel ShadingModel { get; set; } = MaterialShadingModel.Phong;

    public SKColor BaseColor { get; set; } = new SKColor(255, 255, 255);

    public Texture2D? BaseColorTexture { get; set; }

    public TextureSampler BaseColorSampler { get; set; } = new();

    public Vector2 UvScale { get; set; } = Vector2.One;

    public Vector2 UvOffset { get; set; } = Vector2.Zero;

    public float BaseColorTextureStrength { get; set; } = 1f;

    public float Metallic { get; set; } = 0f;

    public float Roughness { get; set; } = 0.6f;

    public Texture2D? MetallicRoughnessTexture { get; set; }

    public TextureSampler MetallicRoughnessSampler { get; set; } = new();

    public float MetallicRoughnessTextureStrength { get; set; } = 1f;

    public Texture2D? NormalTexture { get; set; }

    public TextureSampler NormalSampler { get; set; } = new();

    public float NormalStrength { get; set; } = 1f;

    public Texture2D? EmissiveTexture { get; set; }

    public TextureSampler EmissiveSampler { get; set; } = new();

    public SKColor EmissiveColor { get; set; } = new SKColor(0, 0, 0);

    public float EmissiveStrength { get; set; } = 1f;

    public Texture2D? OcclusionTexture { get; set; }

    public TextureSampler OcclusionSampler { get; set; } = new();

    public float OcclusionStrength { get; set; } = 1f;

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

    public static Light Spot(Vector3 position, Vector3 direction, SKColor color, float intensity = 1f, float range = 10f, float innerAngle = 0.35f, float outerAngle = 0.6f)
    {
        return new Light
        {
            Position = position,
            Direction = direction,
            Color = color,
            Intensity = intensity,
            Range = range,
            InnerConeAngle = innerAngle,
            OuterConeAngle = outerAngle,
            Type = LightType.Spot
        };
    }

    public static Light Area(Vector3 position, Vector3 direction, Vector2 size, SKColor color, float intensity = 1f, float range = 10f)
    {
        return new Light
        {
            Position = position,
            Direction = direction,
            Size = size,
            Color = color,
            Intensity = intensity,
            Range = range,
            Type = LightType.Area
        };
    }

    public LightType Type { get; init; } = LightType.Directional;

    public Vector3 Direction { get; init; } = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.6f));

    public Vector3 Position { get; init; } = Vector3.Zero;

    public SKColor Color { get; init; } = new SKColor(255, 255, 255);

    public float Intensity { get; init; } = 1f;

    public float Range { get; init; } = 10f;

    public float InnerConeAngle { get; init; } = 0.35f;

    public float OuterConeAngle { get; init; } = 0.6f;

    public Vector2 Size { get; init; } = new(0.5f, 0.5f);
}

public enum LightType
{
    Directional,
    Point,
    Spot,
    Area
}
