using Skia3D.Core;

namespace Skia3D.Rendering;

public sealed class SoftwareRenderBackend : IRenderBackend
{
    public SoftwareRenderBackend(Renderer3D renderer)
    {
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public Renderer3D Renderer { get; }

    public string Name => "Skia3D.Software";

    public RenderBackendCapabilities Capabilities => RenderBackendCapabilities.DepthPass | RenderBackendCapabilities.ShadowPass | RenderBackendCapabilities.MainPass;

    public void Render(RenderPassType pass, in RenderFrame frame, RenderPipelineSettings settings)
    {
        if (pass != RenderPassType.Main)
        {
            return;
        }

        ApplySettings(settings);

        if (frame.Lights != null)
        {
            Renderer.Lights.Clear();
            Renderer.Lights.AddRange(frame.Lights);
        }

        Renderer.Render(frame.Canvas, frame.Viewport, frame.Camera, frame.Instances);
    }

    private void ApplySettings(RenderPipelineSettings settings)
    {
        Renderer.UseDepthBuffer = settings.EnableDepthPass;
        Renderer.EnableShadows = settings.EnableShadowPass;
    }
}
