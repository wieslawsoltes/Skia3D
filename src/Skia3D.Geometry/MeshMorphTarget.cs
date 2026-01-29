using System.Numerics;

namespace Skia3D.Geometry;

public sealed class MeshMorphTarget
{
    public MeshMorphTarget(string? name, Vector3[]? positionDeltas, Vector3[]? normalDeltas = null, Vector4[]? tangentDeltas = null)
    {
        Name = name;
        PositionDeltas = positionDeltas;
        NormalDeltas = normalDeltas;
        TangentDeltas = tangentDeltas;
    }

    public string? Name { get; }

    public Vector3[]? PositionDeltas { get; }

    public Vector3[]? NormalDeltas { get; }

    public Vector4[]? TangentDeltas { get; }

    public void Validate(int vertexCount)
    {
        if (PositionDeltas != null && PositionDeltas.Length != vertexCount)
        {
            throw new ArgumentException("Morph target position delta count must match vertex count.", nameof(PositionDeltas));
        }

        if (NormalDeltas != null && NormalDeltas.Length != vertexCount)
        {
            throw new ArgumentException("Morph target normal delta count must match vertex count.", nameof(NormalDeltas));
        }

        if (TangentDeltas != null && TangentDeltas.Length != vertexCount)
        {
            throw new ArgumentException("Morph target tangent delta count must match vertex count.", nameof(TangentDeltas));
        }
    }

    public MeshMorphTarget Clone()
    {
        return new MeshMorphTarget(
            Name,
            PositionDeltas != null ? (Vector3[])PositionDeltas.Clone() : null,
            NormalDeltas != null ? (Vector3[])NormalDeltas.Clone() : null,
            TangentDeltas != null ? (Vector4[])TangentDeltas.Clone() : null);
    }
}
