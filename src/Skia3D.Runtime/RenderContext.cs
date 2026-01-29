using Skia3D.Core;
using SkiaSharp;

namespace Skia3D.Runtime;

public readonly record struct RenderContext(SKCanvas Canvas, SKRect Viewport, Camera Camera);
