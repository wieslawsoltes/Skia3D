using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Geometry;

namespace Skia3D.Acceleration;

public sealed class UniformGrid
{
    private readonly SoaVector3 _positions;
    private readonly int[] _indices;
    private readonly Aabb _bounds;
    private readonly int _cellsX;
    private readonly int _cellsY;
    private readonly int _cellsZ;
    private readonly Vector3 _cellSize;
    private readonly Vector3 _invCellSize;
    private readonly int[] _cellOffsets;
    private readonly int[] _cellTriangles;

    private UniformGrid(
        SoaVector3 positions,
        int[] indices,
        Aabb bounds,
        int cellsX,
        int cellsY,
        int cellsZ,
        Vector3 cellSize,
        Vector3 invCellSize,
        int[] cellOffsets,
        int[] cellTriangles)
    {
        _positions = positions;
        _indices = indices;
        _bounds = bounds;
        _cellsX = cellsX;
        _cellsY = cellsY;
        _cellsZ = cellsZ;
        _cellSize = cellSize;
        _invCellSize = invCellSize;
        _cellOffsets = cellOffsets;
        _cellTriangles = cellTriangles;
    }

    public Aabb Bounds => _bounds;

    public int CellsX => _cellsX;

    public int CellsY => _cellsY;

    public int CellsZ => _cellsZ;

    public static UniformGrid Build(SoaVector3 positions, int[] indices, int cellsPerAxis = 16)
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
            return new UniformGrid(positions, indices, Aabb.Empty, 0, 0, 0, Vector3.Zero, Vector3.Zero, Array.Empty<int>(), Array.Empty<int>());
        }

        var bounds = GeometryKernels.ComputeAabb(positions);
        var size = bounds.Size;
        var maxExtent = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        if (maxExtent < 1e-6f)
        {
            return new UniformGrid(positions, indices, bounds, 1, 1, 1, Vector3.Zero, Vector3.Zero, new[] { 0, 0 }, Array.Empty<int>());
        }

        cellsPerAxis = Math.Clamp(cellsPerAxis, 2, 128);
        int cellsX = Math.Max(1, (int)MathF.Round(cellsPerAxis * (size.X / maxExtent)));
        int cellsY = Math.Max(1, (int)MathF.Round(cellsPerAxis * (size.Y / maxExtent)));
        int cellsZ = Math.Max(1, (int)MathF.Round(cellsPerAxis * (size.Z / maxExtent)));

        var cellSize = new Vector3(size.X / cellsX, size.Y / cellsY, size.Z / cellsZ);
        var invCellSize = new Vector3(
            cellSize.X > 1e-6f ? 1f / cellSize.X : 0f,
            cellSize.Y > 1e-6f ? 1f / cellSize.Y : 0f,
            cellSize.Z > 1e-6f ? 1f / cellSize.Z : 0f);

        int cellCount = cellsX * cellsY * cellsZ;
        var buckets = new List<int>[cellCount];

        int triCount = indices.Length / 3;
        var xs = positions.X;
        var ys = positions.Y;
        var zs = positions.Z;

        for (int tri = 0; tri < triCount; tri++)
        {
            int baseIndex = tri * 3;
            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];

            if ((uint)i0 >= (uint)positions.Length || (uint)i1 >= (uint)positions.Length || (uint)i2 >= (uint)positions.Length)
            {
                continue;
            }

            var v0 = new Vector3(xs[i0], ys[i0], zs[i0]);
            var v1 = new Vector3(xs[i1], ys[i1], zs[i1]);
            var v2 = new Vector3(xs[i2], ys[i2], zs[i2]);

            var min = Vector3.Min(v0, Vector3.Min(v1, v2));
            var max = Vector3.Max(v0, Vector3.Max(v1, v2));

            int minX = ToCell(min.X, bounds.Min.X, invCellSize.X, cellsX);
            int minY = ToCell(min.Y, bounds.Min.Y, invCellSize.Y, cellsY);
            int minZ = ToCell(min.Z, bounds.Min.Z, invCellSize.Z, cellsZ);
            int maxX = ToCell(max.X, bounds.Min.X, invCellSize.X, cellsX);
            int maxY = ToCell(max.Y, bounds.Min.Y, invCellSize.Y, cellsY);
            int maxZ = ToCell(max.Z, bounds.Min.Z, invCellSize.Z, cellsZ);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int cellIndex = ToIndex(x, y, z, cellsX, cellsY);
                        var bucket = buckets[cellIndex];
                        if (bucket is null)
                        {
                            bucket = new List<int>();
                            buckets[cellIndex] = bucket;
                        }
                        bucket.Add(tri);
                    }
                }
            }
        }

        var offsets = new int[cellCount + 1];
        int total = 0;
        for (int i = 0; i < cellCount; i++)
        {
            offsets[i] = total;
            total += buckets[i]?.Count ?? 0;
        }
        offsets[cellCount] = total;

        var triangles = new int[total];
        int write = 0;
        for (int i = 0; i < cellCount; i++)
        {
            if (buckets[i] is null)
            {
                continue;
            }

            foreach (var tri in buckets[i])
            {
                triangles[write++] = tri;
            }
        }

        return new UniformGrid(positions, indices, bounds, cellsX, cellsY, cellsZ, cellSize, invCellSize, offsets, triangles);
    }

    public bool TryIntersectRay(in Ray ray, float maxDistance, bool doubleSided, bool cullBackface, out BvhHit hit)
    {
        hit = default;
        if (_cellsX == 0 || _cellsY == 0 || _cellsZ == 0)
        {
            return false;
        }

        if (!IntersectAabb(ray, _bounds, maxDistance, out var tmin, out var tmax))
        {
            return false;
        }

        float bestT = maxDistance;
        bool hitAny = false;

        var origin = ray.Origin + ray.Direction * MathF.Max(tmin, 0f);
        int ix = ToCell(origin.X, _bounds.Min.X, _invCellSize.X, _cellsX);
        int iy = ToCell(origin.Y, _bounds.Min.Y, _invCellSize.Y, _cellsY);
        int iz = ToCell(origin.Z, _bounds.Min.Z, _invCellSize.Z, _cellsZ);

        float dx = ray.Direction.X;
        float dy = ray.Direction.Y;
        float dz = ray.Direction.Z;

        int stepX = dx >= 0f ? 1 : -1;
        int stepY = dy >= 0f ? 1 : -1;
        int stepZ = dz >= 0f ? 1 : -1;

        float nextX = _bounds.Min.X + (ix + (stepX > 0 ? 1 : 0)) * _cellSize.X;
        float nextY = _bounds.Min.Y + (iy + (stepY > 0 ? 1 : 0)) * _cellSize.Y;
        float nextZ = _bounds.Min.Z + (iz + (stepZ > 0 ? 1 : 0)) * _cellSize.Z;

        float tMaxX = MathF.Abs(dx) < 1e-8f ? float.PositiveInfinity : (nextX - origin.X) / dx;
        float tMaxY = MathF.Abs(dy) < 1e-8f ? float.PositiveInfinity : (nextY - origin.Y) / dy;
        float tMaxZ = MathF.Abs(dz) < 1e-8f ? float.PositiveInfinity : (nextZ - origin.Z) / dz;

        float tDeltaX = MathF.Abs(dx) < 1e-8f ? float.PositiveInfinity : _cellSize.X / MathF.Abs(dx);
        float tDeltaY = MathF.Abs(dy) < 1e-8f ? float.PositiveInfinity : _cellSize.Y / MathF.Abs(dy);
        float tDeltaZ = MathF.Abs(dz) < 1e-8f ? float.PositiveInfinity : _cellSize.Z / MathF.Abs(dz);

        float t = MathF.Max(tmin, 0f);

        while (ix >= 0 && ix < _cellsX && iy >= 0 && iy < _cellsY && iz >= 0 && iz < _cellsZ)
        {
            int cellIndex = ToIndex(ix, iy, iz, _cellsX, _cellsY);
            int start = _cellOffsets[cellIndex];
            int end = _cellOffsets[cellIndex + 1];

            for (int i = start; i < end; i++)
            {
                int triIndex = _cellTriangles[i];
                if (!TryIntersectTriangle(ray, triIndex, doubleSided, cullBackface, out var hitT, out var normal, out var bary))
                {
                    continue;
                }

                if (hitT <= 0f || hitT >= bestT)
                {
                    continue;
                }

                bestT = hitT;
                var position = ray.Origin + ray.Direction * hitT;
                hit = new BvhHit(triIndex, hitT, position, normal, bary);
                hitAny = true;
            }

            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    ix += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    iz += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    iy += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    iz += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                }
            }

            if (t > tmax || t > bestT)
            {
                break;
            }
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

    private static bool IntersectAabb(in Ray ray, Aabb bounds, float maxDistance, out float tmin, out float tmax)
    {
        tmin = 0f;
        tmax = maxDistance;

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

    private static int ToCell(float value, float min, float invCell, int cells)
    {
        if (cells <= 1 || invCell <= 0f)
        {
            return 0;
        }

        int cell = (int)MathF.Floor((value - min) * invCell);
        return Math.Clamp(cell, 0, cells - 1);
    }

    private static int ToIndex(int x, int y, int z, int cellsX, int cellsY)
    {
        return (z * cellsY + y) * cellsX + x;
    }
}
