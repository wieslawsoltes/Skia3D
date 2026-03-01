using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Geometry;
using Skia3D.Modeling;
using Skia3D.Scene;
using SkiaSharp;

namespace Skia3D.Editor;

public sealed class EditorDocument
{
    private const int LodTriangleThreshold = 200;
    private readonly Dictionary<MeshInstance, SceneNode> _instanceNodes = new();
    private readonly Dictionary<MeshInstance, EditableMesh> _editableMeshes = new();

    public IReadOnlyDictionary<MeshInstance, SceneNode> InstanceNodes => _instanceNodes;

    public IReadOnlyDictionary<MeshInstance, EditableMesh> EditableMeshes => _editableMeshes;

    public void Clear()
    {
        _instanceNodes.Clear();
        _editableMeshes.Clear();
    }

    public void RegisterInstance(SceneNode node)
    {
        var instance = node.MeshInstance;
        if (instance is null)
        {
            return;
        }

        _instanceNodes[instance] = node;
        ConfigureLods(node);
    }

    public void RegisterEditable(SceneNode node)
    {
        var instance = node.MeshInstance;
        if (instance is null)
        {
            return;
        }

        _instanceNodes[instance] = node;
        _editableMeshes[instance] = CreateEditableMesh(instance.Mesh);
        ConfigureLods(node);
    }

    public void RegisterSceneRecursive(SceneNode node)
    {
        if (node.MeshRenderer != null)
        {
            RegisterEditable(node);
        }

        foreach (var child in node.Children)
        {
            RegisterSceneRecursive(child);
        }
    }

    public void UnregisterInstance(MeshInstance instance)
    {
        if (instance is null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        _instanceNodes.Remove(instance);
        _editableMeshes.Remove(instance);
    }

    public void UnregisterSceneRecursive(SceneNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var instance = node.MeshInstance;
        if (instance != null)
        {
            UnregisterInstance(instance);
        }

        foreach (var child in node.Children)
        {
            UnregisterSceneRecursive(child);
        }
    }

    public bool TryGetEditableMesh(MeshInstance instance, out EditableMesh editable)
    {
        return _editableMeshes.TryGetValue(instance, out editable!);
    }

    public bool TryGetNode(MeshInstance instance, out SceneNode node)
    {
        return _instanceNodes.TryGetValue(instance, out node!);
    }

    public MeshInstance? ReplaceInstance(MeshInstance oldInstance, Mesh mesh)
    {
        if (!_instanceNodes.TryGetValue(oldInstance, out var node))
        {
            return null;
        }

        var renderer = new MeshRenderer(mesh, oldInstance.Material)
        {
            OverrideColor = oldInstance.OverrideColor,
            IsVisible = oldInstance.IsVisible
        };
        node.MeshRenderer = renderer;
        ConfigureLods(node);

        var newInstance = node.MeshInstance;
        if (newInstance is null)
        {
            return null;
        }

        _instanceNodes.Remove(oldInstance);
        _instanceNodes[newInstance] = node;

        if (_editableMeshes.Remove(oldInstance, out var editable))
        {
            _editableMeshes[newInstance] = editable;
        }

        return newInstance;
    }

    public MeshInstance? RebuildEditableInstance(MeshInstance instance, EditableMesh editable)
    {
        BuildGeometryFromEditable(editable, out var vertices, out var indices);

        var targetInstance = instance;
        if (IsMeshShared(instance.Mesh, instance))
        {
            var mesh = new Mesh(vertices, indices, editable.HasTangents ? editable.Tangents : null);
            var newInstance = ReplaceInstance(instance, mesh);
            if (newInstance is null)
            {
                return null;
            }
            targetInstance = newInstance;
        }
        else
        {
            instance.Mesh.UpdateGeometry(vertices, indices, allowRefit: true, parallelRefit: true);
            RefreshLods(targetInstance);
        }

        return targetInstance;
    }

    public MeshInstance? RebuildMeshForEditable(EditableMesh editable)
    {
        foreach (var pair in _editableMeshes)
        {
            if (ReferenceEquals(pair.Value, editable))
            {
                return RebuildEditableInstance(pair.Key, editable);
            }
        }

        return null;
    }

    public void RefreshLods(MeshInstance instance)
    {
        if (_instanceNodes.TryGetValue(instance, out var node))
        {
            ConfigureLods(node);
        }
    }

    public static Mesh BuildMeshFromEditable(EditableMesh editable)
    {
        BuildGeometryFromEditable(editable, out var vertices, out var indices);
        return new Mesh(vertices, indices, editable.HasTangents ? editable.Tangents : null);
    }

    private static EditableMesh CreateEditableMesh(Mesh mesh)
    {
        var positions = new List<Vector3>(mesh.Vertices.Count);
        var uvs = new List<Vector2>(mesh.Vertices.Count);
        var normals = new List<Vector3>(mesh.Vertices.Count);
        var colors = new List<Vector4>(mesh.Vertices.Count);
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            positions.Add(mesh.Vertices[i].Position);
            uvs.Add(mesh.Vertices[i].UV);
            normals.Add(mesh.Vertices[i].Normal);
            colors.Add(ToVector(mesh.Vertices[i].Color));
        }

        var indices = new List<int>(mesh.Indices.Count);
        for (int i = 0; i < mesh.Indices.Count; i++)
        {
            indices.Add(mesh.Indices[i]);
        }

        List<Vector4>? tangents = null;
        if (mesh.Tangents != null && mesh.Tangents.Count == mesh.Vertices.Count)
        {
            tangents = new List<Vector4>(mesh.Tangents.Count);
            for (int i = 0; i < mesh.Tangents.Count; i++)
            {
                tangents.Add(mesh.Tangents[i]);
            }
        }

        return new EditableMesh(positions, indices, uvs, normals, colors, tangents);
    }

    private void ConfigureLods(SceneNode node)
    {
        var renderer = node.MeshRenderer;
        if (renderer?.Mesh is null)
        {
            return;
        }

        var mesh = renderer.Mesh;
        if (mesh.Vertices.Count == 0 || mesh.Indices.Count == 0)
        {
            renderer.ClearLods();
            return;
        }

        var triCount = mesh.Indices.Count / 3;
        if (triCount < LodTriangleThreshold)
        {
            renderer.ClearLods();
            return;
        }

        var ratios = GetLodRatios(triCount);
        if (ratios.Length == 0)
        {
            renderer.ClearLods();
            return;
        }

        var source = MeshFactory.ToMeshData(mesh);
        var options = new MeshSimplifyOptions { RecalculateNormals = true };
        var chain = MeshSimplifier.GenerateLodChain(source, ratios, options);

        var levels = new List<MeshLodLevel>(ratios.Length);
        var baseFraction = renderer.BaseLodScreenFraction;
        var fallback = mesh.Vertices.Count > 0 ? mesh.Vertices[0].Color : (SKColor?)null;
        for (int i = 1; i < chain.Count; i++)
        {
            var ratio = ratios[i - 1];
            var screenFraction = Math.Clamp(baseFraction * ratio, 0.01f, baseFraction);
            var lodMesh = MeshFactory.CreateFromData(chain[i], fallback);
            levels.Add(new MeshLodLevel(lodMesh, screenFraction));
        }

        renderer.SetLods(levels);
    }

    private static float[] GetLodRatios(int triangleCount)
    {
        if (triangleCount >= 20000)
        {
            return new[] { 0.55f, 0.25f, 0.12f };
        }

        if (triangleCount >= 8000)
        {
            return new[] { 0.6f, 0.3f };
        }

        if (triangleCount >= 2000)
        {
            return new[] { 0.7f, 0.4f };
        }

        return new[] { 0.8f, 0.55f };
    }

    private bool IsMeshShared(Mesh mesh, MeshInstance instance)
    {
        foreach (var other in _instanceNodes.Keys)
        {
            if (ReferenceEquals(other, instance))
            {
                continue;
            }

            if (ReferenceEquals(other.Mesh, mesh))
            {
                return true;
            }
        }

        return false;
    }

    private static void BuildGeometryFromEditable(EditableMesh editable, out Vertex[] vertices, out int[] indices)
    {
        var positions = editable.Positions;
        var uvs = editable.UVs;
        indices = editable.Indices.ToArray();
        var normals = editable.HasNormals ? editable.Normals!.ToArray() : ComputeNormals(positions, indices);
        var colors = editable.HasColors ? ConvertColors(editable.Colors!) : CreateVertexColors(positions.Count);

        vertices = new Vertex[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            var uv = i < uvs.Count ? uvs[i] : Vector2.Zero;
            vertices[i] = new Vertex(positions[i], normals[i], colors[i], uv);
        }
    }

    private static Vector3[] ComputeNormals(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices)
    {
        var normals = new Vector3[positions.Count];

        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var n = Vector3.Cross(p1 - p0, p2 - p0);

            normals[i0] += n;
            normals[i1] += n;
            normals[i2] += n;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            var n = normals[i];
            if (n.LengthSquared() > 1e-8f)
            {
                normals[i] = Vector3.Normalize(n);
            }
            else
            {
                normals[i] = Vector3.UnitY;
            }
        }

        return normals;
    }

    private static SKColor[] CreateVertexColors(int count)
    {
        var colors = new SKColor[count];
        for (int i = 0; i < count; i++)
        {
            colors[i] = SKColors.White;
        }

        return colors;
    }

    private static SKColor[] ConvertColors(IReadOnlyList<Vector4> colors)
    {
        var result = new SKColor[colors.Count];
        for (int i = 0; i < colors.Count; i++)
        {
            result[i] = ToColor(colors[i]);
        }

        return result;
    }

    private static Vector4 ToVector(SKColor color)
    {
        const float inv = 1f / 255f;
        return new Vector4(color.Red * inv, color.Green * inv, color.Blue * inv, color.Alpha * inv);
    }

    private static SKColor ToColor(Vector4 color)
    {
        return new SKColor(
            ToByte(color.X),
            ToByte(color.Y),
            ToByte(color.Z),
            ToByte(color.W));
    }

    private static byte ToByte(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return (byte)MathF.Round(value * 255f);
    }
}
