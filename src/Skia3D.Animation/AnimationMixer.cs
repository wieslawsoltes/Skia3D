using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Scene;

namespace Skia3D.Animation;

public sealed class AnimationMixer
{
    private readonly List<AnimationLayer> _layers = new();
    private readonly Dictionary<SceneNode, BlendedTransform> _blend = new();

    public IReadOnlyList<AnimationLayer> Layers => _layers;

    public AnimationLayer AddLayer(AnimationPlayer player, float weight = 1f)
    {
        var layer = new AnimationLayer(player, weight);
        _layers.Add(layer);
        return layer;
    }

    public bool RemoveLayer(AnimationLayer layer) => _layers.Remove(layer);

    public void ClearLayers() => _layers.Clear();

    public void Update(float deltaSeconds)
    {
        _blend.Clear();

        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            layer.Pose.Clear();

            if (layer.Weight <= 0f)
            {
                layer.Player.Advance(deltaSeconds);
                continue;
            }

            layer.Player.Update(deltaSeconds, layer.Pose);
            BlendPose(layer.Pose, layer.Weight);
        }

        ApplyBlend();
    }

    public void Seek(float time)
    {
        _blend.Clear();

        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            layer.Pose.Clear();
            layer.Player.Seek(time, layer.Pose);

            if (layer.Weight <= 0f)
            {
                continue;
            }

            BlendPose(layer.Pose, layer.Weight);
        }

        ApplyBlend();
    }

    private void BlendPose(AnimationPose pose, float weight)
    {
        foreach (var pair in pose.Transforms)
        {
            var node = pair.Key;
            var transform = pair.Value;
            if (!_blend.TryGetValue(node, out var blended))
            {
                blended = new BlendedTransform();
            }

            if (transform.HasPosition)
            {
                blended.PositionSum += transform.Position * weight;
                blended.PositionWeight += weight;
            }

            if (transform.HasScale)
            {
                blended.ScaleSum += transform.Scale * weight;
                blended.ScaleWeight += weight;
            }

            if (transform.HasRotation)
            {
                var q = transform.Rotation;
                var qv = new Vector4(q.X, q.Y, q.Z, q.W);
                if (blended.RotationWeight > 0f && Vector4.Dot(blended.RotationSum, qv) < 0f)
                {
                    qv = -qv;
                }

                blended.RotationSum += qv * weight;
                blended.RotationWeight += weight;
            }

            _blend[node] = blended;
        }
    }

    private void ApplyBlend()
    {
        foreach (var pair in _blend)
        {
            var node = pair.Key;
            var blended = pair.Value;

            if (blended.PositionWeight > 0f)
            {
                node.Transform.LocalPosition = blended.PositionSum / blended.PositionWeight;
            }

            if (blended.ScaleWeight > 0f)
            {
                node.Transform.LocalScale = blended.ScaleSum / blended.ScaleWeight;
            }

            if (blended.RotationWeight > 0f)
            {
                var q = new Quaternion(
                    blended.RotationSum.X,
                    blended.RotationSum.Y,
                    blended.RotationSum.Z,
                    blended.RotationSum.W);
                if (q.LengthSquared() > 1e-8f)
                {
                    q = Quaternion.Normalize(q);
                    node.Transform.LocalRotation = q;
                }
            }
        }
    }

    private struct BlendedTransform
    {
        public Vector3 PositionSum;
        public float PositionWeight;
        public Vector3 ScaleSum;
        public float ScaleWeight;
        public Vector4 RotationSum;
        public float RotationWeight;
    }
}
