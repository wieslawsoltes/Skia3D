using System;
using System.Collections.Generic;
using System.Numerics;
using SkiaSharp;

namespace Skia3D.Core;

public static class DecalBuilder
{
    public sealed record DecalAtlas(SKBitmap Atlas, SKRectI UvRect);
    public sealed record DecalWithAtlas(SKBitmap Atlas, Renderer3D.Decal Decal);
    public sealed record TextPathLayout(SKPath Path, SKRect PathBounds, SKRect PaddedBounds);
    public readonly record struct PlanarFrame(Vector3 Anchor, Vector3 UDir, Vector3 VDir);

    /// <summary>
    /// Render arbitrary content into a new atlas using a draw callback. The callback receives a cleared canvas.
    /// </summary>
    public static DecalAtlas BuildFromDraw(int width, int height, SKColor clear, Action<SKCanvas> draw)
    {
        var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var gc = new SKCanvas(bmp);
        gc.Clear(clear);
        draw(gc);
        var uvRect = new SKRectI(0, 0, bmp.Width, bmp.Height);
        return new DecalAtlas(bmp, uvRect);
    }

    /// <summary>
    /// Render one or more vector paths with their paints into a new atlas.
    /// </summary>
    public static DecalAtlas BuildFromPaths(int width, int height, SKColor clear, IReadOnlyList<(SKPath path, SKPaint paint)> items)
    {
        return BuildFromDraw(width, height, clear, canvas =>
        {
            foreach (var (path, paint) in items)
            {
                canvas.DrawPath(path, paint);
            }
        });
    }

    public static TextPathLayout BuildTextPath(string text, SKPaint paint, float padX, float padY, float inflateX, float inflateY)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (paint is null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        using var raw = paint.GetTextPath(text, 0, 0);
        return LayoutPath(raw, padX, padY, inflateX, inflateY);
    }

    public static TextPathLayout LayoutPath(SKPath source, float padX, float padY, float inflateX, float inflateY)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var path = new SKPath(source);
        var bounds = path.TightBounds;
        path.Offset(padX - bounds.Left, padY - bounds.Top);

        var layoutBounds = path.TightBounds;
        var padded = layoutBounds;
        padded.Inflate(inflateX, inflateY);
        return new TextPathLayout(path, layoutBounds, padded);
    }

    public static bool TryCreatePlanarFrame(Matrix4x4 transform, float uScale, float vScale, float forwardOffset, out PlanarFrame frame)
    {
        var center = Vector3.Transform(Vector3.Zero, transform);
        var uWorld = Vector3.TransformNormal(Vector3.UnitX, transform);
        var vWorld = Vector3.TransformNormal(Vector3.UnitZ, transform);

        if (uWorld.LengthSquared() < 1e-8f || vWorld.LengthSquared() < 1e-8f)
        {
            frame = default;
            return false;
        }

        var uDir = Vector3.Normalize(uWorld) * uScale;
        var vDir = Vector3.Normalize(vWorld) * vScale;
        var anchor = center + Vector3.Normalize(vWorld) * forwardOffset;

        frame = new PlanarFrame(anchor, uDir, vDir);
        return true;
    }

    public static Renderer3D.Decal CreatePlanarDecal(DecalAtlas atlas, Vector3 centerWorld, Vector3 uDirWorld, Vector3 vDirWorld, SKColor? tint = null, float opacity = 1f)
    {
        return new Renderer3D.Decal(atlas.Atlas, atlas.UvRect, centerWorld, uDirWorld, vDirWorld, tint, opacity);
    }

    public static Renderer3D.Decal CreatePlanarDecal(DecalAtlas atlas, PlanarFrame frame, SKColor? tint = null, float opacity = 1f)
    {
        return CreatePlanarDecal(atlas, frame.Anchor, frame.UDir, frame.VDir, tint, opacity);
    }
}
