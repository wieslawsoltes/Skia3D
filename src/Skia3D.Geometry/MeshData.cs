using System;
using System.Numerics;

namespace Skia3D.Geometry;

public sealed class MeshData
{
    private Vector3[]? _positionsArray;

    public MeshData(SoaVector3 positions, int[] indices, MeshAttributes? attributes = null)
    {
        Positions = positions;
        Indices = indices ?? Array.Empty<int>();
        Attributes = attributes;

        if (Indices.Length % 3 != 0)
        {
            throw new ArgumentException("Indices length must be divisible by 3.", nameof(indices));
        }

        Attributes?.Validate(positions.Length);
    }

    public SoaVector3 Positions { get; }

    public int[] Indices { get; }

    public MeshAttributes? Attributes { get; }

    public int VertexCount => Positions.Length;

    public int TriangleCount => Indices.Length / 3;

    public Vector3[] GetPositionsArray()
    {
        if (_positionsArray != null)
        {
            return _positionsArray;
        }

        var array = new Vector3[Positions.Length];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = Positions.Get(i);
        }

        _positionsArray = array;
        return array;
    }

    public static MeshData FromPositions(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, MeshAttributes? attributes = null)
    {
        var soa = SoaVector3.From(positions);
        var idx = new int[indices.Length];
        indices.CopyTo(idx);
        return new MeshData(soa, idx, attributes);
    }
}
