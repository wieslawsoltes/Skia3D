using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Core;
using ShaderGraphModel = Skia3D.ShaderGraph.ShaderGraph;
using SkiaSharp;

namespace Skia3D.Scene;

public sealed class MeshRenderer : SceneComponent
{
    public MeshRenderer(Mesh mesh, Material? material = null)
    {
        Mesh = mesh;
        Material = material ?? Material.Default();
        Instance = new MeshInstance(mesh)
        {
            Material = Material
        };
        ActiveInstance = Instance;
    }

    public Mesh Mesh { get; }

    public Material Material { get; set; }

    public ShaderGraphModel? MaterialGraph { get; set; }

    public SKColor? OverrideColor { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool UseLods { get; set; } = true;

    public float BaseLodScreenFraction { get; set; } = 0.2f;

    public IReadOnlyList<MeshLodLevel> Lods => _lods;

    internal MeshInstance Instance { get; }

    internal MeshInstance ActiveInstance { get; private set; }

    private Skin? _skin;

    public Skin? Skin
    {
        get => _skin;
        set
        {
            _skin = value;
            ActiveInstance = _skin?.Instance ?? Instance;
        }
    }

    private readonly List<MeshLodLevel> _lods = new();

    public void SetLods(IEnumerable<MeshLodLevel> levels)
    {
        if (levels is null)
        {
            throw new ArgumentNullException(nameof(levels));
        }

        _lods.Clear();
        foreach (var level in levels)
        {
            _lods.Add(level);
        }

        _lods.Sort((a, b) => b.ScreenFraction.CompareTo(a.ScreenFraction));
    }

    public void ClearLods() => _lods.Clear();

    internal MeshInstance GetInstance(Camera? camera, Matrix4x4 worldMatrix)
    {
        var instance = Instance;
        if (Skin != null)
        {
            Skin.Update(worldMatrix);
            instance = Skin.Instance;
        }
        else if (UseLods && camera != null && _lods.Count > 0)
        {
            float screenFraction = EstimateScreenFraction(camera, worldMatrix);
            if (screenFraction < BaseLodScreenFraction)
            {
                instance = SelectLod(screenFraction);
            }
        }

        instance.Transform = worldMatrix;
        instance.Material = Material;
        instance.OverrideColor = OverrideColor;
        instance.IsVisible = IsVisible;
        instance.MaterialOverrides = MaterialGraph != null ? BuildOverrides(MaterialGraph.Evaluate()) : null;
        ActiveInstance = instance;
        return instance;
    }

    private static MaterialOverrides BuildOverrides(ShaderGraphModel graph)
    {
        var result = graph.Evaluate();
        return BuildOverrides(result);
    }

    private static MaterialOverrides BuildOverrides(Skia3D.ShaderGraph.ShaderGraphResult result)
    {
        var overrides = new MaterialParameterBlock
        {
            BaseColor = ToColor(result.BaseColor),
            Metallic = result.Metallic,
            Roughness = result.Roughness,
            EmissiveColor = ToColor(result.Emissive),
            BaseColorTexture = result.BaseColorTexture,
            BaseColorSampler = result.BaseColorSampler,
            BaseColorTextureStrength = result.BaseColorTextureStrength,
            MetallicRoughnessTexture = result.MetallicRoughnessTexture,
            MetallicRoughnessSampler = result.MetallicRoughnessSampler,
            MetallicRoughnessTextureStrength = result.MetallicRoughnessStrength,
            NormalTexture = result.NormalTexture,
            NormalSampler = result.NormalSampler,
            NormalStrength = result.NormalStrength,
            EmissiveTexture = result.EmissiveTexture,
            EmissiveSampler = result.EmissiveSampler,
            EmissiveStrength = result.EmissiveStrength,
            OcclusionTexture = result.OcclusionTexture,
            OcclusionSampler = result.OcclusionSampler,
            OcclusionStrength = result.OcclusionStrength,
            ShadingModel = MaterialShadingModel.MetallicRoughness
        };

        return overrides;
    }

    private static SKColor ToColor(Vector4 color)
    {
        return new SKColor(
            (byte)Math.Clamp(color.X * 255f, 0f, 255f),
            (byte)Math.Clamp(color.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(color.Z * 255f, 0f, 255f),
            (byte)Math.Clamp(color.W * 255f, 0f, 255f));
    }

    private MeshInstance SelectLod(float screenFraction)
    {
        for (int i = 0; i < _lods.Count; i++)
        {
            if (screenFraction >= _lods[i].ScreenFraction)
            {
                return _lods[i].Instance;
            }
        }

        return _lods[^1].Instance;
    }

    private float EstimateScreenFraction(Camera camera, Matrix4x4 worldMatrix)
    {
        if (Mesh.BoundingRadius <= 1e-6f)
        {
            return 1f;
        }

        var center = new Vector3(worldMatrix.M41, worldMatrix.M42, worldMatrix.M43);
        var distance = Vector3.Distance(center, camera.Position);
        if (distance <= 1e-4f)
        {
            return 1f;
        }

        var scale = ExtractMaxScale(worldMatrix);
        var radius = Mesh.BoundingRadius * scale;
        var screenHeight = 2f * distance * MathF.Tan(camera.FieldOfView * 0.5f);
        if (screenHeight <= 1e-6f)
        {
            return 1f;
        }

        var fraction = (radius * 2f) / screenHeight;
        return Math.Clamp(fraction, 0f, 1f);
    }

    private static float ExtractMaxScale(Matrix4x4 m)
    {
        var sx = new Vector3(m.M11, m.M12, m.M13).Length();
        var sy = new Vector3(m.M21, m.M22, m.M23).Length();
        var sz = new Vector3(m.M31, m.M32, m.M33).Length();
        return MathF.Max(sx, MathF.Max(sy, sz));
    }
}

public sealed class MeshLodLevel
{
    public MeshLodLevel(Mesh mesh, float screenFraction)
    {
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        ScreenFraction = Math.Clamp(screenFraction, 0f, 1f);
        Instance = new MeshInstance(mesh);
    }

    public Mesh Mesh { get; }

    public float ScreenFraction { get; set; }

    internal MeshInstance Instance { get; }
}

public sealed class LightComponent : SceneComponent
{
    public LightComponent(Light light)
    {
        Light = light;
    }

    public Light Light { get; set; }

    public bool IsEnabled
    {
        get => Enabled;
        set => Enabled = value;
    }
}

public sealed class CameraComponent : SceneComponent
{
    public CameraComponent(Camera camera)
    {
        Camera = camera;
    }

    public Camera Camera { get; }

    public bool IsEnabled
    {
        get => Enabled;
        set => Enabled = value;
    }
}
