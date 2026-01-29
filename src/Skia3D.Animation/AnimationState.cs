using System;

namespace Skia3D.Animation;

public sealed class AnimationState
{
    public AnimationState(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    public AnimationPlayer? Player { get; set; }

    public BlendTree1D? BlendTree { get; set; }

    internal AnimationPose Pose { get; } = new();

    public void Reset()
    {
        Player?.Reset();
        if (BlendTree != null)
        {
            foreach (var motion in BlendTree.Motions)
            {
                motion.Player.Reset();
            }
        }
    }

    public void Update(float deltaSeconds, AnimationParameterSet parameters)
    {
        Pose.Clear();

        if (BlendTree != null)
        {
            float value = parameters?.GetFloat(BlendTree.Parameter) ?? 0f;
            BlendTree.Evaluate(deltaSeconds, value, Pose);
            return;
        }

        Player?.Update(deltaSeconds, Pose);
    }

    public float GetNormalizedTime()
    {
        if (BlendTree != null)
        {
            if (BlendTree.Motions.Count == 0)
            {
                return 0f;
            }

            return GetNormalizedTime(BlendTree.Motions[0].Player);
        }

        return Player != null ? GetNormalizedTime(Player) : 0f;
    }

    private static float GetNormalizedTime(AnimationPlayer player)
    {
        var duration = player.Clip.Clip.Duration;
        if (duration <= 1e-6f)
        {
            return 0f;
        }

        return player.Time / duration;
    }
}
