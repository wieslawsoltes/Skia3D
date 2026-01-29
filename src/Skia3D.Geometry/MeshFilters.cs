using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;

namespace Skia3D.Geometry;

public static class MeshFilters
{
    private const int ParallelTriangleThreshold = 4096;
    private const int TriangleChunkSize = 2048;

    public static Vector3[] LaplacianSmooth(ReadOnlySpan<Vector3> positions, MeshAdjacencyData adjacency, int iterations, float lambda)
    {
        if (adjacency is null)
        {
            throw new ArgumentNullException(nameof(adjacency));
        }

        if (positions.Length == 0)
        {
            return Array.Empty<Vector3>();
        }

        if (iterations <= 0)
        {
            return positions.ToArray();
        }

        lambda = Math.Clamp(lambda, 0f, 1f);
        var current = positions.ToArray();
        var next = new Vector3[current.Length];

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < current.Length; i++)
            {
                var neighbors = adjacency.GetNeighbors(i);
                if (neighbors.Length == 0)
                {
                    next[i] = current[i];
                    continue;
                }

                var sum = Vector3.Zero;
                for (int n = 0; n < neighbors.Length; n++)
                {
                    sum += current[neighbors[n]];
                }

                var avg = sum / neighbors.Length;
                next[i] = Vector3.Lerp(current[i], avg, lambda);
            }

            (current, next) = (next, current);
        }

        return current;
    }

    public static Vector3[] ComputeTriangleNormals(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices)
    {
        if (positions.Length == 0 || indices.Length == 0)
        {
            return Array.Empty<Vector3>();
        }

        int triCount = indices.Length / 3;
        var normals = new Vector3[triCount];
        for (int i = 0; i < triCount; i++)
        {
            int baseIndex = i * 3;
            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];

            if (!IsValid(i0, positions.Length) || !IsValid(i1, positions.Length) || !IsValid(i2, positions.Length))
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.LengthSquared() > 1e-8f)
            {
                n = Vector3.Normalize(n);
            }
            normals[i] = n;
        }

        return normals;
    }

    public static Vector3[] ComputeVertexNormals(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices)
    {
        if (positions.Length == 0 || indices.Length == 0)
        {
            return Array.Empty<Vector3>();
        }

        var normals = new Vector3[positions.Length];
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if (!IsValid(i0, positions.Length) || !IsValid(i1, positions.Length) || !IsValid(i2, positions.Length))
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var n = Vector3.Cross(p1 - p0, p2 - p0);
            normals[i0] += n;
            normals[i1] += n;
            normals[i2] += n;
        }

        NormalizeNormals(normals);
        return normals;
    }

    public static Vector3[] ComputeVertexNormalsParallel(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, int? maxDegreeOfParallelism = null)
    {
        if (positions.Length == 0 || indices.Length == 0)
        {
            return Array.Empty<Vector3>();
        }

        if (indices.Length < ParallelTriangleThreshold)
        {
            return ComputeVertexNormals(positions, indices);
        }

        var positionsArray = positions.ToArray();
        var indicesArray = indices.ToArray();
        int vertexCount = positionsArray.Length;
        var normals = new Vector3[vertexCount];
        int triCount = indicesArray.Length / 3;
        var ranges = Partitioner.Create(0, triCount, TriangleChunkSize);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism ?? -1
        };

        object gate = new();

        Parallel.ForEach(
            ranges,
            options,
            () => new Vector3[vertexCount],
            (range, _, local) =>
            {
                int end = Math.Min(range.Item2, triCount);
                for (int tri = range.Item1; tri < end; tri++)
                {
                    int baseIndex = tri * 3;
                    int i0 = indicesArray[baseIndex];
                    int i1 = indicesArray[baseIndex + 1];
                    int i2 = indicesArray[baseIndex + 2];

                    if (!IsValid(i0, vertexCount) || !IsValid(i1, vertexCount) || !IsValid(i2, vertexCount))
                    {
                        continue;
                    }

                    var p0 = positionsArray[i0];
                    var p1 = positionsArray[i1];
                    var p2 = positionsArray[i2];
                    var n = Vector3.Cross(p1 - p0, p2 - p0);
                    local[i0] += n;
                    local[i1] += n;
                    local[i2] += n;
                }

                return local;
            },
            local =>
            {
                lock (gate)
                {
                    for (int i = 0; i < vertexCount; i++)
                    {
                        normals[i] += local[i];
                    }
                }
            });

        NormalizeNormals(normals);
        return normals;
    }

    private static void NormalizeNormals(Vector3[] normals)
    {
        for (int i = 0; i < normals.Length; i++)
        {
            var n = normals[i];
            if (n.LengthSquared() > 1e-8f)
            {
                normals[i] = Vector3.Normalize(n);
            }
            else
            {
                normals[i] = Vector3.UnitY;
            }
        }
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;
}
