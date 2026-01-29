using System;

namespace Skia3D.Animation;

public sealed class AnimationPlayer
{
    public AnimationPlayer(BoundAnimationClip clip)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
    }

    public BoundAnimationClip Clip { get; }

    public bool Loop { get; set; } = true;

    public bool IsPlaying { get; set; } = true;

    public float Speed { get; set; } = 1f;

    public float Time { get; private set; }

    public void Update(float deltaSeconds, AnimationPose? pose = null)
    {
        if (!IsPlaying)
        {
            return;
        }

        Time += deltaSeconds * Speed;
        if (pose is null)
        {
            Clip.Apply(Time, Loop);
        }
        else
        {
            Clip.Evaluate(Time, Loop, pose);
        }
    }

    public void Advance(float deltaSeconds)
    {
        if (!IsPlaying)
        {
            return;
        }

        Time += deltaSeconds * Speed;
    }

    public void Seek(float time, AnimationPose? pose = null)
    {
        Time = time;
        if (pose is null)
        {
            Clip.Apply(Time, Loop);
        }
        else
        {
            Clip.Evaluate(Time, Loop, pose);
        }
    }

    public void Reset()
    {
        Time = 0f;
        Clip.Apply(Time, Loop);
    }
}
