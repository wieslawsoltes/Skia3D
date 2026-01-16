using System.Numerics;
using SkiaSharp;

namespace Skia3D.Core;

public readonly record struct Vertex(Vector3 Position, Vector3 Normal, SKColor Color);

public sealed class Mesh
{
    public Mesh(IReadOnlyList<Vertex> vertices, IReadOnlyList<int> indices)
    {
        Vertices = vertices;
        Indices = indices;
        BoundingRadius = ComputeBoundingRadius(vertices);
    }

    public IReadOnlyList<Vertex> Vertices { get; }

    public IReadOnlyList<int> Indices { get; }

    public float BoundingRadius { get; }

    private static float ComputeBoundingRadius(IReadOnlyList<Vertex> vertices)
    {
        var max = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            var len = vertices[i].Position.Length();
            if (len > max)
            {
                max = len;
            }
        }
        return max;
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

    public bool IsVisible { get; set; } = true;
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
            verts.Add(new Vertex(p0, normal, color));
            verts.Add(new Vertex(p1, normal, color));
            verts.Add(new Vertex(p2, normal, color));
            verts.Add(new Vertex(p3, normal, color));

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
            new(new Vector3(-half, 0f, -half), bottomNormal, color),
            new(new Vector3(half, 0f, -half), bottomNormal, color),
            new(new Vector3(half, 0f, half), bottomNormal, color),
            new(new Vector3(-half, 0f, half), bottomNormal, color)
        };

        var inds = new List<int> { 0, 1, 2, 0, 2, 3 };

        void Side(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            var start = verts.Count;
            verts.Add(new Vertex(a, normal, color));
            verts.Add(new Vertex(b, normal, color));
            verts.Add(new Vertex(c, normal, color));
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
            new(new Vector3(-half, 0f, -half), normal, color),
            new(new Vector3(half, 0f, -half), normal, color),
            new(new Vector3(half, 0f, half), normal, color),
            new(new Vector3(-half, 0f, half), normal, color),
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
                verts.Add(new Vertex(normal * radius, normal, color));
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
            verts.Add(new Vertex(new Vector3(x, -halfH, z), normal, color));
            verts.Add(new Vertex(new Vector3(x, halfH, z), normal, color));
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
        verts.Add(new Vertex(new Vector3(0f, halfH, 0f), new Vector3(0f, 1f, 0f), color));
        for (int i = 0; i < segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * MathF.PI * 2f;
            var x = MathF.Cos(angle) * radius;
            var z = MathF.Sin(angle) * radius;
            verts.Add(new Vertex(new Vector3(x, halfH, z), new Vector3(0f, 1f, 0f), color));
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
        verts.Add(new Vertex(new Vector3(0f, -halfH, 0f), new Vector3(0f, -1f, 0f), color));
        for (int i = 0; i < segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * MathF.PI * 2f;
            var x = MathF.Cos(angle) * radius;
            var z = MathF.Sin(angle) * radius;
            verts.Add(new Vertex(new Vector3(x, -halfH, z), new Vector3(0f, -1f, 0f), color));
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
                verts.Add(new Vertex(new Vector3(px, 0f, pz), normal, color));
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

        return new Mesh(verts, inds);
    }

    public static Mesh CreateFromData(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices, IReadOnlyList<Vector3>? normals = null, IReadOnlyList<SKColor>? colors = null)
    {
        var verts = new List<Vertex>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            var normal = normals != null && i < normals.Count ? normals[i] : Vector3.UnitY;
            var color = colors != null && i < colors.Count ? colors[i] : new SKColor(200, 200, 200);
            verts.Add(new Vertex(positions[i], normal, color));
        }

        return new Mesh(verts, indices);
    }

    /// <summary>
    /// Minimal OBJ loader: supports "v" and "vn" lines, triangle faces only.
    /// </summary>
    public static Mesh LoadObj(ReadOnlySpan<char> objContent, SKColor defaultColor)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var faces = new List<int>();
        var faceNormals = new List<int>();

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
                    }
                    break;
            }
        }

        var vertexList = new List<Vertex>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            var n = i < normals.Count ? normals[i] : Vector3.UnitY;
            vertexList.Add(new Vertex(positions[i], n, defaultColor));
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

        return new Mesh(vertexList, faces);

        static float Parse(string s) => float.TryParse(s, out var v) ? v : 0f;

        static (int pos, int normal) ParseFace(string token)
        {
            var parts = token.Split('/');
            int pos = int.Parse(parts[0]);
            int normal = parts.Length >= 3 && parts[2].Length > 0 ? int.Parse(parts[2]) : 0;
            return (pos, normal);
        }
    }
}
