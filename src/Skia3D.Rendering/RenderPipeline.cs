using SkiaSharp;

namespace Skia3D.Rendering;

public sealed class RenderPipeline
{
    public RenderPipeline(IRenderBackend backend, RenderPipelineSettings? settings = null, PostProcessor? postProcessor = null)
    {
        Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        Settings = settings ?? new RenderPipelineSettings();
        PostProcessor = postProcessor ?? new PostProcessor();
    }

    public IRenderBackend Backend { get; }

    public RenderPipelineSettings Settings { get; }

    public PostProcessor PostProcessor { get; }

    public void Render(in RenderFrame frame)
    {
        if (!Settings.EnableMainPass)
        {
            return;
        }

        if (!Settings.PostProcess.Enabled)
        {
            RenderPasses(frame);
            return;
        }

        int width = Math.Max(1, (int)MathF.Ceiling(frame.Viewport.Width));
        int height = Math.Max(1, (int)MathF.Ceiling(frame.Viewport.Height));
        using var offscreen = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var offscreenCanvas = new SKCanvas(offscreen);
        var offViewport = new SKRect(0, 0, width, height);
        var offFrame = new RenderFrame(offscreenCanvas, offViewport, frame.Camera, frame.Instances, frame.Lights);

        RenderPasses(offFrame);
        PostProcessor.Apply(offscreen, frame.Canvas, frame.Viewport, Settings.PostProcess);
    }

    private void RenderPasses(in RenderFrame frame)
    {
        if (Settings.EnableDepthPass && Backend.Capabilities.HasFlag(RenderBackendCapabilities.DepthPass))
        {
            Backend.Render(RenderPassType.Depth, frame, Settings);
        }

        if (Settings.EnableShadowPass && Backend.Capabilities.HasFlag(RenderBackendCapabilities.ShadowPass))
        {
            Backend.Render(RenderPassType.Shadow, frame, Settings);
        }

        if (Settings.EnableMainPass && Backend.Capabilities.HasFlag(RenderBackendCapabilities.MainPass))
        {
            Backend.Render(RenderPassType.Main, frame, Settings);
        }
    }
}
