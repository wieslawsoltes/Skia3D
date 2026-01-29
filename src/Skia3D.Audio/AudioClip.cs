namespace Skia3D.Audio;

public sealed class AudioClip
{
    public AudioClip(string name, float durationSeconds, int sampleRate = 44100, int channels = 2, float[]? samples = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DurationSeconds = MathF.Max(0f, durationSeconds);
        SampleRate = Math.Max(1, sampleRate);
        Channels = Math.Max(1, channels);
        Samples = samples;
    }

    public string Name { get; }

    public float DurationSeconds { get; }

    public int SampleRate { get; }

    public int Channels { get; }

    public float[]? Samples { get; }
}
