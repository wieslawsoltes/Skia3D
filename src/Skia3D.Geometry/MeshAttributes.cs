using System;
using System.Numerics;

namespace Skia3D.Geometry;

public sealed class MeshAttributes
{
    public MeshAttributes(
        Vector3[]? normals = null,
        Vector2[]? texCoords = null,
        Vector4[]? colors = null,
        Vector4[]? tangents = null,
        Vector3[]? bitangents = null,
        Vector4[]? skinWeights = null,
        Int4[]? skinIndices = null,
        MeshMorphTarget[]? morphTargets = null)
    {
        Normals = normals;
        TexCoords = texCoords;
        Colors = colors;
        Tangents = tangents;
        Bitangents = bitangents;
        SkinWeights = skinWeights;
        SkinIndices = skinIndices;
        MorphTargets = morphTargets;
    }

    public Vector3[]? Normals { get; }

    public Vector2[]? TexCoords { get; }

    public Vector4[]? Colors { get; }

    public Vector4[]? Tangents { get; }

    public Vector3[]? Bitangents { get; }

    public Vector4[]? SkinWeights { get; }

    public Int4[]? SkinIndices { get; }

    public MeshMorphTarget[]? MorphTargets { get; }

    public bool HasNormals => Normals is { Length: > 0 };

    public bool HasTexCoords => TexCoords is { Length: > 0 };

    public bool HasColors => Colors is { Length: > 0 };

    public bool HasTangents => Tangents is { Length: > 0 };

    public bool HasBitangents => Bitangents is { Length: > 0 };

    public bool HasSkinWeights => SkinWeights is { Length: > 0 };

    public bool HasSkinIndices => SkinIndices is { Length: > 0 };

    public bool HasMorphTargets => MorphTargets is { Length: > 0 };

    public void Validate(int vertexCount)
    {
        if (Normals != null && Normals.Length != vertexCount)
        {
            throw new ArgumentException("Normals length must match vertex count.", nameof(Normals));
        }

        if (TexCoords != null && TexCoords.Length != vertexCount)
        {
            throw new ArgumentException("TexCoords length must match vertex count.", nameof(TexCoords));
        }

        if (Colors != null && Colors.Length != vertexCount)
        {
            throw new ArgumentException("Colors length must match vertex count.", nameof(Colors));
        }

        if (Tangents != null && Tangents.Length != vertexCount)
        {
            throw new ArgumentException("Tangents length must match vertex count.", nameof(Tangents));
        }

        if (Bitangents != null && Bitangents.Length != vertexCount)
        {
            throw new ArgumentException("Bitangents length must match vertex count.", nameof(Bitangents));
        }

        if (SkinWeights != null && SkinWeights.Length != vertexCount)
        {
            throw new ArgumentException("SkinWeights length must match vertex count.", nameof(SkinWeights));
        }

        if (SkinIndices != null && SkinIndices.Length != vertexCount)
        {
            throw new ArgumentException("SkinIndices length must match vertex count.", nameof(SkinIndices));
        }

        if (MorphTargets != null)
        {
            for (int i = 0; i < MorphTargets.Length; i++)
            {
                MorphTargets[i]?.Validate(vertexCount);
            }
        }
    }

    public MeshAttributes Clone()
    {
        MeshMorphTarget[]? morphTargets = null;
        if (MorphTargets != null)
        {
            morphTargets = new MeshMorphTarget[MorphTargets.Length];
            for (int i = 0; i < MorphTargets.Length; i++)
            {
                morphTargets[i] = MorphTargets[i].Clone();
            }
        }

        return new MeshAttributes(
            Normals != null ? (Vector3[])Normals.Clone() : null,
            TexCoords != null ? (Vector2[])TexCoords.Clone() : null,
            Colors != null ? (Vector4[])Colors.Clone() : null,
            Tangents != null ? (Vector4[])Tangents.Clone() : null,
            Bitangents != null ? (Vector3[])Bitangents.Clone() : null,
            SkinWeights != null ? (Vector4[])SkinWeights.Clone() : null,
            SkinIndices != null ? (Int4[])SkinIndices.Clone() : null,
            morphTargets);
    }
}
