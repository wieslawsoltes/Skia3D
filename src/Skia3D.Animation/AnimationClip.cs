using System;
using System.Collections.Generic;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Animation;

public sealed class AnimationClip
{
    public AnimationClip(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    public List<TransformTrack> Tracks { get; } = new();

    public float Duration { get; private set; }

    public void RecalculateDuration()
    {
        float max = 0f;
        for (int i = 0; i < Tracks.Count; i++)
        {
            max = MathF.Max(max, Tracks[i].MaxTime);
        }

        Duration = max;
    }

    public BoundAnimationClip Bind(SceneGraph scene)
    {
        if (scene is null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        if (Duration <= 0f)
        {
            RecalculateDuration();
        }

        return new BoundAnimationClip(this, scene);
    }
}
