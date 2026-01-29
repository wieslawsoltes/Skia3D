using System.Collections.Generic;
using Skia3D.Core;
using SkiaSharp;

namespace Skia3D.Editor;

public readonly struct EditorSelectionContext
{
    public EditorSelectionContext(Renderer3D renderer, Camera camera, SKRect viewport, IReadOnlyList<MeshInstance> instances)
    {
        Renderer = renderer;
        Camera = camera;
        Viewport = viewport;
        Instances = instances;
    }

    public Renderer3D Renderer { get; }

    public Camera Camera { get; }

    public SKRect Viewport { get; }

    public IReadOnlyList<MeshInstance> Instances { get; }
}
