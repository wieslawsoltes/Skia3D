using System.Numerics;

namespace Skia3D.Audio;

public readonly record struct AudioListenerState(Vector3 Position, Vector3 Forward, Vector3 Up, float Volume);

public readonly record struct AudioSourceState(
    AudioSourceComponent Source,
    Vector3 Position,
    Vector3 Velocity,
    float Volume,
    float Pitch,
    float Attenuation);

public interface IAudioBackend
{
    void Initialize();
    void Shutdown();
    void UpdateListener(AudioListenerState state);
    void UpdateSource(AudioSourceState state);
}

public sealed class NullAudioBackend : IAudioBackend
{
    public void Initialize()
    {
    }

    public void Shutdown()
    {
    }

    public void UpdateListener(AudioListenerState state)
    {
    }

    public void UpdateSource(AudioSourceState state)
    {
    }
}
