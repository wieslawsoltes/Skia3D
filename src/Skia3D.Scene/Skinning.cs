using System;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Geometry;

namespace Skia3D.Scene;

public sealed class Skeleton
{
    private readonly SceneNode[] _joints;
    private readonly Matrix4x4[] _inverseBindMatrices;

    public Skeleton(IReadOnlyList<SceneNode> joints, IReadOnlyList<Matrix4x4>? inverseBindMatrices = null)
    {
        if (joints is null)
        {
            throw new ArgumentNullException(nameof(joints));
        }

        _joints = new SceneNode[joints.Count];
        for (int i = 0; i < joints.Count; i++)
        {
            _joints[i] = joints[i] ?? throw new ArgumentException("Joint list contains null.", nameof(joints));
        }

        _inverseBindMatrices = new Matrix4x4[_joints.Length];
        if (inverseBindMatrices != null && inverseBindMatrices.Count > 0)
        {
            int count = Math.Min(_joints.Length, inverseBindMatrices.Count);
            for (int i = 0; i < count; i++)
            {
                _inverseBindMatrices[i] = inverseBindMatrices[i];
            }
            for (int i = count; i < _inverseBindMatrices.Length; i++)
            {
                _inverseBindMatrices[i] = Matrix4x4.Identity;
            }
        }
        else
        {
            Array.Fill(_inverseBindMatrices, Matrix4x4.Identity);
        }
    }

    public IReadOnlyList<SceneNode> Joints => _joints;

    public IReadOnlyList<Matrix4x4> InverseBindMatrices => _inverseBindMatrices;
}

public sealed class Skin
{
    private readonly Vertex[] _baseVertices;
    private readonly Vector4[]? _baseTangents;
    private readonly Vector3[]? _baseBitangents;
    private readonly Vector4[]? _skinWeights;
    private readonly Int4[]? _skinIndices;
    private readonly MeshMorphTarget[]? _morphTargets;
    private readonly Vertex[] _skinnedVertices;
    private readonly Vector4[]? _skinnedTangents;
    private readonly Vector3[]? _skinnedBitangents;
    private readonly Matrix4x4[] _skinMatrices;
    private float[]? _morphWeights;

    public Skin(Mesh sourceMesh, Skeleton skeleton)
    {
        SourceMesh = sourceMesh ?? throw new ArgumentNullException(nameof(sourceMesh));
        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));

        _baseVertices = CopyVertices(sourceMesh.Vertices);
        _baseTangents = CopyVector4Array(sourceMesh.Tangents);
        _baseBitangents = CopyVector3Array(sourceMesh.Bitangents);
        _skinWeights = CopyVector4Array(sourceMesh.SkinWeights);
        _skinIndices = CopyInt4Array(sourceMesh.SkinIndices);
        _morphTargets = CopyMorphTargets(sourceMesh.MorphTargets);

        _skinnedVertices = new Vertex[_baseVertices.Length];
        Array.Copy(_baseVertices, _skinnedVertices, _baseVertices.Length);

        if (_baseTangents != null)
        {
            _skinnedTangents = new Vector4[_baseTangents.Length];
        }

        if (_baseTangents != null || _baseBitangents != null)
        {
            _skinnedBitangents = new Vector3[_baseVertices.Length];
        }

        _skinMatrices = new Matrix4x4[Skeleton.Joints.Count];

        SkinnedMesh = new Mesh(
            _skinnedVertices,
            sourceMesh.Indices,
            _skinnedTangents,
            _skinnedBitangents,
            sourceMesh.SkinWeights,
            sourceMesh.SkinIndices);

        Instance = new MeshInstance(SkinnedMesh);
    }

    public Mesh SourceMesh { get; }

    public Mesh SkinnedMesh { get; }

    public MeshInstance Instance { get; }

    public Skeleton Skeleton { get; }

    public bool Enabled { get; set; } = true;

    public IReadOnlyList<float>? MorphWeights => _morphWeights;

    public void SetMorphWeights(IReadOnlyList<float>? weights)
    {
        if (weights == null || weights.Count == 0)
        {
            _morphWeights = null;
            return;
        }

        if (_morphWeights == null || _morphWeights.Length != weights.Count)
        {
            _morphWeights = new float[weights.Count];
        }

        for (int i = 0; i < weights.Count; i++)
        {
            _morphWeights[i] = weights[i];
        }
    }

    public void Update(Matrix4x4 meshWorld)
    {
        if (!Enabled)
        {
            return;
        }

        if (_baseVertices.Length == 0)
        {
            return;
        }

        bool hasSkinning = _skinWeights != null && _skinIndices != null && _skinWeights.Length == _baseVertices.Length &&
            _skinIndices.Length == _baseVertices.Length && Skeleton.Joints.Count > 0;
        bool hasMorphs = _morphTargets != null && _morphTargets.Length > 0 && _morphWeights != null && _morphWeights.Length > 0;

        if (!hasSkinning && !hasMorphs)
        {
            return;
        }

        if (hasSkinning)
        {
            if (!Matrix4x4.Invert(meshWorld, out var invMeshWorld))
            {
                invMeshWorld = Matrix4x4.Identity;
            }

            int jointCount = Skeleton.Joints.Count;
            var inverseBind = Skeleton.InverseBindMatrices;
            for (int i = 0; i < jointCount; i++)
            {
                var jointWorld = Skeleton.Joints[i].Transform.WorldMatrix;
                var bind = i < inverseBind.Count ? inverseBind[i] : Matrix4x4.Identity;
                _skinMatrices[i] = bind * jointWorld * invMeshWorld;
            }
        }

        int vertexCount = _baseVertices.Length;
        int morphCount = hasMorphs ? Math.Min(_morphTargets!.Length, _morphWeights!.Length) : 0;
        bool hasTangents = _baseTangents != null && _baseTangents.Length == vertexCount;

        for (int i = 0; i < vertexCount; i++)
        {
            var baseVertex = _baseVertices[i];
            var position = baseVertex.Position;
            var normal = baseVertex.Normal;
            var tangent = hasTangents ? _baseTangents![i] : Vector4.Zero;
            var tangentVector = hasTangents ? new Vector3(tangent.X, tangent.Y, tangent.Z) : Vector3.Zero;

            if (hasMorphs)
            {
                for (int t = 0; t < morphCount; t++)
                {
                    float weight = _morphWeights![t];
                    if (MathF.Abs(weight) <= 1e-6f)
                    {
                        continue;
                    }

                    var target = _morphTargets![t];
                    if (target.PositionDeltas != null)
                    {
                        position += target.PositionDeltas[i] * weight;
                    }

                    if (target.NormalDeltas != null)
                    {
                        normal += target.NormalDeltas[i] * weight;
                    }

                    if (hasTangents && target.TangentDeltas != null)
                    {
                        tangent += target.TangentDeltas[i] * weight;
                    }
                }
            }

            Vector3 skinnedPosition = position;
            Vector3 skinnedNormal = normal;
            Vector3 skinnedTangent = tangentVector;

            if (hasSkinning)
            {
                var weights = _skinWeights![i];
                var indices = _skinIndices![i];
                float weightSum = 0f;

                skinnedPosition = Vector3.Zero;
                skinnedNormal = Vector3.Zero;
                skinnedTangent = Vector3.Zero;

                ApplySkin(ref skinnedPosition, ref skinnedNormal, ref skinnedTangent, position, normal, tangentVector, indices.X, weights.X, ref weightSum, _skinMatrices, hasTangents);
                ApplySkin(ref skinnedPosition, ref skinnedNormal, ref skinnedTangent, position, normal, tangentVector, indices.Y, weights.Y, ref weightSum, _skinMatrices, hasTangents);
                ApplySkin(ref skinnedPosition, ref skinnedNormal, ref skinnedTangent, position, normal, tangentVector, indices.Z, weights.Z, ref weightSum, _skinMatrices, hasTangents);
                ApplySkin(ref skinnedPosition, ref skinnedNormal, ref skinnedTangent, position, normal, tangentVector, indices.W, weights.W, ref weightSum, _skinMatrices, hasTangents);

                if (weightSum > 1e-6f)
                {
                    float inv = 1f / weightSum;
                    skinnedPosition *= inv;
                    skinnedNormal *= inv;
                    skinnedTangent *= inv;
                }
                else
                {
                    skinnedPosition = position;
                    skinnedNormal = normal;
                    skinnedTangent = tangentVector;
                }
            }

            if (skinnedNormal.LengthSquared() > 1e-8f)
            {
                skinnedNormal = Vector3.Normalize(skinnedNormal);
            }

            _skinnedVertices[i] = baseVertex with
            {
                Position = skinnedPosition,
                Normal = skinnedNormal
            };

            if (hasTangents && _skinnedTangents != null)
            {
                if (skinnedTangent.LengthSquared() > 1e-8f)
                {
                    skinnedTangent = Vector3.Normalize(skinnedTangent);
                }

                _skinnedTangents[i] = new Vector4(skinnedTangent, tangent.W);

                if (_skinnedBitangents != null)
                {
                    var bitangent = Vector3.Cross(skinnedNormal, skinnedTangent) * tangent.W;
                    if (bitangent.LengthSquared() > 1e-8f)
                    {
                        bitangent = Vector3.Normalize(bitangent);
                    }

                    _skinnedBitangents[i] = bitangent;
                }
            }
            else if (_skinnedBitangents != null)
            {
                _skinnedBitangents[i] = Vector3.Zero;
            }
        }

        SkinnedMesh.UpdateVertices(_skinnedVertices, allowRefit: true, parallelRefit: true);
        if (_skinnedTangents != null)
        {
            SkinnedMesh.SetTangents(_skinnedTangents);
        }
        if (_skinnedBitangents != null)
        {
            SkinnedMesh.SetBitangents(_skinnedBitangents);
        }
    }

    private static void ApplySkin(
        ref Vector3 skinnedPosition,
        ref Vector3 skinnedNormal,
        ref Vector3 skinnedTangent,
        Vector3 position,
        Vector3 normal,
        Vector3 tangent,
        int index,
        float weight,
        ref float weightSum,
        Matrix4x4[] skinMatrices,
        bool applyTangent)
    {
        if (weight <= 0f)
        {
            return;
        }

        if ((uint)index >= (uint)skinMatrices.Length)
        {
            return;
        }

        var matrix = skinMatrices[index];
        skinnedPosition += Vector3.Transform(position, matrix) * weight;
        skinnedNormal += Vector3.TransformNormal(normal, matrix) * weight;
        if (applyTangent)
        {
            skinnedTangent += Vector3.TransformNormal(tangent, matrix) * weight;
        }
        weightSum += weight;
    }

    private static Vertex[] CopyVertices(IReadOnlyList<Vertex> vertices)
    {
        if (vertices is Vertex[] array)
        {
            return (Vertex[])array.Clone();
        }

        var result = new Vertex[vertices.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = vertices[i];
        }

        return result;
    }

    private static Vector4[]? CopyVector4Array(IReadOnlyList<Vector4>? values)
    {
        if (values is null)
        {
            return null;
        }

        if (values is Vector4[] array)
        {
            return (Vector4[])array.Clone();
        }

        var result = new Vector4[values.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = values[i];
        }

        return result;
    }

    private static Vector3[]? CopyVector3Array(IReadOnlyList<Vector3>? values)
    {
        if (values is null)
        {
            return null;
        }

        if (values is Vector3[] array)
        {
            return (Vector3[])array.Clone();
        }

        var result = new Vector3[values.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = values[i];
        }

        return result;
    }

    private static Int4[]? CopyInt4Array(IReadOnlyList<Int4>? values)
    {
        if (values is null)
        {
            return null;
        }

        if (values is Int4[] array)
        {
            return (Int4[])array.Clone();
        }

        var result = new Int4[values.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = values[i];
        }

        return result;
    }

    private static MeshMorphTarget[]? CopyMorphTargets(IReadOnlyList<MeshMorphTarget>? targets)
    {
        if (targets is null || targets.Count == 0)
        {
            return null;
        }

        var result = new MeshMorphTarget[targets.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = targets[i].Clone();
        }

        return result;
    }
}
