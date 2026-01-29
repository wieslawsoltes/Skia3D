using System.Numerics;

namespace Skia3D.Scene;

public sealed class Transform
{
    private Vector3 _localPosition;
    private Quaternion _localRotation = Quaternion.Identity;
    private Vector3 _localScale = Vector3.One;
    private Matrix4x4 _localMatrix = Matrix4x4.Identity;
    private bool _localDirty = true;

    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            _localPosition = value;
            _localDirty = true;
        }
    }

    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            _localRotation = Quaternion.Normalize(value);
            _localDirty = true;
        }
    }

    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            _localScale = value;
            _localDirty = true;
        }
    }

    public Matrix4x4 LocalMatrix
    {
        get
        {
            if (_localDirty)
            {
                RebuildLocal();
            }

            return _localMatrix;
        }
    }

    public Matrix4x4 WorldMatrix { get; private set; } = Matrix4x4.Identity;

    public bool WorldDirty { get; private set; } = true;

    public void MarkDirty() => WorldDirty = true;

    internal void UpdateWorld(Matrix4x4 parentWorld, bool parentDirty)
    {
        if (_localDirty)
        {
            RebuildLocal();
        }

        if (parentDirty || WorldDirty)
        {
            WorldMatrix = _localMatrix * parentWorld;
            WorldDirty = false;
        }
    }

    public bool SetLocalFromWorld(Matrix4x4 world, Matrix4x4 parentWorld)
    {
        if (!Matrix4x4.Invert(parentWorld, out var invParent))
        {
            return false;
        }

        var local = world * invParent;
        if (!Matrix4x4.Decompose(local, out var scale, out var rotation, out var translation))
        {
            return false;
        }

        _localScale = scale;
        _localRotation = Quaternion.Normalize(rotation);
        _localPosition = translation;
        _localMatrix = local;
        _localDirty = false;
        WorldDirty = true;
        return true;
    }

    private void RebuildLocal()
    {
        _localMatrix = Matrix4x4.CreateScale(_localScale)
            * Matrix4x4.CreateFromQuaternion(_localRotation)
            * Matrix4x4.CreateTranslation(_localPosition);
        _localDirty = false;
        WorldDirty = true;
    }
}
