using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;

namespace Skia3D.Geometry;

public static class GeometryKernels
{
    private const int ParallelThreshold = 8192;
    private const int ChunkSize = 4096;

    public static Aabb ComputeAabb(ReadOnlySpan<Vector3> positions)
    {
        if (positions.Length == 0)
        {
            return Aabb.Empty;
        }

        var min = positions[0];
        var max = positions[0];

        for (int i = 1; i < positions.Length; i++)
        {
            var p = positions[i];
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new Aabb(min, max);
    }

    public static Aabb ComputeAabb(SoaVector3 positions)
    {
        if (positions.Length == 0)
        {
            return Aabb.Empty;
        }

        return ComputeAabbRange(positions.X, positions.Y, positions.Z, 0, positions.Length);
    }

    public static Aabb ComputeAabbParallel(SoaVector3 positions, int? maxDegreeOfParallelism = null)
    {
        if (positions.Length == 0)
        {
            return Aabb.Empty;
        }

        if (positions.Length < ParallelThreshold)
        {
            return ComputeAabb(positions);
        }

        var ranges = Partitioner.Create(0, positions.Length, ChunkSize);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism ?? -1
        };

        object gate = new();
        var global = Aabb.Empty;

        Parallel.ForEach(
            ranges,
            options,
            () => Aabb.Empty,
            (range, _, local) =>
            {
                var aabb = ComputeAabbRange(positions.X, positions.Y, positions.Z, range.Item1, range.Item2 - range.Item1);
                return Aabb.Merge(local, aabb);
            },
            local =>
            {
                lock (gate)
                {
                    global = Aabb.Merge(global, local);
                }
            });

        return global;
    }

    public static float ComputeBoundingRadius(ReadOnlySpan<Vector3> positions)
    {
        if (positions.Length == 0)
        {
            return 0f;
        }

        float maxLenSq = 0f;
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            var lenSq = p.X * p.X + p.Y * p.Y + p.Z * p.Z;
            if (lenSq > maxLenSq)
            {
                maxLenSq = lenSq;
            }
        }

        return MathF.Sqrt(maxLenSq);
    }

    public static float ComputeBoundingRadius(SoaVector3 positions)
    {
        var count = positions.Length;
        if (count == 0)
        {
            return 0f;
        }

        var xs = positions.X;
        var ys = positions.Y;
        var zs = positions.Z;

        float maxLenSq = 0f;
        int i = 0;
        int vecSize = Vector<float>.Count;

        if (count >= vecSize)
        {
            int simdEnd = count - (count % vecSize);
            var maxVec = Vector<float>.Zero;

            for (; i < simdEnd; i += vecSize)
            {
                var vx = new Vector<float>(xs, i);
                var vy = new Vector<float>(ys, i);
                var vz = new Vector<float>(zs, i);
                var lenSq = (vx * vx) + (vy * vy) + (vz * vz);
                maxVec = Vector.Max(maxVec, lenSq);
            }

            maxLenSq = ReduceMax(maxVec);
        }

        for (; i < count; i++)
        {
            var lenSq = xs[i] * xs[i] + ys[i] * ys[i] + zs[i] * zs[i];
            if (lenSq > maxLenSq)
            {
                maxLenSq = lenSq;
            }
        }

        return MathF.Sqrt(maxLenSq);
    }

    private static Aabb ComputeAabbRange(float[] xs, float[] ys, float[] zs, int start, int length)
    {
        if (length <= 0)
        {
            return Aabb.Empty;
        }

        int end = start + length;
        float minX = xs[start];
        float maxX = xs[start];
        float minY = ys[start];
        float maxY = ys[start];
        float minZ = zs[start];
        float maxZ = zs[start];

        int i = start + 1;
        int vecSize = Vector<float>.Count;

        if (length >= vecSize)
        {
            int simdStart = start;
            int simdEnd = end - ((end - simdStart) % vecSize);

            var minXVec = new Vector<float>(xs, simdStart);
            var maxXVec = minXVec;
            var minYVec = new Vector<float>(ys, simdStart);
            var maxYVec = minYVec;
            var minZVec = new Vector<float>(zs, simdStart);
            var maxZVec = minZVec;

            i = simdStart + vecSize;
            for (; i < simdEnd; i += vecSize)
            {
                var vx = new Vector<float>(xs, i);
                var vy = new Vector<float>(ys, i);
                var vz = new Vector<float>(zs, i);

                minXVec = Vector.Min(minXVec, vx);
                maxXVec = Vector.Max(maxXVec, vx);
                minYVec = Vector.Min(minYVec, vy);
                maxYVec = Vector.Max(maxYVec, vy);
                minZVec = Vector.Min(minZVec, vz);
                maxZVec = Vector.Max(maxZVec, vz);
            }

            ReduceMinMax(minXVec, maxXVec, out var simdMinX, out var simdMaxX);
            ReduceMinMax(minYVec, maxYVec, out var simdMinY, out var simdMaxY);
            ReduceMinMax(minZVec, maxZVec, out var simdMinZ, out var simdMaxZ);

            minX = MathF.Min(minX, simdMinX);
            maxX = MathF.Max(maxX, simdMaxX);
            minY = MathF.Min(minY, simdMinY);
            maxY = MathF.Max(maxY, simdMaxY);
            minZ = MathF.Min(minZ, simdMinZ);
            maxZ = MathF.Max(maxZ, simdMaxZ);

            i = simdEnd;
        }

        for (; i < end; i++)
        {
            var x = xs[i];
            var y = ys[i];
            var z = zs[i];

            if (x < minX)
            {
                minX = x;
            }
            else if (x > maxX)
            {
                maxX = x;
            }

            if (y < minY)
            {
                minY = y;
            }
            else if (y > maxY)
            {
                maxY = y;
            }

            if (z < minZ)
            {
                minZ = z;
            }
            else if (z > maxZ)
            {
                maxZ = z;
            }
        }

        return new Aabb(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
    }

    private static void ReduceMinMax(Vector<float> minVec, Vector<float> maxVec, out float min, out float max)
    {
        Span<float> scratch = stackalloc float[Vector<float>.Count];

        minVec.CopyTo(scratch);
        min = scratch[0];
        for (int i = 1; i < scratch.Length; i++)
        {
            if (scratch[i] < min)
            {
                min = scratch[i];
            }
        }

        maxVec.CopyTo(scratch);
        max = scratch[0];
        for (int i = 1; i < scratch.Length; i++)
        {
            if (scratch[i] > max)
            {
                max = scratch[i];
            }
        }
    }

    private static float ReduceMax(Vector<float> maxVec)
    {
        Span<float> scratch = stackalloc float[Vector<float>.Count];
        maxVec.CopyTo(scratch);
        float max = scratch[0];
        for (int i = 1; i < scratch.Length; i++)
        {
            if (scratch[i] > max)
            {
                max = scratch[i];
            }
        }

        return max;
    }
}
