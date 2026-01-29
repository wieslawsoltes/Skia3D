namespace Skia3D.Rendering;

public interface IRenderBackend
{
    string Name { get; }

    RenderBackendCapabilities Capabilities { get; }

    void Render(RenderPassType pass, in RenderFrame frame, RenderPipelineSettings settings);
}
