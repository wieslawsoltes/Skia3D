using System;
using System.Numerics;
using Skia3D.Acceleration;
using Skia3D.Geometry;
using SkiaSharp;

namespace Skia3D.Core;

public readonly record struct Vertex(Vector3 Position, Vector3 Normal, SKColor Color, Vector2 UV)
{
    public Vertex(Vector3 position, Vector3 normal, SKColor color)
        : this(position, normal, color, Vector2.Zero)
    {
    }
}

public sealed class Mesh
{
    private SoaVector3? _positionsSoa;
    private Vertex[] _vertices;
    private int[] _indices;
    private Vector4[]? _tangents;
    private Vector3[]? _bitangents;
    private Vector4[]? _skinWeights;
    private Int4[]? _skinIndices;
    private MeshMorphTarget[]? _morphTargets;
    private Bvh? _bvh;
    private int _bvhLeafSize;
    private UniformGrid? _grid;
    private int _gridCellsPerAxis;

    public Mesh(
        IReadOnlyList<Vertex> vertices,
        IReadOnlyList<int> indices,
        IReadOnlyList<Vector4>? tangents = null,
        IReadOnlyList<Vector3>? bitangents = null,
        IReadOnlyList<Vector4>? skinWeights = null,
        IReadOnlyList<Int4>? skinIndices = null,
        IReadOnlyList<MeshMorphTarget>? morphTargets = null)
    {
        _vertices = CopyVertices(vertices);
        _indices = CopyIndices(indices);
        _tangents = CopyTangents(tangents, _vertices.Length);
        _bitangents = CopyBitangents(bitangents, _vertices.Length);
        _skinWeights = CopySkinWeights(skinWeights, _vertices.Length);
        _skinIndices = CopySkinIndices(skinIndices, _vertices.Length);
        _morphTargets = CopyMorphTargets(morphTargets, _vertices.Length);
        UpdateBoundsFromVertices();
    }

    public IReadOnlyList<Vertex> Vertices => _vertices;

    public IReadOnlyList<int> Indices => _indices;

    public IReadOnlyList<Vector4>? Tangents => _tangents;

    public IReadOnlyList<Vector3>? Bitangents => _bitangents;

    public IReadOnlyList<Vector4>? SkinWeights => _skinWeights;

    public IReadOnlyList<Int4>? SkinIndices => _skinIndices;

    public IReadOnlyList<MeshMorphTarget>? MorphTargets => _morphTargets;

    public bool SetTangents(IReadOnlyList<Vector4>? tangents)
    {
        if (tangents is null)
        {
            _tangents = null;
            return true;
        }

        if (tangents.Count != _vertices.Length)
        {
            return false;
        }

        _tangents = CopyTangents(tangents, _vertices.Length);
        return true;
    }

    public bool SetBitangents(IReadOnlyList<Vector3>? bitangents)
    {
        if (bitangents is null)
        {
            _bitangents = null;
            return true;
        }

        if (bitangents.Count != _vertices.Length)
        {
            return false;
        }

        _bitangents = CopyBitangents(bitangents, _vertices.Length);
        return true;
    }

    public float BoundingRadius { get; private set; }

    public Vector3 BoundsMin { get; private set; }

    public Vector3 BoundsMax { get; private set; }

    public bool HasBounds { get; private set; }

    internal bool TryGetPositionsSoa(out SoaVector3 positions)
    {
        if (_positionsSoa.HasValue)
        {
            positions = _positionsSoa.Value;
            return true;
        }

        positions = default;
        return false;
    }

    internal int[] GetIndexArray()
    {
        return _indices;
    }

    internal Bvh? GetOrBuildBvh(int maxLeafSize)
    {
        if (!_positionsSoa.HasValue || _indices.Length == 0)
        {
            return null;
        }

        if (_bvh != null && _bvhLeafSize == maxLeafSize)
        {
            return _bvh;
        }

        var indices = GetIndexArray();
        _bvh = Bvh.Build(_positionsSoa.Value, indices, maxLeafSize);
        _bvhLeafSize = maxLeafSize;
        return _bvh;
    }

    internal UniformGrid? GetOrBuildGrid(int cellsPerAxis)
    {
        if (!_positionsSoa.HasValue || _indices.Length == 0)
        {
            return null;
        }

        cellsPerAxis = Math.Clamp(cellsPerAxis, 2, 128);
        if (_grid != null && _gridCellsPerAxis == cellsPerAxis)
        {
            return _grid;
        }

        var indices = GetIndexArray();
        _grid = UniformGrid.Build(_positionsSoa.Value, indices, cellsPerAxis);
        _gridCellsPerAxis = cellsPerAxis;
        return _grid;
    }

    public bool UpdateGeometry(IReadOnlyList<Vertex> vertices, IReadOnlyList<int> indices, bool allowRefit = true, bool parallelRefit = false)
    {
        bool sameVertexCount = vertices.Count == _vertices.Length;
        bool sameIndexCount = indices.Count == _indices.Length;
        bool topologySame = sameVertexCount && sameIndexCount;

        if (!topologySame)
        {
            _vertices = CopyVertices(vertices);
            _indices = CopyIndices(indices);
        }
        else
        {
            CopyInto(_vertices, vertices);
            CopyInto(_indices, indices);
        }

        UpdateBoundsFromVertices();
        _tangents = null;
        _bitangents = null;
        if (!topologySame)
        {
            _skinWeights = null;
            _skinIndices = null;
            _morphTargets = null;
        }

        if (_bvh != null)
        {
            if (allowRefit && topologySame && _positionsSoa.HasValue)
            {
                _bvh.Refit(_positionsSoa.Value, parallelRefit);
            }
            else
            {
                _bvh = null;
            }
        }

        _grid = null;
        _gridCellsPerAxis = 0;
        return topologySame;
    }

    public bool UpdateVertices(IReadOnlyList<Vertex> vertices, bool allowRefit = true, bool parallelRefit = false)
    {
        if (vertices.Count != _vertices.Length)
        {
            return false;
        }

        return UpdateGeometry(vertices, _indices, allowRefit, parallelRefit);
    }

    public bool UpdatePositions(IReadOnlyList<Vector3> positions, bool allowRefit = true, bool parallelRefit = false)
    {
        if (positions.Count != _vertices.Length)
        {
            return false;
        }

        for (int i = 0; i < _vertices.Length; i++)
        {
            _vertices[i] = _vertices[i] with { Position = positions[i] };
        }

        UpdateSoaPositionsFromPositions(positions);
        UpdateBoundsFromSoa();
        _tangents = null;
        _bitangents = null;

        if (_bvh != null)
        {
            if (allowRefit && _positionsSoa.HasValue)
            {
                _bvh.Refit(_positionsSoa.Value, parallelRefit);
            }
            else
            {
                _bvh = null;
            }
        }

        _grid = null;
        _gridCellsPerAxis = 0;
        return true;
    }

    private static SoaVector3 BuildSoaPositions(IReadOnlyList<Vertex> vertices)
    {
        var count = vertices.Count;
        var xs = new float[count];
        var ys = new float[count];
        var zs = new float[count];

        for (int i = 0; i < count; i++)
        {
            var p = vertices[i].Position;
            xs[i] = p.X;
            ys[i] = p.Y;
            zs[i] = p.Z;
        }

        return new SoaVector3(xs, ys, zs);
    }

    private static Vertex[] CopyVertices(IReadOnlyList<Vertex> vertices)
    {
        if (vertices is Vertex[] array)
        {
            return (Vertex[])array.Clone();
        }

        var list = new Vertex[vertices.Count];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = vertices[i];
        }

        return list;
    }

    private static Vector4[]? CopyTangents(IReadOnlyList<Vector4>? tangents, int vertexCount)
    {
        if (tangents is null)
        {
            return null;
        }

        if (tangents.Count != vertexCount)
        {
            throw new ArgumentException("Tangents length must match vertex count.", nameof(tangents));
        }

        if (tangents is Vector4[] array)
        {
            return (Vector4[])array.Clone();
        }

        var list = new Vector4[vertexCount];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = tangents[i];
        }

        return list;
    }

    private static Vector3[]? CopyBitangents(IReadOnlyList<Vector3>? bitangents, int vertexCount)
    {
        if (bitangents is null)
        {
            return null;
        }

        if (bitangents.Count != vertexCount)
        {
            throw new ArgumentException("Bitangents length must match vertex count.", nameof(bitangents));
        }

        if (bitangents is Vector3[] array)
        {
            return (Vector3[])array.Clone();
        }

        var list = new Vector3[vertexCount];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = bitangents[i];
        }

        return list;
    }

    private static Vector4[]? CopySkinWeights(IReadOnlyList<Vector4>? weights, int vertexCount)
    {
        if (weights is null)
        {
            return null;
        }

        if (weights.Count != vertexCount)
        {
            throw new ArgumentException("SkinWeights length must match vertex count.", nameof(weights));
        }

        if (weights is Vector4[] array)
        {
            return (Vector4[])array.Clone();
        }

        var list = new Vector4[vertexCount];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = weights[i];
        }

        return list;
    }

    private static Int4[]? CopySkinIndices(IReadOnlyList<Int4>? indices, int vertexCount)
    {
        if (indices is null)
        {
            return null;
        }

        if (indices.Count != vertexCount)
        {
            throw new ArgumentException("SkinIndices length must match vertex count.", nameof(indices));
        }

        if (indices is Int4[] array)
        {
            return (Int4[])array.Clone();
        }

        var list = new Int4[vertexCount];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = indices[i];
        }

        return list;
    }

    private static MeshMorphTarget[]? CopyMorphTargets(IReadOnlyList<MeshMorphTarget>? targets, int vertexCount)
    {
        if (targets is null)
        {
            return null;
        }

        var list = new MeshMorphTarget[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i] ?? throw new ArgumentException("MorphTargets cannot contain null entries.", nameof(targets));
            target.Validate(vertexCount);
            list[i] = target.Clone();
        }

        return list;
    }

    private static int[] CopyIndices(IReadOnlyList<int> indices)
    {
        if (indices is int[] array)
        {
            return (int[])array.Clone();
        }

        var list = new int[indices.Count];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = indices[i];
        }

        return list;
    }

    private static void CopyInto(Vertex[] destination, IReadOnlyList<Vertex> source)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void CopyInto(int[] destination, IReadOnlyList<int> source)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private void UpdateBoundsFromVertices()
    {
        if (_vertices.Length == 0)
        {
            _positionsSoa = null;
            BoundingRadius = 0f;
            BoundsMin = default;
            BoundsMax = default;
            HasBounds = false;
            return;
        }

        UpdateSoaPositionsFromVertices(_vertices);
        UpdateBoundsFromSoa();
    }

    private void UpdateBoundsFromSoa()
    {
        if (!_positionsSoa.HasValue || _positionsSoa.Value.Length == 0)
        {
            BoundingRadius = 0f;
            BoundsMin = default;
            BoundsMax = default;
            HasBounds = false;
            return;
        }

        BoundingRadius = GeometryKernels.ComputeBoundingRadius(_positionsSoa.Value);
        var aabb = GeometryKernels.ComputeAabb(_positionsSoa.Value);
        BoundsMin = aabb.Min;
        BoundsMax = aabb.Max;
        HasBounds = aabb.IsValid;
    }

    private void UpdateSoaPositionsFromVertices(IReadOnlyList<Vertex> vertices)
    {
        if (_positionsSoa.HasValue && _positionsSoa.Value.Length == vertices.Count)
        {
            var soa = _positionsSoa.Value;
            var xs = soa.X;
            var ys = soa.Y;
            var zs = soa.Z;
            for (int i = 0; i < vertices.Count; i++)
            {
                var p = vertices[i].Position;
                xs[i] = p.X;
                ys[i] = p.Y;
                zs[i] = p.Z;
            }
            return;
        }

        _positionsSoa = BuildSoaPositions(vertices);
    }

    private void UpdateSoaPositionsFromPositions(IReadOnlyList<Vector3> positions)
    {
        if (_positionsSoa.HasValue && _positionsSoa.Value.Length == positions.Count)
        {
            var soa = _positionsSoa.Value;
            var xs = soa.X;
            var ys = soa.Y;
            var zs = soa.Z;
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                xs[i] = p.X;
                ys[i] = p.Y;
                zs[i] = p.Z;
            }
            return;
        }

        var newXs = new float[positions.Count];
        var newYs = new float[positions.Count];
        var newZs = new float[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            newXs[i] = p.X;
            newYs[i] = p.Y;
            newZs[i] = p.Z;
        }

        _positionsSoa = new SoaVector3(newXs, newYs, newZs);
    }
}

public sealed class MeshInstance
{
    public MeshInstance(Mesh mesh)
    {
        Mesh = mesh;
        Material = Material.Default();
    }

    public Mesh Mesh { get; }

    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    public SKColor? OverrideColor { get; set; }

    public Material Material { get; set; }

    public MaterialOverrides? MaterialOverrides { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool TryGetWorldBounds(out Vector3 min, out Vector3 max)
    {
        if (!Mesh.HasBounds)
        {
            min = default;
            max = default;
            return false;
        }

        var localMin = Mesh.BoundsMin;
        var localMax = Mesh.BoundsMax;

        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(localMin.X, localMin.Y, localMin.Z);
        corners[1] = new Vector3(localMax.X, localMin.Y, localMin.Z);
        corners[2] = new Vector3(localMin.X, localMax.Y, localMin.Z);
        corners[3] = new Vector3(localMax.X, localMax.Y, localMin.Z);
        corners[4] = new Vector3(localMin.X, localMin.Y, localMax.Z);
        corners[5] = new Vector3(localMax.X, localMin.Y, localMax.Z);
        corners[6] = new Vector3(localMin.X, localMax.Y, localMax.Z);
        corners[7] = new Vector3(localMax.X, localMax.Y, localMax.Z);

        min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < corners.Length; i++)
        {
            var world = Vector3.Transform(corners[i], Transform);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }

        return true;
    }
}

public static class MeshFactory
{
    public static Mesh CreateCube(float size, SKColor color)
    {
        var h = size * 0.5f;
        var verts = new List<Vertex>();
        var inds = new List<int>();

        void Face(Vector3 normal, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            // Ensure triangle winding matches the provided normal so backface culling works reliably
            var start = verts.Count;
            verts.Add(new Vertex(p0, normal, color, new Vector2(0f, 1f)));
            verts.Add(new Vertex(p1, normal, color, new Vector2(1f, 1f)));
            verts.Add(new Vertex(p2, normal, color, new Vector2(1f, 0f)));
            verts.Add(new Vertex(p3, normal, color, new Vector2(0f, 0f)));

            var cross = Vector3.Cross(p1 - p0, p2 - p0);
            bool flip = Vector3.Dot(cross, normal) < 0f;

            if (!flip)
            {
                inds.AddRange(new[] { start + 0, start + 1, start + 2, start + 0, start + 2, start + 3 });
            }
            else
            {
                inds.AddRange(new[] { start + 0, start + 2, start + 1, start + 0, start + 3, start + 2 });
            }
        }

        Face(new Vector3(0, 0, -1), new(-h, -h, -h), new(h, -h, -h), new(h, h, -h), new(-h, h, -h));
        Face(new Vector3(0, 0, 1), new(-h, -h, h), new(h, -h, h), new(h, h, h), new(-h, h, h));
        Face(new Vector3(0, -1, 0), new(-h, -h, -h), new(h, -h, -h), new(h, -h, h), new(-h, -h, h));
        Face(new Vector3(0, 1, 0), new(-h, h, -h), new(h, h, -h), new(h, h, h), new(-h, h, h));
        Face(new Vector3(1, 0, 0), new(h, -h, -h), new(h, -h, h), new(h, h, h), new(h, h, -h));
        Face(new Vector3(-1, 0, 0), new(-h, -h, -h), new(-h, -h, h), new(-h, h, h), new(-h, h, -h));

        return new Mesh(verts, inds);
    }

    public static Mesh CreatePyramid(float width, float height, SKColor color)
    {
        var half = width * 0.5f;
        var top = new Vector3(0f, height, 0f);
        var bottomNormal = new Vector3(0f, -1f, 0f);

        var verts = new List<Vertex>
        {
            new(new Vector3(-half, 0f, -half), bottomNormal, color, new Vector2(0f, 1f)),
            new(new Vector3(half, 0f, -half), bottomNormal, color, new Vector2(1f, 1f)),
            new(new Vector3(half, 0f, half), bottomNormal, color, new Vector2(1f, 0f)),
            new(new Vector3(-half, 0f, half), bottomNormal, color, new Vector2(0f, 0f))
        };

        var inds = new List<int> { 0, 1, 2, 0, 2, 3 };

        void Side(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            var start = verts.Count;
            verts.Add(new Vertex(a, normal, color, new Vector2(0f, 1f)));
            verts.Add(new Vertex(b, normal, color, new Vector2(0.5f, 0f)));
            verts.Add(new Vertex(c, normal, color, new Vector2(1f, 1f)));
            inds.AddRange(new[] { start, start + 1, start + 2 });
        }

        var p0 = new Vector3(-half, 0f, -half);
        var p1 = new Vector3(half, 0f, -half);
        var p2 = new Vector3(half, 0f, half);
        var p3 = new Vector3(-half, 0f, half);

        Side(p0, top, p1);
        Side(p1, top, p2);
        Side(p2, top, p3);
        Side(p3, top, p0);

        return new Mesh(verts, inds);
    }

    public static Mesh CreatePlane(float size, SKColor color)
    {
        var half = size * 0.5f;
        var normal = new Vector3(0f, 1f, 0f);
        var vertices = new Vertex[]
        {
            new(new Vector3(-half, 0f, -half), normal, color, new Vector2(0f, 1f)),
            new(new Vector3(half, 0f, -half), normal, color, new Vector2(1f, 1f)),
            new(new Vector3(half, 0f, half), normal, color, new Vector2(1f, 0f)),
            new(new Vector3(-half, 0f, half), normal, color, new Vector2(0f, 0f)),
        };

        var indices = new[] { 0, 1, 2, 0, 2, 3 };
        return new Mesh(vertices, indices);
    }

    public static Mesh CreateSphere(float radius, int slices, int stacks, SKColor color)
    {
        var verts = new List<Vertex>();
        var inds = new List<int>();

        for (int stack = 0; stack <= stacks; stack++)
        {
            var v = (float)stack / stacks;
            var phi = v * MathF.PI;
            var y = MathF.Cos(phi);
            var r = MathF.Sin(phi);
            for (int slice = 0; slice <= slices; slice++)
            {
                var u = (float)slice / slices;
                var theta = u * MathF.PI * 2f;
                var x = r * MathF.Cos(theta);
                var z = r * MathF.Sin(theta);
                var normal = Vector3.Normalize(new Vector3(x, y, z));
                verts.Add(new Vertex(normal * radius, normal, color, new Vector2(u, 1f - v)));
            }
        }

        int stride = slices + 1;
        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                int i0 = stack * stride + slice;
                int i1 = i0 + 1;
                int i2 = i0 + stride;
                int i3 = i2 + 1;
                inds.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
            }
        }

        return new Mesh(verts, inds);
    }

    public static Mesh CreateCylinder(float radius, float height, int segments, SKColor color)
    {
        var verts = new List<Vertex>();
        var inds = new List<int>();
        var halfH = height * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * MathF.PI * 2f;
            var x = MathF.Cos(angle) * radius;
            var z = MathF.Sin(angle) * radius;
            var normal = Vector3.Normalize(new Vector3(x, 0f, z));
            verts.Add(new Vertex(new Vector3(x, -halfH, z), normal, color, new Vector2(t, 0f)));
            verts.Add(new Vertex(new Vector3(x, halfH, z), normal, color, new Vector2(t, 1f)));
        }

        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 2;
            // quad vertices: v0 (bottom), v1 (top), v2 (next bottom), v3 (next top)
            int v0 = baseIndex;
            int v1 = baseIndex + 1;
            int v2 = baseIndex + 2;
            int v3 = baseIndex + 3;

            var p0 = verts[v0].Position;
            var p1 = verts[v1].Position;
            var p2 = verts[v2].Position;
            var faceNormal = Vector3.Normalize(Vector3.Cross(p1 - p0, p2 - p0));
            bool flip = Vector3.Dot(faceNormal, verts[v0].Normal) < 0f;

            if (!flip)
            {
                inds.AddRange(new[] { v0, v1, v3, v0, v3, v2 });
            }
            else
            {
                inds.AddRange(new[] { v0, v3, v1, v0, v2, v3 });
            }
        }

        // top cap
        var topCenterIndex = verts.Count;
        verts.Add(new Vertex(new Vector3(0f, halfH, 0f), new Vector3(0f, 1f, 0f), color, new Vector2(0.5f, 0.5f)));
        for (int i = 0; i < segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * MathF.PI * 2f;
            var x = MathF.Cos(angle) * radius;
            var z = MathF.Sin(angle) * radius;
            var uv = new Vector2((x / (radius * 2f)) + 0.5f, (z / (radius * 2f)) + 0.5f);
            verts.Add(new Vertex(new Vector3(x, halfH, z), new Vector3(0f, 1f, 0f), color, uv));
        }
        for (int i = 0; i < segments; i++)
        {
            int current = topCenterIndex + 1 + i;
            int next = topCenterIndex + 1 + ((i + 1) % segments);
            var p1 = verts[current].Position;
            var p2 = verts[next].Position;
            var cross = Vector3.Cross(p1 - verts[topCenterIndex].Position, p2 - verts[topCenterIndex].Position);
            bool flip = Vector3.Dot(cross, new Vector3(0f, 1f, 0f)) < 0f;
            if (!flip)
            {
                inds.AddRange(new[] { topCenterIndex, current, next });
            }
            else
            {
                inds.AddRange(new[] { topCenterIndex, next, current });
            }
        }

        // bottom cap
        var bottomCenterIndex = verts.Count;
        verts.Add(new Vertex(new Vector3(0f, -halfH, 0f), new Vector3(0f, -1f, 0f), color, new Vector2(0.5f, 0.5f)));
        for (int i = 0; i < segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * MathF.PI * 2f;
            var x = MathF.Cos(angle) * radius;
            var z = MathF.Sin(angle) * radius;
            var uv = new Vector2((x / (radius * 2f)) + 0.5f, (z / (radius * 2f)) + 0.5f);
            verts.Add(new Vertex(new Vector3(x, -halfH, z), new Vector3(0f, -1f, 0f), color, uv));
        }
        for (int i = 0; i < segments; i++)
        {
            int current = bottomCenterIndex + 1 + i;
            int next = bottomCenterIndex + 1 + ((i + 1) % segments);
            var p1 = verts[current].Position;
            var p2 = verts[next].Position;
            var cross = Vector3.Cross(p1 - verts[bottomCenterIndex].Position, p2 - verts[bottomCenterIndex].Position);
            bool flip = Vector3.Dot(cross, new Vector3(0f, -1f, 0f)) < 0f;
            if (!flip)
            {
                inds.AddRange(new[] { bottomCenterIndex, next, current });
            }
            else
            {
                inds.AddRange(new[] { bottomCenterIndex, current, next });
            }
        }

        return new Mesh(verts, inds);
    }

    public static Mesh CreateGrid(int segments, float size, SKColor color, bool twoSided = false)
    {
        var verts = new List<Vertex>();
        var inds = new List<int>();
        var half = size * 0.5f;
        var step = size / segments;
        var normal = new Vector3(0f, 1f, 0f);

        for (int z = 0; z <= segments; z++)
        {
            for (int x = 0; x <= segments; x++)
            {
                float px = -half + x * step;
                float pz = -half + z * step;
                float u = x / (float)segments;
                float v = 1f - (z / (float)segments);
                verts.Add(new Vertex(new Vector3(px, 0f, pz), normal, color, new Vector2(u, v)));
            }
        }

        int stride = segments + 1;
        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int i0 = z * stride + x;
                int i1 = i0 + 1;
                int i2 = i0 + stride;
                int i3 = i2 + 1;
                inds.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
            }
        }

        if (twoSided)
        {
            int vertexOffset = verts.Count;
            for (int i = 0; i < vertexOffset; i++)
            {
                var v = verts[i];
                verts.Add(v with { Normal = -v.Normal });
            }

            int indexCount = inds.Count;
            for (int i = 0; i < indexCount; i += 3)
            {
                inds.Add(vertexOffset + inds[i]);
                inds.Add(vertexOffset + inds[i + 2]);
                inds.Add(vertexOffset + inds[i + 1]);
            }
        }

        return new Mesh(verts, inds);
    }

    public static Mesh CreateFromData(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> indices,
        IReadOnlyList<Vector3>? normals = null,
        IReadOnlyList<SKColor>? colors = null,
        IReadOnlyList<Vector2>? uvs = null,
        IReadOnlyList<Vector4>? tangents = null,
        IReadOnlyList<Vector3>? bitangents = null,
        IReadOnlyList<Vector4>? skinWeights = null,
        IReadOnlyList<Int4>? skinIndices = null,
        IReadOnlyList<MeshMorphTarget>? morphTargets = null)
    {
        var verts = new List<Vertex>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            var normal = normals != null && i < normals.Count ? normals[i] : Vector3.UnitY;
            var color = colors != null && i < colors.Count ? colors[i] : new SKColor(200, 200, 200);
            var uv = uvs != null && i < uvs.Count ? uvs[i] : Vector2.Zero;
            verts.Add(new Vertex(positions[i], normal, color, uv));
        }

        var computedBitangents = bitangents;
        if (computedBitangents == null && tangents != null && tangents.Count == verts.Count)
        {
            computedBitangents = ComputeBitangents(verts, tangents);
        }

        return new Mesh(verts, indices, tangents, computedBitangents, skinWeights, skinIndices, morphTargets);
    }

    public static Mesh CreateFromData(MeshData data, SKColor? fallbackColor = null)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var positions = data.GetPositionsArray();
        var attributes = data.Attributes;
        var normals = attributes?.Normals;
        var uvs = attributes?.TexCoords;
        var colors = attributes?.Colors;
        var tangents = attributes?.Tangents;
        var bitangents = attributes?.Bitangents;
        var skinWeights = attributes?.SkinWeights;
        var skinIndices = attributes?.SkinIndices;
        var morphTargets = attributes?.MorphTargets;

        SKColor[]? skColors = null;
        if (colors != null && colors.Length == positions.Length)
        {
            skColors = new SKColor[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = ToColor(colors[i]);
            }
        }
        else if (fallbackColor.HasValue)
        {
            skColors = new SKColor[positions.Length];
            for (int i = 0; i < skColors.Length; i++)
            {
                skColors[i] = fallbackColor.Value;
            }
        }

        return CreateFromData(positions, data.Indices, normals, skColors, uvs, tangents, bitangents, skinWeights, skinIndices, morphTargets);
    }

    public static MeshData ToMeshData(Mesh mesh)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        var vertices = mesh.Vertices;
        var positions = new Vector3[vertices.Count];
        var normals = new Vector3[vertices.Count];
        var uvs = new Vector2[vertices.Count];
        var colors = new Vector4[vertices.Count];

        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            positions[i] = v.Position;
            normals[i] = v.Normal;
            uvs[i] = v.UV;
            colors[i] = ToVector(v.Color);
        }

        var indices = new int[mesh.Indices.Count];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = mesh.Indices[i];
        }

        Vector4[]? tangents = null;
        if (mesh.Tangents != null && mesh.Tangents.Count == vertices.Count)
        {
            tangents = new Vector4[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                tangents[i] = mesh.Tangents[i];
            }
        }

        Vector3[]? bitangents = null;
        if (mesh.Bitangents != null && mesh.Bitangents.Count == vertices.Count)
        {
            bitangents = new Vector3[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                bitangents[i] = mesh.Bitangents[i];
            }
        }

        Vector4[]? skinWeights = null;
        if (mesh.SkinWeights != null && mesh.SkinWeights.Count == vertices.Count)
        {
            skinWeights = new Vector4[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                skinWeights[i] = mesh.SkinWeights[i];
            }
        }

        Int4[]? skinIndices = null;
        if (mesh.SkinIndices != null && mesh.SkinIndices.Count == vertices.Count)
        {
            skinIndices = new Int4[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                skinIndices[i] = mesh.SkinIndices[i];
            }
        }

        MeshMorphTarget[]? morphTargets = null;
        if (mesh.MorphTargets != null)
        {
            morphTargets = new MeshMorphTarget[mesh.MorphTargets.Count];
            for (int i = 0; i < morphTargets.Length; i++)
            {
                morphTargets[i] = mesh.MorphTargets[i].Clone();
            }
        }

        var attributes = new MeshAttributes(normals, uvs, colors, tangents, bitangents, skinWeights, skinIndices, morphTargets);
        return MeshData.FromPositions(positions, indices, attributes);
    }

    public static IReadOnlyList<Mesh> GenerateLodChain(Mesh mesh, ReadOnlySpan<float> ratios, MeshSimplifyOptions? options = null)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        var source = ToMeshData(mesh);
        var chain = MeshSimplifier.GenerateLodChain(source, ratios, options);
        var result = new List<Mesh>(chain.Count);
        for (int i = 0; i < chain.Count; i++)
        {
            result.Add(CreateFromData(chain[i]));
        }

        return result;
    }

    private static Vector3[] ComputeBitangents(IReadOnlyList<Vertex> vertices, IReadOnlyList<Vector4> tangents)
    {
        var bitangents = new Vector3[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            var n = vertices[i].Normal;
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

    /// <summary>
    /// Minimal OBJ loader: supports "v" and "vn" lines, triangle faces only.
    /// </summary>
    public static Mesh LoadObj(ReadOnlySpan<char> objContent, SKColor defaultColor)
    {
        return LoadObj(objContent, defaultColor, generateNormals: false);
    }

    /// <summary>
    /// Minimal OBJ loader with optional normal generation.
    /// </summary>
    public static Mesh LoadObj(ReadOnlySpan<char> objContent, SKColor defaultColor, bool generateNormals)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texcoords = new List<Vector2>();
        var faces = new List<int>();
        var faceNormals = new List<int>();
        var faceTexcoords = new List<int>();

        foreach (var line in objContent.ToString().Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            switch (parts[0])
            {
                case "v" when parts.Length >= 4:
                    positions.Add(new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3])));
                    break;
                case "vn" when parts.Length >= 4:
                    normals.Add(Vector3.Normalize(new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3]))));
                    break;
                case "vt" when parts.Length >= 3:
                    texcoords.Add(new Vector2(Parse(parts[1]), Parse(parts[2])));
                    break;
                case "f" when parts.Length >= 4:
                    for (int i = 1; i + 2 < parts.Length; i++)
                    {
                        var a = ParseFace(parts[1]);
                        var b = ParseFace(parts[i]);
                        var c = ParseFace(parts[i + 1]);
                        faces.Add(a.pos - 1);
                        faces.Add(b.pos - 1);
                        faces.Add(c.pos - 1);
                        faceNormals.Add(a.normal - 1);
                        faceNormals.Add(b.normal - 1);
                        faceNormals.Add(c.normal - 1);
                        faceTexcoords.Add(a.tex - 1);
                        faceTexcoords.Add(b.tex - 1);
                        faceTexcoords.Add(c.tex - 1);
                    }
                    break;
            }
        }

        var vertexList = new List<Vertex>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            var n = i < normals.Count ? normals[i] : Vector3.UnitY;
            var uv = i < texcoords.Count ? texcoords[i] : Vector2.Zero;
            vertexList.Add(new Vertex(positions[i], n, defaultColor, uv));
        }

        // If per-face normals exist, override per-index normals
        if (faceNormals.Count == faces.Count)
        {
            for (int i = 0; i < faces.Count; i++)
            {
                var idx = faces[i];
                var nIdx = faceNormals[i];
                if ((uint)nIdx < (uint)normals.Count)
                {
                    vertexList[idx] = vertexList[idx] with { Normal = normals[nIdx] };
                }
            }
        }
        if (faceTexcoords.Count == faces.Count)
        {
            for (int i = 0; i < faces.Count; i++)
            {
                var idx = faces[i];
                var tIdx = faceTexcoords[i];
                if ((uint)tIdx < (uint)texcoords.Count)
                {
                    vertexList[idx] = vertexList[idx] with { UV = texcoords[tIdx] };
                }
            }
        }
        else if (generateNormals && normals.Count == 0 && positions.Count > 0)
        {
            var accum = new Vector3[positions.Count];
            for (int i = 0; i + 2 < faces.Count; i += 3)
            {
                var i0 = faces[i];
                var i1 = faces[i + 1];
                var i2 = faces[i + 2];
                if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
                {
                    continue;
                }

                var p0 = positions[i0];
                var p1 = positions[i1];
                var p2 = positions[i2];
                var faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
                accum[i0] += faceNormal;
                accum[i1] += faceNormal;
                accum[i2] += faceNormal;
            }

            for (int i = 0; i < accum.Length; i++)
            {
                var n = accum[i];
                if (n.LengthSquared() > 1e-8f)
                {
                    n = Vector3.Normalize(n);
                    vertexList[i] = vertexList[i] with { Normal = n };
                }
            }
        }

        return new Mesh(vertexList, faces);

        static float Parse(string s) => float.TryParse(s, out var v) ? v : 0f;

        static (int pos, int tex, int normal) ParseFace(string token)
        {
            var parts = token.Split('/');
            int pos = int.Parse(parts[0]);
            int tex = parts.Length >= 2 && parts[1].Length > 0 ? int.Parse(parts[1]) : 0;
            int normal = parts.Length >= 3 && parts[2].Length > 0 ? int.Parse(parts[2]) : 0;
            return (pos, tex, normal);
        }
    }
}
