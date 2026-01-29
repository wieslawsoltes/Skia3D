using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using Skia3D.Core;
using Skia3D.Geometry;
using SkiaSharp;

namespace Skia3D.Scene;

public sealed class ScenePackageData
{
    public SceneGraphData Scene { get; set; } = new();
    public List<MeshAssetData> Meshes { get; set; } = new();
    public List<MaterialAssetData> Materials { get; set; } = new();
    public List<TextureAssetData> Textures { get; set; } = new();
}

public sealed class MeshAssetData
{
    public string Id { get; set; } = string.Empty;
    public Vector3Data[] Positions { get; set; } = Array.Empty<Vector3Data>();
    public int[] Indices { get; set; } = Array.Empty<int>();
    public Vector3Data[]? Normals { get; set; }
    public Vector2Data[]? TexCoords { get; set; }
    public Vector4Data[]? Colors { get; set; }
    public Vector4Data[]? Tangents { get; set; }
    public Vector3Data[]? Bitangents { get; set; }
    public Vector4Data[]? SkinWeights { get; set; }
    public Int4Data[]? SkinIndices { get; set; }
    public MeshMorphTargetData[]? MorphTargets { get; set; }
    public EdgeData[]? SeamEdges { get; set; }
    public int[]? UvFaceGroups { get; set; }
}

public sealed class MeshMorphTargetData
{
    public string? Name { get; set; }
    public Vector3Data[]? PositionDeltas { get; set; }
    public Vector3Data[]? NormalDeltas { get; set; }
    public Vector4Data[]? TangentDeltas { get; set; }
}

public sealed class MeshEditData
{
    public EdgeData[]? SeamEdges { get; set; }
    public int[]? UvFaceGroups { get; set; }
}

public sealed class MaterialAssetData
{
    public string Id { get; set; } = string.Empty;
    public MaterialShadingModel ShadingModel { get; set; } = MaterialShadingModel.Phong;
    public ColorData BaseColor { get; set; } = ColorData.White;
    public string? BaseColorTextureId { get; set; }
    public TextureSamplerData BaseColorSampler { get; set; } = new();
    public Vector2Data UvScale { get; set; } = Vector2Data.From(Vector2.One);
    public Vector2Data UvOffset { get; set; } = Vector2Data.From(Vector2.Zero);
    public float BaseColorTextureStrength { get; set; } = 1f;
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 0.6f;
    public string? MetallicRoughnessTextureId { get; set; }
    public TextureSamplerData MetallicRoughnessSampler { get; set; } = new();
    public float MetallicRoughnessTextureStrength { get; set; } = 1f;
    public string? NormalTextureId { get; set; }
    public TextureSamplerData NormalSampler { get; set; } = new();
    public float NormalStrength { get; set; } = 1f;
    public string? EmissiveTextureId { get; set; }
    public TextureSamplerData EmissiveSampler { get; set; } = new();
    public ColorData EmissiveColor { get; set; } = new() { R = 0, G = 0, B = 0, A = 255 };
    public float EmissiveStrength { get; set; } = 1f;
    public string? OcclusionTextureId { get; set; }
    public TextureSamplerData OcclusionSampler { get; set; } = new();
    public float OcclusionStrength { get; set; } = 1f;
    public float Ambient { get; set; } = 0.15f;
    public float Diffuse { get; set; } = 0.85f;
    public float Specular { get; set; } = 0.2f;
    public float Shininess { get; set; } = 16f;
    public bool UseVertexColor { get; set; } = true;
    public bool DoubleSided { get; set; }
}

public sealed class TextureAssetData
{
    public string Id { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Pixels { get; set; } = Array.Empty<byte>();
}

public readonly struct EdgeData
{
    public EdgeData(int a, int b)
    {
        A = a;
        B = b;
    }

    public int A { get; }

    public int B { get; }
}

public sealed class ScenePackageAssetLibrary : ISceneAssetResolver
{
    private readonly Dictionary<string, Mesh> _meshes = new();
    private readonly Dictionary<string, Material> _materials = new();
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly Dictionary<Mesh, MeshAssetData> _meshMetadata = new();
    private readonly Dictionary<Mesh, string> _meshIds = new();
    private readonly Dictionary<Material, string> _materialIds = new();
    private readonly Dictionary<Texture2D, string> _textureIds = new();

    public IReadOnlyDictionary<Mesh, MeshAssetData> MeshMetadata => _meshMetadata;

    public string? GetMeshId(Mesh mesh)
    {
        return _meshIds.TryGetValue(mesh, out var id) ? id : null;
    }

    public Mesh? ResolveMesh(string id)
    {
        return _meshes.TryGetValue(id, out var mesh) ? mesh : null;
    }

    public string? GetMaterialId(Material material)
    {
        return _materialIds.TryGetValue(material, out var id) ? id : null;
    }

    public Material? ResolveMaterial(string id)
    {
        return _materials.TryGetValue(id, out var material) ? material : null;
    }

    public string? GetTextureId(Texture2D texture)
    {
        return _textureIds.TryGetValue(texture, out var id) ? id : null;
    }

    public Texture2D? ResolveTexture(string id)
    {
        return _textures.TryGetValue(id, out var texture) ? texture : null;
    }

    internal static ScenePackageAssetLibrary FromPackage(ScenePackageData package)
    {
        var library = new ScenePackageAssetLibrary();

        foreach (var textureData in package.Textures)
        {
            var texture = BuildTexture(textureData);
            if (texture == null)
            {
                continue;
            }

            library._textures[textureData.Id] = texture;
            library._textureIds[texture] = textureData.Id;
        }

        foreach (var materialData in package.Materials)
        {
            var material = BuildMaterial(materialData, library);
            library._materials[materialData.Id] = material;
            library._materialIds[material] = materialData.Id;
        }

        foreach (var meshData in package.Meshes)
        {
            var mesh = BuildMesh(meshData);
            library._meshes[meshData.Id] = mesh;
            library._meshIds[mesh] = meshData.Id;
            library._meshMetadata[mesh] = meshData;
        }

        return library;
    }

    private static Mesh BuildMesh(MeshAssetData data)
    {
        var positions = ToVector3Array(data.Positions);
        var indices = data.Indices ?? Array.Empty<int>();
        var normals = data.Normals != null ? ToVector3Array(data.Normals) : null;
        var texcoords = data.TexCoords != null ? ToVector2Array(data.TexCoords) : null;
        var colors = data.Colors != null ? ToColorArray(data.Colors) : null;
        var tangents = data.Tangents != null ? ToVector4Array(data.Tangents) : null;
        var bitangents = data.Bitangents != null ? ToVector3Array(data.Bitangents) : null;
        var skinWeights = data.SkinWeights != null ? ToVector4Array(data.SkinWeights) : null;
        var skinIndices = data.SkinIndices != null ? ToInt4Array(data.SkinIndices) : null;
        var morphTargets = data.MorphTargets != null ? ToMorphTargets(data.MorphTargets) : null;

        return MeshFactory.CreateFromData(positions, indices, normals, colors, texcoords, tangents, bitangents, skinWeights, skinIndices, morphTargets);
    }

    private static Material BuildMaterial(MaterialAssetData data, ScenePackageAssetLibrary library)
    {
        var material = new Material
        {
            ShadingModel = data.ShadingModel,
            BaseColor = data.BaseColor.ToColor(),
            BaseColorTexture = !string.IsNullOrWhiteSpace(data.BaseColorTextureId)
                ? library.ResolveTexture(data.BaseColorTextureId)
                : null,
            BaseColorSampler = data.BaseColorSampler.ToSampler(),
            UvScale = data.UvScale.ToVector2(),
            UvOffset = data.UvOffset.ToVector2(),
            BaseColorTextureStrength = data.BaseColorTextureStrength,
            Metallic = data.Metallic,
            Roughness = data.Roughness,
            MetallicRoughnessTexture = !string.IsNullOrWhiteSpace(data.MetallicRoughnessTextureId)
                ? library.ResolveTexture(data.MetallicRoughnessTextureId)
                : null,
            MetallicRoughnessSampler = data.MetallicRoughnessSampler.ToSampler(),
            MetallicRoughnessTextureStrength = data.MetallicRoughnessTextureStrength,
            NormalTexture = !string.IsNullOrWhiteSpace(data.NormalTextureId)
                ? library.ResolveTexture(data.NormalTextureId)
                : null,
            NormalSampler = data.NormalSampler.ToSampler(),
            NormalStrength = data.NormalStrength,
            EmissiveTexture = !string.IsNullOrWhiteSpace(data.EmissiveTextureId)
                ? library.ResolveTexture(data.EmissiveTextureId)
                : null,
            EmissiveSampler = data.EmissiveSampler.ToSampler(),
            EmissiveColor = data.EmissiveColor.ToColor(),
            EmissiveStrength = data.EmissiveStrength,
            OcclusionTexture = !string.IsNullOrWhiteSpace(data.OcclusionTextureId)
                ? library.ResolveTexture(data.OcclusionTextureId)
                : null,
            OcclusionSampler = data.OcclusionSampler.ToSampler(),
            OcclusionStrength = data.OcclusionStrength,
            Ambient = data.Ambient,
            Diffuse = data.Diffuse,
            Specular = data.Specular,
            Shininess = data.Shininess,
            UseVertexColor = data.UseVertexColor,
            DoubleSided = data.DoubleSided
        };

        return material;
    }

    private static Texture2D? BuildTexture(TextureAssetData data)
    {
        if (data.Width <= 0 || data.Height <= 0)
        {
            return null;
        }

        var pixelCount = data.Width * data.Height;
        var colors = new SKColor[pixelCount];
        if (data.Pixels.Length >= pixelCount * 4)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * 4;
                colors[i] = new SKColor(
                    data.Pixels[offset],
                    data.Pixels[offset + 1],
                    data.Pixels[offset + 2],
                    data.Pixels[offset + 3]);
            }
        }
        else
        {
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = SKColors.White;
            }
        }

        return Texture2D.FromPixels(data.Width, data.Height, colors);
    }

    private static Vector3[] ToVector3Array(Vector3Data[] values)
    {
        var result = new Vector3[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = values[i].ToVector3();
        }

        return result;
    }

    private static Vector2[] ToVector2Array(Vector2Data[] values)
    {
        var result = new Vector2[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = values[i].ToVector2();
        }

        return result;
    }

    private static Vector4[] ToVector4Array(Vector4Data[] values)
    {
        var result = new Vector4[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = values[i].ToVector4();
        }

        return result;
    }

    private static Int4[] ToInt4Array(Int4Data[] values)
    {
        var result = new Int4[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = values[i].ToInt4();
        }

        return result;
    }

    private static MeshMorphTarget[] ToMorphTargets(MeshMorphTargetData[] values)
    {
        var result = new MeshMorphTarget[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var target = values[i];
            var pos = target.PositionDeltas != null ? ToVector3Array(target.PositionDeltas) : null;
            var norm = target.NormalDeltas != null ? ToVector3Array(target.NormalDeltas) : null;
            var tan = target.TangentDeltas != null ? ToVector4Array(target.TangentDeltas) : null;
            result[i] = new MeshMorphTarget(target.Name, pos, norm, tan);
        }

        return result;
    }

    private static SKColor[] ToColorArray(Vector4Data[] values)
    {
        var result = new SKColor[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var v = values[i];
            result[i] = new SKColor(
                ToByte(v.X),
                ToByte(v.Y),
                ToByte(v.Z),
                ToByte(v.W));
        }

        return result;
    }

    private static byte ToByte(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return (byte)MathF.Round(value * 255f);
    }
}

public static class ScenePackageSerializer
{
    public static ScenePackageData ToPackage(Scene scene, Func<Mesh, MeshEditData?>? editProvider = null)
    {
        if (scene is null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        var collector = new AssetCollector();
        var sceneData = SceneSerializer.ToData(scene, collector);

        var package = new ScenePackageData { Scene = sceneData };
        foreach (var entry in collector.Meshes)
        {
            var mesh = entry.Mesh;
            var meshData = BuildMeshAsset(entry.Id, mesh, editProvider?.Invoke(mesh));
            package.Meshes.Add(meshData);
        }

        foreach (var entry in collector.Materials)
        {
            package.Materials.Add(BuildMaterialAsset(entry.Id, entry.Material, collector));
        }

        foreach (var entry in collector.Textures)
        {
            package.Textures.Add(BuildTextureAsset(entry.Id, entry.Texture));
        }

        return package;
    }

    public static string ToJson(Scene scene, Func<Mesh, MeshEditData?>? editProvider = null, JsonSerializerOptions? options = null)
    {
        var data = ToPackage(scene, editProvider);
        options ??= CreateDefaultOptions();
        return JsonSerializer.Serialize(data, options);
    }

    public static Scene FromJson(string json, out ScenePackageAssetLibrary assets, JsonSerializerOptions? options = null)
    {
        options ??= CreateDefaultOptions();
        var package = JsonSerializer.Deserialize<ScenePackageData>(json, options) ?? new ScenePackageData();
        assets = ScenePackageAssetLibrary.FromPackage(package);
        return SceneSerializer.FromData(package.Scene, assets);
    }

    private static MeshAssetData BuildMeshAsset(string id, Mesh mesh, MeshEditData? edit)
    {
        var data = MeshFactory.ToMeshData(mesh);
        var positions = data.GetPositionsArray();
        var indices = data.Indices;
        var attributes = data.Attributes;
        var normals = attributes?.Normals;
        var texcoords = attributes?.TexCoords;
        var colors = attributes?.Colors;
        var tangents = attributes?.Tangents;
        var bitangents = attributes?.Bitangents;
        var skinWeights = attributes?.SkinWeights;
        var skinIndices = attributes?.SkinIndices;
        var morphTargets = attributes?.MorphTargets;

        return new MeshAssetData
        {
            Id = id,
            Positions = ToVector3Data(positions),
            Indices = indices,
            Normals = normals != null ? ToVector3Data(normals) : null,
            TexCoords = texcoords != null ? ToVector2Data(texcoords) : null,
            Colors = colors != null ? ToVector4Data(colors) : null,
            Tangents = tangents != null ? ToVector4Data(tangents) : null,
            Bitangents = bitangents != null ? ToVector3Data(bitangents) : null,
            SkinWeights = skinWeights != null ? ToVector4Data(skinWeights) : null,
            SkinIndices = skinIndices != null ? ToInt4Data(skinIndices) : null,
            MorphTargets = morphTargets != null ? ToMorphTargetData(morphTargets) : null,
            SeamEdges = edit?.SeamEdges,
            UvFaceGroups = edit?.UvFaceGroups
        };
    }

    private static MaterialAssetData BuildMaterialAsset(string id, Material material, AssetCollector collector)
    {
        return new MaterialAssetData
        {
            Id = id,
            ShadingModel = material.ShadingModel,
            BaseColor = ColorData.From(material.BaseColor),
            BaseColorTextureId = material.BaseColorTexture != null ? collector.GetTextureId(material.BaseColorTexture) : null,
            BaseColorSampler = TextureSamplerData.From(material.BaseColorSampler),
            UvScale = Vector2Data.From(material.UvScale),
            UvOffset = Vector2Data.From(material.UvOffset),
            BaseColorTextureStrength = material.BaseColorTextureStrength,
            Metallic = material.Metallic,
            Roughness = material.Roughness,
            MetallicRoughnessTextureId = material.MetallicRoughnessTexture != null ? collector.GetTextureId(material.MetallicRoughnessTexture) : null,
            MetallicRoughnessSampler = TextureSamplerData.From(material.MetallicRoughnessSampler),
            MetallicRoughnessTextureStrength = material.MetallicRoughnessTextureStrength,
            NormalTextureId = material.NormalTexture != null ? collector.GetTextureId(material.NormalTexture) : null,
            NormalSampler = TextureSamplerData.From(material.NormalSampler),
            NormalStrength = material.NormalStrength,
            EmissiveTextureId = material.EmissiveTexture != null ? collector.GetTextureId(material.EmissiveTexture) : null,
            EmissiveSampler = TextureSamplerData.From(material.EmissiveSampler),
            EmissiveColor = ColorData.From(material.EmissiveColor),
            EmissiveStrength = material.EmissiveStrength,
            OcclusionTextureId = material.OcclusionTexture != null ? collector.GetTextureId(material.OcclusionTexture) : null,
            OcclusionSampler = TextureSamplerData.From(material.OcclusionSampler),
            OcclusionStrength = material.OcclusionStrength,
            Ambient = material.Ambient,
            Diffuse = material.Diffuse,
            Specular = material.Specular,
            Shininess = material.Shininess,
            UseVertexColor = material.UseVertexColor,
            DoubleSided = material.DoubleSided
        };
    }

    private static TextureAssetData BuildTextureAsset(string id, Texture2D texture)
    {
        var pixels = texture.GetPixels();
        var bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            int offset = i * 4;
            bytes[offset] = pixels[i].Red;
            bytes[offset + 1] = pixels[i].Green;
            bytes[offset + 2] = pixels[i].Blue;
            bytes[offset + 3] = pixels[i].Alpha;
        }

        return new TextureAssetData
        {
            Id = id,
            Width = texture.Width,
            Height = texture.Height,
            Pixels = bytes
        };
    }

    private static Vector3Data[] ToVector3Data(IReadOnlyList<Vector3> values)
    {
        var result = new Vector3Data[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            result[i] = Vector3Data.From(values[i]);
        }

        return result;
    }

    private static Vector2Data[] ToVector2Data(IReadOnlyList<Vector2> values)
    {
        var result = new Vector2Data[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            result[i] = Vector2Data.From(values[i]);
        }

        return result;
    }

    private static Vector4Data[] ToVector4Data(IReadOnlyList<Vector4> values)
    {
        var result = new Vector4Data[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            result[i] = Vector4Data.From(values[i]);
        }

        return result;
    }

    private static Int4Data[] ToInt4Data(IReadOnlyList<Int4> values)
    {
        var result = new Int4Data[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            result[i] = Int4Data.From(values[i]);
        }

        return result;
    }

    private static MeshMorphTargetData[] ToMorphTargetData(IReadOnlyList<MeshMorphTarget> values)
    {
        var result = new MeshMorphTargetData[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            var target = values[i];
            result[i] = new MeshMorphTargetData
            {
                Name = target.Name,
                PositionDeltas = target.PositionDeltas != null ? ToVector3Data(target.PositionDeltas) : null,
                NormalDeltas = target.NormalDeltas != null ? ToVector3Data(target.NormalDeltas) : null,
                TangentDeltas = target.TangentDeltas != null ? ToVector4Data(target.TangentDeltas) : null
            };
        }

        return result;
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    private sealed class AssetCollector : ISceneAssetResolver
    {
        private int _meshIndex;
        private int _materialIndex;
        private int _textureIndex;
        private readonly Dictionary<Mesh, string> _meshIds = new();
        private readonly Dictionary<Material, string> _materialIds = new();
        private readonly Dictionary<Texture2D, string> _textureIds = new();

        public List<(string Id, Mesh Mesh)> Meshes { get; } = new();

        public List<(string Id, Material Material)> Materials { get; } = new();

        public List<(string Id, Texture2D Texture)> Textures { get; } = new();

        public string? GetMeshId(Mesh mesh)
        {
            if (_meshIds.TryGetValue(mesh, out var id))
            {
                return id;
            }

            id = $"mesh_{_meshIndex++}";
            _meshIds[mesh] = id;
            Meshes.Add((id, mesh));
            return id;
        }

        public Mesh? ResolveMesh(string id) => null;

        public string? GetMaterialId(Material material)
        {
            if (_materialIds.TryGetValue(material, out var id))
            {
                return id;
            }

            id = $"mat_{_materialIndex++}";
            _materialIds[material] = id;
            Materials.Add((id, material));
            return id;
        }

        public Material? ResolveMaterial(string id) => null;

        public string? GetTextureId(Texture2D texture)
        {
            if (_textureIds.TryGetValue(texture, out var id))
            {
                return id;
            }

            id = $"tex_{_textureIndex++}";
            _textureIds[texture] = id;
            Textures.Add((id, texture));
            return id;
        }

        public Texture2D? ResolveTexture(string id) => null;
    }
}
