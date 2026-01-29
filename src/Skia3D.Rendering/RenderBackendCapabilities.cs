namespace Skia3D.Rendering;

[Flags]
public enum RenderBackendCapabilities
{
    None = 0,
    DepthPass = 1 << 0,
    ShadowPass = 1 << 1,
    MainPass = 1 << 2
}
