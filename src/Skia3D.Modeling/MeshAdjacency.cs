using System;
using System.Collections.Generic;

namespace Skia3D.Modeling;

public readonly record struct EdgeFaces(EdgeKey Edge, int[] Faces)
{
    public bool IsBoundary => Faces.Length <= 1;
}

public sealed class MeshAdjacency
{
    private MeshAdjacency(
        int vertexCount,
        int triangleCount,
        Dictionary<EdgeKey, EdgeFaces> edges,
        int[][] vertexFaces,
        EdgeKey[][] faceEdges,
        int[][] faceNeighbors)
    {
        VertexCount = vertexCount;
        TriangleCount = triangleCount;
        Edges = edges;
        VertexFaces = vertexFaces;
        FaceEdges = faceEdges;
        FaceNeighbors = faceNeighbors;
    }

    public int VertexCount { get; }

    public int TriangleCount { get; }

    public IReadOnlyDictionary<EdgeKey, EdgeFaces> Edges { get; }

    public IReadOnlyList<int[]> VertexFaces { get; }

    public IReadOnlyList<EdgeKey[]> FaceEdges { get; }

    public IReadOnlyList<int[]> FaceNeighbors { get; }

    public static MeshAdjacency Build(EditableMesh mesh)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return Build(mesh.VertexCount, mesh.Indices);
    }

    public static MeshAdjacency Build(int vertexCount, IReadOnlyList<int> indices)
    {
        if (indices is null)
        {
            throw new ArgumentNullException(nameof(indices));
        }

        var triangleCount = indices.Count / 3;
        var vertexFacesLists = new List<int>[vertexCount];
        var edgeFaces = new Dictionary<EdgeKey, List<int>>();
        var faceEdges = new EdgeKey[triangleCount][];

        for (int tri = 0; tri < triangleCount; tri++)
        {
            int baseIndex = tri * 3;
            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];

            if (!IsValid(i0, vertexCount) || !IsValid(i1, vertexCount) || !IsValid(i2, vertexCount))
            {
                faceEdges[tri] = Array.Empty<EdgeKey>();
                continue;
            }

            AddVertexFace(vertexFacesLists, i0, tri);
            AddVertexFace(vertexFacesLists, i1, tri);
            AddVertexFace(vertexFacesLists, i2, tri);

            var e0 = new EdgeKey(i0, i1);
            var e1 = new EdgeKey(i1, i2);
            var e2 = new EdgeKey(i2, i0);
            faceEdges[tri] = new[] { e0, e1, e2 };

            AddEdgeFace(edgeFaces, e0, tri);
            AddEdgeFace(edgeFaces, e1, tri);
            AddEdgeFace(edgeFaces, e2, tri);
        }

        var vertexFaces = new int[vertexCount][];
        for (int i = 0; i < vertexCount; i++)
        {
            vertexFaces[i] = vertexFacesLists[i]?.ToArray() ?? Array.Empty<int>();
        }

        var edgeDict = new Dictionary<EdgeKey, EdgeFaces>(edgeFaces.Count);
        foreach (var pair in edgeFaces)
        {
            edgeDict[pair.Key] = new EdgeFaces(pair.Key, pair.Value.ToArray());
        }

        var faceNeighbors = new int[triangleCount][];
        for (int tri = 0; tri < triangleCount; tri++)
        {
            var edges = faceEdges[tri];
            if (edges.Length == 0)
            {
                faceNeighbors[tri] = Array.Empty<int>();
                continue;
            }

            var neighbors = new HashSet<int>();
            for (int e = 0; e < edges.Length; e++)
            {
                if (!edgeDict.TryGetValue(edges[e], out var faces))
                {
                    continue;
                }

                foreach (var face in faces.Faces)
                {
                    if (face != tri)
                    {
                        neighbors.Add(face);
                    }
                }
            }

            faceNeighbors[tri] = neighbors.Count == 0 ? Array.Empty<int>() : neighbors.ToArray();
        }

        return new MeshAdjacency(vertexCount, triangleCount, edgeDict, vertexFaces, faceEdges, faceNeighbors);
    }

    private static void AddVertexFace(List<int>[] vertexFaces, int vertex, int face)
    {
        var list = vertexFaces[vertex];
        if (list is null)
        {
            list = new List<int>();
            vertexFaces[vertex] = list;
        }

        list.Add(face);
    }

    private static void AddEdgeFace(Dictionary<EdgeKey, List<int>> edgeFaces, EdgeKey edge, int face)
    {
        if (!edgeFaces.TryGetValue(edge, out var faces))
        {
            faces = new List<int>();
            edgeFaces[edge] = faces;
        }

        faces.Add(face);
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;
}
