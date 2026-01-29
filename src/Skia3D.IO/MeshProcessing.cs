using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Geometry;
using SkiaSharp;

namespace Skia3D.IO;

public sealed class MeshProcessingOptions
{
    public bool RecalculateNormals { get; set; }

    public bool CenterToOrigin { get; set; }

    public float? ScaleToRadius { get; set; }

    public SKColor? OverrideColor { get; set; }
}

public static class MeshProcessing
{
    public static Mesh Apply(Mesh mesh, MeshProcessingOptions options)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var vertices = mesh.Vertices;
        var positions = new Vector3[vertices.Count];
        var normals = new Vector3[vertices.Count];
        var colors = new SKColor[vertices.Count];
        var uvs = new Vector2[vertices.Count];
        IReadOnlyList<Vector4>? tangents = mesh.Tangents;
        IReadOnlyList<Vector3>? bitangents = mesh.Bitangents;
        IReadOnlyList<Vector4>? skinWeights = mesh.SkinWeights;
        IReadOnlyList<Int4>? skinIndices = mesh.SkinIndices;
        IReadOnlyList<MeshMorphTarget>? morphTargets = mesh.MorphTargets;

        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            positions[i] = v.Position;
            normals[i] = v.Normal;
            colors[i] = options.OverrideColor ?? v.Color;
            uvs[i] = v.UV;
        }

        if (options.CenterToOrigin)
        {
            ApplyCentering(positions);
        }

        if (options.ScaleToRadius.HasValue)
        {
            ApplyScaling(positions, options.ScaleToRadius.Value);
        }

        if (options.RecalculateNormals)
        {
            normals = ComputeNormals(positions, mesh.Indices);
            tangents = ComputeTangents(positions, normals, uvs, mesh.Indices);
            bitangents = ComputeBitangents(normals, tangents);
        }

        return MeshFactory.CreateFromData(positions, mesh.Indices, normals, colors, uvs, tangents, bitangents, skinWeights, skinIndices, morphTargets);
    }

    private static void ApplyCentering(Vector3[] positions)
    {
        if (positions.Length == 0)
        {
            return;
        }

        ComputeBounds(positions, out var min, out var max);
        var center = (min + max) * 0.5f;
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] -= center;
        }
    }

    private static void ApplyScaling(Vector3[] positions, float targetRadius)
    {
        if (positions.Length == 0)
        {
            return;
        }

        var radius = ComputeRadius(positions);
        if (radius <= 1e-6f)
        {
            return;
        }

        var scale = targetRadius / radius;
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] *= scale;
        }
    }

    private static void ComputeBounds(IReadOnlyList<Vector3> positions, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
    }

    private static float ComputeRadius(IReadOnlyList<Vector3> positions)
    {
        float maxSq = 0f;
        for (int i = 0; i < positions.Count; i++)
        {
            var lenSq = positions[i].LengthSquared();
            if (lenSq > maxSq)
            {
                maxSq = lenSq;
            }
        }

        return MathF.Sqrt(maxSq);
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

    private static Vector4[]? ComputeTangents(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<Vector3> normals,
        IReadOnlyList<Vector2> uvs,
        IReadOnlyList<int> indices)
    {
        if (positions.Count == 0 || indices.Count < 3)
        {
            return null;
        }

        var tan1 = new Vector3[positions.Count];
        var tan2 = new Vector3[positions.Count];

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
            var uv0 = uvs[i0];
            var uv1 = uvs[i1];
            var uv2 = uvs[i2];

            var dp1 = p1 - p0;
            var dp2 = p2 - p0;
            var duv1 = uv1 - uv0;
            var duv2 = uv2 - uv0;

            float denom = duv1.X * duv2.Y - duv1.Y * duv2.X;
            if (MathF.Abs(denom) <= 1e-8f)
            {
                continue;
            }

            float inv = 1f / denom;
            var sdir = (dp1 * duv2.Y - dp2 * duv1.Y) * inv;
            var tdir = (dp2 * duv1.X - dp1 * duv2.X) * inv;

            tan1[i0] += sdir;
            tan1[i1] += sdir;
            tan1[i2] += sdir;

            tan2[i0] += tdir;
            tan2[i1] += tdir;
            tan2[i2] += tdir;
        }

        var tangents = new Vector4[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            var n = normals[i];
            var t = tan1[i];
            if (t.LengthSquared() <= 1e-8f)
            {
                tangents[i] = new Vector4(1f, 0f, 0f, 1f);
                continue;
            }

            var tangent = Vector3.Normalize(t - n * Vector3.Dot(n, t));
            float w = Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f ? -1f : 1f;
            tangents[i] = new Vector4(tangent, w);
        }

        return tangents;
    }

    private static Vector3[]? ComputeBitangents(IReadOnlyList<Vector3> normals, IReadOnlyList<Vector4>? tangents)
    {
        if (tangents is null || tangents.Count == 0)
        {
            return null;
        }

        var bitangents = new Vector3[normals.Count];
        int count = Math.Min(normals.Count, tangents.Count);
        for (int i = 0; i < count; i++)
        {
            var n = normals[i];
            var t4 = tangents[i];
            var t = new Vector3(t4.X, t4.Y, t4.Z);
            if (n.LengthSquared() > 1e-8f && t.LengthSquared() > 1e-8f)
            {
                var b = Vector3.Cross(n, t) * t4.W;
                if (b.LengthSquared() > 1e-8f)
                {
                    b = Vector3.Normalize(b);
                }
                bitangents[i] = b;
            }
            else
            {
                bitangents[i] = Vector3.Zero;
            }
        }

        return bitangents;
    }
}
