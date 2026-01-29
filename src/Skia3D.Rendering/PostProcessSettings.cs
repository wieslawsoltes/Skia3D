namespace Skia3D.Rendering;

public enum ToneMappingMode
{
    None,
    Reinhard,
    Aces
}

public sealed class PostProcessSettings
{
    public bool Enabled { get; set; }

    public ToneMappingMode ToneMapping { get; set; } = ToneMappingMode.Reinhard;

    public float Exposure { get; set; } = 1f;

    public BloomSettings Bloom { get; } = new();

    public FxaaSettings Fxaa { get; } = new();
}

public sealed class BloomSettings
{
    public bool Enabled { get; set; }

    public float Threshold { get; set; } = 0.75f;

    public float Intensity { get; set; } = 0.6f;

    public int Radius { get; set; } = 6;
}

public sealed class FxaaSettings
{
    public bool Enabled { get; set; }

    public float EdgeThreshold { get; set; } = 0.125f;

    public float EdgeThresholdMin { get; set; } = 0.0312f;

    public float SubpixelBlend { get; set; } = 0.75f;
}
