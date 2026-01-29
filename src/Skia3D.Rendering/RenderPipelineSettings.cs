namespace Skia3D.Rendering;

public sealed class RenderPipelineSettings
{
    public bool EnableDepthPass { get; set; } = true;

    public bool EnableShadowPass { get; set; } = true;

    public bool EnableMainPass { get; set; } = true;

    public PostProcessSettings PostProcess { get; } = new();
}
