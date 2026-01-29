using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Geometry;

namespace Skia3D.Modeling;

public enum ProportionalFalloff
{
    Linear,
    Smooth,
    Sharp,
    Root
}

public static class ProportionalEditing
{
    public static int ApplyTransform(
        EditableMesh mesh,
        Matrix4x4 transform,
        IReadOnlyCollection<int> selection,
        float radius,
        ProportionalFalloff falloff)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (selection is null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (selection.Count == 0 || mesh.Positions.Count == 0)
        {
            return 0;
        }

        radius = MathF.Max(radius, 1e-4f);
        var distances = ComputeDistances(mesh, selection, radius);

        int affected = 0;
        for (int i = 0; i < mesh.Positions.Count; i++)
        {
            var d = distances[i];
            if (!float.IsFinite(d) || d > radius)
            {
                continue;
            }

            var weight = ComputeWeight(d, radius, falloff);
            if (weight <= 0f)
            {
                continue;
            }

            var p = mesh.Positions[i];
            var target = Vector3.Transform(p, transform);
            mesh.Positions[i] = Vector3.Lerp(p, target, weight);
            affected++;
        }

        if (affected > 0)
        {
            mesh.InvalidateNormals();
        }

        return affected;
    }

    private static float[] ComputeDistances(EditableMesh mesh, IReadOnlyCollection<int> selection, float radius)
    {
        int vertexCount = mesh.VertexCount;
        var distances = new float[vertexCount];
        Array.Fill(distances, float.PositiveInfinity);

        var indices = mesh.Indices.ToArray();
        var adjacency = MeshAdjacencyBuilder.BuildVertexAdjacency(vertexCount, indices);
        var queue = new PriorityQueue<int, float>();

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)vertexCount)
            {
                continue;
            }

            if (distances[index] > 0f)
            {
                distances[index] = 0f;
                queue.Enqueue(index, 0f);
            }
        }

        var positions = mesh.Positions;
        while (queue.TryDequeue(out var vertex, out var dist))
        {
            if (dist > distances[vertex])
            {
                continue;
            }

            if (dist > radius)
            {
                continue;
            }

            var neighbors = adjacency.GetNeighbors(vertex);
            for (int i = 0; i < neighbors.Length; i++)
            {
                int neighbor = neighbors[i];
                if ((uint)neighbor >= (uint)vertexCount)
                {
                    continue;
                }

                var edgeLen = Vector3.Distance(positions[vertex], positions[neighbor]);
                var next = dist + edgeLen;
                if (next < distances[neighbor] && next <= radius)
                {
                    distances[neighbor] = next;
                    queue.Enqueue(neighbor, next);
                }
            }
        }

        return distances;
    }

    private static float ComputeWeight(float distance, float radius, ProportionalFalloff falloff)
    {
        if (radius <= 1e-6f)
        {
            return 1f;
        }

        float t = Math.Clamp(distance / radius, 0f, 1f);
        return falloff switch
        {
            ProportionalFalloff.Smooth => 1f - (t * t * (3f - 2f * t)),
            ProportionalFalloff.Sharp => (1f - t) * (1f - t),
            ProportionalFalloff.Root => MathF.Sqrt(1f - t),
            _ => 1f - t
        };
    }
}
