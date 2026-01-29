using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Skia3D.Geometry;

namespace Skia3D.Modeling;

public sealed class EditableMesh
{
    public EditableMesh(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> indices,
        IReadOnlyList<Vector2>? uvs = null,
        IReadOnlyList<Vector3>? normals = null,
        IReadOnlyList<Vector4>? colors = null,
        IReadOnlyList<Vector4>? tangents = null)
    {
        if (positions is null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        if (indices is null)
        {
            throw new ArgumentNullException(nameof(indices));
        }

        if (uvs != null && uvs.Count != positions.Count)
        {
            throw new ArgumentException("UV count must match vertex count.", nameof(uvs));
        }
        if (normals != null && normals.Count != positions.Count)
        {
            throw new ArgumentException("Normals count must match vertex count.", nameof(normals));
        }
        if (colors != null && colors.Count != positions.Count)
        {
            throw new ArgumentException("Colors count must match vertex count.", nameof(colors));
        }
        if (tangents != null && tangents.Count != positions.Count)
        {
            throw new ArgumentException("Tangents count must match vertex count.", nameof(tangents));
        }

        Positions = new List<Vector3>(positions);
        Indices = new List<int>(indices);
        UVs = new List<Vector2>(positions.Count);
        if (uvs != null)
        {
            UVs.AddRange(uvs);
        }
        else
        {
            for (int i = 0; i < positions.Count; i++)
            {
                UVs.Add(Vector2.Zero);
            }
        }

        if (normals != null)
        {
            Normals = new List<Vector3>(normals);
        }

        if (colors != null)
        {
            Colors = new List<Vector4>(colors);
        }

        if (tangents != null)
        {
            Tangents = new List<Vector4>(tangents);
        }

        EnsureUvFaceGroups();
    }

    public List<Vector3> Positions { get; }

    public List<int> Indices { get; }

    public List<Vector2> UVs { get; }

    public List<Vector3>? Normals { get; private set; }

    public List<Vector4>? Colors { get; private set; }

    public List<Vector4>? Tangents { get; private set; }

    public HashSet<EdgeKey> SeamEdges { get; } = new();

    public List<int> UvFaceGroups { get; } = new();

    public int VertexCount => Positions.Count;

    public int TriangleCount => Indices.Count / 3;

    public bool HasNormals => Normals != null && Normals.Count == Positions.Count;

    public bool HasColors => Colors != null && Colors.Count == Positions.Count;

    public bool HasTangents => Tangents != null && Tangents.Count == Positions.Count;

    public MeshData ToMeshData()
    {
        var pos = Positions.ToArray();
        var idx = Indices.ToArray();
        MeshAttributes? attributes = null;
        var hasUvs = UVs.Count == pos.Length;
        var hasNormals = HasNormals;
        var hasColors = HasColors;
        var hasTangents = HasTangents;
        if (hasUvs || hasNormals || hasColors || hasTangents)
        {
            attributes = new MeshAttributes(
                hasNormals ? Normals!.ToArray() : null,
                hasUvs ? UVs.ToArray() : null,
                hasColors ? Colors!.ToArray() : null,
                hasTangents ? Tangents!.ToArray() : null);
        }
        return new MeshData(SoaVector3.From(pos), idx, attributes);
    }

    public void EnsureUvFaceGroups()
    {
        var triangleCount = TriangleCount;
        if (UvFaceGroups.Count == triangleCount)
        {
            return;
        }

        UvFaceGroups.Clear();
        for (int i = 0; i < triangleCount; i++)
        {
            UvFaceGroups.Add(0);
        }
    }

    public void SetNormals(IReadOnlyList<Vector3>? normals)
    {
        Normals = normals != null ? new List<Vector3>(normals) : null;
    }

    public void SetColors(IReadOnlyList<Vector4>? colors)
    {
        Colors = colors != null ? new List<Vector4>(colors) : null;
    }

    public void SetTangents(IReadOnlyList<Vector4>? tangents)
    {
        Tangents = tangents != null ? new List<Vector4>(tangents) : null;
    }

    public void InvalidateNormals()
    {
        Normals = null;
        Tangents = null;
    }

    public bool TrimSeamEdges()
    {
        if (SeamEdges.Count == 0)
        {
            return false;
        }

        var edges = MeshAdjacency.Build(this).Edges;
        return SeamEdges.RemoveWhere(edge => !edges.ContainsKey(edge)) > 0;
    }

    public Aabb ComputeBounds()
    {
        if (Positions.Count == 0)
        {
            return Aabb.Empty;
        }

        var span = CollectionsMarshal.AsSpan(Positions);
        return GeometryKernels.ComputeAabb(span);
    }
}
