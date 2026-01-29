using System;
using System.Collections.Generic;
using System.Numerics;

namespace Skia3D.Modeling;

public enum UvUnwrapMethod
{
    Planar,
    Lscm,
    Abf
}

public readonly record struct UvUnwrapOptions(UvUnwrapMethod Method, float AngleThresholdDegrees, float Scale);

public readonly record struct UvPackOptions(float Padding, bool AllowRotate, bool PreserveTexelDensity, float TexelDensity, bool UseGroups);

public static class UvOperations
{
    public static void ProjectPlanar(EditableMesh mesh, Vector3 normal, Vector3 origin, Vector2 scale, Vector2 offset)
    {
        ProjectPlanar(mesh, normal, origin, scale, offset, selection: null);
    }

    public static void ProjectPlanar(EditableMesh mesh, Vector3 normal, Vector3 origin, Vector2 scale, Vector2 offset, IReadOnlyCollection<int>? selection)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (normal.LengthSquared() < 1e-8f)
        {
            return;
        }

        EnsureUvCount(mesh);
        var n = Vector3.Normalize(normal);
        var up = MathF.Abs(n.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var uAxis = Vector3.Normalize(Vector3.Cross(up, n));
        var vAxis = Vector3.Normalize(Vector3.Cross(n, uAxis));

        var positions = mesh.Positions;
        var uvs = mesh.UVs;
        if (selection is null)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                var local = positions[i] - origin;
                var u = Vector3.Dot(local, uAxis) * scale.X + offset.X;
                var v = Vector3.Dot(local, vAxis) * scale.Y + offset.Y;
                uvs[i] = new Vector2(u, v);
            }
            return;
        }

        if (selection.Count == 0)
        {
            return;
        }

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)positions.Count)
            {
                continue;
            }

            var local = positions[index] - origin;
            var u = Vector3.Dot(local, uAxis) * scale.X + offset.X;
            var v = Vector3.Dot(local, vAxis) * scale.Y + offset.Y;
            uvs[index] = new Vector2(u, v);
        }
    }

    public static void ProjectBox(EditableMesh mesh, Vector3 center, Vector3 size, Vector2 scale, Vector2 offset)
    {
        ProjectBox(mesh, center, size, scale, offset, selection: null);
    }

    public static void ProjectBox(EditableMesh mesh, Vector3 center, Vector3 size, Vector2 scale, Vector2 offset, IReadOnlyCollection<int>? selection)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        EnsureUvCount(mesh);
        var positions = mesh.Positions;
        var uvs = mesh.UVs;

        var inv = new Vector3(
            size.X != 0f ? 1f / size.X : 0f,
            size.Y != 0f ? 1f / size.Y : 0f,
            size.Z != 0f ? 1f / size.Z : 0f);

        if (selection is null)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i] - center;
                var abs = new Vector3(MathF.Abs(p.X), MathF.Abs(p.Y), MathF.Abs(p.Z));

                Vector2 uv;
                if (abs.X >= abs.Y && abs.X >= abs.Z)
                {
                    uv = new Vector2(p.Z * inv.Z, p.Y * inv.Y);
                }
                else if (abs.Y >= abs.X && abs.Y >= abs.Z)
                {
                    uv = new Vector2(p.X * inv.X, p.Z * inv.Z);
                }
                else
                {
                    uv = new Vector2(p.X * inv.X, p.Y * inv.Y);
                }

                uvs[i] = new Vector2(uv.X * scale.X + offset.X, uv.Y * scale.Y + offset.Y);
            }
            return;
        }

        if (selection.Count == 0)
        {
            return;
        }

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)positions.Count)
            {
                continue;
            }

            var p = positions[index] - center;
            var abs = new Vector3(MathF.Abs(p.X), MathF.Abs(p.Y), MathF.Abs(p.Z));

            Vector2 uv;
            if (abs.X >= abs.Y && abs.X >= abs.Z)
            {
                uv = new Vector2(p.Z * inv.Z, p.Y * inv.Y);
            }
            else if (abs.Y >= abs.X && abs.Y >= abs.Z)
            {
                uv = new Vector2(p.X * inv.X, p.Z * inv.Z);
            }
            else
            {
                uv = new Vector2(p.X * inv.X, p.Y * inv.Y);
            }

            uvs[index] = new Vector2(uv.X * scale.X + offset.X, uv.Y * scale.Y + offset.Y);
        }
    }

    public static void NormalizeUVs(EditableMesh mesh)
    {
        NormalizeUVs(mesh, selection: null);
    }

    public static void NormalizeUVs(EditableMesh mesh, IReadOnlyCollection<int>? selection)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        EnsureUvCount(mesh);
        var uvs = mesh.UVs;
        if (uvs.Count == 0)
        {
            return;
        }

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        if (selection is null)
        {
            for (int i = 0; i < uvs.Count; i++)
            {
                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
            }
        }
        else
        {
            if (selection.Count == 0)
            {
                return;
            }

            bool any = false;
            foreach (var index in selection)
            {
                if ((uint)index >= (uint)uvs.Count)
                {
                    continue;
                }

                min = Vector2.Min(min, uvs[index]);
                max = Vector2.Max(max, uvs[index]);
                any = true;
            }

            if (!any)
            {
                return;
            }
        }

        var size = max - min;
        var invX = size.X != 0f ? 1f / size.X : 0f;
        var invY = size.Y != 0f ? 1f / size.Y : 0f;
        if (selection is null)
        {
            for (int i = 0; i < uvs.Count; i++)
            {
                var uv = uvs[i] - min;
                uvs[i] = new Vector2(uv.X * invX, uv.Y * invY);
            }
            return;
        }

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)uvs.Count)
            {
                continue;
            }

            var uv = uvs[index] - min;
            uvs[index] = new Vector2(uv.X * invX, uv.Y * invY);
        }
    }

    public static void FlipU(EditableMesh mesh)
    {
        FlipU(mesh, selection: null);
    }

    public static void FlipU(EditableMesh mesh, IReadOnlyCollection<int>? selection)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        EnsureUvCount(mesh);
        var uvs = mesh.UVs;
        if (selection is null)
        {
            for (int i = 0; i < uvs.Count; i++)
            {
                uvs[i] = new Vector2(1f - uvs[i].X, uvs[i].Y);
            }
            return;
        }

        if (selection.Count == 0)
        {
            return;
        }

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)uvs.Count)
            {
                continue;
            }

            uvs[index] = new Vector2(1f - uvs[index].X, uvs[index].Y);
        }
    }

    public static void FlipV(EditableMesh mesh)
    {
        FlipV(mesh, selection: null);
    }

    public static void FlipV(EditableMesh mesh, IReadOnlyCollection<int>? selection)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        EnsureUvCount(mesh);
        var uvs = mesh.UVs;
        if (selection is null)
        {
            for (int i = 0; i < uvs.Count; i++)
            {
                uvs[i] = new Vector2(uvs[i].X, 1f - uvs[i].Y);
            }
            return;
        }

        if (selection.Count == 0)
        {
            return;
        }

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)uvs.Count)
            {
                continue;
            }

            uvs[index] = new Vector2(uvs[index].X, 1f - uvs[index].Y);
        }
    }

    public static bool Unwrap(
        EditableMesh mesh,
        UvUnwrapOptions options,
        IReadOnlyCollection<EdgeKey>? seamEdges = null,
        IReadOnlyCollection<int>? faceSelection = null)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        EnsureUvCount(mesh);
        var indices = mesh.Indices;
        var positions = mesh.Positions;
        var triCount = indices.Count / 3;
        if (triCount == 0 || positions.Count == 0)
        {
            return false;
        }

        var selectedMask = BuildSelectedFaceMask(triCount, faceSelection, out var selectedCount);
        if (selectedCount == 0)
        {
            return false;
        }

        var islands = BuildUvIslands(mesh, seamEdges, faceSelection, options.AngleThresholdDegrees);
        if (islands.Count == 0)
        {
            return false;
        }

        var split = SplitMeshByIslands(mesh, islands);
        if (split.Islands.Count == 0 || split.Positions.Count == 0 || split.Indices.Count == 0)
        {
            return false;
        }

        var scale = options.Scale <= 0f ? 1f : options.Scale;
        foreach (var island in split.Islands)
        {
            switch (options.Method)
            {
                case UvUnwrapMethod.Lscm:
                    if (!ApplyHarmonicIslandUv(split, island, useCotangent: false, lengthWeightedBoundary: false, scale))
                    {
                        ApplyPlanarIslandUv(split, island, scale);
                    }
                    break;
                case UvUnwrapMethod.Abf:
                    if (!ApplyHarmonicIslandUv(split, island, useCotangent: true, lengthWeightedBoundary: true, scale))
                    {
                        ApplyPlanarIslandUv(split, island, scale);
                    }
                    break;
                default:
                    ApplyPlanarIslandUv(split, island, scale);
                    break;
            }
        }

        bool fullSelection = selectedCount == triCount;
        HashSet<EdgeKey>? preserveSeams = null;
        if (!fullSelection && mesh.SeamEdges.Count > 0)
        {
            preserveSeams = new HashSet<EdgeKey>(mesh.SeamEdges);
        }

        if (fullSelection)
        {
            positions.Clear();
            positions.AddRange(split.Positions);
            indices.Clear();
            indices.AddRange(split.Indices);
            mesh.UVs.Clear();
            mesh.UVs.AddRange(split.Uvs);
            mesh.UvFaceGroups.Clear();
            mesh.UvFaceGroups.AddRange(split.FaceGroups);
            mesh.EnsureUvFaceGroups();
        }
        else
        {
            if (!ApplySplitToSelection(mesh, split, selectedMask, preserveSeams, out var remappedSeams))
            {
                return false;
            }

            if (remappedSeams != null && remappedSeams.Count > 0)
            {
                preserveSeams ??= new HashSet<EdgeKey>();
                preserveSeams.UnionWith(remappedSeams);
            }
        }

        RebuildSeamEdges(mesh, preserveSeams);
        if (!fullSelection)
        {
            MeshOperations.RemoveUnusedVertices(mesh);
        }

        return true;
    }

    public static bool PackIslands(
        EditableMesh mesh,
        UvPackOptions options,
        IReadOnlyCollection<EdgeKey>? seamEdges = null,
        IReadOnlyList<int>? faceGroups = null,
        IReadOnlyCollection<int>? faceSelection = null)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        EnsureUvCount(mesh);
        var positions = mesh.Positions;
        var indices = mesh.Indices;
        var uvs = mesh.UVs;
        var triCount = indices.Count / 3;
        if (triCount == 0 || uvs.Count == 0)
        {
            return false;
        }

        var islandFaces = BuildUvIslands(mesh, seamEdges, faceSelection, angleThresholdDegrees: 180f);
        if (islandFaces.Count == 0)
        {
            return false;
        }

        var islands = new List<UvIsland>(islandFaces.Count);
        foreach (var faces in islandFaces)
        {
            var island = new UvIsland();
            foreach (var face in faces)
            {
                int baseIndex = face * 3;
                if ((uint)(baseIndex + 2) >= (uint)indices.Count)
                {
                    continue;
                }

                int i0 = indices[baseIndex];
                int i1 = indices[baseIndex + 1];
                int i2 = indices[baseIndex + 2];
                if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
                {
                    continue;
                }

                island.Faces.Add(face);
                island.Vertices.Add(i0);
                island.Vertices.Add(i1);
                island.Vertices.Add(i2);

                var p0 = positions[i0];
                var p1 = positions[i1];
                var p2 = positions[i2];
                var area3d = Vector3.Cross(p1 - p0, p2 - p0).Length() * 0.5f;
                island.Area3d += area3d;

                if ((uint)i0 < (uint)uvs.Count && (uint)i1 < (uint)uvs.Count && (uint)i2 < (uint)uvs.Count)
                {
                    var uv0 = uvs[i0];
                    var uv1 = uvs[i1];
                    var uv2 = uvs[i2];
                    var areaUv = MathF.Abs((uv1.X - uv0.X) * (uv2.Y - uv0.Y) - (uv1.Y - uv0.Y) * (uv2.X - uv0.X)) * 0.5f;
                    island.AreaUv += areaUv;
                }
            }

            if (island.Vertices.Count == 0)
            {
                continue;
            }

            island.Min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            island.Max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var vertex in island.Vertices)
            {
                if ((uint)vertex >= (uint)uvs.Count)
                {
                    continue;
                }

                island.Min = Vector2.Min(island.Min, uvs[vertex]);
                island.Max = Vector2.Max(island.Max, uvs[vertex]);
            }

            island.Size = island.Max - island.Min;
            if (island.Size.LengthSquared() < 1e-8f)
            {
                continue;
            }

            if (options.UseGroups && faceGroups != null && faceGroups.Count == triCount)
            {
                foreach (var face in island.Faces)
                {
                    int groupId = faceGroups[face];
                    if (groupId > 0)
                    {
                        island.GroupId = groupId;
                        break;
                    }
                }
            }

            island.Scale = 1f;
            if (options.PreserveTexelDensity && island.Area3d > 1e-8f && island.AreaUv > 1e-8f)
            {
                var currentDensity = MathF.Sqrt(island.AreaUv / island.Area3d);
                var targetDensity = options.TexelDensity > 1e-6f ? options.TexelDensity : currentDensity;
                if (currentDensity > 1e-6f)
                {
                    island.Scale = targetDensity / currentDensity;
                }
            }

            island.BaseSize = island.Size * island.Scale;
            islands.Add(island);
        }

        if (islands.Count == 0)
        {
            return false;
        }

        float padding = Math.Clamp(options.Padding, 0f, 0.25f);
        var groups = BuildUvPackGroups(islands, options.UseGroups);
        foreach (var group in groups)
        {
            PackGroupIslands(group, padding, options.AllowRotate);
        }

        float maxDim = 0f;
        foreach (var group in groups)
        {
            maxDim = MathF.Max(maxDim, MathF.Max(group.Size.X, group.Size.Y));
        }

        if (maxDim <= 1e-8f)
        {
            return false;
        }

        float normalizeScale = 1f / maxDim;
        foreach (var group in groups)
        {
            group.Size *= normalizeScale;
        }

        PackGroups(groups, padding * normalizeScale, out var packedWidth, out var packedHeight);
        var packedMax = MathF.Max(packedWidth, packedHeight);
        if (packedMax <= 1e-8f)
        {
            return false;
        }

        float packScale = 1f / packedMax;
        var packedSize = new Vector2(packedWidth, packedHeight) * packScale;
        var pad = (Vector2.One - packedSize) * 0.5f;

        foreach (var group in groups)
        {
            var groupOffset = group.Offset;
            foreach (var island in group.Islands)
            {
                var islandOffset = island.LocalOffset * normalizeScale;
                foreach (var vertex in island.Vertices)
                {
                    if ((uint)vertex >= (uint)uvs.Count)
                    {
                        continue;
                    }

                    var local = (uvs[vertex] - island.Min) * island.Scale;
                    if (island.Rotated)
                    {
                        local = new Vector2(local.Y, island.BaseSize.X - local.X);
                    }

                    var uv = ((local * normalizeScale) + islandOffset + groupOffset) * packScale + pad;
                    uvs[vertex] = uv;
                }
            }
        }

        return true;
    }

    public static List<List<int>> BuildUvIslands(
        EditableMesh mesh,
        IReadOnlyCollection<EdgeKey>? seamEdges = null,
        IReadOnlyCollection<int>? faceSelection = null,
        float angleThresholdDegrees = 180f)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        var indices = mesh.Indices;
        var positions = mesh.Positions;
        var triCount = indices.Count / 3;
        var islands = new List<List<int>>();
        if (triCount == 0 || positions.Count == 0)
        {
            return islands;
        }

        var selected = BuildSelectedFaceMask(triCount, faceSelection, out var selectedCount);
        if (selectedCount == 0)
        {
            return islands;
        }

        var adjacency = MeshAdjacency.Build(mesh);
        var normals = ComputeFaceNormals(positions, indices);

        var seam = new HashSet<EdgeKey>();
        if (seamEdges != null)
        {
            foreach (var edge in seamEdges)
            {
                seam.Add(edge);
            }
        }

        var angle = Math.Clamp(angleThresholdDegrees, 0f, 180f);
        var cosThreshold = MathF.Cos(angle * (MathF.PI / 180f));
        var useAngle = angle < 179.9f;

        foreach (var edgeFaces in adjacency.Edges.Values)
        {
            if (edgeFaces.IsBoundary)
            {
                seam.Add(edgeFaces.Edge);
                continue;
            }

            var faces = edgeFaces.Faces;
            if (faces.Length == 0)
            {
                continue;
            }

            bool anySelected = false;
            bool anyUnselected = false;
            for (int i = 0; i < faces.Length; i++)
            {
                if (selected[faces[i]])
                {
                    anySelected = true;
                }
                else
                {
                    anyUnselected = true;
                }
            }

            if (anySelected && anyUnselected)
            {
                seam.Add(edgeFaces.Edge);
                continue;
            }

            if (!anySelected || !useAngle)
            {
                continue;
            }

            var reference = normals[faces[0]];
            for (int i = 1; i < faces.Length; i++)
            {
                if (Vector3.Dot(reference, normals[faces[i]]) < cosThreshold)
                {
                    seam.Add(edgeFaces.Edge);
                    break;
                }
            }
        }

        var chartByFace = new int[triCount];
        Array.Fill(chartByFace, -1);
        var queue = new Queue<int>();

        for (int face = 0; face < triCount; face++)
        {
            if (!selected[face] || chartByFace[face] >= 0)
            {
                continue;
            }

            int chartIndex = islands.Count;
            var faces = new List<int>();
            islands.Add(faces);
            queue.Enqueue(face);
            chartByFace[face] = chartIndex;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                faces.Add(current);

                var edges = adjacency.FaceEdges[current];
                for (int e = 0; e < edges.Length; e++)
                {
                    var edge = edges[e];
                    if (seam.Contains(edge))
                    {
                        continue;
                    }

                    if (!adjacency.Edges.TryGetValue(edge, out var connected))
                    {
                        continue;
                    }

                    var connectedFaces = connected.Faces;
                    for (int i = 0; i < connectedFaces.Length; i++)
                    {
                        var neighbor = connectedFaces[i];
                        if (!selected[neighbor] || chartByFace[neighbor] >= 0)
                        {
                            continue;
                        }

                        chartByFace[neighbor] = chartIndex;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return islands;
    }

    private static SplitMeshResult SplitMeshByIslands(EditableMesh mesh, List<List<int>> islands)
    {
        var result = new SplitMeshResult();
        var indices = mesh.Indices;
        var positions = mesh.Positions;
        var faceGroups = mesh.UvFaceGroups;
        var triCount = indices.Count / 3;
        var hasGroups = faceGroups.Count == triCount;

        foreach (var islandFaces in islands)
        {
            var island = new UvIslandInfo();
            var vertexMap = new Dictionary<int, int>();

            foreach (var face in islandFaces)
            {
                int baseIndex = face * 3;
                if ((uint)(baseIndex + 2) >= (uint)indices.Count)
                {
                    continue;
                }

                int i0 = indices[baseIndex];
                int i1 = indices[baseIndex + 1];
                int i2 = indices[baseIndex + 2];
                if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
                {
                    continue;
                }

                int faceIndex = result.FaceGroups.Count;
                AddVertex(i0);
                AddVertex(i1);
                AddVertex(i2);
                island.Faces.Add(faceIndex);
                result.FaceGroups.Add(hasGroups && (uint)face < (uint)faceGroups.Count ? faceGroups[face] : 0);
                result.FaceSources.Add(face);
            }

            if (island.Faces.Count > 0)
            {
                result.Islands.Add(island);
            }

            void AddVertex(int sourceIndex)
            {
                if (!vertexMap.TryGetValue(sourceIndex, out var newIndex))
                {
                    newIndex = result.Positions.Count;
                    result.Positions.Add(positions[sourceIndex]);
                    result.Uvs.Add(Vector2.Zero);
                    vertexMap[sourceIndex] = newIndex;
                }

                island.Vertices.Add(newIndex);
                result.Indices.Add(newIndex);
            }
        }

        return result;
    }

    private static bool ApplySplitToSelection(
        EditableMesh mesh,
        SplitMeshResult split,
        bool[] selectedMask,
        IReadOnlyCollection<EdgeKey>? preserveSeams,
        out HashSet<EdgeKey>? remappedSeams)
    {
        remappedSeams = null;
        var indices = mesh.Indices;
        var positions = mesh.Positions;
        var uvs = mesh.UVs;
        int triCount = indices.Count / 3;
        if (triCount == 0)
        {
            return false;
        }

        var faceMap = new int[triCount];
        Array.Fill(faceMap, -1);
        for (int i = 0; i < split.FaceSources.Count; i++)
        {
            int source = split.FaceSources[i];
            if ((uint)source < (uint)triCount)
            {
                faceMap[source] = i;
            }
        }

        HashSet<EdgeKey>? seamLookup = null;
        if (preserveSeams != null && preserveSeams.Count > 0)
        {
            seamLookup = preserveSeams as HashSet<EdgeKey> ?? new HashSet<EdgeKey>(preserveSeams);
            remappedSeams = new HashSet<EdgeKey>();
        }

        var newPositions = new List<Vector3>(positions.Count + split.Positions.Count);
        newPositions.AddRange(positions);
        var newUvs = new List<Vector2>(uvs.Count + split.Uvs.Count);
        newUvs.AddRange(uvs);
        int offset = newPositions.Count;
        newPositions.AddRange(split.Positions);
        newUvs.AddRange(split.Uvs);

        var newIndices = indices.ToArray();
        for (int face = 0; face < triCount; face++)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            if (!selectedMask[face])
            {
                newIndices[baseIndex] = indices[baseIndex];
                newIndices[baseIndex + 1] = indices[baseIndex + 1];
                newIndices[baseIndex + 2] = indices[baseIndex + 2];
                continue;
            }

            int splitFace = faceMap[face];
            if (splitFace < 0)
            {
                continue;
            }

            int splitBase = splitFace * 3;
            if ((uint)(splitBase + 2) >= (uint)split.Indices.Count)
            {
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            int n0 = split.Indices[splitBase] + offset;
            int n1 = split.Indices[splitBase + 1] + offset;
            int n2 = split.Indices[splitBase + 2] + offset;

            newIndices[baseIndex] = n0;
            newIndices[baseIndex + 1] = n1;
            newIndices[baseIndex + 2] = n2;

            if (seamLookup != null && remappedSeams != null)
            {
                if (seamLookup.Contains(new EdgeKey(i0, i1)))
                {
                    remappedSeams.Add(new EdgeKey(n0, n1));
                }
                if (seamLookup.Contains(new EdgeKey(i1, i2)))
                {
                    remappedSeams.Add(new EdgeKey(n1, n2));
                }
                if (seamLookup.Contains(new EdgeKey(i2, i0)))
                {
                    remappedSeams.Add(new EdgeKey(n2, n0));
                }
            }
        }

        positions.Clear();
        positions.AddRange(newPositions);
        indices.Clear();
        indices.AddRange(newIndices);
        uvs.Clear();
        uvs.AddRange(newUvs);
        mesh.EnsureUvFaceGroups();
        return true;
    }

    private static void RebuildSeamEdges(EditableMesh mesh, HashSet<EdgeKey>? preserveSeams)
    {
        mesh.SeamEdges.Clear();
        var adjacency = MeshAdjacency.Build(mesh);
        foreach (var edge in adjacency.Edges.Values)
        {
            if (edge.IsBoundary)
            {
                mesh.SeamEdges.Add(edge.Edge);
            }
        }

        if (preserveSeams == null || preserveSeams.Count == 0)
        {
            return;
        }

        foreach (var edge in preserveSeams)
        {
            if (adjacency.Edges.ContainsKey(edge))
            {
                mesh.SeamEdges.Add(edge);
            }
        }
    }

    private static void ApplyPlanarIslandUv(SplitMeshResult split, UvIslandInfo island, float scale)
    {
        if (island.Vertices.Count == 0)
        {
            return;
        }

        var positions = split.Positions;
        var indices = split.Indices;
        var normalSum = Vector3.Zero;
        var center = Vector3.Zero;

        foreach (var face in island.Faces)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            normalSum += Vector3.Cross(p1 - p0, p2 - p0);
        }

        foreach (var vertex in island.Vertices)
        {
            center += positions[vertex];
        }
        center /= island.Vertices.Count;

        if (normalSum.LengthSquared() < 1e-10f)
        {
            normalSum = Vector3.UnitY;
        }

        var n = Vector3.Normalize(normalSum);
        var up = MathF.Abs(n.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var uAxis = Vector3.Normalize(Vector3.Cross(up, n));
        var vAxis = Vector3.Normalize(Vector3.Cross(n, uAxis));

        foreach (var vertex in island.Vertices)
        {
            var local = positions[vertex] - center;
            var u = Vector3.Dot(local, uAxis) * scale;
            var v = Vector3.Dot(local, vAxis) * scale;
            split.Uvs[vertex] = new Vector2(u, v);
        }
    }

    private static bool ApplyHarmonicIslandUv(
        SplitMeshResult split,
        UvIslandInfo island,
        bool useCotangent,
        bool lengthWeightedBoundary,
        float scale)
    {
        var loops = BuildBoundaryLoops(split.Indices, island.Faces);
        if (loops.Count == 0)
        {
            return false;
        }

        var boundary = new Dictionary<int, Vector2>();
        if (!MapBoundaryLoops(loops, split.Positions, lengthWeightedBoundary, boundary))
        {
            return false;
        }

        var boundaryVertices = new HashSet<int>(boundary.Keys);
        foreach (var vertex in island.Vertices)
        {
            split.Uvs[vertex] = new Vector2(0.5f, 0.5f);
        }

        foreach (var pair in boundary)
        {
            split.Uvs[pair.Key] = pair.Value;
        }

        var weights = BuildIslandWeights(split.Positions, split.Indices, island.Faces, useCotangent);
        if (weights.Count == 0)
        {
            return false;
        }

        int iterations = Math.Clamp(island.Vertices.Count * 2, 40, 300);
        float epsilon = 1e-4f;
        for (int iter = 0; iter < iterations; iter++)
        {
            float maxDelta = 0f;
            foreach (var vertex in island.Vertices)
            {
                if (boundaryVertices.Contains(vertex))
                {
                    continue;
                }

                if (!weights.TryGetValue(vertex, out var neighbors) || neighbors.Count == 0)
                {
                    continue;
                }

                float sumW = 0f;
                var sum = Vector2.Zero;
                foreach (var neighbor in neighbors)
                {
                    float w = neighbor.Value;
                    sumW += w;
                    sum += split.Uvs[neighbor.Key] * w;
                }

                if (sumW <= 1e-8f)
                {
                    continue;
                }

                var next = sum / sumW;
                var delta = next - split.Uvs[vertex];
                maxDelta = MathF.Max(maxDelta, MathF.Abs(delta.X) + MathF.Abs(delta.Y));
                split.Uvs[vertex] = next;
            }

            if (maxDelta < epsilon)
            {
                break;
            }
        }

        if (MathF.Abs(scale - 1f) > 1e-5f)
        {
            var center = new Vector2(0.5f, 0.5f);
            foreach (var vertex in island.Vertices)
            {
                split.Uvs[vertex] = center + (split.Uvs[vertex] - center) * scale;
            }
        }

        return true;
    }

    private static List<List<int>> BuildBoundaryLoops(IReadOnlyList<int> indices, IReadOnlyCollection<int> faces)
    {
        var edgeCounts = new Dictionary<EdgeKey, int>();
        foreach (var face in faces)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            AddEdge(new EdgeKey(i0, i1));
            AddEdge(new EdgeKey(i1, i2));
            AddEdge(new EdgeKey(i2, i0));
        }

        var boundaryEdges = new HashSet<EdgeKey>();
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var pair in edgeCounts)
        {
            if (pair.Value != 1)
            {
                continue;
            }

            var edge = pair.Key;
            boundaryEdges.Add(edge);
            AddNeighbor(edge.A, edge.B);
            AddNeighbor(edge.B, edge.A);
        }

        var loops = new List<List<int>>();
        var visited = new HashSet<EdgeKey>();
        foreach (var edge in boundaryEdges)
        {
            if (visited.Contains(edge))
            {
                continue;
            }

            var loop = new List<int>();
            int start = edge.A;
            int current = edge.B;
            int prev = start;
            loop.Add(start);
            loop.Add(current);
            visited.Add(edge);

            while (current != start && adjacency.TryGetValue(current, out var neighbors))
            {
                int next = -1;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    var candidate = neighbors[i];
                    if (candidate == prev)
                    {
                        continue;
                    }

                    var candidateEdge = new EdgeKey(current, candidate);
                    if (boundaryEdges.Contains(candidateEdge) && !visited.Contains(candidateEdge))
                    {
                        next = candidate;
                        break;
                    }
                }

                if (next < 0)
                {
                    break;
                }

                prev = current;
                current = next;
                loop.Add(current);
                visited.Add(new EdgeKey(prev, current));
                if (current == start)
                {
                    break;
                }
            }

            if (loop.Count >= 3)
            {
                loops.Add(loop);
            }
        }

        return loops;

        void AddEdge(EdgeKey edge)
        {
            if (!edgeCounts.TryGetValue(edge, out var count))
            {
                edgeCounts[edge] = 1;
            }
            else
            {
                edgeCounts[edge] = count + 1;
            }
        }

        void AddNeighbor(int from, int to)
        {
            if (!adjacency.TryGetValue(from, out var list))
            {
                list = new List<int>();
                adjacency[from] = list;
            }

            if (!list.Contains(to))
            {
                list.Add(to);
            }
        }
    }

    private static bool MapBoundaryLoops(
        List<List<int>> loops,
        IReadOnlyList<Vector3> positions,
        bool lengthWeighted,
        Dictionary<int, Vector2> boundary)
    {
        if (loops.Count == 0)
        {
            return false;
        }

        int outerIndex = 0;
        float outerPerimeter = 0f;
        for (int i = 0; i < loops.Count; i++)
        {
            float perimeter = ComputeLoopPerimeter(loops[i], positions);
            if (perimeter > outerPerimeter)
            {
                outerPerimeter = perimeter;
                outerIndex = i;
            }
        }

        if (outerPerimeter <= 1e-6f)
        {
            return false;
        }

        var center = new Vector2(0.5f, 0.5f);
        const float baseRadius = 0.45f;
        var twoPi = MathF.PI * 2f;

        for (int loopIndex = 0; loopIndex < loops.Count; loopIndex++)
        {
            var loop = loops[loopIndex];
            if (loop.Count < 2)
            {
                continue;
            }

            float perimeter = ComputeLoopPerimeter(loop, positions);
            if (perimeter <= 1e-6f)
            {
                continue;
            }

            float radius = baseRadius;
            if (loopIndex != outerIndex)
            {
                radius *= Math.Clamp(perimeter / outerPerimeter, 0.15f, 0.65f);
            }

            float t = 0f;
            float total = lengthWeighted ? perimeter : loop.Count;
            for (int i = 0; i < loop.Count; i++)
            {
                float angle = total > 0f ? (t / total) * twoPi : 0f;
                var uv = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                boundary[loop[i]] = uv;

                int next = (i + 1) % loop.Count;
                if (lengthWeighted)
                {
                    var p0 = positions[loop[i]];
                    var p1 = positions[loop[next]];
                    t += Vector3.Distance(p0, p1);
                }
                else
                {
                    t += 1f;
                }
            }
        }

        return boundary.Count > 0;
    }

    private static float ComputeLoopPerimeter(List<int> loop, IReadOnlyList<Vector3> positions)
    {
        float perimeter = 0f;
        for (int i = 0; i < loop.Count; i++)
        {
            int next = (i + 1) % loop.Count;
            var p0 = positions[loop[i]];
            var p1 = positions[loop[next]];
            perimeter += Vector3.Distance(p0, p1);
        }

        return perimeter;
    }

    private static Dictionary<int, Dictionary<int, float>> BuildIslandWeights(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> indices,
        IReadOnlyCollection<int> faces,
        bool useCotangent)
    {
        var edgeWeights = new Dictionary<EdgeKey, float>();
        foreach (var face in faces)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                continue;
            }

            if (useCotangent)
            {
                var p0 = positions[i0];
                var p1 = positions[i1];
                var p2 = positions[i2];

                float cot0 = ComputeCotangent(p1 - p0, p2 - p0);
                float cot1 = ComputeCotangent(p2 - p1, p0 - p1);
                float cot2 = ComputeCotangent(p0 - p2, p1 - p2);
                AddWeight(new EdgeKey(i1, i2), cot0);
                AddWeight(new EdgeKey(i2, i0), cot1);
                AddWeight(new EdgeKey(i0, i1), cot2);
            }
            else
            {
                AddWeight(new EdgeKey(i0, i1), 1f);
                AddWeight(new EdgeKey(i1, i2), 1f);
                AddWeight(new EdgeKey(i2, i0), 1f);
            }
        }

        var adjacency = new Dictionary<int, Dictionary<int, float>>();
        foreach (var pair in edgeWeights)
        {
            float weight = pair.Value;
            if (weight <= 0f)
            {
                continue;
            }

            AddNeighbor(pair.Key.A, pair.Key.B, weight);
            AddNeighbor(pair.Key.B, pair.Key.A, weight);
        }

        return adjacency;

        void AddWeight(EdgeKey edge, float weight)
        {
            if (weight <= 0f)
            {
                return;
            }

            if (edgeWeights.TryGetValue(edge, out var current))
            {
                edgeWeights[edge] = current + weight;
            }
            else
            {
                edgeWeights[edge] = weight;
            }
        }

        void AddNeighbor(int from, int to, float weight)
        {
            if (!adjacency.TryGetValue(from, out var neighbors))
            {
                neighbors = new Dictionary<int, float>();
                adjacency[from] = neighbors;
            }

            if (neighbors.TryGetValue(to, out var current))
            {
                neighbors[to] = current + weight;
            }
            else
            {
                neighbors[to] = weight;
            }
        }
    }

    private static float ComputeCotangent(Vector3 a, Vector3 b)
    {
        var cross = Vector3.Cross(a, b);
        float area = cross.Length();
        if (area < 1e-8f)
        {
            return 0f;
        }

        float cot = Vector3.Dot(a, b) / area;
        return cot < 0f ? 0f : cot;
    }

    private static Vector3[] ComputeFaceNormals(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices)
    {
        int triCount = indices.Count / 3;
        var normals = new Vector3[triCount];
        for (int face = 0; face < triCount; face++)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                normals[face] = Vector3.UnitY;
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                normals[face] = Vector3.UnitY;
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var normal = Vector3.Cross(p1 - p0, p2 - p0);
            normals[face] = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.UnitY;
        }

        return normals;
    }

    private static bool[] BuildSelectedFaceMask(int triCount, IReadOnlyCollection<int>? faceSelection, out int selectedCount)
    {
        var selected = new bool[triCount];
        selectedCount = 0;
        if (faceSelection is null || faceSelection.Count == 0)
        {
            for (int i = 0; i < triCount; i++)
            {
                selected[i] = true;
            }
            selectedCount = triCount;
            return selected;
        }

        foreach (var face in faceSelection)
        {
            if ((uint)face >= (uint)triCount)
            {
                continue;
            }

            if (!selected[face])
            {
                selected[face] = true;
                selectedCount++;
            }
        }

        return selected;
    }

    private static List<UvPackGroup> BuildUvPackGroups(List<UvIsland> islands, bool useGroups)
    {
        var groups = new List<UvPackGroup>();
        if (useGroups)
        {
            var map = new Dictionary<int, UvPackGroup>();
            foreach (var island in islands)
            {
                if (island.GroupId > 0)
                {
                    if (!map.TryGetValue(island.GroupId, out var group))
                    {
                        group = new UvPackGroup(island.GroupId);
                        map[island.GroupId] = group;
                        groups.Add(group);
                    }

                    group.Islands.Add(island);
                }
                else
                {
                    var group = new UvPackGroup(0);
                    group.Islands.Add(island);
                    groups.Add(group);
                }
            }
        }
        else
        {
            foreach (var island in islands)
            {
                var group = new UvPackGroup(0);
                group.Islands.Add(island);
                groups.Add(group);
            }
        }

        return groups;
    }

    private static void PackGroupIslands(UvPackGroup group, float padding, bool allowRotate)
    {
        var islands = group.Islands;
        islands.Sort((a, b) => b.BaseSize.Y.CompareTo(a.BaseSize.Y));

        float totalArea = 0f;
        float maxWidth = 0f;
        for (int i = 0; i < islands.Count; i++)
        {
            var size = islands[i].BaseSize;
            totalArea += size.X * size.Y;
            maxWidth = MathF.Max(maxWidth, size.X + padding);
        }

        var targetWidth = MathF.Max(MathF.Sqrt(MathF.Max(totalArea, 1e-6f)), maxWidth);
        float cursorX = 0f;
        float cursorY = 0f;
        float rowHeight = 0f;
        float packedWidth = 0f;

        for (int i = 0; i < islands.Count; i++)
        {
            var island = islands[i];
            var size = island.BaseSize;
            bool rotated = false;

            if (allowRotate)
            {
                var rotatedSize = new Vector2(size.Y, size.X);
                bool fits = cursorX + size.X <= targetWidth || cursorX == 0f;
                bool fitsRotated = cursorX + rotatedSize.X <= targetWidth || cursorX == 0f;
                if (fitsRotated && (!fits || rotatedSize.Y < size.Y))
                {
                    rotated = true;
                    size = rotatedSize;
                }
            }

            if (cursorX + size.X > targetWidth && cursorX > 0f)
            {
                cursorX = 0f;
                cursorY += rowHeight;
                rowHeight = 0f;
            }

            island.Rotated = rotated;
            island.LocalSize = size;
            island.LocalOffset = new Vector2(cursorX + padding * 0.5f, cursorY + padding * 0.5f);
            cursorX += size.X + padding;
            rowHeight = MathF.Max(rowHeight, size.Y + padding);
            packedWidth = MathF.Max(packedWidth, cursorX);
        }

        group.Size = new Vector2(packedWidth, cursorY + rowHeight);
    }

    private static void PackGroups(List<UvPackGroup> groups, float padding, out float packedWidth, out float packedHeight)
    {
        groups.Sort((a, b) => b.Size.Y.CompareTo(a.Size.Y));

        float totalArea = 0f;
        float maxWidth = 0f;
        for (int i = 0; i < groups.Count; i++)
        {
            var size = groups[i].Size;
            totalArea += size.X * size.Y;
            maxWidth = MathF.Max(maxWidth, size.X + padding);
        }

        var targetWidth = MathF.Max(MathF.Sqrt(MathF.Max(totalArea, 1e-6f)), maxWidth);
        float cursorX = 0f;
        float cursorY = 0f;
        float rowHeight = 0f;
        packedWidth = 0f;

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var size = group.Size;
            var paddedWidth = size.X + padding;
            var paddedHeight = size.Y + padding;

            if (cursorX + paddedWidth > targetWidth && cursorX > 0f)
            {
                cursorX = 0f;
                cursorY += rowHeight;
                rowHeight = 0f;
            }

            group.Offset = new Vector2(cursorX + padding * 0.5f, cursorY + padding * 0.5f);
            cursorX += paddedWidth;
            rowHeight = MathF.Max(rowHeight, paddedHeight);
            packedWidth = MathF.Max(packedWidth, cursorX);
        }

        packedHeight = cursorY + rowHeight;
    }

    private static void EnsureUvCount(EditableMesh mesh)
    {
        var positions = mesh.Positions;
        var uvs = mesh.UVs;
        if (uvs.Count == positions.Count)
        {
            return;
        }

        uvs.Clear();
        for (int i = 0; i < positions.Count; i++)
        {
            uvs.Add(Vector2.Zero);
        }
    }

    private sealed class SplitMeshResult
    {
        public List<Vector3> Positions { get; } = new();

        public List<int> Indices { get; } = new();

        public List<Vector2> Uvs { get; } = new();

        public List<int> FaceGroups { get; } = new();

        public List<int> FaceSources { get; } = new();

        public List<UvIslandInfo> Islands { get; } = new();
    }

    private sealed class UvIslandInfo
    {
        public List<int> Faces { get; } = new();

        public HashSet<int> Vertices { get; } = new();
    }

    private sealed class UvIsland
    {
        public List<int> Faces { get; } = new();

        public HashSet<int> Vertices { get; } = new();

        public Vector2 Min;

        public Vector2 Max;

        public Vector2 Size;

        public float Area3d;

        public float AreaUv;

        public float Scale = 1f;

        public Vector2 BaseSize;

        public Vector2 LocalSize;

        public Vector2 LocalOffset;

        public bool Rotated;

        public int GroupId;
    }

    private sealed class UvPackGroup
    {
        public UvPackGroup(int groupId)
        {
            GroupId = groupId;
        }

        public int GroupId { get; }

        public List<UvIsland> Islands { get; } = new();

        public Vector2 Size;

        public Vector2 Offset;
    }
}
