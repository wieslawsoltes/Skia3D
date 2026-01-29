using System;

namespace Skia3D.Geometry;

public sealed class MeshAdjacencyData
{
    public MeshAdjacencyData(int vertexCount, int[] vertexOffsets, int[] vertexNeighbors)
    {
        if (vertexCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount));
        }

        VertexCount = vertexCount;
        VertexOffsets = vertexOffsets ?? throw new ArgumentNullException(nameof(vertexOffsets));
        VertexNeighbors = vertexNeighbors ?? throw new ArgumentNullException(nameof(vertexNeighbors));

        if (VertexOffsets.Length != vertexCount + 1)
        {
            throw new ArgumentException("VertexOffsets length must be vertexCount + 1.", nameof(vertexOffsets));
        }
    }

    public int VertexCount { get; }

    public int[] VertexOffsets { get; }

    public int[] VertexNeighbors { get; }

    public ReadOnlySpan<int> GetNeighbors(int vertex)
    {
        if ((uint)vertex >= (uint)VertexCount)
        {
            return ReadOnlySpan<int>.Empty;
        }

        int start = VertexOffsets[vertex];
        int end = VertexOffsets[vertex + 1];
        return new ReadOnlySpan<int>(VertexNeighbors, start, end - start);
    }
}
