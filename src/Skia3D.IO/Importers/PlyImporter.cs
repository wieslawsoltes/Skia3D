using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Skia3D.Animation;
using Skia3D.Core;
using Skia3D.Scene;
using SceneGraph = Skia3D.Scene.Scene;
using SkiaSharp;

namespace Skia3D.IO;

public sealed class PlyImporter : IMeshImporter, ISceneImporter
{
    public IReadOnlyList<string> Extensions { get; } = new[] { ".ply" };

    public Mesh Load(Stream stream, MeshLoadOptions options)
    {
        return LoadMesh(stream, options);
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
        var mesh = LoadMesh(stream, meshOptions);
        if (meshOptions.Processing != null)
        {
            mesh = MeshProcessing.Apply(mesh, meshOptions.Processing);
        }

        var material = Material.Default();
        material.UseVertexColor = true;

        var scene = new SceneGraph();
        var node = new SceneNode(GetRootName(meshOptions.SourcePath))
        {
            MeshRenderer = new MeshRenderer(mesh, material)
        };
        scene.AddRoot(node);

        return new SceneImportResult(scene, Array.Empty<AnimationClip>());
    }

    private static Mesh LoadMesh(Stream stream, MeshLoadOptions options)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var elements = ReadHeader(stream, out var format);

        bool hasNormals = false;
        bool hasTexcoords = false;
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].Name == "vertex")
            {
                hasNormals = HasNormalProperties(elements[i].Properties);
                hasTexcoords = HasTexcoordProperties(elements[i].Properties);
                break;
            }
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<SKColor>();
        var texcoords = new List<Vector2>();
        var indices = new List<int>();

        switch (format)
        {
            case PlyFormat.Ascii:
                LoadAscii(stream, elements, options, hasNormals, hasTexcoords, positions, normals, colors, texcoords, indices);
                break;
            case PlyFormat.BinaryLittleEndian:
                LoadBinary(stream, elements, options, hasNormals, hasTexcoords, bigEndian: false, positions, normals, colors, texcoords, indices);
                break;
            case PlyFormat.BinaryBigEndian:
                LoadBinary(stream, elements, options, hasNormals, hasTexcoords, bigEndian: true, positions, normals, colors, texcoords, indices);
                break;
            default:
                throw new NotSupportedException($"Unsupported PLY format: {format}.");
        }

        IReadOnlyList<Vector3>? normalList = null;
        if (hasNormals && normals.Count == positions.Count)
        {
            normalList = normals;
        }
        else if (!hasNormals && options.GenerateNormals && indices.Count > 0 && positions.Count > 0)
        {
            normalList = ComputeNormals(positions, indices);
        }

        IReadOnlyList<Vector2>? texcoordList = null;
        if (hasTexcoords && texcoords.Count == positions.Count)
        {
            texcoordList = texcoords;
        }

        IReadOnlyList<Vector4>? tangentList = null;
        if (normalList != null && texcoordList != null && indices.Count > 0)
        {
            tangentList = ComputeTangents(positions, normalList, texcoordList, indices);
        }

        return MeshFactory.CreateFromData(positions, indices, normalList, colors, texcoordList, tangentList);
    }

    private static void LoadAscii(
        Stream stream,
        IReadOnlyList<PlyElement> elements,
        MeshLoadOptions options,
        bool hasNormals,
        bool hasTexcoords,
        List<Vector3> positions,
        List<Vector3> normals,
        List<SKColor> colors,
        List<Vector2> texcoords,
        List<int> indices)
    {
        foreach (var element in elements)
        {
            if (element.Count <= 0)
            {
                continue;
            }

            if (element.Name == "vertex")
            {
                positions.Clear();
                colors.Clear();
                texcoords.Clear();
                positions.Capacity = element.Count;
                colors.Capacity = element.Count;
                texcoords.Capacity = element.Count;
                if (hasNormals)
                {
                    normals.Clear();
                    normals.Capacity = element.Count;
                }

                for (int i = 0; i < element.Count; i++)
                {
                    var line = ReadAsciiLine(stream);
                    if (line is null)
                    {
                        break;
                    }

                    ParseVertexLine(line, element, options, hasNormals, hasTexcoords, out var pos, out var normal, out var color, out var uv);
                    positions.Add(pos);
                    colors.Add(color);
                    if (hasTexcoords)
                    {
                        texcoords.Add(uv);
                    }
                    if (hasNormals)
                    {
                        normals.Add(normal);
                    }
                }
            }
            else if (element.Name == "face")
            {
                for (int i = 0; i < element.Count; i++)
                {
                    var line = ReadAsciiLine(stream);
                    if (line is null)
                    {
                        break;
                    }

                    ParseFaceLine(line, element, indices);
                }
            }
            else
            {
                for (int i = 0; i < element.Count; i++)
                {
                    if (ReadAsciiLine(stream) is null)
                    {
                        break;
                    }
                }
            }
        }
    }

    private static void LoadBinary(
        Stream stream,
        IReadOnlyList<PlyElement> elements,
        MeshLoadOptions options,
        bool hasNormals,
        bool hasTexcoords,
        bool bigEndian,
        List<Vector3> positions,
        List<Vector3> normals,
        List<SKColor> colors,
        List<Vector2> texcoords,
        List<int> indices)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        foreach (var element in elements)
        {
            if (element.Count <= 0)
            {
                continue;
            }

            if (element.Name == "vertex")
            {
                positions.Clear();
                colors.Clear();
                texcoords.Clear();
                positions.Capacity = element.Count;
                colors.Capacity = element.Count;
                texcoords.Capacity = element.Count;
                if (hasNormals)
                {
                    normals.Clear();
                    normals.Capacity = element.Count;
                }

                for (int i = 0; i < element.Count; i++)
                {
                    ParseVertexBinary(reader, element, options, hasNormals, hasTexcoords, bigEndian, out var pos, out var normal, out var color, out var uv);
                    positions.Add(pos);
                    colors.Add(color);
                    if (hasTexcoords)
                    {
                        texcoords.Add(uv);
                    }
                    if (hasNormals)
                    {
                        normals.Add(normal);
                    }
                }
            }
            else if (element.Name == "face")
            {
                for (int i = 0; i < element.Count; i++)
                {
                    ParseFaceBinary(reader, element, indices, bigEndian);
                }
            }
            else
            {
                SkipElementBinary(reader, element, element.Count, bigEndian);
            }
        }
    }

    private static List<PlyElement> ReadHeader(Stream stream, out PlyFormat format)
    {
        format = PlyFormat.Ascii;
        var elements = new List<PlyElement>();
        PlyElement? current = null;

        while (true)
        {
            var line = ReadAsciiLine(stream);
            if (line is null)
            {
                break;
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            switch (parts[0])
            {
                case "ply":
                    break;
                case "format":
                    if (parts.Length < 2)
                    {
                        throw new InvalidDataException("PLY header format missing.");
                    }

                    switch (parts[1].ToLowerInvariant())
                    {
                        case "ascii":
                            format = PlyFormat.Ascii;
                            break;
                        case "binary_little_endian":
                            format = PlyFormat.BinaryLittleEndian;
                            break;
                        case "binary_big_endian":
                            format = PlyFormat.BinaryBigEndian;
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported PLY format '{parts[1]}'.");
                    }
                    break;
                case "comment":
                case "obj_info":
                    break;
                case "element":
                    if (parts.Length >= 3)
                    {
                        current = new PlyElement
                        {
                            Name = parts[1].ToLowerInvariant(),
                            Count = ParseInt(parts[2])
                        };
                        elements.Add(current);
                    }
                    break;
                case "property":
                    if (current == null || parts.Length < 3)
                    {
                        break;
                    }

                    if (parts[1].Equals("list", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parts.Length >= 5)
                        {
                            current.Properties.Add(new PlyProperty
                            {
                                Kind = PlyPropertyKind.List,
                                CountTypeKind = ParseScalarType(parts[2]),
                                ValueTypeKind = ParseScalarType(parts[3]),
                                Name = parts[4].ToLowerInvariant()
                            });
                        }
                    }
                    else
                    {
                        current.Properties.Add(new PlyProperty
                        {
                            Kind = PlyPropertyKind.Scalar,
                            TypeKind = ParseScalarType(parts[1]),
                            Name = parts[2].ToLowerInvariant()
                        });
                    }
                    break;
                case "end_header":
                    return elements;
            }
        }

        throw new InvalidDataException("PLY header missing end_header.");
    }

    private static string? ReadAsciiLine(Stream stream)
    {
        var bytes = new List<byte>();
        while (true)
        {
            int value = stream.ReadByte();
            if (value == -1)
            {
                if (bytes.Count == 0)
                {
                    return null;
                }

                break;
            }

            byte b = (byte)value;
            if (b == (byte)'\n')
            {
                break;
            }

            if (b != (byte)'\r')
            {
                bytes.Add(b);
            }
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static void ParseVertexLine(
        string line,
        PlyElement element,
        MeshLoadOptions options,
        bool hasNormals,
        bool hasTexcoords,
        out Vector3 position,
        out Vector3 normal,
        out SKColor color,
        out Vector2 texcoord)
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;
        float nx = 0f;
        float ny = 0f;
        float nz = 0f;
        float u = 0f;
        float v = 0f;

        var baseColor = options.DefaultColor;
        byte r = baseColor.Red;
        byte g = baseColor.Green;
        byte b = baseColor.Blue;
        byte a = baseColor.Alpha;

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int tokenIndex = 0;

        for (int i = 0; i < element.Properties.Count; i++)
        {
            var prop = element.Properties[i];
            if (prop.Kind == PlyPropertyKind.List)
            {
                if (tokenIndex >= tokens.Length)
                {
                    break;
                }

                var count = ParseInt(tokens[tokenIndex++]);
                tokenIndex += Math.Min(count, tokens.Length - tokenIndex);
                continue;
            }

            if (tokenIndex >= tokens.Length)
            {
                break;
            }

            var token = tokens[tokenIndex++];
            switch (prop.Name)
            {
                case "x":
                    x = ParseFloat(token);
                    break;
                case "y":
                    y = ParseFloat(token);
                    break;
                case "z":
                    z = ParseFloat(token);
                    break;
                case "nx":
                    nx = ParseFloat(token);
                    break;
                case "ny":
                    ny = ParseFloat(token);
                    break;
                case "nz":
                    nz = ParseFloat(token);
                    break;
                case "red":
                case "r":
                    r = ParseColor(token, prop.IsFloat, r);
                    break;
                case "green":
                case "g":
                    g = ParseColor(token, prop.IsFloat, g);
                    break;
                case "blue":
                case "b":
                    b = ParseColor(token, prop.IsFloat, b);
                    break;
                case "alpha":
                case "a":
                    a = ParseColor(token, prop.IsFloat, a);
                    break;
                case "u":
                case "s":
                case "texture_u":
                case "texcoord_u":
                    u = ParseFloat(token);
                    break;
                case "v":
                case "t":
                case "texture_v":
                case "texcoord_v":
                    v = ParseFloat(token);
                    break;
            }
        }

        position = new Vector3(x, y, z);
        normal = hasNormals ? new Vector3(nx, ny, nz) : Vector3.UnitY;
        color = new SKColor(r, g, b, a);
        texcoord = hasTexcoords ? new Vector2(u, v) : Vector2.Zero;
    }

    private static void ParseFaceLine(string line, PlyElement element, List<int> indices)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int tokenIndex = 0;

        for (int i = 0; i < element.Properties.Count; i++)
        {
            var prop = element.Properties[i];
            if (prop.Kind == PlyPropertyKind.Scalar)
            {
                if (tokenIndex < tokens.Length)
                {
                    tokenIndex++;
                }
                continue;
            }

            if (tokenIndex >= tokens.Length)
            {
                return;
            }

            var count = ParseInt(tokens[tokenIndex++]);
            if (count <= 0)
            {
                continue;
            }

            if (prop.Name == "vertex_indices" || prop.Name == "vertex_index")
            {
                if (tokenIndex + count > tokens.Length)
                {
                    return;
                }

                int v0 = ParseInt(tokens[tokenIndex]);
                for (int iTri = 2; iTri < count; iTri++)
                {
                    int v1 = ParseInt(tokens[tokenIndex + iTri - 1]);
                    int v2 = ParseInt(tokens[tokenIndex + iTri]);
                    indices.Add(v0);
                    indices.Add(v1);
                    indices.Add(v2);
                }
            }

            tokenIndex += Math.Min(count, tokens.Length - tokenIndex);
        }
    }

    private static void ParseVertexBinary(
        BinaryReader reader,
        PlyElement element,
        MeshLoadOptions options,
        bool hasNormals,
        bool hasTexcoords,
        bool bigEndian,
        out Vector3 position,
        out Vector3 normal,
        out SKColor color,
        out Vector2 texcoord)
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;
        float nx = 0f;
        float ny = 0f;
        float nz = 0f;
        float u = 0f;
        float v = 0f;

        var baseColor = options.DefaultColor;
        byte r = baseColor.Red;
        byte g = baseColor.Green;
        byte b = baseColor.Blue;
        byte a = baseColor.Alpha;

        for (int i = 0; i < element.Properties.Count; i++)
        {
            var prop = element.Properties[i];
            if (prop.Kind == PlyPropertyKind.List)
            {
                int count = ReadCount(reader, prop.CountTypeKind, bigEndian);
                for (int j = 0; j < count; j++)
                {
                    _ = ReadNumber(reader, prop.ValueTypeKind, bigEndian);
                }
                continue;
            }

            var value = ReadNumber(reader, prop.TypeKind, bigEndian);
            switch (prop.Name)
            {
                case "x":
                    x = (float)value;
                    break;
                case "y":
                    y = (float)value;
                    break;
                case "z":
                    z = (float)value;
                    break;
                case "nx":
                    nx = (float)value;
                    break;
                case "ny":
                    ny = (float)value;
                    break;
                case "nz":
                    nz = (float)value;
                    break;
                case "red":
                case "r":
                    r = ToColorByte(value, prop.IsFloat, r);
                    break;
                case "green":
                case "g":
                    g = ToColorByte(value, prop.IsFloat, g);
                    break;
                case "blue":
                case "b":
                    b = ToColorByte(value, prop.IsFloat, b);
                    break;
                case "alpha":
                case "a":
                    a = ToColorByte(value, prop.IsFloat, a);
                    break;
                case "u":
                case "s":
                case "texture_u":
                case "texcoord_u":
                    u = (float)value;
                    break;
                case "v":
                case "t":
                case "texture_v":
                case "texcoord_v":
                    v = (float)value;
                    break;
            }
        }

        position = new Vector3(x, y, z);
        normal = hasNormals ? new Vector3(nx, ny, nz) : Vector3.UnitY;
        color = new SKColor(r, g, b, a);
        texcoord = hasTexcoords ? new Vector2(u, v) : Vector2.Zero;
    }

    private static void ParseFaceBinary(BinaryReader reader, PlyElement element, List<int> indices, bool bigEndian)
    {
        for (int i = 0; i < element.Properties.Count; i++)
        {
            var prop = element.Properties[i];
            if (prop.Kind == PlyPropertyKind.Scalar)
            {
                _ = ReadNumber(reader, prop.TypeKind, bigEndian);
                continue;
            }

            int count = ReadCount(reader, prop.CountTypeKind, bigEndian);
            if (count <= 0)
            {
                continue;
            }

            if (prop.Name == "vertex_indices" || prop.Name == "vertex_index")
            {
                var values = new int[count];
                for (int j = 0; j < count; j++)
                {
                    values[j] = ReadIndex(reader, prop.ValueTypeKind, bigEndian);
                }

                int v0 = values[0];
                for (int iTri = 2; iTri < count; iTri++)
                {
                    indices.Add(v0);
                    indices.Add(values[iTri - 1]);
                    indices.Add(values[iTri]);
                }
            }
            else
            {
                for (int j = 0; j < count; j++)
                {
                    _ = ReadNumber(reader, prop.ValueTypeKind, bigEndian);
                }
            }
        }
    }

    private static void SkipElementBinary(BinaryReader reader, PlyElement element, int count, bool bigEndian)
    {
        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < element.Properties.Count; j++)
            {
                var prop = element.Properties[j];
                if (prop.Kind == PlyPropertyKind.Scalar)
                {
                    _ = ReadNumber(reader, prop.TypeKind, bigEndian);
                }
                else
                {
                    int listCount = ReadCount(reader, prop.CountTypeKind, bigEndian);
                    for (int k = 0; k < listCount; k++)
                    {
                        _ = ReadNumber(reader, prop.ValueTypeKind, bigEndian);
                    }
                }
            }
        }
    }

    private static bool HasNormalProperties(IReadOnlyList<PlyProperty> properties)
    {
        bool nx = false;
        bool ny = false;
        bool nz = false;

        for (int i = 0; i < properties.Count; i++)
        {
            var name = properties[i].Name;
            nx |= name == "nx";
            ny |= name == "ny";
            nz |= name == "nz";
        }

        return nx && ny && nz;
    }

    private static bool HasTexcoordProperties(IReadOnlyList<PlyProperty> properties)
    {
        bool u = false;
        bool v = false;

        for (int i = 0; i < properties.Count; i++)
        {
            var name = properties[i].Name;
            switch (name)
            {
                case "u":
                case "s":
                case "texture_u":
                case "texcoord_u":
                    u = true;
                    break;
                case "v":
                case "t":
                case "texture_v":
                case "texcoord_v":
                    v = true;
                    break;
            }
        }

        return u && v;
    }

    private static byte ParseColor(string token, bool isFloat, byte fallback)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return fallback;
        }

        if (isFloat)
        {
            var value = ParseFloat(token);
            if (value <= 1f)
            {
                value *= 255f;
            }

            return (byte)Math.Clamp(MathF.Round(value), 0f, 255f);
        }

        var ivalue = ParseInt(token);
        return (byte)Math.Clamp(ivalue, 0, 255);
    }

    private static byte ToColorByte(double value, bool isFloat, byte fallback)
    {
        if (double.IsNaN(value))
        {
            return fallback;
        }

        if (isFloat)
        {
            if (value <= 1.0)
            {
                value *= 255.0;
            }

            return (byte)Math.Clamp(MathF.Round((float)value), 0f, 255f);
        }

        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }

    private static float ParseFloat(string token)
    {
        return float.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static int ParseInt(string token)
    {
        return int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static PlyScalarType ParseScalarType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new NotSupportedException("PLY property type missing.");
        }

        return type.ToLowerInvariant() switch
        {
            "char" or "int8" => PlyScalarType.Int8,
            "uchar" or "uint8" => PlyScalarType.UInt8,
            "short" or "int16" => PlyScalarType.Int16,
            "ushort" or "uint16" => PlyScalarType.UInt16,
            "int" or "int32" => PlyScalarType.Int32,
            "uint" or "uint32" => PlyScalarType.UInt32,
            "long" or "int64" => PlyScalarType.Int64,
            "ulong" or "uint64" => PlyScalarType.UInt64,
            "float" or "float32" => PlyScalarType.Float32,
            "double" or "float64" => PlyScalarType.Float64,
            _ => throw new NotSupportedException($"PLY type '{type}' is not supported.")
        };
    }

    private static bool IsFloatType(PlyScalarType type)
    {
        return type == PlyScalarType.Float32 || type == PlyScalarType.Float64;
    }

    private static double ReadNumber(BinaryReader reader, PlyScalarType type, bool bigEndian)
    {
        return type switch
        {
            PlyScalarType.Int8 => reader.ReadSByte(),
            PlyScalarType.UInt8 => reader.ReadByte(),
            PlyScalarType.Int16 => ReadInt16(reader, bigEndian),
            PlyScalarType.UInt16 => ReadUInt16(reader, bigEndian),
            PlyScalarType.Int32 => ReadInt32(reader, bigEndian),
            PlyScalarType.UInt32 => ReadUInt32(reader, bigEndian),
            PlyScalarType.Int64 => ReadInt64(reader, bigEndian),
            PlyScalarType.UInt64 => ReadUInt64(reader, bigEndian),
            PlyScalarType.Float32 => ReadSingle(reader, bigEndian),
            PlyScalarType.Float64 => ReadDouble(reader, bigEndian),
            _ => throw new NotSupportedException($"Unsupported scalar type {type}.")
        };
    }

    private static int ReadCount(BinaryReader reader, PlyScalarType type, bool bigEndian)
    {
        var value = ReadNumber(reader, type, bigEndian);
        if (double.IsNaN(value) || value <= 0)
        {
            return 0;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)Math.Round(value);
    }

    private static int ReadIndex(BinaryReader reader, PlyScalarType type, bool bigEndian)
    {
        var value = ReadNumber(reader, type, bigEndian);
        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)Math.Round(value);
    }

    private static short ReadInt16(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadInt16();
        }

        var bytes = ReadBytes(reader, 2);
        Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    private static ushort ReadUInt16(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadUInt16();
        }

        var bytes = ReadBytes(reader, 2);
        Array.Reverse(bytes);
        return BitConverter.ToUInt16(bytes, 0);
    }

    private static int ReadInt32(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadInt32();
        }

        var bytes = ReadBytes(reader, 4);
        Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static uint ReadUInt32(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadUInt32();
        }

        var bytes = ReadBytes(reader, 4);
        Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static long ReadInt64(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadInt64();
        }

        var bytes = ReadBytes(reader, 8);
        Array.Reverse(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }

    private static ulong ReadUInt64(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadUInt64();
        }

        var bytes = ReadBytes(reader, 8);
        Array.Reverse(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static float ReadSingle(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadSingle();
        }

        var bytes = ReadBytes(reader, 4);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static double ReadDouble(BinaryReader reader, bool bigEndian)
    {
        if (!bigEndian)
        {
            return reader.ReadDouble();
        }

        var bytes = ReadBytes(reader, 8);
        Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    private static byte[] ReadBytes(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new EndOfStreamException("Unexpected end of PLY stream.");
        }

        return bytes;
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

    private static string GetRootName(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return "PlyScene";
        }

        return Path.GetFileNameWithoutExtension(sourcePath);
    }

    private enum PlyFormat
    {
        Ascii,
        BinaryLittleEndian,
        BinaryBigEndian
    }

    private enum PlyPropertyKind
    {
        Scalar,
        List
    }

    private enum PlyScalarType
    {
        Int8,
        UInt8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float32,
        Float64
    }

    private sealed class PlyProperty
    {
        public PlyPropertyKind Kind { get; set; }

        public string Name { get; set; } = string.Empty;

        public PlyScalarType TypeKind { get; set; }

        public PlyScalarType CountTypeKind { get; set; }

        public PlyScalarType ValueTypeKind { get; set; }

        public bool IsFloat => IsFloatType(TypeKind);
    }

    private sealed class PlyElement
    {
        public string Name { get; set; } = string.Empty;

        public int Count { get; set; }

        public List<PlyProperty> Properties { get; } = new();
    }
}
