using System;
using System.Collections.Generic;

namespace Skia3D.Animation;

public sealed class BlendTree1D
{
    private readonly List<BlendTreeMotion> _motions = new();
    private bool _dirty = true;

    public BlendTree1D(string parameter)
    {
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    public string Parameter { get; }

    public IReadOnlyList<BlendTreeMotion> Motions => _motions;

    public BlendTreeMotion AddMotion(AnimationPlayer player, float threshold)
    {
        if (player is null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        var motion = new BlendTreeMotion(player, threshold);
        _motions.Add(motion);
        _dirty = true;
        return motion;
    }

    public void Clear()
    {
        _motions.Clear();
        _dirty = true;
    }

    public void Evaluate(float deltaSeconds, float parameterValue, AnimationPose output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        output.Clear();

        if (_motions.Count == 0)
        {
            return;
        }

        if (_dirty)
        {
            _motions.Sort((a, b) => a.Threshold.CompareTo(b.Threshold));
            _dirty = false;
        }

        if (_motions.Count == 1)
        {
            var motion = _motions[0];
            motion.Pose.Clear();
            motion.Player.Update(deltaSeconds, motion.Pose);
            PoseBlender.Copy(motion.Pose, output);
            return;
        }

        if (parameterValue <= _motions[0].Threshold)
        {
            var motion = _motions[0];
            motion.Pose.Clear();
            motion.Player.Update(deltaSeconds, motion.Pose);
            PoseBlender.Copy(motion.Pose, output);
            return;
        }

        if (parameterValue >= _motions[^1].Threshold)
        {
            var motion = _motions[^1];
            motion.Pose.Clear();
            motion.Player.Update(deltaSeconds, motion.Pose);
            PoseBlender.Copy(motion.Pose, output);
            return;
        }

        for (int i = 0; i + 1 < _motions.Count; i++)
        {
            var a = _motions[i];
            var b = _motions[i + 1];
            if (parameterValue >= a.Threshold && parameterValue <= b.Threshold)
            {
                float span = b.Threshold - a.Threshold;
                float t = span <= 1e-6f ? 0f : (parameterValue - a.Threshold) / span;

                a.Pose.Clear();
                b.Pose.Clear();
                a.Player.Update(deltaSeconds, a.Pose);
                b.Player.Update(deltaSeconds, b.Pose);
                PoseBlender.Blend(a.Pose, b.Pose, t, output);
                return;
            }
        }
    }
}

public sealed class BlendTreeMotion
{
    public BlendTreeMotion(AnimationPlayer player, float threshold)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Threshold = threshold;
    }

    public AnimationPlayer Player { get; }

    public float Threshold { get; set; }

    internal AnimationPose Pose { get; } = new();
}

