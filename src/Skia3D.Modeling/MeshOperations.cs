using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Geometry;

namespace Skia3D.Modeling;

public static class MeshOperations
{
    public static void ApplyTransform(EditableMesh mesh, Matrix4x4 transform, IReadOnlyCollection<int>? selection = null)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        var normals = mesh.Normals;
        Matrix4x4 normalMatrix = default;
        bool updateNormals = normals != null && normals.Count == mesh.Positions.Count;
        if (updateNormals)
        {
            if (!Matrix4x4.Invert(transform, out var inverse))
            {
                normalMatrix = transform;
            }
            else
            {
                normalMatrix = Matrix4x4.Transpose(inverse);
            }
        }

        if (selection is null)
        {
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                mesh.Positions[i] = Vector3.Transform(mesh.Positions[i], transform);
            }
            if (updateNormals)
            {
                for (int i = 0; i < normals!.Count; i++)
                {
                    normals[i] = TransformNormal(normals[i], normalMatrix);
                }
            }
            return;
        }

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)mesh.Positions.Count)
            {
                continue;
            }

            mesh.Positions[index] = Vector3.Transform(mesh.Positions[index], transform);
            if (updateNormals)
            {
                normals![index] = TransformNormal(normals[index], normalMatrix);
            }
        }
    }

    public static void Translate(EditableMesh mesh, Vector3 delta, IReadOnlyCollection<int>? selection = null)
    {
        ApplyTransform(mesh, Matrix4x4.CreateTranslation(delta), selection);
    }

    public static void Scale(EditableMesh mesh, Vector3 scale, IReadOnlyCollection<int>? selection = null)
    {
        ApplyTransform(mesh, Matrix4x4.CreateScale(scale), selection);
    }

    public static void Rotate(EditableMesh mesh, Vector3 axis, float angleRadians, IReadOnlyCollection<int>? selection = null)
    {
        if (axis.LengthSquared() < 1e-8f)
        {
            return;
        }

        var rotation = Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), angleRadians);
        ApplyTransform(mesh, rotation, selection);
    }

    public static int WeldVertices(EditableMesh mesh, float tolerance, bool removeDegenerateTriangles = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (mesh.Positions.Count == 0 || mesh.Indices.Count == 0 || tolerance <= 0f)
        {
            return 0;
        }

        var tolSq = tolerance * tolerance;
        var invCell = 1f / tolerance;
        var positions = mesh.Positions;
        var indices = mesh.Indices;
        var uvs = mesh.UVs;
        var colors = GetValidColors(mesh);
        mesh.EnsureUvFaceGroups();
        var faceGroups = mesh.UvFaceGroups;
        int triCount = indices.Count / 3;

        var remap = new int[positions.Count];
        var newPositions = new List<Vector3>(positions.Count);
        var newUVs = new List<Vector2>(positions.Count);
        var newColors = colors != null ? new List<Vector4>(positions.Count) : null;
        var colorCounts = newColors != null ? new List<int>(positions.Count) : null;
        var cellMap = new Dictionary<CellKey, List<int>>();
        int merged = 0;

        for (int i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            var uv = i < uvs.Count ? uvs[i] : Vector2.Zero;
            var cell = CellKey.From(p, invCell);
            int existing = FindExisting(cellMap, newPositions, cell, p, tolSq);

            if (existing >= 0)
            {
                remap[i] = existing;
                merged++;
                if (newColors != null && colorCounts != null)
                {
                    newColors[existing] += colors![i];
                    colorCounts[existing]++;
                }
            }
            else
            {
                var newIndex = newPositions.Count;
                newPositions.Add(p);
                newUVs.Add(uv);
                remap[i] = newIndex;
                if (newColors != null && colorCounts != null)
                {
                    newColors.Add(colors![i]);
                    colorCounts.Add(1);
                }

                if (!cellMap.TryGetValue(cell, out var list))
                {
                    list = new List<int>();
                    cellMap[cell] = list;
                }

                list.Add(newIndex);
            }
        }

        var newIndices = new List<int>(indices.Count);
        var newFaceGroups = new List<int>(triCount);
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if ((uint)i0 >= (uint)remap.Length || (uint)i1 >= (uint)remap.Length || (uint)i2 >= (uint)remap.Length)
            {
                continue;
            }

            var r0 = remap[i0];
            var r1 = remap[i1];
            var r2 = remap[i2];

            if (removeDegenerateTriangles && (r0 == r1 || r1 == r2 || r0 == r2))
            {
                continue;
            }

            newIndices.Add(r0);
            newIndices.Add(r1);
            newIndices.Add(r2);
            int faceIndex = i / 3;
            if ((uint)faceIndex < (uint)faceGroups.Count)
            {
                newFaceGroups.Add(faceGroups[faceIndex]);
            }
        }

        positions.Clear();
        positions.AddRange(newPositions);
        uvs.Clear();
        uvs.AddRange(newUVs);
        if (newColors != null && colorCounts != null)
        {
            for (int i = 0; i < newColors.Count; i++)
            {
                int count = colorCounts[i];
                if (count > 1)
                {
                    newColors[i] *= 1f / count;
                }
            }
            mesh.SetColors(newColors);
        }
        indices.Clear();
        indices.AddRange(newIndices);
        mesh.UvFaceGroups.Clear();
        mesh.UvFaceGroups.AddRange(newFaceGroups);
        RemapSeamEdges(mesh, remap);
        mesh.InvalidateNormals();

        return merged;
    }

    public static int RemoveDegenerateTriangles(EditableMesh mesh, float areaEpsilon = 1e-8f, bool removeUnusedVertices = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (mesh.Indices.Count == 0 || mesh.Positions.Count == 0)
        {
            return 0;
        }

        var positions = mesh.Positions;
        var indices = mesh.Indices;
        mesh.EnsureUvFaceGroups();
        var faceGroups = mesh.UvFaceGroups;
        var cleaned = new List<int>(indices.Count);
        var newFaceGroups = new List<int>(indices.Count / 3);
        int removed = 0;
        var areaThreshold = MathF.Max(areaEpsilon, 1e-12f);

        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                removed++;
                continue;
            }

            if (i0 == i1 || i1 == i2 || i2 == i0)
            {
                removed++;
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.LengthSquared() < areaThreshold)
            {
                removed++;
                continue;
            }

            cleaned.Add(i0);
            cleaned.Add(i1);
            cleaned.Add(i2);
            int faceIndex = i / 3;
            if ((uint)faceIndex < (uint)faceGroups.Count)
            {
                newFaceGroups.Add(faceGroups[faceIndex]);
            }
        }

        if (removed == 0)
        {
            return 0;
        }

        indices.Clear();
        indices.AddRange(cleaned);
        mesh.UvFaceGroups.Clear();
        mesh.UvFaceGroups.AddRange(newFaceGroups);

        if (removeUnusedVertices)
        {
            RemoveUnusedVertices(mesh);
        }
        else
        {
            mesh.InvalidateNormals();
        }

        return removed;
    }

    public static int RemoveUnusedVertices(EditableMesh mesh)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        int vertexCount = mesh.Positions.Count;
        if (vertexCount == 0)
        {
            return 0;
        }

        var used = new bool[vertexCount];
        for (int i = 0; i < mesh.Indices.Count; i++)
        {
            int idx = mesh.Indices[i];
            if ((uint)idx < (uint)vertexCount)
            {
                used[idx] = true;
            }
        }

        var remap = new int[vertexCount];
        int newCount = 0;
        for (int i = 0; i < vertexCount; i++)
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

        if (newCount == vertexCount)
        {
            return 0;
        }

        var newPositions = new List<Vector3>(newCount);
        var newUvs = new List<Vector2>(newCount);
        var colors = GetValidColors(mesh);
        var newColors = colors != null ? new List<Vector4>(newCount) : null;
        for (int i = 0; i < vertexCount; i++)
        {
            if (!used[i])
            {
                continue;
            }

            newPositions.Add(mesh.Positions[i]);
            newUvs.Add(mesh.UVs[i]);
            if (newColors != null)
            {
                newColors.Add(colors![i]);
            }
        }

        for (int i = 0; i < mesh.Indices.Count; i++)
        {
            int idx = mesh.Indices[i];
            mesh.Indices[i] = (uint)idx < (uint)vertexCount ? remap[idx] : -1;
        }

        mesh.Positions.Clear();
        mesh.Positions.AddRange(newPositions);
        mesh.UVs.Clear();
        mesh.UVs.AddRange(newUvs);
        if (newColors != null)
        {
            mesh.SetColors(newColors);
        }
        RemapSeamEdges(mesh, remap);
        mesh.InvalidateNormals();

        return vertexCount - newCount;
    }

    public static bool Smooth(EditableMesh mesh, int iterations, float lambda, bool preserveBoundary = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (mesh.Positions.Count == 0 || mesh.Indices.Count == 0)
        {
            return false;
        }

        iterations = Math.Clamp(iterations, 1, 50);
        lambda = Math.Clamp(lambda, 0f, 1f);

        var positions = mesh.Positions.ToArray();
        var indices = mesh.Indices.ToArray();
        var adjacency = MeshAdjacencyBuilder.BuildVertexAdjacency(positions.Length, indices);
        var smoothed = MeshFilters.LaplacianSmooth(positions, adjacency, iterations, lambda);

        if (preserveBoundary)
        {
            var boundary = BuildBoundaryMask(mesh);
            for (int i = 0; i < boundary.Length; i++)
            {
                if (boundary[i])
                {
                    smoothed[i] = positions[i];
                }
            }
        }

        mesh.Positions.Clear();
        mesh.Positions.AddRange(smoothed);
        mesh.InvalidateNormals();
        return true;
    }

    public static bool Simplify(EditableMesh mesh, MeshSimplifyOptions options)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (mesh.Positions.Count == 0 || mesh.Indices.Count == 0)
        {
            return false;
        }

        var positions = mesh.Positions.ToArray();
        var indices = mesh.Indices.ToArray();
        MeshAttributes? attributes = null;
        var hadColors = mesh.HasColors;
        var normals = mesh.HasNormals ? mesh.Normals!.ToArray() : null;
        var texcoords = mesh.UVs.Count == positions.Length ? mesh.UVs.ToArray() : null;
        var colors = mesh.HasColors ? mesh.Colors!.ToArray() : null;
        if (normals != null || texcoords != null || colors != null)
        {
            attributes = new MeshAttributes(normals, texcoords, colors);
        }

        var data = MeshData.FromPositions(positions, indices, attributes);
        var simplified = MeshSimplifier.Simplify(data, options);

        var newPositions = simplified.GetPositionsArray();
        var newIndices = simplified.Indices;
        var newUvs = simplified.Attributes?.TexCoords;
        var newNormals = simplified.Attributes?.Normals;
        var newColors = simplified.Attributes?.Colors;

        mesh.Positions.Clear();
        mesh.Positions.AddRange(newPositions);
        mesh.Indices.Clear();
        mesh.Indices.AddRange(newIndices);
        mesh.UVs.Clear();
        if (newUvs != null && newUvs.Length == newPositions.Length)
        {
            mesh.UVs.AddRange(newUvs);
        }
        else
        {
            for (int i = 0; i < newPositions.Length; i++)
            {
                mesh.UVs.Add(Vector2.Zero);
            }
        }

        if (newNormals != null && newNormals.Length == newPositions.Length)
        {
            mesh.SetNormals(newNormals);
        }
        else
        {
            mesh.SetNormals(null);
        }

        if (newColors != null && newColors.Length == newPositions.Length)
        {
            mesh.SetColors(newColors);
        }
        else if (hadColors)
        {
            var defaults = new Vector4[newPositions.Length];
            for (int i = 0; i < defaults.Length; i++)
            {
                defaults[i] = Vector4.One;
            }
            mesh.SetColors(defaults);
        }
        else
        {
            mesh.SetColors(null);
        }

        mesh.SeamEdges.Clear();
        mesh.UvFaceGroups.Clear();
        mesh.EnsureUvFaceGroups();

        return true;
    }

    private static bool[] BuildBoundaryMask(EditableMesh mesh)
    {
        var adjacency = MeshAdjacency.Build(mesh);
        var boundary = new bool[mesh.VertexCount];

        foreach (var edge in adjacency.Edges.Values)
        {
            if (!edge.IsBoundary)
            {
                continue;
            }

            boundary[edge.Edge.A] = true;
            boundary[edge.Edge.B] = true;
        }

        return boundary;
    }

    public static int SplitEdge(EditableMesh mesh, EdgeKey edge, float t = 0.5f)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (mesh.Positions.Count == 0 || mesh.Indices.Count == 0)
        {
            return -1;
        }

        if (edge.A == edge.B)
        {
            return -1;
        }

        if ((uint)edge.A >= (uint)mesh.Positions.Count || (uint)edge.B >= (uint)mesh.Positions.Count)
        {
            return -1;
        }

        t = Math.Clamp(t, 0f, 1f);
        var positions = mesh.Positions;
        var indices = mesh.Indices;
        var uvs = mesh.UVs;
        var colors = GetValidColors(mesh);
        mesh.EnsureUvFaceGroups();
        var faceGroups = mesh.UvFaceGroups;
        var p0 = positions[edge.A];
        var p1 = positions[edge.B];
        var newPos = Vector3.Lerp(p0, p1, t);
        var uv0 = edge.A < uvs.Count ? uvs[edge.A] : Vector2.Zero;
        var uv1 = edge.B < uvs.Count ? uvs[edge.B] : Vector2.Zero;
        var newUv = Vector2.Lerp(uv0, uv1, t);
        var newColor = colors != null ? Vector4.Lerp(colors[edge.A], colors[edge.B], t) : default;
        var newIndex = positions.Count;
        positions.Add(newPos);
        uvs.Add(newUv);
        if (colors != null)
        {
            colors.Add(newColor);
        }

        var newIndices = new List<int>(indices.Count + 6);
        var newFaceGroups = new List<int>(indices.Count / 3 + 2);
        bool splitAny = false;

        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int a = indices[i];
            int b = indices[i + 1];
            int c = indices[i + 2];
            int faceIndex = i / 3;
            int group = (uint)faceIndex < (uint)faceGroups.Count ? faceGroups[faceIndex] : 0;

            if (IsEdge(a, b, edge))
            {
                AddSplit(newIndices, a, b, c, newIndex);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                splitAny = true;
                continue;
            }

            if (IsEdge(b, c, edge))
            {
                AddSplit(newIndices, b, c, a, newIndex);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                splitAny = true;
                continue;
            }

            if (IsEdge(c, a, edge))
            {
                AddSplit(newIndices, c, a, b, newIndex);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                splitAny = true;
                continue;
            }

            newIndices.Add(a);
            newIndices.Add(b);
            newIndices.Add(c);
            newFaceGroups.Add(group);
        }

        if (!splitAny)
        {
            positions.RemoveAt(newIndex);
            uvs.RemoveAt(newIndex);
            if (colors != null)
            {
                colors.RemoveAt(newIndex);
            }
            return -1;
        }

        indices.Clear();
        indices.AddRange(newIndices);
        mesh.UvFaceGroups.Clear();
        mesh.UvFaceGroups.AddRange(newFaceGroups);
        var seamEdge = new EdgeKey(edge.A, edge.B);
        if (mesh.SeamEdges.Remove(seamEdge))
        {
            mesh.SeamEdges.Add(new EdgeKey(edge.A, newIndex));
            mesh.SeamEdges.Add(new EdgeKey(edge.B, newIndex));
        }
        mesh.InvalidateNormals();
        return newIndex;
    }

    public static int ExtrudeFaces(EditableMesh mesh, IReadOnlyCollection<int> faces, float distance, bool keepBase = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (faces is null)
        {
            throw new ArgumentNullException(nameof(faces));
        }

        if (mesh.Positions.Count == 0 || mesh.Indices.Count == 0 || faces.Count == 0)
        {
            return 0;
        }

        var selected = faces as HashSet<int> ?? new HashSet<int>(faces);
        var indices = mesh.Indices;
        var positions = mesh.Positions;
        var uvs = mesh.UVs;
        var colors = GetValidColors(mesh);
        mesh.EnsureUvFaceGroups();
        var faceGroups = mesh.UvFaceGroups;
        int triCount = indices.Count / 3;

        var targets = new List<(int face, int group, int i0, int i1, int i2)>(selected.Count);
        foreach (var face in selected)
        {
            if ((uint)face >= (uint)triCount)
            {
                continue;
            }

            int baseIndex = face * 3;
            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if (IsValid(i0, positions.Count) && IsValid(i1, positions.Count) && IsValid(i2, positions.Count))
            {
                int group = (uint)face < (uint)faceGroups.Count ? faceGroups[face] : 0;
                targets.Add((face, group, i0, i1, i2));
            }
        }

        if (targets.Count == 0)
        {
            return 0;
        }

        var newIndices = new List<int>(indices.Count + targets.Count * 12);
        var newFaceGroups = new List<int>(triCount + targets.Count * 4);
        for (int tri = 0; tri < triCount; tri++)
        {
            if (!keepBase && selected.Contains(tri))
            {
                continue;
            }

            int baseIndex = tri * 3;
            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if (!IsValid(i0, positions.Count) || !IsValid(i1, positions.Count) || !IsValid(i2, positions.Count))
            {
                continue;
            }

            newIndices.Add(i0);
            newIndices.Add(i1);
            newIndices.Add(i2);
            if ((uint)tri < (uint)faceGroups.Count)
            {
                newFaceGroups.Add(faceGroups[tri]);
            }
        }

        var adjacency = MeshAdjacency.Build(mesh);
        foreach (var (face, group, i0, i1, i2) in targets)
        {
            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var uv0 = i0 < uvs.Count ? uvs[i0] : Vector2.Zero;
            var uv1 = i1 < uvs.Count ? uvs[i1] : Vector2.Zero;
            var uv2 = i2 < uvs.Count ? uvs[i2] : Vector2.Zero;
            var c0 = colors != null ? colors[i0] : default;
            var c1 = colors != null ? colors[i1] : default;
            var c2 = colors != null ? colors[i2] : default;
            var normal = Vector3.Cross(p1 - p0, p2 - p0);
            if (normal.LengthSquared() < 1e-8f)
            {
                continue;
            }

            normal = Vector3.Normalize(normal);
            var n0 = positions.Count;
            positions.Add(p0 + normal * distance);
            uvs.Add(uv0);
            if (colors != null)
            {
                colors.Add(c0);
            }
            var n1 = positions.Count;
            positions.Add(p1 + normal * distance);
            uvs.Add(uv1);
            if (colors != null)
            {
                colors.Add(c1);
            }
            var n2 = positions.Count;
            positions.Add(p2 + normal * distance);
            uvs.Add(uv2);
            if (colors != null)
            {
                colors.Add(c2);
            }

            newIndices.Add(n0);
            newIndices.Add(n1);
            newIndices.Add(n2);
            newFaceGroups.Add(group);

            var edges = adjacency.FaceEdges[face];
            if (edges.Length >= 3)
            {
                if (IsBoundaryEdge(edges[0], selected, adjacency.Edges))
                {
                    AddQuad(newIndices, i0, i1, n1, n0);
                    newFaceGroups.Add(group);
                    newFaceGroups.Add(group);
                }
                if (IsBoundaryEdge(edges[1], selected, adjacency.Edges))
                {
                    AddQuad(newIndices, i1, i2, n2, n1);
                    newFaceGroups.Add(group);
                    newFaceGroups.Add(group);
                }
                if (IsBoundaryEdge(edges[2], selected, adjacency.Edges))
                {
                    AddQuad(newIndices, i2, i0, n0, n2);
                    newFaceGroups.Add(group);
                    newFaceGroups.Add(group);
                }
            }
            else
            {
                AddQuad(newIndices, i0, i1, n1, n0);
                AddQuad(newIndices, i1, i2, n2, n1);
                AddQuad(newIndices, i2, i0, n0, n2);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
            }
        }

        indices.Clear();
        indices.AddRange(newIndices);
        mesh.UvFaceGroups.Clear();
        mesh.UvFaceGroups.AddRange(newFaceGroups);
        mesh.InvalidateNormals();
        return targets.Count;
    }

    public static int BevelFaces(EditableMesh mesh, IReadOnlyCollection<int> faces, float insetDistance, float height, bool keepBase = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (faces is null)
        {
            throw new ArgumentNullException(nameof(faces));
        }

        if (mesh.Positions.Count == 0 || mesh.Indices.Count == 0 || faces.Count == 0)
        {
            return 0;
        }

        var selected = faces as HashSet<int> ?? new HashSet<int>(faces);
        var indices = mesh.Indices;
        var positions = mesh.Positions;
        var uvs = mesh.UVs;
        var colors = GetValidColors(mesh);
        mesh.EnsureUvFaceGroups();
        var faceGroups = mesh.UvFaceGroups;
        int triCount = indices.Count / 3;

        var targets = new List<(int face, int group, int i0, int i1, int i2)>(selected.Count);
        foreach (var face in selected)
        {
            if ((uint)face >= (uint)triCount)
            {
                continue;
            }

            int baseIndex = face * 3;
            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if (IsValid(i0, positions.Count) && IsValid(i1, positions.Count) && IsValid(i2, positions.Count))
            {
                int group = (uint)face < (uint)faceGroups.Count ? faceGroups[face] : 0;
                targets.Add((face, group, i0, i1, i2));
            }
        }

        if (targets.Count == 0)
        {
            return 0;
        }

        var newIndices = new List<int>(indices.Count + targets.Count * 12);
        var newFaceGroups = new List<int>(triCount + targets.Count * 4);
        for (int tri = 0; tri < triCount; tri++)
        {
            if (!keepBase && selected.Contains(tri))
            {
                continue;
            }

            int baseIndex = tri * 3;
            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if (!IsValid(i0, positions.Count) || !IsValid(i1, positions.Count) || !IsValid(i2, positions.Count))
            {
                continue;
            }

            newIndices.Add(i0);
            newIndices.Add(i1);
            newIndices.Add(i2);
            if ((uint)tri < (uint)faceGroups.Count)
            {
                newFaceGroups.Add(faceGroups[tri]);
            }
        }

        var adjacency = MeshAdjacency.Build(mesh);
        foreach (var (face, group, i0, i1, i2) in targets)
        {
            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var uv0 = i0 < uvs.Count ? uvs[i0] : Vector2.Zero;
            var uv1 = i1 < uvs.Count ? uvs[i1] : Vector2.Zero;
            var uv2 = i2 < uvs.Count ? uvs[i2] : Vector2.Zero;
            var c0 = colors != null ? colors[i0] : default;
            var c1 = colors != null ? colors[i1] : default;
            var c2 = colors != null ? colors[i2] : default;
            var normal = Vector3.Cross(p1 - p0, p2 - p0);
            if (normal.LengthSquared() < 1e-8f)
            {
                continue;
            }

            normal = Vector3.Normalize(normal);
            var center = (p0 + p1 + p2) / 3f;
            var inset0 = InsetToward(p0, center, insetDistance);
            var inset1 = InsetToward(p1, center, insetDistance);
            var inset2 = InsetToward(p2, center, insetDistance);
            var uvCenter = (uv0 + uv1 + uv2) / 3f;
            var uvInset0 = InsetToward(uv0, uvCenter, insetDistance);
            var uvInset1 = InsetToward(uv1, uvCenter, insetDistance);
            var uvInset2 = InsetToward(uv2, uvCenter, insetDistance);

            var n0 = positions.Count;
            positions.Add(inset0 + normal * height);
            uvs.Add(uvInset0);
            if (colors != null)
            {
                colors.Add(c0);
            }
            var n1 = positions.Count;
            positions.Add(inset1 + normal * height);
            uvs.Add(uvInset1);
            if (colors != null)
            {
                colors.Add(c1);
            }
            var n2 = positions.Count;
            positions.Add(inset2 + normal * height);
            uvs.Add(uvInset2);
            if (colors != null)
            {
                colors.Add(c2);
            }

            newIndices.Add(n0);
            newIndices.Add(n1);
            newIndices.Add(n2);
            newFaceGroups.Add(group);

            var edges = adjacency.FaceEdges[face];
            if (edges.Length >= 3)
            {
                if (IsBoundaryEdge(edges[0], selected, adjacency.Edges))
                {
                    AddQuad(newIndices, i0, i1, n1, n0);
                    newFaceGroups.Add(group);
                    newFaceGroups.Add(group);
                }
                if (IsBoundaryEdge(edges[1], selected, adjacency.Edges))
                {
                    AddQuad(newIndices, i1, i2, n2, n1);
                    newFaceGroups.Add(group);
                    newFaceGroups.Add(group);
                }
                if (IsBoundaryEdge(edges[2], selected, adjacency.Edges))
                {
                    AddQuad(newIndices, i2, i0, n0, n2);
                    newFaceGroups.Add(group);
                    newFaceGroups.Add(group);
                }
            }
            else
            {
                AddQuad(newIndices, i0, i1, n1, n0);
                AddQuad(newIndices, i1, i2, n2, n1);
                AddQuad(newIndices, i2, i0, n0, n2);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
                newFaceGroups.Add(group);
            }
        }

        indices.Clear();
        indices.AddRange(newIndices);
        mesh.UvFaceGroups.Clear();
        mesh.UvFaceGroups.AddRange(newFaceGroups);
        mesh.InvalidateNormals();
        return targets.Count;
    }

    public static int InsetFaces(EditableMesh mesh, IReadOnlyCollection<int> faces, float insetDistance, bool keepBase = true)
    {
        return BevelFaces(mesh, faces, insetDistance, height: 0f, keepBase);
    }

    public static int BridgeEdges(EditableMesh mesh, EdgeKey edgeA, EdgeKey edgeB, bool flip = false)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (mesh.Positions.Count == 0 || mesh.Indices.Count == 0)
        {
            return 0;
        }

        if (!IsValid(edgeA.A, mesh.Positions.Count) || !IsValid(edgeA.B, mesh.Positions.Count) ||
            !IsValid(edgeB.A, mesh.Positions.Count) || !IsValid(edgeB.B, mesh.Positions.Count))
        {
            return 0;
        }

        mesh.EnsureUvFaceGroups();
        var indices = mesh.Indices;
        if (!flip)
        {
            indices.Add(edgeA.A);
            indices.Add(edgeA.B);
            indices.Add(edgeB.B);

            indices.Add(edgeA.A);
            indices.Add(edgeB.B);
            indices.Add(edgeB.A);
        }
        else
        {
            indices.Add(edgeA.A);
            indices.Add(edgeA.B);
            indices.Add(edgeB.A);

            indices.Add(edgeA.B);
            indices.Add(edgeB.B);
            indices.Add(edgeB.A);
        }

        mesh.UvFaceGroups.Add(0);
        mesh.UvFaceGroups.Add(0);
        mesh.InvalidateNormals();
        return 1;
    }

    public static int BridgeEdgePairs(EditableMesh mesh, IReadOnlyList<EdgeKey> edgesA, IReadOnlyList<EdgeKey> edgesB, bool flip = false)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (edgesA is null)
        {
            throw new ArgumentNullException(nameof(edgesA));
        }

        if (edgesB is null)
        {
            throw new ArgumentNullException(nameof(edgesB));
        }

        if (edgesA.Count == 0 || edgesB.Count == 0 || edgesA.Count != edgesB.Count)
        {
            return 0;
        }

        int bridged = 0;
        for (int i = 0; i < edgesA.Count; i++)
        {
            bridged += BridgeEdges(mesh, edgesA[i], edgesB[i], flip);
        }

        return bridged;
    }

    public static int MergeVertices(EditableMesh mesh, IReadOnlyCollection<int> vertices, bool removeDegenerateTriangles = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (vertices is null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        if (vertices.Count < 2 || mesh.Positions.Count == 0)
        {
            return 0;
        }

        var positions = mesh.Positions;
        var uvs = mesh.UVs;
        var colors = GetValidColors(mesh);
        int root = -1;
        var sumPos = Vector3.Zero;
        var sumUv = Vector2.Zero;
        var sumColor = Vector4.Zero;
        int valid = 0;

        foreach (var index in vertices)
        {
            if (!IsValid(index, positions.Count))
            {
                continue;
            }

            if (root < 0)
            {
                root = index;
            }

            sumPos += positions[index];
            if (index < uvs.Count)
            {
                sumUv += uvs[index];
            }
            if (colors != null)
            {
                sumColor += colors[index];
            }
            valid++;
        }

        if (valid < 2 || root < 0)
        {
            return 0;
        }

        var inv = 1f / valid;
        positions[root] = sumPos * inv;
        if (uvs.Count == positions.Count)
        {
            uvs[root] = sumUv * inv;
        }
        if (colors != null)
        {
            colors[root] = sumColor * inv;
        }

        var remap = BuildIdentityRemap(positions.Count);

        int merged = 0;
        foreach (var index in vertices)
        {
            if (!IsValid(index, positions.Count) || index == root)
            {
                continue;
            }

            remap[index] = root;
            merged++;
        }

        if (merged == 0)
        {
            return 0;
        }

        var indices = mesh.Indices;
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];
            if ((uint)idx < (uint)remap.Length)
            {
                indices[i] = remap[idx];
            }
        }
        RemapSeamEdges(mesh, remap);

        if (removeDegenerateTriangles)
        {
            RemoveDegenerateTriangles(mesh, 1e-8f, removeUnusedVertices: true);
        }
        mesh.InvalidateNormals();

        return merged;
    }

    public static int CollapseEdge(EditableMesh mesh, EdgeKey edge, float t = 0.5f, bool removeUnusedVertices = true)
    {
        return CollapseEdges(mesh, new[] { edge }, t, removeUnusedVertices);
    }

    public static int CollapseEdges(EditableMesh mesh, IReadOnlyCollection<EdgeKey> edges, float t = 0.5f, bool removeUnusedVertices = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (edges is null)
        {
            throw new ArgumentNullException(nameof(edges));
        }

        if (edges.Count == 0 || mesh.Positions.Count == 0)
        {
            return 0;
        }

        t = Math.Clamp(t, 0f, 1f);
        var unique = edges as HashSet<EdgeKey> ?? new HashSet<EdgeKey>(edges);
        var remap = BuildIdentityRemap(mesh.Positions.Count);
        int collapsed = 0;
        foreach (var edge in unique)
        {
            if (CollapseEdgeInternal(mesh, edge, t))
            {
                collapsed++;
                if ((uint)edge.A < (uint)remap.Length && (uint)edge.B < (uint)remap.Length)
                {
                    remap[edge.B] = edge.A;
                }
            }
        }

        if (collapsed == 0)
        {
            return 0;
        }

        ResolveRemap(remap);
        RemapSeamEdges(mesh, remap);
        RemoveDegenerateTriangles(mesh, 1e-8f, removeUnusedVertices: false);
        if (removeUnusedVertices)
        {
            RemoveUnusedVertices(mesh);
        }
        mesh.InvalidateNormals();

        return collapsed;
    }

    public static int DissolveEdges(EditableMesh mesh, IReadOnlyCollection<EdgeKey> edges, bool removeUnusedVertices = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (edges is null)
        {
            throw new ArgumentNullException(nameof(edges));
        }

        if (edges.Count == 0 || mesh.Indices.Count == 0)
        {
            return 0;
        }

        var adjacency = MeshAdjacency.Build(mesh);
        var faces = new HashSet<int>();
        foreach (var edge in edges)
        {
            if (!adjacency.Edges.TryGetValue(edge, out var edgeFaces))
            {
                continue;
            }

            foreach (var face in edgeFaces.Faces)
            {
                faces.Add(face);
            }
        }

        if (faces.Count == 0)
        {
            return 0;
        }

        return DissolveFaces(mesh, faces, removeUnusedVertices);
    }

    public static int DissolveFaces(EditableMesh mesh, IReadOnlyCollection<int> faces, bool removeUnusedVertices = true)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (faces is null)
        {
            throw new ArgumentNullException(nameof(faces));
        }

        var indices = mesh.Indices;
        mesh.EnsureUvFaceGroups();
        var faceGroups = mesh.UvFaceGroups;
        int triCount = indices.Count / 3;
        if (triCount == 0 || faces.Count == 0)
        {
            return 0;
        }

        var remove = faces as HashSet<int> ?? new HashSet<int>(faces);
        if (remove.Count == 0)
        {
            return 0;
        }

        var newIndices = new List<int>(indices.Count);
        var newFaceGroups = new List<int>(triCount);
        int removed = 0;
        for (int face = 0; face < triCount; face++)
        {
            if (remove.Contains(face))
            {
                removed++;
                continue;
            }

            int baseIndex = face * 3;
            newIndices.Add(indices[baseIndex]);
            newIndices.Add(indices[baseIndex + 1]);
            newIndices.Add(indices[baseIndex + 2]);
            if ((uint)face < (uint)faceGroups.Count)
            {
                newFaceGroups.Add(faceGroups[face]);
            }
        }

        if (removed == 0)
        {
            return 0;
        }

        indices.Clear();
        indices.AddRange(newIndices);
        mesh.UvFaceGroups.Clear();
        mesh.UvFaceGroups.AddRange(newFaceGroups);

        if (removeUnusedVertices)
        {
            RemoveUnusedVertices(mesh);
        }
        else
        {
            mesh.InvalidateNormals();
        }

        return removed;
    }

    public static int LoopCutEdges(EditableMesh mesh, IReadOnlyCollection<EdgeKey> edges, float t = 0.5f)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (edges is null)
        {
            throw new ArgumentNullException(nameof(edges));
        }

        if (edges.Count == 0)
        {
            return 0;
        }

        var unique = edges as HashSet<EdgeKey> ?? new HashSet<EdgeKey>(edges);
        int splits = 0;
        foreach (var edge in unique)
        {
            if (SplitEdge(mesh, edge, t) >= 0)
            {
                splits++;
            }
        }

        return splits;
    }

    private static bool CollapseEdgeInternal(EditableMesh mesh, EdgeKey edge, float t)
    {
        var positions = mesh.Positions;
        if (!IsValid(edge.A, positions.Count) || !IsValid(edge.B, positions.Count))
        {
            return false;
        }

        if (edge.A == edge.B)
        {
            return false;
        }

        var posA = positions[edge.A];
        var posB = positions[edge.B];
        positions[edge.A] = Vector3.Lerp(posA, posB, t);

        var uvs = mesh.UVs;
        if (uvs.Count == positions.Count)
        {
            var uvA = uvs[edge.A];
            var uvB = uvs[edge.B];
            uvs[edge.A] = Vector2.Lerp(uvA, uvB, t);
        }
        var colors = GetValidColors(mesh);
        if (colors != null && colors.Count == positions.Count)
        {
            var colorA = colors[edge.A];
            var colorB = colors[edge.B];
            colors[edge.A] = Vector4.Lerp(colorA, colorB, t);
        }

        var indices = mesh.Indices;
        for (int i = 0; i < indices.Count; i++)
        {
            if (indices[i] == edge.B)
            {
                indices[i] = edge.A;
            }
        }

        return true;
    }

    private static int FindExisting(Dictionary<CellKey, List<int>> map, List<Vector3> positions, CellKey cell, Vector3 p, float tolSq)
    {
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var key = new CellKey(cell.X + dx, cell.Y + dy, cell.Z + dz);
                    if (!map.TryGetValue(key, out var list))
                    {
                        continue;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        var idx = list[i];
                        var d = positions[idx] - p;
                        if (d.LengthSquared() <= tolSq)
                        {
                            return idx;
                        }
                    }
                }
            }
        }

        return -1;
    }

    private static bool IsEdge(int a, int b, EdgeKey edge)
    {
        return (a == edge.A && b == edge.B) || (a == edge.B && b == edge.A);
    }

    private static void AddSplit(List<int> indices, int a, int b, int c, int split)
    {
        indices.Add(a);
        indices.Add(split);
        indices.Add(c);

        indices.Add(split);
        indices.Add(b);
        indices.Add(c);
    }

    private static void AddQuad(List<int> indices, int a, int b, int c, int d)
    {
        indices.Add(a);
        indices.Add(b);
        indices.Add(c);

        indices.Add(a);
        indices.Add(c);
        indices.Add(d);
    }

    private static bool IsBoundaryEdge(EdgeKey edge, HashSet<int> selectedFaces, IReadOnlyDictionary<EdgeKey, EdgeFaces> edges)
    {
        if (!edges.TryGetValue(edge, out var faces))
        {
            return true;
        }

        if (faces.Faces.Length <= 1)
        {
            return true;
        }

        foreach (var face in faces.Faces)
        {
            if (!selectedFaces.Contains(face))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 InsetToward(Vector3 point, Vector3 center, float insetDistance)
    {
        if (insetDistance <= 0f)
        {
            return point;
        }

        var dir = center - point;
        var len = dir.Length();
        if (len < 1e-6f)
        {
            return point;
        }

        var t = Math.Clamp(insetDistance / len, 0f, 1f);
        return point + dir * t;
    }

    private static Vector2 InsetToward(Vector2 point, Vector2 center, float insetDistance)
    {
        if (insetDistance <= 0f)
        {
            return point;
        }

        var dir = center - point;
        var len = dir.Length();
        if (len < 1e-6f)
        {
            return point;
        }

        var t = Math.Clamp(insetDistance / len, 0f, 1f);
        return point + dir * t;
    }

    private static int[] BuildIdentityRemap(int count)
    {
        var remap = new int[count];
        for (int i = 0; i < count; i++)
        {
            remap[i] = i;
        }

        return remap;
    }

    private static void ResolveRemap(int[] remap)
    {
        for (int i = 0; i < remap.Length; i++)
        {
            int current = remap[i];
            if (current < 0 || current == i)
            {
                continue;
            }

            int guard = 0;
            while (current >= 0 && current < remap.Length && remap[current] != current)
            {
                current = remap[current];
                guard++;
                if (guard > remap.Length)
                {
                    break;
                }
            }

            if (current >= 0 && current < remap.Length)
            {
                remap[i] = current;
            }
        }
    }

    private static void RemapSeamEdges(EditableMesh mesh, int[] remap)
    {
        if (mesh.SeamEdges.Count == 0)
        {
            return;
        }

        var updated = new HashSet<EdgeKey>();
        foreach (var edge in mesh.SeamEdges)
        {
            if ((uint)edge.A >= (uint)remap.Length || (uint)edge.B >= (uint)remap.Length)
            {
                continue;
            }

            int a = remap[edge.A];
            int b = remap[edge.B];
            if (a < 0 || b < 0 || a == b)
            {
                continue;
            }

            updated.Add(new EdgeKey(a, b));
        }

        mesh.SeamEdges.Clear();
        mesh.SeamEdges.UnionWith(updated);
    }

    private static List<Vector4>? GetValidColors(EditableMesh mesh)
    {
        var colors = mesh.Colors;
        if (colors is null)
        {
            return null;
        }

        if (colors.Count != mesh.Positions.Count)
        {
            mesh.SetColors(null);
            return null;
        }

        return colors;
    }

    private static Vector3 TransformNormal(Vector3 normal, Matrix4x4 normalMatrix)
    {
        var transformed = Vector3.TransformNormal(normal, normalMatrix);
        if (transformed.LengthSquared() > 1e-8f)
        {
            return Vector3.Normalize(transformed);
        }

        return Vector3.UnitY;
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;

    private readonly record struct CellKey(int X, int Y, int Z)
    {
        public static CellKey From(Vector3 position, float invCell)
        {
            var x = (int)MathF.Floor(position.X * invCell);
            var y = (int)MathF.Floor(position.Y * invCell);
            var z = (int)MathF.Floor(position.Z * invCell);
            return new CellKey(x, y, z);
        }
    }
}
