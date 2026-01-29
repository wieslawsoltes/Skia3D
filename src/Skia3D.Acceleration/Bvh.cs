using System;
using System.Collections.Generic;
using System.Numerics;
using System.Buffers;
using System.Threading.Tasks;
using Skia3D.Geometry;

namespace Skia3D.Acceleration;

public readonly record struct BvhHit(
    int TriangleIndex,
    float Distance,
    Vector3 Position,
    Vector3 Normal,
    Vector3 Barycentric);

public readonly record struct BvhNode(
    Aabb Bounds,
    int Left,
    int Right,
    int Start,
    int Count)
{
    public bool IsLeaf => Count > 0;
}

public sealed class Bvh
{
    private SoaVector3 _positions;
    private readonly int[] _indices;
    private BvhNode[] _nodes;
    private readonly int[] _triangles;

    private Bvh(SoaVector3 positions, int[] indices, BvhNode[] nodes, int[] triangles, int root)
    {
        _positions = positions;
        _indices = indices;
        _nodes = nodes;
        _triangles = triangles;
        RootIndex = root;
    }

    public int RootIndex { get; }

    public IReadOnlyList<BvhNode> Nodes => _nodes;

    public static Bvh Build(SoaVector3 positions, int[] indices, int maxLeafSize = 8)
    {
        if (indices is null)
        {
            throw new ArgumentNullException(nameof(indices));
        }

        if (indices.Length % 3 != 0)
        {
            throw new ArgumentException("Indices length must be divisible by 3.", nameof(indices));
        }

        if (positions.Length == 0 || indices.Length == 0)
        {
            return new Bvh(positions, indices, Array.Empty<BvhNode>(), Array.Empty<int>(), -1);
        }

        var builder = new Builder(positions, indices, maxLeafSize);
        var root = builder.Build(0, builder.TriangleCount);
        return new Bvh(positions, indices, builder.Nodes.ToArray(), builder.Triangles, root);
    }

    public void Refit(bool parallel = false)
    {
        Refit(_positions, parallel);
    }

    public void Refit(SoaVector3 positions, bool parallel = false)
    {
        _positions = positions;
        if (_nodes.Length == 0 || RootIndex < 0)
        {
            return;
        }

        if (parallel)
        {
            Parallel.For(0, _nodes.Length, i =>
            {
                var node = _nodes[i];
                if (!node.IsLeaf)
                {
                    return;
                }

                var bounds = ComputeLeafBounds(node.Start, node.Count);
                _nodes[i] = node with { Bounds = bounds };
            });
        }
        else
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (!node.IsLeaf)
                {
                    continue;
                }

                var bounds = ComputeLeafBounds(node.Start, node.Count);
                _nodes[i] = node with { Bounds = bounds };
            }
        }

        for (int i = _nodes.Length - 1; i >= 0; i--)
        {
            var node = _nodes[i];
            if (node.IsLeaf)
            {
                continue;
            }

            var left = _nodes[node.Left].Bounds;
            var right = _nodes[node.Right].Bounds;
            _nodes[i] = node with { Bounds = Aabb.Merge(left, right) };
        }
    }

    public bool TryIntersectRay(in Ray ray, float maxDistance, bool doubleSided, bool cullBackface, out BvhHit hit)
    {
        hit = default;
        if (_nodes.Length == 0 || RootIndex < 0)
        {
            return false;
        }

        var stack = ArrayPool<int>.Shared.Rent(128);
        int stackCount = 0;
        bool hitAny = false;
        float bestT = maxDistance;

        try
        {
            stack[stackCount++] = RootIndex;

            while (stackCount > 0)
            {
                var nodeIndex = stack[--stackCount];
                var node = _nodes[nodeIndex];

                if (!IntersectAabb(ray, node.Bounds, bestT))
                {
                    continue;
                }

                if (node.IsLeaf)
                {
                    for (int i = 0; i < node.Count; i++)
                    {
                        var triIndex = _triangles[node.Start + i];
                        if (!TryIntersectTriangle(ray, triIndex, doubleSided, cullBackface, out var t, out var normal, out var barycentric))
                        {
                            continue;
                        }

                        if (t <= 0f || t >= bestT)
                        {
                            continue;
                        }

                        bestT = t;
                        var position = ray.Origin + ray.Direction * t;
                        hit = new BvhHit(triIndex, t, position, normal, barycentric);
                        hitAny = true;
                    }
                }
                else
                {
                    EnsureStackCapacity(ref stack, stackCount + 2);
                    stack[stackCount++] = node.Left;
                    stack[stackCount++] = node.Right;
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(stack);
        }

        return hitAny;
    }

    private bool TryIntersectTriangle(in Ray ray, int triangleIndex, bool doubleSided, bool cullBackface, out float t, out Vector3 normal, out Vector3 barycentric)
    {
        t = 0f;
        normal = default;
        barycentric = default;

        var idx = triangleIndex * 3;
        if ((uint)(idx + 2) >= (uint)_indices.Length)
        {
            return false;
        }

        int i0 = _indices[idx];
        int i1 = _indices[idx + 1];
        int i2 = _indices[idx + 2];

        if ((uint)i0 >= (uint)_positions.Length || (uint)i1 >= (uint)_positions.Length || (uint)i2 >= (uint)_positions.Length)
        {
            return false;
        }

        var xs = _positions.X;
        var ys = _positions.Y;
        var zs = _positions.Z;

        var v0 = new Vector3(xs[i0], ys[i0], zs[i0]);
        var v1 = new Vector3(xs[i1], ys[i1], zs[i1]);
        var v2 = new Vector3(xs[i2], ys[i2], zs[i2]);

        return IntersectTriangle(ray, v0, v1, v2, doubleSided, cullBackface, out t, out normal, out barycentric);
    }

    private static bool IntersectTriangle(in Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, bool doubleSided, bool cullBackface, out float t, out Vector3 normal, out Vector3 barycentric)
    {
        const float epsilon = 1e-6f;
        t = 0f;
        normal = default;
        barycentric = default;

        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var pvec = Vector3.Cross(ray.Direction, edge2);
        var det = Vector3.Dot(edge1, pvec);

        if (cullBackface && !doubleSided)
        {
            if (det < epsilon)
            {
                return false;
            }
        }
        else
        {
            if (MathF.Abs(det) < epsilon)
            {
                return false;
            }
        }

        var invDet = 1f / det;
        var tvec = ray.Origin - v0;
        var u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0f || u > 1f)
        {
            return false;
        }

        var qvec = Vector3.Cross(tvec, edge1);
        var v = Vector3.Dot(ray.Direction, qvec) * invDet;
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        t = Vector3.Dot(edge2, qvec) * invDet;
        if (t < 0f)
        {
            return false;
        }

        barycentric = new Vector3(1f - u - v, u, v);
        normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
        if (Vector3.Dot(normal, ray.Direction) > 0f)
        {
            normal = -normal;
        }

        return true;
    }

    private static bool IntersectAabb(in Ray ray, Aabb bounds, float maxDistance)
    {
        float tmin = 0f;
        float tmax = maxDistance;

        if (!IntersectAxis(ray.Origin.X, ray.Direction.X, bounds.Min.X, bounds.Max.X, ref tmin, ref tmax))
        {
            return false;
        }

        if (!IntersectAxis(ray.Origin.Y, ray.Direction.Y, bounds.Min.Y, bounds.Max.Y, ref tmin, ref tmax))
        {
            return false;
        }

        if (!IntersectAxis(ray.Origin.Z, ray.Direction.Z, bounds.Min.Z, bounds.Max.Z, ref tmin, ref tmax))
        {
            return false;
        }

        return tmax >= tmin;
    }

    private static bool IntersectAxis(float origin, float direction, float min, float max, ref float tmin, ref float tmax)
    {
        const float epsilon = 1e-8f;

        if (MathF.Abs(direction) < epsilon)
        {
            return origin >= min && origin <= max;
        }

        var inv = 1f / direction;
        var t0 = (min - origin) * inv;
        var t1 = (max - origin) * inv;
        if (t0 > t1)
        {
            (t0, t1) = (t1, t0);
        }

        if (t0 > tmin)
        {
            tmin = t0;
        }

        if (t1 < tmax)
        {
            tmax = t1;
        }

        return tmax >= tmin;
    }

    private static void EnsureStackCapacity(ref int[] stack, int required)
    {
        if (required <= stack.Length)
        {
            return;
        }

        var nextSize = stack.Length * 2;
        while (nextSize < required)
        {
            nextSize *= 2;
        }

        var newStack = ArrayPool<int>.Shared.Rent(nextSize);
        Array.Copy(stack, newStack, stack.Length);
        ArrayPool<int>.Shared.Return(stack);
        stack = newStack;
    }

    private Aabb ComputeLeafBounds(int start, int count)
    {
        var bounds = Aabb.Empty;
        for (int i = 0; i < count; i++)
        {
            int triIndex = _triangles[start + i];
            bounds = Aabb.Merge(bounds, ComputeTriangleBounds(triIndex));
        }

        return bounds;
    }

    private Aabb ComputeTriangleBounds(int triIndex)
    {
        var baseIndex = triIndex * 3;
        if ((uint)(baseIndex + 2) >= (uint)_indices.Length)
        {
            return Aabb.Empty;
        }

        int i0 = _indices[baseIndex];
        int i1 = _indices[baseIndex + 1];
        int i2 = _indices[baseIndex + 2];

        if ((uint)i0 >= (uint)_positions.Length || (uint)i1 >= (uint)_positions.Length || (uint)i2 >= (uint)_positions.Length)
        {
            return Aabb.Empty;
        }

        var xs = _positions.X;
        var ys = _positions.Y;
        var zs = _positions.Z;

        var v0 = new Vector3(xs[i0], ys[i0], zs[i0]);
        var v1 = new Vector3(xs[i1], ys[i1], zs[i1]);
        var v2 = new Vector3(xs[i2], ys[i2], zs[i2]);
        var min = Vector3.Min(v0, Vector3.Min(v1, v2));
        var max = Vector3.Max(v0, Vector3.Max(v1, v2));
        return new Aabb(min, max);
    }

    private sealed class Builder
    {
        private readonly SoaVector3 _positions;
        private readonly int[] _indices;
        private readonly int _maxLeafSize;
        private readonly Aabb[] _triBounds;
        private readonly Vector3[] _centroids;
        private readonly AxisComparer _xComparer;
        private readonly AxisComparer _yComparer;
        private readonly AxisComparer _zComparer;

        public Builder(SoaVector3 positions, int[] indices, int maxLeafSize)
        {
            _positions = positions;
            _indices = indices;
            _maxLeafSize = Math.Max(2, maxLeafSize);

            var triCount = indices.Length / 3;
            Triangles = new int[triCount];
            _triBounds = new Aabb[triCount];
            _centroids = new Vector3[triCount];

            var xs = positions.X;
            var ys = positions.Y;
            var zs = positions.Z;

            for (int i = 0; i < triCount; i++)
            {
                Triangles[i] = i;

                var baseIndex = i * 3;
                int i0 = indices[baseIndex];
                int i1 = indices[baseIndex + 1];
                int i2 = indices[baseIndex + 2];

                if ((uint)i0 >= (uint)positions.Length || (uint)i1 >= (uint)positions.Length || (uint)i2 >= (uint)positions.Length)
                {
                    _triBounds[i] = Aabb.Empty;
                    _centroids[i] = Vector3.Zero;
                    continue;
                }

                var v0 = new Vector3(xs[i0], ys[i0], zs[i0]);
                var v1 = new Vector3(xs[i1], ys[i1], zs[i1]);
                var v2 = new Vector3(xs[i2], ys[i2], zs[i2]);

                var min = Vector3.Min(v0, Vector3.Min(v1, v2));
                var max = Vector3.Max(v0, Vector3.Max(v1, v2));
                _triBounds[i] = new Aabb(min, max);
                _centroids[i] = (v0 + v1 + v2) * (1f / 3f);
            }

            _xComparer = new AxisComparer(_centroids, 0);
            _yComparer = new AxisComparer(_centroids, 1);
            _zComparer = new AxisComparer(_centroids, 2);
        }

        public int TriangleCount => Triangles.Length;

        public int[] Triangles { get; }

        public List<BvhNode> Nodes { get; } = new();

        public int Build(int start, int count)
        {
            var bounds = ComputeBounds(start, count);
            if (count <= _maxLeafSize)
            {
                var leafIndex = Nodes.Count;
                Nodes.Add(new BvhNode(bounds, -1, -1, start, count));
                return leafIndex;
            }

            var centroidBounds = ComputeCentroidBounds(start, count);
            var extent = centroidBounds.Size;

            int axis = 0;
            if (extent.Y > extent.X && extent.Y >= extent.Z)
            {
                axis = 1;
            }
            else if (extent.Z > extent.X && extent.Z > extent.Y)
            {
                axis = 2;
            }

            if (extent.X < 1e-6f && extent.Y < 1e-6f && extent.Z < 1e-6f)
            {
                var leafIndex = Nodes.Count;
                Nodes.Add(new BvhNode(bounds, -1, -1, start, count));
                return leafIndex;
            }

            SortByAxis(start, count, axis);
            var mid = start + (count / 2);

            var nodeIndex = Nodes.Count;
            Nodes.Add(default);

            var left = Build(start, mid - start);
            var right = Build(mid, start + count - mid);

            Nodes[nodeIndex] = new BvhNode(bounds, left, right, 0, 0);
            return nodeIndex;
        }

        private void SortByAxis(int start, int count, int axis)
        {
            switch (axis)
            {
                case 0:
                    Array.Sort(Triangles, start, count, _xComparer);
                    break;
                case 1:
                    Array.Sort(Triangles, start, count, _yComparer);
                    break;
                default:
                    Array.Sort(Triangles, start, count, _zComparer);
                    break;
            }
        }

        private Aabb ComputeBounds(int start, int count)
        {
            var bounds = Aabb.Empty;
            for (int i = 0; i < count; i++)
            {
                bounds = Aabb.Merge(bounds, _triBounds[Triangles[start + i]]);
            }
            return bounds;
        }

        private Aabb ComputeCentroidBounds(int start, int count)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < count; i++)
            {
                var c = _centroids[Triangles[start + i]];
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }

            return new Aabb(min, max);
        }
    }

    private sealed class AxisComparer : IComparer<int>
    {
        private readonly Vector3[] _centroids;
        private readonly int _axis;

        public AxisComparer(Vector3[] centroids, int axis)
        {
            _centroids = centroids;
            _axis = axis;
        }

        public int Compare(int a, int b)
        {
            var ca = _axis switch
            {
                1 => _centroids[a].Y,
                2 => _centroids[a].Z,
                _ => _centroids[a].X
            };

            var cb = _axis switch
            {
                1 => _centroids[b].Y,
                2 => _centroids[b].Z,
                _ => _centroids[b].X
            };

            return ca.CompareTo(cb);
        }
    }
}
