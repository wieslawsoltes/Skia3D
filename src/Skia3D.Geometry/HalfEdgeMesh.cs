using System;
using System.Collections.Generic;
using System.Numerics;

namespace Skia3D.Geometry;

public sealed class HalfEdgeMesh
{
    public const int Invalid = -1;

    public struct Vertex
    {
        public int HalfEdge;
    }

    public struct Face
    {
        public int HalfEdge;
    }

    public struct HalfEdge
    {
        public int Vertex;
        public int Face;
        public int Twin;
        public int Next;
        public int Prev;
    }

    private readonly Vector3[] _positions;
    private readonly Vertex[] _vertices;
    private readonly Face[] _faces;
    private readonly HalfEdge[] _halfEdges;

    private HalfEdgeMesh(Vector3[] positions, Vertex[] vertices, Face[] faces, HalfEdge[] halfEdges)
    {
        _positions = positions;
        _vertices = vertices;
        _faces = faces;
        _halfEdges = halfEdges;
    }

    public IReadOnlyList<Vector3> Positions => _positions;

    public IReadOnlyList<Vertex> Vertices => _vertices;

    public IReadOnlyList<Face> Faces => _faces;

    public IReadOnlyList<HalfEdge> HalfEdges => _halfEdges;

    public static HalfEdgeMesh FromMeshData(MeshData data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return Build(data.GetPositionsArray(), data.Indices);
    }

    public static HalfEdgeMesh Build(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices)
    {
        var vertices = new Vertex[positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].HalfEdge = Invalid;
        }

        var faces = new List<Face>(indices.Length / 3);
        var halfEdges = new List<HalfEdge>(indices.Length);
        var map = new Dictionary<DirectedEdgeKey, int>(indices.Length);

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if (!IsValid(i0, positions.Length) || !IsValid(i1, positions.Length) || !IsValid(i2, positions.Length))
            {
                continue;
            }

            int faceIndex = faces.Count;
            int baseEdge = halfEdges.Count;

            halfEdges.Add(new HalfEdge
            {
                Vertex = i0,
                Face = faceIndex,
                Twin = Invalid,
                Next = baseEdge + 1,
                Prev = baseEdge + 2
            });
            halfEdges.Add(new HalfEdge
            {
                Vertex = i1,
                Face = faceIndex,
                Twin = Invalid,
                Next = baseEdge + 2,
                Prev = baseEdge
            });
            halfEdges.Add(new HalfEdge
            {
                Vertex = i2,
                Face = faceIndex,
                Twin = Invalid,
                Next = baseEdge,
                Prev = baseEdge + 1
            });

            faces.Add(new Face { HalfEdge = baseEdge });

            if (vertices[i0].HalfEdge == Invalid)
            {
                vertices[i0].HalfEdge = baseEdge;
            }
            if (vertices[i1].HalfEdge == Invalid)
            {
                vertices[i1].HalfEdge = baseEdge + 1;
            }
            if (vertices[i2].HalfEdge == Invalid)
            {
                vertices[i2].HalfEdge = baseEdge + 2;
            }

            map[new DirectedEdgeKey(i0, i1)] = baseEdge;
            map[new DirectedEdgeKey(i1, i2)] = baseEdge + 1;
            map[new DirectedEdgeKey(i2, i0)] = baseEdge + 2;
        }

        var halfEdgeArray = halfEdges.ToArray();
        for (int i = 0; i < halfEdgeArray.Length; i++)
        {
            ref var edge = ref halfEdgeArray[i];
            int nextIndex = edge.Next;
            if ((uint)nextIndex >= (uint)halfEdgeArray.Length)
            {
                continue;
            }

            int origin = edge.Vertex;
            int dest = halfEdgeArray[nextIndex].Vertex;
            if (map.TryGetValue(new DirectedEdgeKey(dest, origin), out var twin))
            {
                edge.Twin = twin;
            }
        }

        return new HalfEdgeMesh(
            positions.ToArray(),
            vertices,
            faces.ToArray(),
            halfEdgeArray);
    }

    public bool TryGetFaceVertices(int faceIndex, Span<int> buffer, out int count)
    {
        count = 0;
        if ((uint)faceIndex >= (uint)_faces.Length || buffer.Length == 0)
        {
            return false;
        }

        int start = _faces[faceIndex].HalfEdge;
        if ((uint)start >= (uint)_halfEdges.Length)
        {
            return false;
        }

        int current = start;
        do
        {
            if (count >= buffer.Length)
            {
                return true;
            }

            buffer[count++] = _halfEdges[current].Vertex;
            current = _halfEdges[current].Next;
        }
        while (current != start && current != Invalid);

        return true;
    }

    public bool TryGetFaceNormal(int faceIndex, out Vector3 normal)
    {
        normal = Vector3.Zero;
        if ((uint)faceIndex >= (uint)_faces.Length)
        {
            return false;
        }

        Span<int> verts = stackalloc int[3];
        if (!TryGetFaceVertices(faceIndex, verts, out var count) || count < 3)
        {
            return false;
        }

        var p0 = _positions[verts[0]];
        var p1 = _positions[verts[1]];
        var p2 = _positions[verts[2]];
        var n = Vector3.Cross(p1 - p0, p2 - p0);
        if (n.LengthSquared() < 1e-8f)
        {
            return false;
        }

        normal = Vector3.Normalize(n);
        return true;
    }

    public MeshData ToMeshData()
    {
        var indices = new List<int>(_faces.Length * 3);
        Span<int> verts = stackalloc int[8];

        for (int i = 0; i < _faces.Length; i++)
        {
            if (!TryGetFaceVertices(i, verts, out var count) || count < 3)
            {
                continue;
            }

            for (int t = 1; t + 1 < count; t++)
            {
                indices.Add(verts[0]);
                indices.Add(verts[t]);
                indices.Add(verts[t + 1]);
            }
        }

        return MeshData.FromPositions(_positions, indices.ToArray());
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;

    private readonly struct DirectedEdgeKey : IEquatable<DirectedEdgeKey>
    {
        public DirectedEdgeKey(int from, int to)
        {
            From = from;
            To = to;
        }

        public int From { get; }

        public int To { get; }

        public bool Equals(DirectedEdgeKey other) => From == other.From && To == other.To;

        public override bool Equals(object? obj) => obj is DirectedEdgeKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(From, To);
    }
}
