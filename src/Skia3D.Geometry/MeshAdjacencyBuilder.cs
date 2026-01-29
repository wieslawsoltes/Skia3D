using System;
using System.Collections.Generic;

namespace Skia3D.Geometry;

public static class MeshAdjacencyBuilder
{
    public static MeshAdjacencyData BuildVertexAdjacency(int vertexCount, ReadOnlySpan<int> indices)
    {
        if (vertexCount <= 0)
        {
            return new MeshAdjacencyData(0, new int[1], Array.Empty<int>());
        }

        if (indices.Length == 0)
        {
            return new MeshAdjacencyData(vertexCount, new int[vertexCount + 1], Array.Empty<int>());
        }

        var neighborSets = new HashSet<int>[vertexCount];
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if (!IsValid(i0, vertexCount) || !IsValid(i1, vertexCount) || !IsValid(i2, vertexCount))
            {
                continue;
            }

            AddNeighbor(neighborSets, i0, i1);
            AddNeighbor(neighborSets, i0, i2);
            AddNeighbor(neighborSets, i1, i0);
            AddNeighbor(neighborSets, i1, i2);
            AddNeighbor(neighborSets, i2, i0);
            AddNeighbor(neighborSets, i2, i1);
        }

        var offsets = new int[vertexCount + 1];
        int total = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            offsets[i] = total;
            total += neighborSets[i]?.Count ?? 0;
        }
        offsets[vertexCount] = total;

        var neighbors = new int[total];
        int write = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            if (neighborSets[i] is null)
            {
                continue;
            }

            foreach (var n in neighborSets[i])
            {
                neighbors[write++] = n;
            }
        }

        return new MeshAdjacencyData(vertexCount, offsets, neighbors);
    }

    private static void AddNeighbor(HashSet<int>[] sets, int vertex, int neighbor)
    {
        var set = sets[vertex];
        if (set is null)
        {
            set = new HashSet<int>();
            sets[vertex] = set;
        }

        set.Add(neighbor);
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;
}
