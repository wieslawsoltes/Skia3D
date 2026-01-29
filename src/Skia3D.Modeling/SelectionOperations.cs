using System;
using System.Collections.Generic;
using System.Numerics;

namespace Skia3D.Modeling;

public static class SelectionOperations
{
    public static HashSet<int> GrowFaces(EditableMesh mesh, IReadOnlyCollection<int> faces, int steps = 1)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (faces is null)
        {
            throw new ArgumentNullException(nameof(faces));
        }

        var current = faces as HashSet<int> ?? new HashSet<int>(faces);
        if (steps <= 0 || current.Count == 0)
        {
            return new HashSet<int>(current);
        }

        var adjacency = MeshAdjacency.Build(mesh);
        for (int step = 0; step < steps; step++)
        {
            var next = new HashSet<int>(current);
            foreach (var face in current)
            {
                if ((uint)face >= (uint)adjacency.FaceNeighbors.Count)
                {
                    continue;
                }

                var neighbors = adjacency.FaceNeighbors[face];
                for (int i = 0; i < neighbors.Length; i++)
                {
                    next.Add(neighbors[i]);
                }
            }

            current = next;
        }

        return current;
    }

    public static HashSet<int> ShrinkFaces(EditableMesh mesh, IReadOnlyCollection<int> faces, int steps = 1)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (faces is null)
        {
            throw new ArgumentNullException(nameof(faces));
        }

        var current = faces as HashSet<int> ?? new HashSet<int>(faces);
        if (steps <= 0 || current.Count == 0)
        {
            return new HashSet<int>(current);
        }

        var adjacency = MeshAdjacency.Build(mesh);
        for (int step = 0; step < steps; step++)
        {
            var next = new HashSet<int>(current);
            foreach (var face in current)
            {
                if ((uint)face >= (uint)adjacency.FaceNeighbors.Count)
                {
                    next.Remove(face);
                    continue;
                }

                var neighbors = adjacency.FaceNeighbors[face];
                for (int i = 0; i < neighbors.Length; i++)
                {
                    if (!current.Contains(neighbors[i]))
                    {
                        next.Remove(face);
                        break;
                    }
                }
            }

            current = next;
        }

        return current;
    }

    public static HashSet<int> GrowVertices(EditableMesh mesh, IReadOnlyCollection<int> vertices, int steps = 1)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (vertices is null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        var current = vertices as HashSet<int> ?? new HashSet<int>(vertices);
        if (steps <= 0 || current.Count == 0)
        {
            return new HashSet<int>(current);
        }

        var neighbors = BuildVertexNeighbors(mesh);
        for (int step = 0; step < steps; step++)
        {
            var next = new HashSet<int>(current);
            foreach (var vertex in current)
            {
                if ((uint)vertex >= (uint)neighbors.Length)
                {
                    continue;
                }

                foreach (var neighbor in neighbors[vertex])
                {
                    next.Add(neighbor);
                }
            }

            current = next;
        }

        return current;
    }

    public static HashSet<int> ShrinkVertices(EditableMesh mesh, IReadOnlyCollection<int> vertices, int steps = 1)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (vertices is null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        var current = vertices as HashSet<int> ?? new HashSet<int>(vertices);
        if (steps <= 0 || current.Count == 0)
        {
            return new HashSet<int>(current);
        }

        var neighbors = BuildVertexNeighbors(mesh);
        for (int step = 0; step < steps; step++)
        {
            var next = new HashSet<int>(current);
            foreach (var vertex in current)
            {
                if ((uint)vertex >= (uint)neighbors.Length)
                {
                    next.Remove(vertex);
                    continue;
                }

                foreach (var neighbor in neighbors[vertex])
                {
                    if (!current.Contains(neighbor))
                    {
                        next.Remove(vertex);
                        break;
                    }
                }
            }

            current = next;
        }

        return current;
    }

    public static HashSet<EdgeKey> EdgeLoop(EditableMesh mesh, EdgeKey startEdge, int maxSteps = 1024)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        var loop = new HashSet<EdgeKey> { startEdge };
        if (mesh.VertexCount == 0 || mesh.Indices.Count == 0)
        {
            return loop;
        }

        var edgeMap = BuildVertexEdges(mesh);
        TraverseEdgeLoop(mesh, edgeMap, startEdge.A, startEdge.B, loop, maxSteps);
        TraverseEdgeLoop(mesh, edgeMap, startEdge.B, startEdge.A, loop, maxSteps);
        return loop;
    }

    public static HashSet<EdgeKey> EdgeRing(EditableMesh mesh, EdgeKey startEdge, int maxSteps = 1024)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        var ring = new HashSet<EdgeKey> { startEdge };
        if (mesh.VertexCount == 0 || mesh.Indices.Count == 0)
        {
            return ring;
        }

        var adjacency = MeshAdjacency.Build(mesh);
        if (!adjacency.Edges.TryGetValue(startEdge, out var faces) || faces.Faces.Length == 0)
        {
            return ring;
        }

        for (int i = 0; i < faces.Faces.Length; i++)
        {
            TraverseEdgeRing(mesh, adjacency, startEdge, faces.Faces[i], ring, maxSteps);
        }

        return ring;
    }

    private static HashSet<int>[] BuildVertexNeighbors(EditableMesh mesh)
    {
        var neighbors = new HashSet<int>[mesh.VertexCount];
        for (int i = 0; i < neighbors.Length; i++)
        {
            neighbors[i] = new HashSet<int>();
        }

        var indices = mesh.Indices;
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if ((uint)i0 >= (uint)neighbors.Length || (uint)i1 >= (uint)neighbors.Length || (uint)i2 >= (uint)neighbors.Length)
            {
                continue;
            }

            neighbors[i0].Add(i1);
            neighbors[i0].Add(i2);
            neighbors[i1].Add(i0);
            neighbors[i1].Add(i2);
            neighbors[i2].Add(i0);
            neighbors[i2].Add(i1);
        }

        return neighbors;
    }

    private static List<EdgeKey>[] BuildVertexEdges(EditableMesh mesh)
    {
        var edges = new List<EdgeKey>[mesh.VertexCount];
        for (int i = 0; i < edges.Length; i++)
        {
            edges[i] = new List<EdgeKey>();
        }

        var indices = mesh.Indices;
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if ((uint)i0 >= (uint)edges.Length || (uint)i1 >= (uint)edges.Length || (uint)i2 >= (uint)edges.Length)
            {
                continue;
            }

            AddEdge(edges, new EdgeKey(i0, i1));
            AddEdge(edges, new EdgeKey(i1, i2));
            AddEdge(edges, new EdgeKey(i2, i0));
        }

        return edges;
    }

    private static void AddEdge(List<EdgeKey>[] edges, EdgeKey edge)
    {
        if (!edges[edge.A].Contains(edge))
        {
            edges[edge.A].Add(edge);
        }
        if (!edges[edge.B].Contains(edge))
        {
            edges[edge.B].Add(edge);
        }
    }

    private static void TraverseEdgeLoop(
        EditableMesh mesh,
        IReadOnlyList<List<EdgeKey>> edgeMap,
        int start,
        int next,
        HashSet<EdgeKey> loop,
        int maxSteps)
    {
        var positions = mesh.Positions;
        int prev = start;
        int current = next;

        for (int step = 0; step < maxSteps; step++)
        {
            if ((uint)current >= (uint)edgeMap.Count)
            {
                return;
            }

            var candidates = edgeMap[current];
            if (candidates.Count == 0)
            {
                return;
            }

            if (!TryFindNextEdge(positions, candidates, current, prev, out var nextEdge))
            {
                return;
            }

            if (!loop.Add(nextEdge))
            {
                return;
            }

            int nextVertex = nextEdge.A == current ? nextEdge.B : nextEdge.A;
            prev = current;
            current = nextVertex;
        }
    }

    private static bool TryFindNextEdge(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<EdgeKey> candidates,
        int current,
        int previous,
        out EdgeKey nextEdge)
    {
        nextEdge = default;
        if ((uint)current >= (uint)positions.Count || (uint)previous >= (uint)positions.Count)
        {
            return false;
        }

        var prevDir = positions[current] - positions[previous];
        if (prevDir.LengthSquared() < 1e-8f)
        {
            prevDir = Vector3.UnitX;
        }
        else
        {
            prevDir = Vector3.Normalize(prevDir);
        }

        bool found = false;
        float bestDot = -2f;

        for (int i = 0; i < candidates.Count; i++)
        {
            var edge = candidates[i];
            int other = edge.A == current ? edge.B : edge.A;
            if (other == previous)
            {
                continue;
            }

            var dir = positions[other] - positions[current];
            if (dir.LengthSquared() < 1e-8f)
            {
                continue;
            }

            dir = Vector3.Normalize(dir);
            float dot = Vector3.Dot(prevDir, dir);
            if (!found || dot > bestDot)
            {
                bestDot = dot;
                nextEdge = edge;
                found = true;
            }
        }

        return found;
    }

    private static void TraverseEdgeRing(
        EditableMesh mesh,
        MeshAdjacency adjacency,
        EdgeKey startEdge,
        int face,
        HashSet<EdgeKey> ring,
        int maxSteps)
    {
        var positions = mesh.Positions;
        var currentEdge = startEdge;
        int currentFace = face;

        for (int step = 0; step < maxSteps; step++)
        {
            if ((uint)currentFace >= (uint)adjacency.FaceEdges.Count)
            {
                return;
            }

            var edges = adjacency.FaceEdges[currentFace];
            if (edges.Length == 0)
            {
                return;
            }

            if (!TryFindParallelEdge(positions, edges, currentEdge, out var nextEdge))
            {
                return;
            }

            if (!ring.Add(nextEdge))
            {
                return;
            }

            if (!adjacency.Edges.TryGetValue(nextEdge, out var faces))
            {
                return;
            }

            int nextFace = -1;
            for (int i = 0; i < faces.Faces.Length; i++)
            {
                if (faces.Faces[i] != currentFace)
                {
                    nextFace = faces.Faces[i];
                    break;
                }
            }

            if (nextFace < 0)
            {
                return;
            }

            currentFace = nextFace;
            currentEdge = nextEdge;
        }
    }

    private static bool TryFindParallelEdge(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<EdgeKey> edges,
        EdgeKey current,
        out EdgeKey parallel)
    {
        parallel = default;
        if (edges.Count < 2)
        {
            return false;
        }

        var currentDir = GetEdgeDirection(positions, current);
        if (currentDir.LengthSquared() < 1e-8f)
        {
            currentDir = Vector3.UnitX;
        }
        else
        {
            currentDir = Vector3.Normalize(currentDir);
        }

        bool found = false;
        float best = -2f;
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (edge.Equals(current))
            {
                continue;
            }

            var dir = GetEdgeDirection(positions, edge);
            if (dir.LengthSquared() < 1e-8f)
            {
                continue;
            }

            dir = Vector3.Normalize(dir);
            float dot = MathF.Abs(Vector3.Dot(currentDir, dir));
            if (!found || dot > best)
            {
                best = dot;
                parallel = edge;
                found = true;
            }
        }

        return found;
    }

    private static Vector3 GetEdgeDirection(IReadOnlyList<Vector3> positions, EdgeKey edge)
    {
        if ((uint)edge.A >= (uint)positions.Count || (uint)edge.B >= (uint)positions.Count)
        {
            return Vector3.Zero;
        }

        return positions[edge.B] - positions[edge.A];
    }
}
