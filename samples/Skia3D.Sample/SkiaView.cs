using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Rendering;
using Avalonia.Skia;
using SkiaSharp;

namespace Skia3D.Sample;

public sealed class SkiaView : Control
{
    public event EventHandler<SkiaRenderEventArgs>? RenderFrame;

    public SkiaView()
    {
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var op = new SkiaCustomDrawOperation(new Rect(Bounds.Size), this, RenderFrame);
        context.Custom(op);
    }

    private sealed class SkiaCustomDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly Control _owner;
        private readonly EventHandler<SkiaRenderEventArgs>? _handler;

        public SkiaCustomDrawOperation(Rect bounds, Control owner, EventHandler<SkiaRenderEventArgs>? handler)
        {
            _bounds = bounds;
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if (leaseFeature is null)
            {
                return;
            }
            using var lease = leaseFeature.Lease();
            if (lease?.SkSurface is null)
            {
                return;
            }

            var width = (int)Math.Ceiling(_bounds.Width);
            var height = (int)Math.Ceiling(_bounds.Height);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var info = new SKImageInfo(width, height);

            var canvas = lease.SkSurface.Canvas;
            using var restore = new SKAutoCanvasRestore(canvas, true);
            canvas.ClipRect(new SKRect(0, 0, (float)_bounds.Width, (float)_bounds.Height));

            _handler?.Invoke(_owner, new SkiaRenderEventArgs(lease.SkSurface, info));
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is SkiaCustomDrawOperation op && op._owner == _owner;
        }
    }
}

public sealed class SkiaRenderEventArgs : EventArgs
{
    public SkiaRenderEventArgs(SKSurface surface, SKImageInfo info)
    {
        Surface = surface;
        Info = info;
    }

    public SKSurface Surface { get; }

    public SKImageInfo Info { get; }
}
