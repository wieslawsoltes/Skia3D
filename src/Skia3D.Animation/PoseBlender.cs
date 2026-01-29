using System;
using System.Numerics;

namespace Skia3D.Animation;

public static class PoseBlender
{
    public static void Copy(AnimationPose source, AnimationPose target)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        target.Clear();
        foreach (var pair in source.Transforms)
        {
            ApplyTransform(target, pair.Key, pair.Value);
        }
    }

    public static void Blend(AnimationPose a, AnimationPose b, float t, AnimationPose output)
    {
        if (a is null)
        {
            throw new ArgumentNullException(nameof(a));
        }
        if (b is null)
        {
            throw new ArgumentNullException(nameof(b));
        }
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        output.Clear();

        foreach (var pair in a.Transforms)
        {
            var node = pair.Key;
            var at = pair.Value;
            if (b.Transforms.TryGetValue(node, out var bt))
            {
                BlendTransform(output, node, at, bt, t);
            }
            else
            {
                ApplyTransform(output, node, at);
            }
        }

        foreach (var pair in b.Transforms)
        {
            if (a.Transforms.ContainsKey(pair.Key))
            {
                continue;
            }

            ApplyTransform(output, pair.Key, pair.Value);
        }
    }

    public static void Apply(AnimationPose pose)
    {
        if (pose is null)
        {
            throw new ArgumentNullException(nameof(pose));
        }

        foreach (var pair in pose.Transforms)
        {
            var node = pair.Key;
            var transform = pair.Value;
            if (transform.HasPosition)
            {
                node.Transform.LocalPosition = transform.Position;
            }
            if (transform.HasRotation)
            {
                node.Transform.LocalRotation = transform.Rotation;
            }
            if (transform.HasScale)
            {
                node.Transform.LocalScale = transform.Scale;
            }
        }
    }

    private static void ApplyTransform(AnimationPose pose, Skia3D.Scene.SceneNode node, PoseTransform transform)
    {
        if (transform.HasPosition)
        {
            pose.SetTranslation(node, transform.Position);
        }
        if (transform.HasRotation)
        {
            pose.SetRotation(node, transform.Rotation);
        }
        if (transform.HasScale)
        {
            pose.SetScale(node, transform.Scale);
        }
    }

    private static void BlendTransform(AnimationPose output, Skia3D.Scene.SceneNode node, PoseTransform a, PoseTransform b, float t)
    {
        if (a.HasPosition || b.HasPosition)
        {
            var p0 = a.HasPosition ? a.Position : b.Position;
            var p1 = b.HasPosition ? b.Position : a.Position;
            output.SetTranslation(node, Vector3.Lerp(p0, p1, t));
        }

        if (a.HasScale || b.HasScale)
        {
            var s0 = a.HasScale ? a.Scale : b.Scale;
            var s1 = b.HasScale ? b.Scale : a.Scale;
            output.SetScale(node, Vector3.Lerp(s0, s1, t));
        }

        if (a.HasRotation || b.HasRotation)
        {
            var r0 = a.HasRotation ? a.Rotation : b.Rotation;
            var r1 = b.HasRotation ? b.Rotation : a.Rotation;
            if (Quaternion.Dot(r0, r1) < 0f)
            {
                r1 = new Quaternion(-r1.X, -r1.Y, -r1.Z, -r1.W);
            }

            var r = Quaternion.Slerp(r0, r1, t);
            if (r.LengthSquared() > 1e-8f)
            {
                r = Quaternion.Normalize(r);
            }
            output.SetRotation(node, r);
        }
    }
}

