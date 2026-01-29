using System;
using System.Collections.Generic;
using Skia3D.Scene;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Animation;

public sealed class BoundAnimationClip
{
    private readonly List<TrackBinding> _bindings;

    public BoundAnimationClip(AnimationClip clip, SceneGraph scene)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        if (scene is null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        _bindings = BuildBindings(clip, scene);
    }

    public AnimationClip Clip { get; }

    public void Apply(float time, bool loop)
    {
        time = NormalizeTime(time, loop);

        for (int i = 0; i < _bindings.Count; i++)
        {
            var binding = _bindings[i];
            var node = binding.Node;
            if (node is null)
            {
                continue;
            }

            var track = binding.Track;
            if (track.TryEvaluateTranslation(time, out var t))
            {
                node.Transform.LocalPosition = t;
            }

            if (track.TryEvaluateRotation(time, out var r))
            {
                node.Transform.LocalRotation = r;
            }

            if (track.TryEvaluateScale(time, out var s))
            {
                node.Transform.LocalScale = s;
            }
        }
    }

    public void Evaluate(float time, bool loop, AnimationPose pose)
    {
        if (pose is null)
        {
            throw new ArgumentNullException(nameof(pose));
        }

        time = NormalizeTime(time, loop);
        for (int i = 0; i < _bindings.Count; i++)
        {
            var binding = _bindings[i];
            var node = binding.Node;
            if (node is null)
            {
                continue;
            }

            var track = binding.Track;
            if (track.TryEvaluateTranslation(time, out var t))
            {
                pose.SetTranslation(node, t);
            }

            if (track.TryEvaluateRotation(time, out var r))
            {
                pose.SetRotation(node, r);
            }

            if (track.TryEvaluateScale(time, out var s))
            {
                pose.SetScale(node, s);
            }
        }
    }

    private static List<TrackBinding> BuildBindings(AnimationClip clip, SceneGraph scene)
    {
        var map = BuildNodeMap(scene);
        var list = new List<TrackBinding>(clip.Tracks.Count);
        for (int i = 0; i < clip.Tracks.Count; i++)
        {
            var track = clip.Tracks[i];
            map.TryGetValue(track.TargetName, out var node);
            list.Add(new TrackBinding(track, node));
        }

        return list;
    }

    private float NormalizeTime(float time, bool loop)
    {
        float duration = Clip.Duration;
        if (duration <= 0f)
        {
            return time;
        }

        if (loop)
        {
            time %= duration;
            if (time < 0f)
            {
                time += duration;
            }
            return time;
        }

        return MathF.Min(time, duration);
    }

    private static Dictionary<string, SceneNode> BuildNodeMap(SceneGraph scene)
    {
        var map = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
        var stack = new Stack<SceneNode>();
        for (int i = 0; i < scene.Roots.Count; i++)
        {
            stack.Push(scene.Roots[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!map.ContainsKey(node.Name))
            {
                map.Add(node.Name, node);
            }

            var children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                stack.Push(children[i]);
            }
        }

        return map;
    }

    private readonly record struct TrackBinding(TransformTrack Track, SceneNode? Node);
}
