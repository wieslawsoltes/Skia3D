using System;
using System.Collections.Generic;
using System.Numerics;

namespace Skia3D.Geometry;

public sealed class MeshSimplifyOptions
{
    public float TargetRatio { get; set; } = 0.5f;

    public int? TargetTriangleCount { get; set; }

    public int? TargetVertexCount { get; set; }

    public float? CellSize { get; set; }

    public bool RecalculateNormals { get; set; }

    public MeshSimplifyOptions Clone()
    {
        return new MeshSimplifyOptions
        {
            TargetRatio = TargetRatio,
            TargetTriangleCount = TargetTriangleCount,
            TargetVertexCount = TargetVertexCount,
            CellSize = CellSize,
            RecalculateNormals = RecalculateNormals
        };
    }
}

public static class MeshSimplifier
{
    public static MeshData Simplify(MeshData source, MeshSimplifyOptions options)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (source.VertexCount == 0 || source.TriangleCount == 0)
        {
            return source;
        }

        float ratio = Math.Clamp(options.TargetRatio, 0.01f, 1f);
        if (options.TargetTriangleCount.HasValue && options.TargetTriangleCount.Value > 0)
        {
            ratio = Math.Clamp(options.TargetTriangleCount.Value / (float)source.TriangleCount, 0.01f, 1f);
        }

        int targetVertexCount = options.TargetVertexCount.HasValue && options.TargetVertexCount.Value > 0
            ? Math.Min(source.VertexCount, options.TargetVertexCount.Value)
            : Math.Max(4, (int)MathF.Round(source.VertexCount * ratio));

        if (targetVertexCount >= source.VertexCount)
        {
            return source;
        }

        var positions = source.GetPositionsArray();
        var indices = source.Indices;
        var aabb = GeometryKernels.ComputeAabb(positions);
        var size = aabb.Max - aabb.Min;
        float cellSize = options.CellSize ?? ComputeCellSize(size, targetVertexCount);
        if (cellSize <= 1e-6f)
        {
            return source;
        }

        var invCell = 1f / cellSize;
        var remap = new int[positions.Length];
        var clusterMap = new Dictionary<CellKey, int>(positions.Length);
        var clusterPositions = new List<Vector3>(targetVertexCount);
        var clusterCounts = new List<int>(targetVertexCount);

        var srcNormals = source.Attributes?.Normals;
        var srcTex = source.Attributes?.TexCoords;
        var srcColors = source.Attributes?.Colors;
        var srcTangents = source.Attributes?.Tangents;
        var srcBitangents = source.Attributes?.Bitangents;
        var srcSkinWeights = source.Attributes?.SkinWeights;
        var srcSkinIndices = source.Attributes?.SkinIndices;
        var srcMorphTargets = source.Attributes?.MorphTargets;

        var clusterNormals = srcNormals != null ? new List<Vector3>(targetVertexCount) : null;
        var clusterTex = srcTex != null ? new List<Vector2>(targetVertexCount) : null;
        var clusterColors = srcColors != null ? new List<Vector4>(targetVertexCount) : null;
        var clusterTangents = srcTangents != null ? new List<Vector3>(targetVertexCount) : null;
        var clusterTangentSigns = srcTangents != null ? new List<float>(targetVertexCount) : null;
        var clusterBitangents = srcBitangents != null ? new List<Vector3>(targetVertexCount) : null;
        var clusterSkin = (srcSkinWeights != null && srcSkinIndices != null) ? new List<Dictionary<int, float>>(targetVertexCount) : null;

        List<Vector3>?[]? morphPositions = null;
        List<Vector3>?[]? morphNormals = null;
        List<Vector4>?[]? morphTangents = null;
        if (srcMorphTargets != null && srcMorphTargets.Length > 0)
        {
            morphPositions = new List<Vector3>?[srcMorphTargets.Length];
            morphNormals = new List<Vector3>?[srcMorphTargets.Length];
            morphTangents = new List<Vector4>?[srcMorphTargets.Length];
            for (int i = 0; i < srcMorphTargets.Length; i++)
            {
                var target = srcMorphTargets[i];
                if (target.PositionDeltas != null)
                {
                    morphPositions[i] = new List<Vector3>(targetVertexCount);
                }
                if (target.NormalDeltas != null)
                {
                    morphNormals[i] = new List<Vector3>(targetVertexCount);
                }
                if (target.TangentDeltas != null)
                {
                    morphTangents[i] = new List<Vector4>(targetVertexCount);
                }
            }
        }

        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            var cell = CellKey.From(p, aabb.Min, invCell);

            if (!clusterMap.TryGetValue(cell, out var clusterIndex))
            {
                clusterIndex = clusterPositions.Count;
                clusterMap[cell] = clusterIndex;
                clusterPositions.Add(p);
                clusterCounts.Add(1);

                if (clusterNormals != null)
                {
                    clusterNormals.Add(srcNormals![i]);
                }
                if (clusterTex != null)
                {
                    clusterTex.Add(srcTex![i]);
                }
                if (clusterColors != null)
                {
                    clusterColors.Add(srcColors![i]);
                }
                if (clusterTangents != null)
                {
                    var t = srcTangents![i];
                    clusterTangents.Add(new Vector3(t.X, t.Y, t.Z));
                    clusterTangentSigns!.Add(t.W);
                }
                if (clusterBitangents != null)
                {
                    clusterBitangents.Add(srcBitangents![i]);
                }
                if (clusterSkin != null)
                {
                    clusterSkin.Add(new Dictionary<int, float>());
                    AccumulateSkinWeights(clusterSkin[clusterIndex], srcSkinIndices![i], srcSkinWeights![i]);
                }
                if (morphPositions != null)
                {
                    for (int t = 0; t < morphPositions.Length; t++)
                    {
                        if (morphPositions[t] != null)
                        {
                            morphPositions[t]!.Add(srcMorphTargets![t].PositionDeltas![i]);
                        }
                        if (morphNormals?[t] != null)
                        {
                            morphNormals[t]!.Add(srcMorphTargets![t].NormalDeltas![i]);
                        }
                        if (morphTangents?[t] != null)
                        {
                            morphTangents[t]!.Add(srcMorphTargets![t].TangentDeltas![i]);
                        }
                    }
                }
            }
            else
            {
                clusterPositions[clusterIndex] += p;
                clusterCounts[clusterIndex] += 1;

                if (clusterNormals != null)
                {
                    clusterNormals[clusterIndex] += srcNormals![i];
                }
                if (clusterTex != null)
                {
                    clusterTex[clusterIndex] += srcTex![i];
                }
                if (clusterColors != null)
                {
                    clusterColors[clusterIndex] += srcColors![i];
                }
                if (clusterTangents != null)
                {
                    var t = srcTangents![i];
                    clusterTangents[clusterIndex] += new Vector3(t.X, t.Y, t.Z);
                    clusterTangentSigns![clusterIndex] += t.W;
                }
                if (clusterBitangents != null)
                {
                    clusterBitangents[clusterIndex] += srcBitangents![i];
                }
                if (clusterSkin != null)
                {
                    AccumulateSkinWeights(clusterSkin[clusterIndex], srcSkinIndices![i], srcSkinWeights![i]);
                }
                if (morphPositions != null)
                {
                    for (int t = 0; t < morphPositions.Length; t++)
                    {
                        if (morphPositions[t] != null)
                        {
                            morphPositions[t]![clusterIndex] += srcMorphTargets![t].PositionDeltas![i];
                        }
                        if (morphNormals?[t] != null)
                        {
                            morphNormals[t]![clusterIndex] += srcMorphTargets![t].NormalDeltas![i];
                        }
                        if (morphTangents?[t] != null)
                        {
                            morphTangents[t]![clusterIndex] += srcMorphTargets![t].TangentDeltas![i];
                        }
                    }
                }
            }

            remap[i] = clusterIndex;
        }

        var newPositions = new Vector3[clusterPositions.Count];
        for (int i = 0; i < clusterPositions.Count; i++)
        {
            newPositions[i] = clusterPositions[i] / clusterCounts[i];
        }

        Vector3[]? newNormals = null;
        Vector2[]? newTex = null;
        Vector4[]? newColors = null;
        Vector4[]? newTangents = null;
        Vector3[]? newBitangents = null;
        Vector4[]? newSkinWeights = null;
        Int4[]? newSkinIndices = null;
        MeshMorphTarget[]? newMorphTargets = null;

        if (!options.RecalculateNormals && clusterNormals != null)
        {
            newNormals = new Vector3[clusterNormals.Count];
            for (int i = 0; i < clusterNormals.Count; i++)
            {
                var n = clusterNormals[i];
                if (n.LengthSquared() > 1e-8f)
                {
                    n = Vector3.Normalize(n);
                }
                newNormals[i] = n;
            }
        }

        if (clusterTex != null)
        {
            newTex = new Vector2[clusterTex.Count];
            for (int i = 0; i < clusterTex.Count; i++)
            {
                newTex[i] = clusterTex[i] / clusterCounts[i];
            }
        }

        if (clusterColors != null)
        {
            newColors = new Vector4[clusterColors.Count];
            for (int i = 0; i < clusterColors.Count; i++)
            {
                newColors[i] = clusterColors[i] / clusterCounts[i];
            }
        }

        if (clusterTangents != null)
        {
            newTangents = new Vector4[clusterTangents.Count];
            for (int i = 0; i < clusterTangents.Count; i++)
            {
                var t = clusterTangents[i];
                if (t.LengthSquared() > 1e-8f)
                {
                    t = Vector3.Normalize(t);
                }
                else
                {
                    t = Vector3.UnitX;
                }

                float sign = clusterTangentSigns != null && clusterTangentSigns.Count > i && clusterTangentSigns[i] < 0f ? -1f : 1f;
                newTangents[i] = new Vector4(t, sign);
            }
        }

        if (clusterBitangents != null)
        {
            newBitangents = new Vector3[clusterBitangents.Count];
            for (int i = 0; i < clusterBitangents.Count; i++)
            {
                var b = clusterBitangents[i];
                if (b.LengthSquared() > 1e-8f)
                {
                    b = Vector3.Normalize(b);
                }
                newBitangents[i] = b;
            }
        }

        if (clusterSkin != null)
        {
            newSkinWeights = new Vector4[clusterSkin.Count];
            newSkinIndices = new Int4[clusterSkin.Count];
            for (int i = 0; i < clusterSkin.Count; i++)
            {
                BuildSkinWeights(clusterSkin[i], out var indices4, out var weights4);
                newSkinIndices[i] = indices4;
                newSkinWeights[i] = weights4;
            }
        }

        if (morphPositions != null && srcMorphTargets != null)
        {
            newMorphTargets = new MeshMorphTarget[srcMorphTargets.Length];
            for (int t = 0; t < srcMorphTargets.Length; t++)
            {
                Vector3[]? pos = null;
                Vector3[]? norm = null;
                Vector4[]? tan = null;
                if (morphPositions[t] != null)
                {
                    pos = new Vector3[morphPositions[t]!.Count];
                    for (int i = 0; i < pos.Length; i++)
                    {
                        pos[i] = morphPositions[t]![i] / clusterCounts[i];
                    }
                }
                if (morphNormals?[t] != null)
                {
                    norm = new Vector3[morphNormals[t]!.Count];
                    for (int i = 0; i < norm.Length; i++)
                    {
                        norm[i] = morphNormals[t]![i] / clusterCounts[i];
                    }
                }
                if (morphTangents?[t] != null)
                {
                    tan = new Vector4[morphTangents[t]!.Count];
                    for (int i = 0; i < tan.Length; i++)
                    {
                        tan[i] = morphTangents[t]![i] / clusterCounts[i];
                    }
                }

                newMorphTargets[t] = new MeshMorphTarget(srcMorphTargets[t].Name, pos, norm, tan);
            }
        }

        var newIndices = new List<int>(indices.Length);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i];
            int b = indices[i + 1];
            int c = indices[i + 2];
            if (!IsValid(a, remap.Length) || !IsValid(b, remap.Length) || !IsValid(c, remap.Length))
            {
                continue;
            }

            int r0 = remap[a];
            int r1 = remap[b];
            int r2 = remap[c];
            if (r0 == r1 || r1 == r2 || r0 == r2)
            {
                continue;
            }

            newIndices.Add(r0);
            newIndices.Add(r1);
            newIndices.Add(r2);
        }

        CompactVertices(
            newIndices,
            ref newPositions,
            ref newNormals,
            ref newTex,
            ref newColors,
            ref newTangents,
            ref newBitangents,
            ref newSkinWeights,
            ref newSkinIndices,
            ref newMorphTargets);

        if (options.RecalculateNormals)
        {
            newNormals = ComputeNormals(newPositions, newIndices);
        }

        var attributes = new MeshAttributes(newNormals, newTex, newColors, newTangents, newBitangents, newSkinWeights, newSkinIndices, newMorphTargets);
        return MeshData.FromPositions(newPositions, newIndices.ToArray(), attributes);
    }

    public static IReadOnlyList<MeshData> GenerateLodChain(MeshData source, ReadOnlySpan<float> ratios, MeshSimplifyOptions? baseOptions = null)
    {
        var list = new List<MeshData>(ratios.Length + 1) { source };
        if (ratios.Length == 0)
        {
            return list;
        }

        foreach (var ratio in ratios)
        {
            var options = baseOptions?.Clone() ?? new MeshSimplifyOptions();
            options.TargetRatio = ratio;
            list.Add(Simplify(source, options));
        }

        return list;
    }

    private static void CompactVertices(
        List<int> indices,
        ref Vector3[] positions,
        ref Vector3[]? normals,
        ref Vector2[]? texCoords,
        ref Vector4[]? colors,
        ref Vector4[]? tangents,
        ref Vector3[]? bitangents,
        ref Vector4[]? skinWeights,
        ref Int4[]? skinIndices,
        ref MeshMorphTarget[]? morphTargets)
    {
        if (indices.Count == 0)
        {
            return;
        }

        var used = new bool[positions.Length];
        for (int i = 0; i < indices.Count; i++)
        {
            used[indices[i]] = true;
        }

        int newCount = 0;
        var remap = new int[positions.Length];
        for (int i = 0; i < used.Length; i++)
        {
            if (used[i])
            {
                remap[i] = newCount++;
            }
            else
            {
                remap[i] = -1;
            }
        }

        if (newCount == positions.Length)
        {
            return;
        }

        var newPositions = new Vector3[newCount];
        Vector3[]? newNormals = normals != null ? new Vector3[newCount] : null;
        Vector2[]? newTex = texCoords != null ? new Vector2[newCount] : null;
        Vector4[]? newColors = colors != null ? new Vector4[newCount] : null;
        Vector4[]? newTangents = tangents != null ? new Vector4[newCount] : null;
        Vector3[]? newBitangents = bitangents != null ? new Vector3[newCount] : null;
        Vector4[]? newSkinWeights = skinWeights != null ? new Vector4[newCount] : null;
        Int4[]? newSkinIndices = skinIndices != null ? new Int4[newCount] : null;
        MeshMorphTarget[]? newMorphTargets = null;
        if (morphTargets != null)
        {
            newMorphTargets = new MeshMorphTarget[morphTargets.Length];
            for (int t = 0; t < morphTargets.Length; t++)
            {
                var target = morphTargets[t];
                Vector3[]? pos = target.PositionDeltas != null ? new Vector3[newCount] : null;
                Vector3[]? norm = target.NormalDeltas != null ? new Vector3[newCount] : null;
                Vector4[]? tan = target.TangentDeltas != null ? new Vector4[newCount] : null;
                newMorphTargets[t] = new MeshMorphTarget(target.Name, pos, norm, tan);
            }
        }

        for (int i = 0; i < positions.Length; i++)
        {
            int dst = remap[i];
            if (dst < 0)
            {
                continue;
            }

            newPositions[dst] = positions[i];
            if (newNormals != null)
            {
                newNormals[dst] = normals![i];
            }
            if (newTex != null)
            {
                newTex[dst] = texCoords![i];
            }
            if (newColors != null)
            {
                newColors[dst] = colors![i];
            }
            if (newTangents != null)
            {
                newTangents[dst] = tangents![i];
            }
            if (newBitangents != null)
            {
                newBitangents[dst] = bitangents![i];
            }
            if (newSkinWeights != null)
            {
                newSkinWeights[dst] = skinWeights![i];
            }
            if (newSkinIndices != null)
            {
                newSkinIndices[dst] = skinIndices![i];
            }
            if (newMorphTargets != null)
            {
                for (int t = 0; t < newMorphTargets.Length; t++)
                {
                    var target = newMorphTargets[t];
                    if (target.PositionDeltas != null)
                    {
                        target.PositionDeltas[dst] = morphTargets![t].PositionDeltas![i];
                    }
                    if (target.NormalDeltas != null)
                    {
                        target.NormalDeltas[dst] = morphTargets![t].NormalDeltas![i];
                    }
                    if (target.TangentDeltas != null)
                    {
                        target.TangentDeltas[dst] = morphTargets![t].TangentDeltas![i];
                    }
                }
            }
        }

        for (int i = 0; i < indices.Count; i++)
        {
            indices[i] = remap[indices[i]];
        }

        positions = newPositions;
        normals = newNormals;
        texCoords = newTex;
        colors = newColors;
        tangents = newTangents;
        bitangents = newBitangents;
        skinWeights = newSkinWeights;
        skinIndices = newSkinIndices;
        morphTargets = newMorphTargets;
    }

    private static float ComputeCellSize(Vector3 size, int targetVertices)
    {
        var volume = size.X * size.Y * size.Z;
        if (volume > 1e-6f)
        {
            return MathF.Pow(volume / targetVertices, 1f / 3f);
        }

        var maxExtent = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        if (maxExtent <= 1e-6f)
        {
            return 0f;
        }

        return maxExtent / MathF.Pow(targetVertices, 1f / 3f);
    }

    private static Vector3[] ComputeNormals(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices)
    {
        var normals = new Vector3[positions.Count];
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if (!IsValid(i0, positions.Count) || !IsValid(i1, positions.Count) || !IsValid(i2, positions.Count))
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

    private static void AccumulateSkinWeights(Dictionary<int, float> weights, Int4 joints, Vector4 values)
    {
        AddWeight(weights, joints.X, values.X);
        AddWeight(weights, joints.Y, values.Y);
        AddWeight(weights, joints.Z, values.Z);
        AddWeight(weights, joints.W, values.W);
    }

    private static void AddWeight(Dictionary<int, float> weights, int joint, float weight)
    {
        if (weight <= 0f)
        {
            return;
        }

        if (weights.TryGetValue(joint, out var current))
        {
            weights[joint] = current + weight;
        }
        else
        {
            weights[joint] = weight;
        }
    }

    private static void BuildSkinWeights(Dictionary<int, float> weights, out Int4 joints, out Vector4 values)
    {
        Span<int> topJoints = stackalloc int[4];
        Span<float> topWeights = stackalloc float[4];

        foreach (var pair in weights)
        {
            var weight = pair.Value;
            for (int i = 0; i < 4; i++)
            {
                if (weight > topWeights[i])
                {
                    for (int j = 3; j > i; j--)
                    {
                        topWeights[j] = topWeights[j - 1];
                        topJoints[j] = topJoints[j - 1];
                    }

                    topWeights[i] = weight;
                    topJoints[i] = pair.Key;
                    break;
                }
            }
        }

        float sum = topWeights[0] + topWeights[1] + topWeights[2] + topWeights[3];
        if (sum > 1e-6f)
        {
            float inv = 1f / sum;
            for (int i = 0; i < 4; i++)
            {
                topWeights[i] *= inv;
            }
        }
        else
        {
            topWeights[0] = 0f;
            topWeights[1] = 0f;
            topWeights[2] = 0f;
            topWeights[3] = 0f;
            topJoints[0] = 0;
            topJoints[1] = 0;
            topJoints[2] = 0;
            topJoints[3] = 0;
        }

        joints = new Int4(topJoints[0], topJoints[1], topJoints[2], topJoints[3]);
        values = new Vector4(topWeights[0], topWeights[1], topWeights[2], topWeights[3]);
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;

    private readonly struct CellKey : IEquatable<CellKey>
    {
        public CellKey(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }

        public int Y { get; }

        public int Z { get; }

        public static CellKey From(Vector3 position, Vector3 min, float invCell)
        {
            var x = (int)MathF.Floor((position.X - min.X) * invCell);
            var y = (int)MathF.Floor((position.Y - min.Y) * invCell);
            var z = (int)MathF.Floor((position.Z - min.Z) * invCell);
            return new CellKey(x, y, z);
        }

        public bool Equals(CellKey other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object? obj) => obj is CellKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    }
}
