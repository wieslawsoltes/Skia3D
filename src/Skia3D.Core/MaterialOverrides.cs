using System.Numerics;
using SkiaSharp;

namespace Skia3D.Core;

public class MaterialOverrides
{
    public MaterialShadingModel? ShadingModel { get; set; }

    public SKColor? BaseColor { get; set; }

    public Texture2D? BaseColorTexture { get; set; }

    public TextureSampler? BaseColorSampler { get; set; }

    public Vector2? UvScale { get; set; }

    public Vector2? UvOffset { get; set; }

    public float? BaseColorTextureStrength { get; set; }

    public float? Metallic { get; set; }

    public float? Roughness { get; set; }

    public Texture2D? MetallicRoughnessTexture { get; set; }

    public TextureSampler? MetallicRoughnessSampler { get; set; }

    public float? MetallicRoughnessTextureStrength { get; set; }

    public Texture2D? NormalTexture { get; set; }

    public TextureSampler? NormalSampler { get; set; }

    public float? NormalStrength { get; set; }

    public Texture2D? EmissiveTexture { get; set; }

    public TextureSampler? EmissiveSampler { get; set; }

    public SKColor? EmissiveColor { get; set; }

    public float? EmissiveStrength { get; set; }

    public Texture2D? OcclusionTexture { get; set; }

    public TextureSampler? OcclusionSampler { get; set; }

    public float? OcclusionStrength { get; set; }

    public float? Ambient { get; set; }

    public float? Diffuse { get; set; }

    public float? Specular { get; set; }

    public float? Shininess { get; set; }

    public bool? UseVertexColor { get; set; }

    public bool? DoubleSided { get; set; }
}
