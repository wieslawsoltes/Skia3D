using Skia3D.Core;
using SkiaSharp;

namespace Skia3D.Rendering;

public readonly record struct RenderFrame(
    SKCanvas Canvas,
    SKRect Viewport,
    Camera Camera,
    IReadOnlyList<MeshInstance> Instances,
    IReadOnlyList<Light>? Lights = null);
