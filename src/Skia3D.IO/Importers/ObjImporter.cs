using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Skia3D.Animation;
using Skia3D.Core;
using Skia3D.Scene;
using SceneGraph = Skia3D.Scene.Scene;
using SkiaSharp;

namespace Skia3D.IO;

public sealed class ObjImporter : IMeshImporter, ISceneImporter
{
    private const string DefaultMaterialName = "_default";

    public IReadOnlyList<string> Extensions { get; } = new[] { ".obj" };

    public Mesh Load(Stream stream, MeshLoadOptions options)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var result = Parse(stream, options, loadMaterials: false);
        if (result.Meshes.Count == 0)
        {
            return MeshFactory.CreateFromData(Array.Empty<Vector3>(), Array.Empty<int>());
        }

        if (result.Meshes.Count == 1)
        {
            return result.Meshes[0].Mesh;
        }

        return MergeMeshes(result.Meshes);
    }

    public SceneImportResult Load(Stream stream, SceneLoadOptions options)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var meshOptions = options.MeshOptions ?? new MeshLoadOptions();
        var result = Parse(stream, meshOptions, loadMaterials: options.LoadMaterials);

        var scene = new SceneGraph();
        var root = new SceneNode(GetRootName(meshOptions.SourcePath));

        for (int i = 0; i < result.Meshes.Count; i++)
        {
            var meshData = result.Meshes[i];
            var mesh = meshData.Mesh;
            if (meshOptions.Processing != null)
            {
                mesh = MeshProcessing.Apply(mesh, meshOptions.Processing);
            }

            var nodeName = string.IsNullOrWhiteSpace(meshData.Name) ? $"Mesh_{i}" : meshData.Name;
            var node = new SceneNode(nodeName)
            {
                MeshRenderer = new MeshRenderer(mesh, meshData.Material)
            };
            root.AddChild(node);
        }

        scene.AddRoot(root);
        return new SceneImportResult(scene, Array.Empty<AnimationClip>());
    }

    private static ObjParseResult Parse(Stream stream, MeshLoadOptions options, bool loadMaterials)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texcoords = new List<Vector2>();
        var builders = new Dictionary<string, MeshBuilder>(StringComparer.OrdinalIgnoreCase);
        var materialLibs = new List<string>();

        string currentMaterial = DefaultMaterialName;
        var currentBuilder = GetBuilder(builders, currentMaterial);

        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var token = ReadToken(line, out var rest);
            switch (token)
            {
                case "v":
                    ParsePosition(rest, positions);
                    break;
                case "vn":
                    ParseNormal(rest, normals);
                    break;
                case "vt":
                    ParseTexcoord(rest, texcoords);
                    break;
                case "f":
                    ParseFace(rest, positions, normals, texcoords, currentBuilder, options.DefaultColor);
                    break;
                case "usemtl":
                    currentMaterial = string.IsNullOrWhiteSpace(rest) ? DefaultMaterialName : rest;
                    currentBuilder = GetBuilder(builders, currentMaterial);
                    break;
                case "mtllib":
                    ParseMaterialLibs(rest, materialLibs);
                    break;
            }
        }

        var materials = loadMaterials ? LoadMaterials(materialLibs, options.SourcePath) : new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        var result = new ObjParseResult();

        foreach (var pair in builders)
        {
            var builder = pair.Value;
            if (builder.Indices.Count == 0 || builder.Positions.Count == 0)
            {
                continue;
            }

            IReadOnlyList<Vector3>? normalList = null;
            if (builder.HasNormals)
            {
                normalList = builder.Normals;
            }
            else if (options.GenerateNormals)
            {
                normalList = ComputeNormals(builder.Positions, builder.Indices);
            }

            IReadOnlyList<Vector4>? tangentList = null;
            if (builder.HasTexcoords && normalList != null)
            {
                tangentList = ComputeTangents(builder.Positions, normalList, builder.Texcoords, builder.Indices);
            }

            var mesh = MeshFactory.CreateFromData(builder.Positions, builder.Indices, normalList, builder.Colors, builder.Texcoords, tangentList);
            var material = ResolveMaterial(materials, pair.Key);
            result.Meshes.Add(new ObjMeshData(pair.Key, mesh, material));
        }

        return result;
    }

    private static MeshBuilder GetBuilder(Dictionary<string, MeshBuilder> builders, string materialName)
    {
        if (!builders.TryGetValue(materialName, out var builder))
        {
            builder = new MeshBuilder();
            builders[materialName] = builder;
        }

        return builder;
    }

    private static Mesh MergeMeshes(IReadOnlyList<ObjMeshData> meshes)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<SKColor>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        List<Vector4>? tangents = null;

        for (int m = 0; m < meshes.Count; m++)
        {
            var mesh = meshes[m].Mesh;
            var baseIndex = positions.Count;
            var vertices = mesh.Vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                positions.Add(v.Position);
                normals.Add(v.Normal);
                colors.Add(v.Color);
                uvs.Add(v.UV);
            }

            var meshIndices = mesh.Indices;
            for (int i = 0; i < meshIndices.Count; i++)
            {
                indices.Add(meshIndices[i] + baseIndex);
            }

            var meshTangents = mesh.Tangents;
            if (meshTangents != null && meshTangents.Count == vertices.Count)
            {
                if (tangents == null)
                {
                    tangents = new List<Vector4>(positions.Count);
                    for (int i = 0; i < baseIndex; i++)
                    {
                        tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                    }
                }

                for (int i = 0; i < meshTangents.Count; i++)
                {
                    tangents.Add(meshTangents[i]);
                }
            }
            else if (tangents != null)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                }
            }
        }

        return MeshFactory.CreateFromData(positions, indices, normals, colors, uvs, tangents);
    }

    private static string ReadToken(string line, out string rest)
    {
        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
        {
            i++;
        }

        int start = i;
        while (i < line.Length && !char.IsWhiteSpace(line[i]))
        {
            i++;
        }

        rest = i < line.Length ? line[i..].Trim() : string.Empty;
        return line.Substring(start, i - start);
    }

    private static void ParsePosition(string rest, List<Vector3> positions)
    {
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return;
        }

        positions.Add(new Vector3(ParseFloat(parts[0]), ParseFloat(parts[1]), ParseFloat(parts[2])));
    }

    private static void ParseNormal(string rest, List<Vector3> normals)
    {
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return;
        }

        var n = new Vector3(ParseFloat(parts[0]), ParseFloat(parts[1]), ParseFloat(parts[2]));
        if (n.LengthSquared() > 1e-8f)
        {
            n = Vector3.Normalize(n);
        }
        normals.Add(n);
    }

    private static void ParseTexcoord(string rest, List<Vector2> texcoords)
    {
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return;
        }

        texcoords.Add(new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1])));
    }

    private static void ParseFace(
        string rest,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<Vector3> normals,
        IReadOnlyList<Vector2> texcoords,
        MeshBuilder builder,
        SKColor defaultColor)
    {
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return;
        }

        var keys = new List<ObjVertexKey>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryParseFaceVertex(parts[i], positions.Count, texcoords.Count, normals.Count, out var key))
            {
                return;
            }
            keys.Add(key);
        }

        for (int i = 1; i + 1 < keys.Count; i++)
        {
            int i0 = builder.AddVertex(keys[0], positions, normals, texcoords, defaultColor);
            int i1 = builder.AddVertex(keys[i], positions, normals, texcoords, defaultColor);
            int i2 = builder.AddVertex(keys[i + 1], positions, normals, texcoords, defaultColor);
            builder.Indices.Add(i0);
            builder.Indices.Add(i1);
            builder.Indices.Add(i2);
        }
    }

    private static bool TryParseFaceVertex(string token, int positionCount, int texcoordCount, int normalCount, out ObjVertexKey key)
    {
        key = default;
        var parts = token.Split('/');
        if (parts.Length == 0)
        {
            return false;
        }

        if (!TryParseIndex(parts[0], positionCount, out var posIndex))
        {
            return false;
        }

        int texIndex = -1;
        if (parts.Length > 1 && parts[1].Length > 0)
        {
            _ = TryParseIndex(parts[1], texcoordCount, out texIndex);
        }

        int normalIndex = -1;
        if (parts.Length > 2 && parts[2].Length > 0)
        {
            _ = TryParseIndex(parts[2], normalCount, out normalIndex);
        }

        key = new ObjVertexKey(posIndex, texIndex, normalIndex);
        return true;
    }

    private static bool TryParseIndex(string token, int count, out int index)
    {
        index = -1;
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
        {
            return false;
        }

        if (raw > 0)
        {
            index = raw - 1;
        }
        else if (raw < 0)
        {
            index = count + raw;
        }
        else
        {
            return false;
        }

        if ((uint)index >= (uint)count)
        {
            return false;
        }

        return true;
    }

    private static void ParseMaterialLibs(string rest, List<string> libs)
    {
        if (string.IsNullOrWhiteSpace(rest))
        {
            return;
        }

        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            libs.Add(parts[i]);
        }
    }

    private static Dictionary<string, Material> LoadMaterials(IReadOnlyList<string> libs, string? sourcePath)
    {
        var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        if (libs.Count == 0)
        {
            return materials;
        }

        string? baseDir = null;
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            baseDir = Path.GetDirectoryName(sourcePath);
        }

        for (int i = 0; i < libs.Count; i++)
        {
            var path = libs[i];
            if (!string.IsNullOrWhiteSpace(baseDir) && !Path.IsPathRooted(path))
            {
                path = Path.Combine(baseDir, path);
            }

            if (!File.Exists(path))
            {
                continue;
            }

            ParseMaterialFile(path, materials);
        }

        return materials;
    }

    private static void ParseMaterialFile(string path, Dictionary<string, Material> materials)
    {
        using var reader = new StreamReader(path);
        Material? current = null;
        string? line;
        string? baseDir = Path.GetDirectoryName(path);

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var token = ReadToken(line, out var rest);
            switch (token)
            {
                case "newmtl":
                    if (string.IsNullOrWhiteSpace(rest))
                    {
                        current = null;
                        break;
                    }

                    current = Material.Default();
                    current.ShadingModel = MaterialShadingModel.Phong;
                    current.UseVertexColor = false;
                    current.Diffuse = 1f;
                    materials[rest] = current;
                    break;
                case "Kd":
                    if (current != null && TryParseColor(rest, current.BaseColor.Alpha / 255f, out var kd))
                    {
                        current.BaseColor = kd;
                    }
                    break;
                case "Ka":
                    if (current != null && TryParseColor(rest, 1f, out var ka))
                    {
                        current.Ambient = (ka.Red + ka.Green + ka.Blue) / (3f * 255f);
                    }
                    break;
                case "Ks":
                    if (current != null && TryParseColor(rest, 1f, out var ks))
                    {
                        current.Specular = (ks.Red + ks.Green + ks.Blue) / (3f * 255f);
                    }
                    break;
                case "Ke":
                    if (current != null && TryParseColor(rest, 1f, out var ke))
                    {
                        current.EmissiveColor = ke;
                        current.EmissiveStrength = 1f;
                    }
                    break;
                case "Ns":
                    if (current != null && float.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out var ns))
                    {
                        current.Shininess = MathF.Max(1f, ns);
                    }
                    break;
                case "d":
                    if (current != null && float.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    {
                        current.BaseColor = ApplyAlpha(current.BaseColor, d);
                    }
                    break;
                case "Tr":
                    if (current != null && float.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out var tr))
                    {
                        current.BaseColor = ApplyAlpha(current.BaseColor, 1f - tr);
                    }
                    break;
                case "map_Kd":
                    if (current != null)
                    {
                        var pathValue = ParseMapPath(rest, out _);
                        var texture = TryLoadTexture(pathValue, baseDir);
                        if (texture != null)
                        {
                            current.BaseColorTexture = texture;
                        }
                    }
                    break;
                case "map_Ke":
                    if (current != null)
                    {
                        var pathValue = ParseMapPath(rest, out _);
                        var texture = TryLoadTexture(pathValue, baseDir);
                        if (texture != null)
                        {
                            current.EmissiveTexture = texture;
                            current.EmissiveStrength = 1f;
                        }
                    }
                    break;
                case "map_Bump":
                case "map_bump":
                case "bump":
                    if (current != null)
                    {
                        var pathValue = ParseMapPath(rest, out var bumpScale);
                        var texture = TryLoadTexture(pathValue, baseDir);
                        if (texture != null)
                        {
                            current.NormalTexture = texture;
                            if (bumpScale.HasValue)
                            {
                                current.NormalStrength = bumpScale.Value;
                            }
                        }
                    }
                    break;
            }
        }
    }

    private static Material ResolveMaterial(Dictionary<string, Material> materials, string name)
    {
        if (materials.TryGetValue(name, out var material))
        {
            return material;
        }

        return Material.Default();
    }

    private static string GetRootName(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return "ObjScene";
        }

        return Path.GetFileNameWithoutExtension(sourcePath);
    }

    private static float ParseFloat(string token)
    {
        return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
    }

    private static bool TryParseColor(string rest, float alpha, out SKColor color)
    {
        color = default;
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        float r = ParseFloat(parts[0]);
        float g = ParseFloat(parts[1]);
        float b = ParseFloat(parts[2]);
        byte a = (byte)Math.Clamp(alpha * 255f, 0f, 255f);
        color = new SKColor(ToByte(r), ToByte(g), ToByte(b), a);
        return true;
    }

    private static SKColor ApplyAlpha(SKColor color, float alpha)
    {
        byte a = (byte)Math.Clamp(alpha * 255f, 0f, 255f);
        return new SKColor(color.Red, color.Green, color.Blue, a);
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp(value * 255f, 0f, 255f);
    }

    private static string? ParseMapPath(string rest, out float? bumpScale)
    {
        bumpScale = null;
        if (string.IsNullOrWhiteSpace(rest))
        {
            return null;
        }

        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], "-bm", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
            {
                if (float.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
                {
                    bumpScale = scale;
                }
            }
        }

        return parts.Length > 0 ? parts[^1] : null;
    }

    private static Texture2D? TryLoadTexture(string? path, string? baseDir)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fullPath = path;
        if (!string.IsNullOrWhiteSpace(baseDir) && !Path.IsPathRooted(path))
        {
            fullPath = Path.Combine(baseDir, path);
        }

        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return Texture2D.FromFile(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private static Vector3[] ComputeNormals(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices)
    {
        var normals = new Vector3[positions.Count];
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];
            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
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

        return normals;
    }

    private static Vector4[]? ComputeTangents(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<Vector3> normals,
        IReadOnlyList<Vector2> uvs,
        IReadOnlyList<int> indices)
    {
        if (positions.Count == 0 || indices.Count < 3)
        {
            return null;
        }

        var tan1 = new Vector3[positions.Count];
        var tan2 = new Vector3[positions.Count];

        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];
            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var uv0 = uvs[i0];
            var uv1 = uvs[i1];
            var uv2 = uvs[i2];

            var dp1 = p1 - p0;
            var dp2 = p2 - p0;
            var duv1 = uv1 - uv0;
            var duv2 = uv2 - uv0;

            float denom = duv1.X * duv2.Y - duv1.Y * duv2.X;
            if (MathF.Abs(denom) <= 1e-8f)
            {
                continue;
            }

            float inv = 1f / denom;
            var sdir = (dp1 * duv2.Y - dp2 * duv1.Y) * inv;
            var tdir = (dp2 * duv1.X - dp1 * duv2.X) * inv;

            tan1[i0] += sdir;
            tan1[i1] += sdir;
            tan1[i2] += sdir;
            tan2[i0] += tdir;
            tan2[i1] += tdir;
            tan2[i2] += tdir;
        }

        var tangents = new Vector4[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            var n = normals[i];
            var t = tan1[i];
            if (t.LengthSquared() <= 1e-8f)
            {
                tangents[i] = new Vector4(1f, 0f, 0f, 1f);
                continue;
            }

            var tangent = Vector3.Normalize(t - n * Vector3.Dot(n, t));
            float w = Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f ? -1f : 1f;
            tangents[i] = new Vector4(tangent, w);
        }

        return tangents;
    }

    private readonly struct ObjVertexKey : IEquatable<ObjVertexKey>
    {
        public ObjVertexKey(int position, int texcoord, int normal)
        {
            Position = position;
            Texcoord = texcoord;
            Normal = normal;
        }

        public int Position { get; }

        public int Texcoord { get; }

        public int Normal { get; }

        public bool Equals(ObjVertexKey other) => Position == other.Position && Texcoord == other.Texcoord && Normal == other.Normal;

        public override bool Equals(object? obj) => obj is ObjVertexKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Position, Texcoord, Normal);
    }

    private sealed class MeshBuilder
    {
        private readonly Dictionary<ObjVertexKey, int> _lookup = new();

        public List<Vector3> Positions { get; } = new();

        public List<Vector3> Normals { get; } = new();

        public List<Vector2> Texcoords { get; } = new();

        public List<SKColor> Colors { get; } = new();

        public List<int> Indices { get; } = new();

        public bool HasNormals { get; private set; }

        public bool HasTexcoords { get; private set; }

        public int AddVertex(ObjVertexKey key, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> normals, IReadOnlyList<Vector2> texcoords, SKColor color)
        {
            if (_lookup.TryGetValue(key, out var index))
            {
                return index;
            }

            var position = positions[key.Position];
            var normal = key.Normal >= 0 && key.Normal < normals.Count ? normals[key.Normal] : Vector3.UnitY;
            var uv = key.Texcoord >= 0 && key.Texcoord < texcoords.Count ? texcoords[key.Texcoord] : Vector2.Zero;

            Positions.Add(position);
            Normals.Add(normal);
            Texcoords.Add(uv);
            Colors.Add(color);

            index = Positions.Count - 1;
            _lookup[key] = index;

            if (key.Normal >= 0)
            {
                HasNormals = true;
            }

            if (key.Texcoord >= 0)
            {
                HasTexcoords = true;
            }

            return index;
        }
    }

    private sealed class ObjMeshData
    {
        public ObjMeshData(string name, Mesh mesh, Material material)
        {
            Name = name;
            Mesh = mesh;
            Material = material;
        }

        public string Name { get; }

        public Mesh Mesh { get; }

        public Material Material { get; }
    }

    private sealed class ObjParseResult
    {
        public List<ObjMeshData> Meshes { get; } = new();
    }
}
