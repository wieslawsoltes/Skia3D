using System;
using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;

namespace Skia3D.Core;

public sealed class Renderer3D
{
    public sealed record Decal(SKBitmap Atlas, SKRectI UvRect, Vector3 CenterWorld, Vector3 UDirWorld, Vector3 VDirWorld, SKColor? Tint = null, float Opacity = 1f);

    public readonly record struct PreparedDecal(
        IntPtr Pixels,
        int Width,
        int Height,
        int RowPixels,
        SKRectI UvRect,
        SKColor Tint,
        Vector3 CenterView,
        Vector3 UDirNorm,
        Vector3 VDirNorm,
        float InvULength,
        float InvVLength,
        float Opacity);

    private record struct ClipVertex(Vector4 Clip, Vector3 ViewPosition, Vector3 ViewNormal, SKColor Color);

    private record struct Triangle(
        ClipVertex A,
        ClipVertex B,
        ClipVertex C,
        SKPoint P0,
        SKPoint P1,
        SKPoint P2,
        Material Material);

    private enum PainterItemKind { Triangle, Path }

    private readonly record struct PainterItem(
        float SortBucket,
        float SortDepthMax,
        float SortDepthAvg,
        float SortDepthMin,
        PainterItemKind Kind,
        Triangle Triangle,
        SKColor Color,
        bool DrawWireframe,
        SKPath? Path,
        SKPaint? Paint)
    {
        public static PainterItem ForTriangle(float bucket, float depthMax, float depthAvg, float depthMin, Triangle tri, SKColor color, bool drawWireframe) =>
            new(bucket, depthMax, depthAvg, depthMin, PainterItemKind.Triangle, tri, color, drawWireframe, null, null);

        public static PainterItem ForPath(float depth, SKPath path, SKPaint paint) =>
            new(depth, depth, depth, depth, PainterItemKind.Path, default, default, false, path, paint);
    }

    public SKColor Background { get; set; } = new SKColor(20, 22, 26);

    public bool ShowWireframe { get; set; }

    public bool EnableBackfaceCulling { get; set; } = true;

    public bool UseDepthBuffer { get; set; } = true;

    public bool EnableLighting { get; set; } = true;

    public List<Light> Lights { get; } = new() { Light.Directional(new Vector3(-0.4f, -1f, -0.6f), new SKColor(255, 255, 255), 1f) };

    public Material DefaultMaterial { get; set; } = Material.Default();

    public float DepthRenderScale { get; set; } = 1f;

    public List<Decal> Decals { get; } = new();

    public sealed record OverlayPath(SKPath Path, SKPaint Paint, Vector3 WorldPosition, SKPoint Offset);

    public sealed record ProjectedPath(SKPath Path, SKPaint Paint, Matrix4x4 World);

    public List<OverlayPath> Overlays { get; } = new();

    public List<ProjectedPath> ProjectedPaths { get; } = new();

    public int ProjectedPathSamples { get; set; } = 64;

    public float ProjectedPathMinStep { get; set; } = 0.5f;

    private const float PainterDepthSpanThreshold = 0.18f;
    private const int PainterDepthBuckets = 256;
    private const float PainterScreenEdgeThreshold = 120f;
    private const int PainterMaxSplits = 1;

    public void ClearDecals() => Decals.Clear();

    public void AddDecal(Decal decal) => Decals.Add(decal);

    public void ClearOverlays() => Overlays.Clear();

    public void AddOverlayPath(SKPath path, SKPaint paint, Vector3 worldPosition, SKPoint? offset = null)
    {
        Overlays.Add(new OverlayPath(path, paint, worldPosition, offset ?? new SKPoint(0, 0)));
    }

    public void ClearProjectedPaths() => ProjectedPaths.Clear();

    public void AddProjectedPath(SKPath path, SKPaint paint, Matrix4x4 world)
    {
        ProjectedPaths.Add(new ProjectedPath(path, paint, world));
    }

    private SKBitmap? _depthBitmap;
    private float[]? _depthBuffer;
    private int _bufferWidth;
    private int _bufferHeight;

    public IReadOnlyList<PreparedDecal> PrepareDecals(Camera camera)
    {
        if (Decals.Count == 0)
        {
            return Array.Empty<PreparedDecal>();
        }

        var view = camera.GetViewMatrix();
        var list = new List<PreparedDecal>(Decals.Count);

        foreach (var decal in Decals)
        {
            if (decal.Atlas == null || decal.Atlas.IsEmpty)
            {
                continue;
            }

            var center = Vector3.Transform(decal.CenterWorld, view);
            var u = Vector3.TransformNormal(decal.UDirWorld, view);
            var v = Vector3.TransformNormal(decal.VDirWorld, view);

            var uLen = u.Length();
            var vLen = v.Length();
            if (uLen < 1e-4f || vLen < 1e-4f)
            {
                continue;
            }

            var pixmap = decal.Atlas.PeekPixels();
            if (pixmap is null)
            {
                continue;
            }

            list.Add(new PreparedDecal(
                pixmap.GetPixels(),
                decal.Atlas.Width,
                decal.Atlas.Height,
                pixmap.RowBytes >> 2,
                decal.UvRect,
                decal.Tint ?? SKColors.White,
                center,
                Vector3.Normalize(u),
                Vector3.Normalize(v),
                1f / uLen,
                1f / vLen,
                decal.Opacity));
        }

        return list;
    }

    public void Render(SKCanvas canvas, SKRect viewport, Camera camera, IEnumerable<MeshInstance> instances)
    {
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        camera.AspectRatio = viewport.Width / viewport.Height;

        if (UseDepthBuffer)
        {
            RenderWithDepthBuffer(canvas, viewport, camera, instances);
            var projected = PrepareProjectedPaths(viewport, camera);
            foreach (var item in projected)
            {
                canvas.DrawPath(item.path, item.paint);
                item.path.Dispose();
            }
            DrawOverlays(canvas, viewport, camera);
            return;
        }

        var projectedList = PrepareProjectedPaths(viewport, camera);
        RenderPainter(canvas, viewport, camera, instances, projectedList);
        DrawOverlays(canvas, viewport, camera);
    }

    private void RenderPainter(SKCanvas canvas, SKRect viewport, Camera camera, IEnumerable<MeshInstance> instances, IReadOnlyList<(float depth, SKPath path, SKPaint paint)> projectedPaths)
    {
        canvas.Save();
        canvas.Clear(Background);

        var triangles = RefineForPainter(CollectTriangles(instances, camera, viewport)).ToList();
        var painterItems = new List<PainterItem>(triangles.Count + projectedPaths.Count);

        foreach (var t in triangles)
        {
            var d0 = t.A.Clip.Z / t.A.Clip.W;
            var d1 = t.B.Clip.Z / t.B.Clip.W;
            var d2 = t.C.Clip.Z / t.C.Clip.W;
            var depthMin = MathF.Min(d0, MathF.Min(d1, d2));
            var depthMax = MathF.Max(d0, MathF.Max(d1, d2));
            var depthAvg = (d0 + d1 + d2) / 3f;
            var depthSpan = depthMax - depthMin;

            var bucket = MathF.Floor(depthMax * PainterDepthBuckets) / PainterDepthBuckets;
            var color = AverageColor(t.A.Color, t.B.Color, t.C.Color);

            painterItems.Add(PainterItem.ForTriangle(bucket, depthMax, depthAvg, depthMin, t, color, drawWireframe: ShowWireframe && depthSpan <= PainterDepthSpanThreshold));

            // Fallback dual-pass: back then front when the slab spans a large depth range.
            if (depthSpan > PainterDepthSpanThreshold)
            {
                var frontBucket = MathF.Floor(depthMin * PainterDepthBuckets) / PainterDepthBuckets;
                painterItems.Add(PainterItem.ForTriangle(frontBucket, depthMin, depthMin, depthMin, t, color, drawWireframe: ShowWireframe));
            }
        }

        foreach (var p in projectedPaths)
        {
            painterItems.Add(PainterItem.ForPath(p.depth, p.path, p.paint));
        }

        painterItems.Sort((a, b) =>
        {
            int cmp = b.SortBucket.CompareTo(a.SortBucket);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = b.SortDepthMax.CompareTo(a.SortDepthMax);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = b.SortDepthAvg.CompareTo(a.SortDepthAvg);
            if (cmp != 0)
            {
                return cmp;
            }

            return b.SortDepthMin.CompareTo(a.SortDepthMin);
        });

        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SKColor(255, 255, 255, 160)
        };

        foreach (var item in painterItems)
        {
            if (item.Kind == PainterItemKind.Triangle)
            {
                fillPaint.Color = item.Color;
                using var path = new SKPath();
                path.MoveTo(item.Triangle.P0);
                path.LineTo(item.Triangle.P1);
                path.LineTo(item.Triangle.P2);
                path.Close();
                canvas.DrawPath(path, fillPaint);
                if (item.DrawWireframe)
                {
                    canvas.DrawPath(path, strokePaint);
                }
                continue;
            }

            canvas.DrawPath(item.Path!, item.Paint!);
            item.Path!.Dispose();
        }

        canvas.Restore();
    }

    private void DrawDecalsPainter(SKCanvas canvas, SKRect viewport, Camera camera)
    {
        if (Decals.Count == 0)
        {
            return;
        }

        var view = camera.GetViewMatrix();
        var projected = new List<(float depth, SKRect dest, SKBitmap bitmap, SKColor tint, float opacity)>(Decals.Count);

        foreach (var decal in Decals)
        {
            if (decal.Atlas == null || decal.Atlas.IsEmpty)
            {
                continue;
            }

            var center = Vector3.Transform(decal.CenterWorld, view);
            var u = Vector3.TransformNormal(decal.UDirWorld, view);
            var v = Vector3.TransformNormal(decal.VDirWorld, view);

            var p0World = decal.CenterWorld - decal.UDirWorld * 0.5f - decal.VDirWorld * 0.5f;
            var p1World = decal.CenterWorld + decal.UDirWorld * 0.5f - decal.VDirWorld * 0.5f;
            var p2World = decal.CenterWorld + decal.UDirWorld * 0.5f + decal.VDirWorld * 0.5f;
            var p3World = decal.CenterWorld - decal.UDirWorld * 0.5f + decal.VDirWorld * 0.5f;

            if (!TryProjectWorld(p0World, viewport, camera, out var p0))
            {
                continue;
            }
            if (!TryProjectWorld(p1World, viewport, camera, out var p1))
            {
                continue;
            }
            if (!TryProjectWorld(p2World, viewport, camera, out var p2))
            {
                continue;
            }
            if (!TryProjectWorld(p3World, viewport, camera, out var p3))
            {
                continue;
            }

            var subset = new SKBitmap();
            if (!decal.Atlas.ExtractSubset(subset, decal.UvRect))
            {
                subset.Dispose();
                continue;
            }

            var minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
            var maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
            var minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
            var maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));
            var dest = new SKRect(minX, minY, maxX, maxY);

            projected.Add((center.Z, dest, subset, decal.Tint ?? SKColors.White, decal.Opacity));
        }

        if (projected.Count == 0)
        {
            return;
        }

        projected.Sort((a, b) => a.depth.CompareTo(b.depth)); // far-to-near

        foreach (var item in projected)
        {
            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true,
                ColorFilter = SKColorFilter.CreateBlendMode(item.tint, SKBlendMode.Modulate),
                Color = new SKColor(255, 255, 255, (byte)(Math.Clamp(item.opacity, 0f, 1f) * 255f))
            };

            canvas.DrawBitmap(item.bitmap, item.dest, paint);
            item.bitmap.Dispose();
        }
    }

    public bool TryProjectWorld(Vector3 worldPosition, SKRect viewport, Camera camera, out SKPoint screen)
    {
        return TryProjectWorld(worldPosition, viewport, camera, out screen, out _);
    }

    public bool TryProjectWorld(Vector3 worldPosition, SKRect viewport, Camera camera, out SKPoint screen, out float ndcZ)
    {
        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();
        var clip = Vector4.Transform(new Vector4(worldPosition, 1f), view * projection);
        screen = default;
        ndcZ = 0f;
        if (MathF.Abs(clip.W) < 1e-5f)
        {
            return false;
        }

        var ndc = clip / clip.W;
        ndcZ = ndc.Z;
        if (ndc.X is < -1f or > 1f || ndc.Y is < -1f or > 1f || ndc.Z is < -1f or > 1f)
        {
            return false;
        }

        screen = ProjectToScreen(ndc, viewport);
        return true;
    }

    private void DrawOverlays(SKCanvas canvas, SKRect viewport, Camera camera)
    {
        if (Overlays.Count == 0)
        {
            return;
        }

        var projected = new List<(float depth, OverlayPath overlay)>(Overlays.Count);
        foreach (var overlay in Overlays)
        {
            if (!TryProjectWorld(overlay.WorldPosition, viewport, camera, out var screen, out var ndcZ))
            {
                continue;
            }

            var drawOverlay = overlay with { Offset = new SKPoint(screen.X + overlay.Offset.X, screen.Y + overlay.Offset.Y) };
            projected.Add((ndcZ, drawOverlay));
        }

        if (projected.Count == 0)
        {
            return;
        }

        projected.Sort((a, b) => a.depth.CompareTo(b.depth)); // far-to-near

        foreach (var item in projected)
        {
            var ov = item.overlay;
            canvas.Save();
            canvas.Translate(ov.Offset.X, ov.Offset.Y);
            canvas.DrawPath(ov.Path, ov.Paint);
            canvas.Restore();
        }
    }

    public bool TryGetDepthAtScreen(SKPoint screen, SKRect viewport, out float depth)
    {
        depth = float.PositiveInfinity;
        if (_depthBuffer is null || _bufferWidth <= 0 || _bufferHeight <= 0)
        {
            return false;
        }

        var sx = _bufferWidth / viewport.Width;
        var sy = _bufferHeight / viewport.Height;
        var px = (int)MathF.Round(screen.X * sx);
        var py = (int)MathF.Round(screen.Y * sy);

        if (px < 0 || py < 0 || px >= _bufferWidth || py >= _bufferHeight)
        {
            return false;
        }

        depth = _depthBuffer[py * _bufferWidth + px];
        return true;
    }

    private void RenderWithDepthBuffer(SKCanvas canvas, SKRect viewport, Camera camera, IEnumerable<MeshInstance> instances)
    {
        var scale = Math.Clamp(DepthRenderScale, 0.25f, 1f);
        var width = (int)MathF.Ceiling(viewport.Width * scale);
        var height = (int)MathF.Ceiling(viewport.Height * scale);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        EnsureBuffers(width, height);
        _depthBitmap!.Erase(Background);
        Array.Fill(_depthBuffer!, float.PositiveInfinity);

        var triangles = CollectTriangles(instances, camera, viewport).ToList();
        var scaled = new List<Triangle>(triangles.Count);
        var sx = width / viewport.Width;
        var sy = height / viewport.Height;
        foreach (var t in triangles)
        {
            scaled.Add(new Triangle(
                t.A,
                t.B,
                t.C,
                new SKPoint(t.P0.X * sx, t.P0.Y * sy),
                new SKPoint(t.P1.X * sx, t.P1.Y * sy),
                new SKPoint(t.P2.X * sx, t.P2.Y * sy),
                t.Material));
        }

        var preparedDecals = PrepareDecals(camera);

        RasterizeParallel(scaled, _depthBitmap, _depthBuffer!, width, height, camera, preparedDecals);

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low, IsAntialias = true };
        canvas.DrawBitmap(_depthBitmap, new SKRect(0, 0, width, height), viewport, paint);

        if (ShowWireframe)
        {
            using var strokePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                Color = new SKColor(255, 255, 255, 200)
            };

            foreach (var tri in triangles)
            {
                using var path = new SKPath();
                path.MoveTo(tri.P0);
                path.LineTo(tri.P1);
                path.LineTo(tri.P2);
                path.Close();
                canvas.DrawPath(path, strokePaint);
            }
        }
    }

    private List<(float depth, SKPath path, SKPaint paint)> PrepareProjectedPaths(SKRect viewport, Camera camera)
    {
        if (ProjectedPaths.Count == 0)
        {
            return new List<(float depth, SKPath path, SKPaint paint)>();
        }

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();
        var viewProj = view * projection;

        var items = new List<(float depth, SKPath path, SKPaint paint)>(ProjectedPaths.Count);

        foreach (var proj in ProjectedPaths)
        {
            using var measure = new SKPathMeasure(proj.Path, false);
            var screenPath = new SKPath();
            float depthAcc = 0f;
            int depthCount = 0;
            bool hasAny = false;

            do
            {
                var length = measure.Length;
                if (length <= 0f)
                {
                    continue;
                }

                var samples = Math.Max(4, ProjectedPathSamples);
                var step = length / samples;
                if (step < ProjectedPathMinStep)
                {
                    step = ProjectedPathMinStep;
                }

                bool first = true;
                for (float d = 0; d <= length; d += step)
                {
                    if (!measure.GetPositionAndTangent(d, out var pos, out _))
                    {
                        continue;
                    }

                    // Flip Y from Skia path space (down) to world up before applying transform.
                    var world = Vector3.Transform(new Vector3(pos.X, -pos.Y, 0f), proj.World);
                    var clip = Vector4.Transform(new Vector4(world, 1f), viewProj);
                    if (MathF.Abs(clip.W) < 1e-5f)
                    {
                        continue;
                    }
                    var ndc = clip / clip.W;
                    if (ndc.X is < -1.2f or > 1.2f || ndc.Y is < -1.2f or > 1.2f || ndc.Z is < -1f or > 1f)
                    {
                        continue;
                    }

                    var screen = ProjectToScreen(ndc, viewport);
                    if (first)
                    {
                        screenPath.MoveTo(screen);
                        first = false;
                    }
                    else
                    {
                        screenPath.LineTo(screen);
                    }
                    depthAcc += ndc.Z;
                    depthCount++;
                    hasAny = true;
                }

                // close contour if we added points
                if (!first)
                {
                    screenPath.Close();
                }

            } while (measure.NextContour());

            if (!hasAny || depthCount == 0)
            {
                screenPath.Dispose();
                continue;
            }

            items.Add(((depthAcc / depthCount) - 0.001f, screenPath, proj.Paint));
        }

        items.Sort((a, b) => b.depth.CompareTo(a.depth)); // far-to-near
        return items;
    }

    private IEnumerable<Triangle> RefineForPainter(IEnumerable<Triangle> source)
    {
        var result = new List<Triangle>();
        var stack = new Stack<(Triangle tri, int level)>();
        foreach (var t in source)
        {
            stack.Push((t, 0));
        }

        while (stack.Count > 0)
        {
            var (tri, level) = stack.Pop();

            var d0 = tri.A.Clip.Z / tri.A.Clip.W;
            var d1 = tri.B.Clip.Z / tri.B.Clip.W;
            var d2 = tri.C.Clip.Z / tri.C.Clip.W;
            var depthMin = MathF.Min(d0, MathF.Min(d1, d2));
            var depthMax = MathF.Max(d0, MathF.Max(d1, d2));
            var depthSpan = depthMax - depthMin;

            float EdgeLen(SKPoint a, SKPoint b)
            {
                var dx = a.X - b.X;
                var dy = a.Y - b.Y;
                return MathF.Sqrt(dx * dx + dy * dy);
            }

            var e0 = EdgeLen(tri.P0, tri.P1);
            var e1 = EdgeLen(tri.P1, tri.P2);
            var e2 = EdgeLen(tri.P2, tri.P0);
            var maxEdge = MathF.Max(e0, MathF.Max(e1, e2));

            bool shouldSplit = depthSpan > PainterDepthSpanThreshold && maxEdge > PainterScreenEdgeThreshold && level < PainterMaxSplits;

            if (!shouldSplit)
            {
                result.Add(tri);
                continue;
            }

            foreach (var sub in SubdivideTriangle(tri))
            {
                stack.Push((sub, level + 1));
            }
        }

        return result;
    }

    private static Triangle[] SubdivideTriangle(Triangle tri)
    {
        var abV = Lerp(tri.A, tri.B, 0.5f);
        var bcV = Lerp(tri.B, tri.C, 0.5f);
        var caV = Lerp(tri.C, tri.A, 0.5f);

        var abP = MidPoint(tri.P0, tri.P1);
        var bcP = MidPoint(tri.P1, tri.P2);
        var caP = MidPoint(tri.P2, tri.P0);

        var m = tri.Material;
        return new[]
        {
            new Triangle(tri.A, abV, caV, tri.P0, abP, caP, m),
            new Triangle(abV, tri.B, bcV, abP, tri.P1, bcP, m),
            new Triangle(caV, bcV, tri.C, caP, bcP, tri.P2, m),
            new Triangle(abV, bcV, caV, abP, bcP, caP, m)
        };
    }

    private static SKPoint MidPoint(SKPoint a, SKPoint b)
    {
        return new SKPoint((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);
    }

    private IEnumerable<Triangle> CollectTriangles(IEnumerable<MeshInstance> instances, Camera camera, SKRect viewport)
    {
        var results = new List<Triangle>();
        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();
        var viewProj = view * projection;
        var planes = ExtractFrustumPlanes(viewProj);

        foreach (var instance in instances)
        {
            if (!instance.IsVisible)
            {
                continue;
            }

            var world = instance.Transform;
            if (IsCulled(instance.Mesh, world, planes))
            {
                continue;
            }

            var worldView = world * view;
            var worldViewProjection = worldView * projection;
            var mesh = instance.Mesh;
            var indices = mesh.Indices;
            var vertices = mesh.Vertices;
            var material = instance.Material ?? DefaultMaterial;

            for (var i = 0; i < indices.Count; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var cv0 = Transform(vertices[i0], worldView, worldViewProjection);
                var cv1 = Transform(vertices[i1], worldView, worldViewProjection);
                var cv2 = Transform(vertices[i2], worldView, worldViewProjection);

                if (!cv0.HasValue || !cv1.HasValue || !cv2.HasValue)
                {
                    continue;
                }

                var clipped = ClipToFrustum(cv0.Value, cv1.Value, cv2.Value);
                foreach (var tri in clipped)
                {
                    if (EnableBackfaceCulling && !(material.DoubleSided) && IsBackFacing(tri.A, tri.B, tri.C))
                    {
                        continue;
                    }

                    var ndc0 = tri.A.Clip / tri.A.Clip.W;
                    var ndc1 = tri.B.Clip / tri.B.Clip.W;
                    var ndc2 = tri.C.Clip / tri.C.Clip.W;

                    const float clipLimit = 1.2f;
                    if (MathF.Abs(ndc0.X) > clipLimit && MathF.Abs(ndc0.Y) > clipLimit &&
                        MathF.Abs(ndc1.X) > clipLimit && MathF.Abs(ndc1.Y) > clipLimit &&
                        MathF.Abs(ndc2.X) > clipLimit && MathF.Abs(ndc2.Y) > clipLimit)
                    {
                        continue;
                    }

                    var p0 = ProjectToScreen(ndc0, viewport);
                    var p1 = ProjectToScreen(ndc1, viewport);
                    var p2 = ProjectToScreen(ndc2, viewport);

                    results.Add(new Triangle(
                        tri.A with { Clip = ndc0 },
                        tri.B with { Clip = ndc1 },
                        tri.C with { Clip = ndc2 },
                        p0,
                        p1,
                        p2,
                        material));
                }
            }
        }

        return results;
    }

    private unsafe void RasterizeParallel(IReadOnlyList<Triangle> triangles, SKBitmap bitmap, float[] depth, int width, int height, Camera camera, IReadOnlyList<PreparedDecal> decals)
    {
        var view = camera.GetViewMatrix();
        var lightInfos = Lights.Select(light => ToView(light, view)).ToList();
        var backgroundPacked = PackColor(Background);

        Array.Fill(depth, float.PositiveInfinity);
        using var pixmap = bitmap.PeekPixels();
        var ptr = (uint*)pixmap.GetPixels();
        var stride = pixmap.RowBytes >> 2;
        int pixels = width * height;
        for (int i = 0; i < pixels; i++)
        {
            ptr[i] = backgroundPacked;
        }

        int workerCount = Math.Min(Environment.ProcessorCount, 8);
        if (workerCount <= 1 || triangles.Count < 64)
        {
            RasterizeBand(triangles, ptr, stride, depth, width, height, 0, height, lightInfos, decals);
            return;
        }

        int rowsPerWorker = (height + workerCount - 1) / workerCount;
        Parallel.For(0, workerCount, workerIndex =>
        {
            int yStart = workerIndex * rowsPerWorker;
            int yEnd = Math.Min(height, yStart + rowsPerWorker);
            if (yStart >= yEnd)
            {
                return;
            }

            RasterizeBand(triangles, ptr, stride, depth, width, height, yStart, yEnd, lightInfos, decals);
        });
    }

    private unsafe void RasterizeBand(IEnumerable<Triangle> triangles, uint* colorPtr, int colorStride, float[] depth, int width, int height, int yStart, int yEnd, List<(Light light, Vector3 dirView)> lightInfos, IReadOnlyList<PreparedDecal> decals)
    {
        foreach (var tri in triangles)
        {
            var area = Edge(tri.P0, tri.P1, tri.P2);
            if (MathF.Abs(area) < 1e-6f)
            {
                continue;
            }

            var minX = (int)MathF.Floor(MathF.Min(tri.P0.X, MathF.Min(tri.P1.X, tri.P2.X)));
            var maxX = (int)MathF.Ceiling(MathF.Max(tri.P0.X, MathF.Max(tri.P1.X, tri.P2.X)));
            var minY = (int)MathF.Floor(MathF.Min(tri.P0.Y, MathF.Min(tri.P1.Y, tri.P2.Y)));
            var maxY = (int)MathF.Ceiling(MathF.Max(tri.P0.Y, MathF.Max(tri.P1.Y, tri.P2.Y)));

            minX = Math.Clamp(minX, 0, width - 1);
            maxX = Math.Clamp(maxX, 0, width - 1);
            minY = Math.Clamp(minY, yStart, yEnd - 1);
            maxY = Math.Clamp(maxY, yStart, yEnd - 1);

            if (minY > maxY || minX > maxX)
            {
                continue;
            }

            var invArea = 1f / area;
            bool areaPositive = area > 0f;

            for (int y = minY; y <= maxY; y++)
            {
                var rowPtr = colorPtr + y * colorStride;
                var rowDepth = depth.AsSpan(y * width, width);
                for (int x = minX; x <= maxX; x++)
                {
                    var w0 = Edge(tri.P1, tri.P2, x + 0.5f, y + 0.5f);
                    var w1 = Edge(tri.P2, tri.P0, x + 0.5f, y + 0.5f);
                    var w2 = Edge(tri.P0, tri.P1, x + 0.5f, y + 0.5f);

                    if (areaPositive)
                    {
                        if (w0 < 0 || w1 < 0 || w2 < 0)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (w0 > 0 || w1 > 0 || w2 > 0)
                        {
                            continue;
                        }
                    }

                    w0 *= invArea;
                    w1 *= invArea;
                    w2 *= invArea;

                    var depthValue = w0 * tri.A.Clip.Z + w1 * tri.B.Clip.Z + w2 * tri.C.Clip.Z;
                    ref var depthSlot = ref rowDepth[x];
                    if (depthValue >= depthSlot)
                    {
                        continue;
                    }

                    depthSlot = depthValue;
                    var viewPos = tri.A.ViewPosition * w0 + tri.B.ViewPosition * w1 + tri.C.ViewPosition * w2;
                    var color = Shade(tri, w0, w1, w2, lightInfos);
                    if (decals.Count > 0)
                    {
                        color = ApplyDecals(color, viewPos, decals);
                    }
                    rowPtr[x] = PackColor(color);
                }
            }
        }
    }

    private SKColor Shade(Triangle tri, float w0, float w1, float w2, List<(Light light, Vector3 dirView)> lightsView)
    {
        var c0 = tri.A.Color;
        var c1 = tri.B.Color;
        var c2 = tri.C.Color;
        var vColor = LerpColor(c0, c1, c2, w0, w1, w2);
        var material = tri.Material ?? DefaultMaterial;
        var baseColor = material.UseVertexColor ? MultiplyColor(material.BaseColor, vColor) : material.BaseColor;

        if (!EnableLighting)
        {
            return baseColor;
        }

        var normal = Vector3.Normalize(tri.A.ViewNormal * w0 + tri.B.ViewNormal * w1 + tri.C.ViewNormal * w2);
        var viewDir = -Vector3.Normalize(tri.A.ViewPosition * w0 + tri.B.ViewPosition * w1 + tri.C.ViewPosition * w2);
        if (material.DoubleSided && Vector3.Dot(normal, viewDir) < 0f)
        {
            normal = -normal;
        }

        float rAcc = 0f, gAcc = 0f, bAcc = 0f;
        foreach (var (light, dirView) in lightsView)
        {
            var lightColor = light.Color;
            float lr = lightColor.Red / 255f;
            float lg = lightColor.Green / 255f;
            float lb = lightColor.Blue / 255f;

            float attenuation = 1f;
            var ldir = dirView;
            if (light.Type == LightType.Point)
            {
                var lightPosView = dirView; // packed for point in dirView
                var toLight = lightPosView - (tri.A.ViewPosition * w0 + tri.B.ViewPosition * w1 + tri.C.ViewPosition * w2);
                var dist = toLight.Length();
                if (dist <= 1e-4f)
                {
                    continue;
                }
                ldir = Vector3.Normalize(-toLight);
                var att = MathF.Max(0f, 1f - dist / MathF.Max(0.001f, light.Range));
                attenuation = att;
            }

            var ndotl = MathF.Max(0f, Vector3.Dot(normal, -ldir));
            var diffuse = material.Diffuse * ndotl * light.Intensity * attenuation;

            var reflect = Vector3.Reflect(ldir, normal);
            var spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(reflect, viewDir)), material.Shininess);
            var specular = material.Specular * spec * light.Intensity * attenuation;

            rAcc += lr * diffuse + lr * specular;
            gAcc += lg * diffuse + lg * specular;
            bAcc += lb * diffuse + lb * specular;
        }

        float br = baseColor.Red / 255f;
        float bg = baseColor.Green / 255f;
        float bb = baseColor.Blue / 255f;

        var ambient = material.Ambient;

        float r = br * (ambient + rAcc);
        float g = bg * (ambient + gAcc);
        float b = bb * (ambient + bAcc);

        return new SKColor(
            (byte)(Math.Clamp(r, 0f, 1f) * 255f),
            (byte)(Math.Clamp(g, 0f, 1f) * 255f),
            (byte)(Math.Clamp(b, 0f, 1f) * 255f),
            baseColor.Alpha);
    }

    private static (Light light, Vector3 dirView) ToView(Light light, Matrix4x4 view)
    {
        return light.Type switch
        {
            LightType.Directional => (light, Vector3.Normalize(Vector3.TransformNormal(light.Direction, view))),
            LightType.Point => (light, Vector3.Transform(light.Position, view)),
            _ => (light, Vector3.Zero)
        };
    }

    private static float Edge(SKPoint a, SKPoint b, SKPoint c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    private static float Edge(SKPoint a, SKPoint b, float x, float y)
    {
        return (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
    }

    private static uint PackColor(SKColor color)
    {
        return (uint)(color.Red | (color.Green << 8) | (color.Blue << 16) | (color.Alpha << 24));
    }

    private static SKColor LerpColor(SKColor c0, SKColor c1, SKColor c2, float w0, float w1, float w2)
    {
        var r = c0.Red * w0 + c1.Red * w1 + c2.Red * w2;
        var g = c0.Green * w0 + c1.Green * w1 + c2.Green * w2;
        var b = c0.Blue * w0 + c1.Blue * w1 + c2.Blue * w2;
        var a = c0.Alpha * w0 + c1.Alpha * w1 + c2.Alpha * w2;
        return new SKColor((byte)r, (byte)g, (byte)b, (byte)a);
    }

    private static SKColor MultiplyColor(SKColor a, SKColor b)
    {
        return new SKColor(
            (byte)(a.Red * b.Red / 255),
            (byte)(a.Green * b.Green / 255),
            (byte)(a.Blue * b.Blue / 255),
            (byte)(a.Alpha * b.Alpha / 255));
    }

    private static SKColor ApplyDecals(SKColor baseColor, Vector3 viewPos, IReadOnlyList<PreparedDecal> decals)
    {
        var br = baseColor.Red;
        var bg = baseColor.Green;
        var bb = baseColor.Blue;
        var ba = baseColor.Alpha;

        for (int i = 0; i < decals.Count; i++)
        {
            var d = decals[i];
            var dp = viewPos - d.CenterView;
            var uLen = Vector3.Dot(dp, d.UDirNorm) * d.InvULength;
            var vLen = Vector3.Dot(dp, d.VDirNorm) * d.InvVLength;

            if (uLen is < 0f or > 1f || vLen is < 0f or > 1f)
            {
                continue;
            }

            // Map to atlas UV rect
            var u = d.UvRect.Left + uLen * (d.UvRect.Width - 1);
            var v = d.UvRect.Top + vLen * (d.UvRect.Height - 1);

            var x0 = (int)MathF.Floor(u);
            var y0 = (int)MathF.Floor(v);
            var x1 = Math.Min(d.UvRect.Right - 1, x0 + 1);
            var y1 = Math.Min(d.UvRect.Bottom - 1, y0 + 1);

            var tx = u - x0;
            var ty = v - y0;

            unsafe
            {
                var ptr = (uint*)d.Pixels;
                var s00 = ptr[y0 * d.RowPixels + x0];
                var s10 = ptr[y0 * d.RowPixels + x1];
                var s01 = ptr[y1 * d.RowPixels + x0];
                var s11 = ptr[y1 * d.RowPixels + x1];

                static void Unpack(uint c, out float r, out float g, out float b, out float a)
                {
                    r = (c & 0xFF) / 255f;
                    g = ((c >> 8) & 0xFF) / 255f;
                    b = ((c >> 16) & 0xFF) / 255f;
                    a = ((c >> 24) & 0xFF) / 255f;
                }

                Unpack(s00, out var r00, out var g00, out var b00, out var a00);
                Unpack(s10, out var r10, out var g10, out var b10, out var a10);
                Unpack(s01, out var r01, out var g01, out var b01, out var a01);
                Unpack(s11, out var r11, out var g11, out var b11, out var a11);

                var r0 = r00 + (r10 - r00) * tx;
                var g0 = g00 + (g10 - g00) * tx;
                var b0 = b00 + (b10 - b00) * tx;
                var a0 = a00 + (a10 - a00) * tx;

                var r1 = r01 + (r11 - r01) * tx;
                var g1 = g01 + (g11 - g01) * tx;
                var b1 = b01 + (b11 - b01) * tx;
                var a1 = a01 + (a11 - a01) * tx;

                var sr = r0 + (r1 - r0) * ty;
                var sg = g0 + (g1 - g0) * ty;
                var sb = b0 + (b1 - b0) * ty;
                var sa = a0 + (a1 - a0) * ty;

                // Apply tint
                sr *= d.Tint.Red / 255f;
                sg *= d.Tint.Green / 255f;
                sb *= d.Tint.Blue / 255f;

                var alpha = sa * d.Opacity;
                if (alpha <= 0f)
                {
                    continue;
                }

                var invA = 1f - alpha;
                br = (byte)(br * invA + sr * 255f * alpha);
                bg = (byte)(bg * invA + sg * 255f * alpha);
                bb = (byte)(bb * invA + sb * 255f * alpha);
                ba = (byte)(ba * invA + 255 * alpha);
            }
        }

        return new SKColor(br, bg, bb, ba);
    }

    private static SKColor AverageColor(SKColor c0, SKColor c1, SKColor c2)
    {
        return new SKColor(
            (byte)((c0.Red + c1.Red + c2.Red) / 3),
            (byte)((c0.Green + c1.Green + c2.Green) / 3),
            (byte)((c0.Blue + c1.Blue + c2.Blue) / 3),
            (byte)((c0.Alpha + c1.Alpha + c2.Alpha) / 3));
    }

    private static bool IsBackFacing(ClipVertex v0, ClipVertex v1, ClipVertex v2)
    {
        var a = v1.ViewPosition - v0.ViewPosition;
        var b = v2.ViewPosition - v0.ViewPosition;
        var normal = Vector3.Normalize(Vector3.Cross(a, b));
        var viewDir = -Vector3.Normalize(v0.ViewPosition);
        return Vector3.Dot(normal, viewDir) <= 0f;
    }

    private static SKPoint ProjectToScreen(in Vector4 ndc, SKRect viewport)
    {
        var x = (ndc.X * 0.5f + 0.5f) * viewport.Width + viewport.Left;
        var y = (-ndc.Y * 0.5f + 0.5f) * viewport.Height + viewport.Top;
        return new SKPoint(x, y);
    }

    private static ClipVertex? Transform(Vertex vertex, Matrix4x4 worldView, Matrix4x4 worldViewProjection)
    {
        var viewPosition = Vector3.Transform(vertex.Position, worldView);
        var clip = Vector4.Transform(new Vector4(vertex.Position, 1f), worldViewProjection);
        if (MathF.Abs(clip.W) < 1e-5f)
        {
            return null;
        }

        var viewNormal = Vector3.Normalize(Vector3.TransformNormal(vertex.Normal, worldView));
        return new ClipVertex(clip, viewPosition, viewNormal, vertex.Color);
    }

    private static IEnumerable<Triangle> ClipToFrustum(ClipVertex a, ClipVertex b, ClipVertex c)
    {
        var verts = new List<ClipVertex> { a, b, c };
        verts = ClipAgainstPlane(verts, v => v.Clip.Z >= -v.Clip.W); // near (standard clip space)
        verts = ClipAgainstPlane(verts, v => v.Clip.Z <= v.Clip.W);  // far

        for (int i = 1; i + 1 < verts.Count; i++)
        {
            yield return new Triangle(verts[0], verts[i], verts[i + 1], default, default, default, default!);
        }
    }

    private static List<ClipVertex> ClipAgainstPlane(List<ClipVertex> input, Func<ClipVertex, bool> inside)
    {
        var output = new List<ClipVertex>();
        if (input.Count == 0)
        {
            return output;
        }

        for (int i = 0; i < input.Count; i++)
        {
            var current = input[i];
            var prev = input[(i + input.Count - 1) % input.Count];
            bool currIn = inside(current);
            bool prevIn = inside(prev);

            if (currIn && prevIn)
            {
                output.Add(current);
            }
            else if (prevIn && !currIn)
            {
                output.Add(Intersect(prev, current, inside));
            }
            else if (!prevIn && currIn)
            {
                output.Add(Intersect(prev, current, inside));
                output.Add(current);
            }
        }

        return output;
    }

    private static ClipVertex Intersect(ClipVertex a, ClipVertex b, Func<ClipVertex, bool> inside)
    {
        float t = 0.5f;
        var va = a.Clip;
        var vb = b.Clip;

        if (inside == null)
        {
            return a;
        }

        // Solve for near/far planes based on lambda sign
        if (!inside(a) && inside(b))
        {
            t = FindT(a, b, inside);
        }
        else if (inside(a) && !inside(b))
        {
            t = FindT(a, b, inside);
        }
        else
        {
            t = FindT(a, b, inside);
        }

        return Lerp(a, b, t);
    }

    private static float FindT(ClipVertex a, ClipVertex b, Func<ClipVertex, bool> inside)
    {
        // Binary search for intersection
        float t0 = 0f, t1 = 1f;
        for (int i = 0; i < 8; i++)
        {
            float mid = (t0 + t1) * 0.5f;
            var m = Lerp(a, b, mid);
            if (inside(m))
            {
                t1 = mid;
            }
            else
            {
                t0 = mid;
            }
        }
        return (t0 + t1) * 0.5f;
    }

    private static ClipVertex Lerp(ClipVertex a, ClipVertex b, float t)
    {
        var clip = Vector4.Lerp(a.Clip, b.Clip, t);
        var viewPos = Vector3.Lerp(a.ViewPosition, b.ViewPosition, t);
        var viewNormal = Vector3.Normalize(Vector3.Lerp(a.ViewNormal, b.ViewNormal, t));
        var color = LerpColor(a.Color, b.Color, t);
        return new ClipVertex(clip, viewPos, viewNormal, color);
    }

    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }

    private static bool IsCulled(Mesh mesh, Matrix4x4 world, Vector4[] planes)
    {
        // Bounding sphere test
        var center = Vector3.Transform(Vector3.Zero, world);
        var maxScale = ExtractMaxScale(world);
        var radius = mesh.BoundingRadius * maxScale;

        foreach (var p in planes)
        {
            var dist = p.X * center.X + p.Y * center.Y + p.Z * center.Z + p.W;
            if (dist < -radius)
            {
                return true;
            }
        }

        return false;
    }

    private static float ExtractMaxScale(Matrix4x4 m)
    {
        var sx = new Vector3(m.M11, m.M12, m.M13).Length();
        var sy = new Vector3(m.M21, m.M22, m.M23).Length();
        var sz = new Vector3(m.M31, m.M32, m.M33).Length();
        return MathF.Max(sx, MathF.Max(sy, sz));
    }

    private static Vector4[] ExtractFrustumPlanes(Matrix4x4 m)
    {
        var planes = new Vector4[6];
        planes[0] = new Vector4(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41); // Left
        planes[1] = new Vector4(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41); // Right
        planes[2] = new Vector4(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42); // Bottom
        planes[3] = new Vector4(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42); // Top
        planes[4] = new Vector4(m.M13, m.M23, m.M33, m.M43);                                 // Near
        planes[5] = new Vector4(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43); // Far

        for (int i = 0; i < planes.Length; i++)
        {
            var n = new Vector3(planes[i].X, planes[i].Y, planes[i].Z);
            var len = n.Length();
            if (len > 1e-5f)
            {
                planes[i] /= len;
            }
        }

        return planes;
    }

    private void EnsureBuffers(int width, int height)
    {
        if (_depthBitmap != null && _bufferWidth == width && _bufferHeight == height && _depthBuffer != null && _depthBuffer.Length == width * height)
        {
            return;
        }

        _depthBitmap?.Dispose();
        _depthBitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        _depthBuffer = new float[width * height];
        _bufferWidth = width;
        _bufferHeight = height;
    }
}
