using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Scene;

namespace Skia3D.Animation;

public struct PoseTransform
{
    public Vector3 Position;
    public Vector3 Scale;
    public Quaternion Rotation;
    public bool HasPosition;
    public bool HasScale;
    public bool HasRotation;
}

public sealed class AnimationPose
{
    private readonly Dictionary<SceneNode, PoseTransform> _transforms = new();

    public IReadOnlyDictionary<SceneNode, PoseTransform> Transforms => _transforms;

    public void Clear() => _transforms.Clear();

    public void SetTranslation(SceneNode node, Vector3 value)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (_transforms.TryGetValue(node, out var transform))
        {
            transform.Position = value;
            transform.HasPosition = true;
            _transforms[node] = transform;
            return;
        }

        _transforms[node] = new PoseTransform
        {
            Position = value,
            HasPosition = true
        };
    }

    public void SetRotation(SceneNode node, Quaternion value)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (_transforms.TryGetValue(node, out var transform))
        {
            transform.Rotation = value;
            transform.HasRotation = true;
            _transforms[node] = transform;
            return;
        }

        _transforms[node] = new PoseTransform
        {
            Rotation = value,
            HasRotation = true
        };
    }

    public void SetScale(SceneNode node, Vector3 value)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (_transforms.TryGetValue(node, out var transform))
        {
            transform.Scale = value;
            transform.HasScale = true;
            _transforms[node] = transform;
            return;
        }

        _transforms[node] = new PoseTransform
        {
            Scale = value,
            HasScale = true
        };
    }
}
