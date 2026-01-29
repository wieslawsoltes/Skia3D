using System;

namespace Skia3D.Animation;

public sealed class AnimationLayer
{
    public AnimationLayer(AnimationPlayer player, float weight = 1f)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Weight = weight;
    }

    public AnimationPlayer Player { get; }

    public float Weight { get; set; } = 1f;

    public AnimationPose Pose { get; } = new();
}
