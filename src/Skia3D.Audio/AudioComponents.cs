using System.Numerics;
using Skia3D.Scene;

namespace Skia3D.Audio;

public sealed class AudioListenerComponent : SceneComponent
{
    public float Volume { get; set; } = 1f;
}

public sealed class AudioSourceComponent : SceneComponent
{
    public AudioClip? Clip { get; set; }

    public bool Loop { get; set; }

    public bool IsPlaying { get; private set; }

    public float Volume { get; set; } = 1f;

    public float Pitch { get; set; } = 1f;

    public float SpatialBlend { get; set; } = 1f;

    public float MinDistance { get; set; } = 1f;

    public float MaxDistance { get; set; } = 30f;

    public Vector3 Velocity { get; set; }

    public float Time { get; private set; }

    public void Play()
    {
        if (Clip == null)
        {
            return;
        }

        IsPlaying = true;
    }

    public void Stop()
    {
        IsPlaying = false;
        Time = 0f;
    }

    internal void Advance(float deltaSeconds)
    {
        if (!IsPlaying || Clip == null)
        {
            return;
        }

        Time += deltaSeconds * MathF.Max(0.01f, Pitch);
        if (Clip.DurationSeconds <= 0f)
        {
            return;
        }

        if (Time >= Clip.DurationSeconds)
        {
            if (Loop)
            {
                Time %= Clip.DurationSeconds;
            }
            else
            {
                Stop();
            }
        }
    }
}
