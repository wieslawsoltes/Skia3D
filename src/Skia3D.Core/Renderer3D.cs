using System;
using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using System.Buffers;
using Skia3D.Acceleration;
using Skia3D.Geometry;
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
        float Opacity,
        bool IsBgra,
        SKBitmap? OwnedBitmap);

    public readonly record struct PickResult(
        MeshInstance Instance,
        int TriangleIndex,
        Vector3 Position,
        Vector3 Normal,
        float Distance);

    public readonly record struct PickDetail(
        MeshInstance Instance,
        int TriangleIndex,
        int VertexIndex0,
        int VertexIndex1,
        int VertexIndex2,
        Vector3 Barycentric,
        Vector3 Position,
        Vector3 Normal,
        float Distance);

    public readonly record struct RenderStats(
        int Triangles,
        long PixelsWritten,
        int Workers,
        int Width,
        int Height,
        bool DepthBuffer,
        float RenderScale);

    public enum PickAccelerationMode
    {
        Auto,
        Bvh,
        UniformGrid
    }

    private sealed record ShadowMapData(
        float[] Depth,
        int Width,
        int Height,
        Matrix4x4 LightViewProj,
        Vector3 LightDir,
        float Bias,
        float NormalBias,
        int PcfRadius,
        float Strength);

    private readonly record struct LightView(
        Light Light,
        Vector3 DirectionView,
        Vector3 PositionView);

    private readonly record struct ResolvedMaterial(
        MaterialShadingModel ShadingModel,
        SKColor BaseColor,
        Texture2D? BaseColorTexture,
        TextureSampler BaseColorSampler,
        Vector2 UvScale,
        Vector2 UvOffset,
        float BaseColorTextureStrength,
        float Metallic,
        float Roughness,
        Texture2D? MetallicRoughnessTexture,
        TextureSampler MetallicRoughnessSampler,
        float MetallicRoughnessTextureStrength,
        Texture2D? NormalTexture,
        TextureSampler NormalSampler,
        float NormalStrength,
        Texture2D? EmissiveTexture,
        TextureSampler EmissiveSampler,
        SKColor EmissiveColor,
        float EmissiveStrength,
        Texture2D? OcclusionTexture,
        TextureSampler OcclusionSampler,
        float OcclusionStrength,
        float Ambient,
        float Diffuse,
        float Specular,
        float Shininess,
        bool UseVertexColor,
        bool DoubleSided);

    private record struct ClipVertex(Vector4 Clip, Vector3 ViewPosition, Vector3 ViewNormal, SKColor Color, Vector2 UV);

    private record struct Triangle(
        ClipVertex A,
        ClipVertex B,
        ClipVertex C,
        SKPoint P0,
        SKPoint P1,
        SKPoint P2,
        Material Material,
        MaterialOverrides? Overrides);

    private enum PainterItemKind { Triangle, Path, Bitmap }

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
        SKPaint? Paint,
        SKBitmap? Bitmap,
        SKRect SourceRect,
        SKRect DestRect,
        float Opacity)
    {
        public static PainterItem ForTriangle(float bucket, float depthMax, float depthAvg, float depthMin, Triangle tri, SKColor color, bool drawWireframe) =>
            new(bucket, depthMax, depthAvg, depthMin, PainterItemKind.Triangle, tri, color, drawWireframe, null, null, null, default, default, 1f);

        public static PainterItem ForPath(float depth, SKPath path, SKPaint paint) =>
            new(depth, depth, depth, depth, PainterItemKind.Path, default, default, false, path, paint, null, default, default, 1f);

        public static PainterItem ForBitmap(float depth, SKBitmap bitmap, SKRect sourceRect, SKRect destRect, SKColor tint, float opacity) =>
            new(depth, depth, depth, depth, PainterItemKind.Bitmap, default, tint, false, null, null, bitmap, sourceRect, destRect, opacity);
    }

    public SKColor Background { get; set; } = new SKColor(20, 22, 26);

    public bool ShowWireframe { get; set; }

    public bool EnableBackfaceCulling { get; set; } = true;

    public bool UseDepthBuffer { get; set; } = true;

    public bool EnableLighting { get; set; } = true;

    public bool EnableImageBasedLighting { get; set; } = true;

    public Texture2D? EnvironmentTexture { get; set; }

    public TextureSampler EnvironmentSampler { get; set; } = new();

    public SKColor EnvironmentColor { get; set; } = new SKColor(24, 28, 32);

    public float EnvironmentIntensity { get; set; } = 0.35f;

    public List<Light> Lights { get; } = new() { Light.Directional(new Vector3(-0.4f, -1f, -0.6f), new SKColor(255, 255, 255), 1f) };

    public Material DefaultMaterial { get; set; } = Material.Default();

    public bool EnableShadows { get; set; }

    public int ShadowMapSize { get; set; } = 512;

    public float ShadowBias { get; set; } = 0.0025f;

    public float ShadowNormalBias { get; set; } = 0.0015f;

    public float ShadowStrength { get; set; } = 0.7f;

    public int ShadowPcfRadius { get; set; } = 1;

    public bool EnableSsao { get; set; }

    public float SsaoRadius { get; set; } = 6f;

    public float SsaoIntensity { get; set; } = 0.6f;

    public float SsaoDepthBias { get; set; } = 0.002f;

    public int SsaoSampleCount { get; set; } = 8;

    public float DepthRenderScale { get; set; } = 1f;

    public int MaxWorkerCount { get; set; } = 8;

    public bool UseAccelerationStructure { get; set; } = true;

    public int PickBvhLeafSize { get; set; } = 8;

    public PickAccelerationMode PickAcceleration { get; set; } = PickAccelerationMode.Bvh;

    public int UniformGridCellsPerAxis { get; set; } = 16;

    public int UniformGridAutoTriangleThreshold { get; set; } = 20000;

    public bool CollectStats { get; set; }

    public RenderStats LastStats { get; private set; }

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
    private static readonly Vector2[] SsaoKernel =
    {
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, 1f),
        new(0f, -1f),
        new(0.7f, 0.7f),
        new(-0.7f, 0.7f),
        new(0.7f, -0.7f),
        new(-0.7f, -0.7f),
        new(0.5f, 0f),
        new(-0.5f, 0f),
        new(0f, 0.5f),
        new(0f, -0.5f)
    };

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
    private float[]? _shadowDepth;
    private int _shadowWidth;
    private int _shadowHeight;
    private float[]? _ssaoBuffer;
    private int _ssaoWidth;
    private int _ssaoHeight;
    private ShadowMapData? _shadowData;
    private Matrix4x4 _frameInvView = Matrix4x4.Identity;

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

            var atlas = decal.Atlas;
            var pixmap = atlas.PeekPixels();
            if (pixmap is null)
            {
                continue;
            }

            bool isBgra = false;
            SKBitmap? ownedAtlas = null;
            if (pixmap.BytesPerPixel != 4 || (pixmap.ColorType != SKColorType.Rgba8888 && pixmap.ColorType != SKColorType.Bgra8888))
            {
                if (!atlas.CanCopyTo(SKColorType.Rgba8888))
                {
                    continue;
                }

                ownedAtlas = atlas.Copy(SKColorType.Rgba8888);
                if (ownedAtlas == null || ownedAtlas.IsEmpty)
                {
                    ownedAtlas?.Dispose();
                    continue;
                }

                pixmap = ownedAtlas.PeekPixels();
                if (pixmap is null)
                {
                    ownedAtlas.Dispose();
                    continue;
                }
            }
            else
            {
                isBgra = pixmap.ColorType == SKColorType.Bgra8888;
            }

            list.Add(new PreparedDecal(
                pixmap.GetPixels(),
                atlas.Width,
                atlas.Height,
                pixmap.RowBytes >> 2,
                decal.UvRect,
                decal.Tint ?? SKColors.White,
                center,
                Vector3.Normalize(u),
                Vector3.Normalize(v),
                1f / uLen,
                1f / vLen,
                decal.Opacity,
                isBgra,
                ownedAtlas));
        }

        return list;
    }

    private static void DisposePreparedDecals(IReadOnlyList<PreparedDecal> decals)
    {
        for (int i = 0; i < decals.Count; i++)
        {
            decals[i].OwnedBitmap?.Dispose();
        }
    }

    public void Render(SKCanvas canvas, SKRect viewport, Camera camera, IEnumerable<MeshInstance> instances)
    {
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        camera.AspectRatio = viewport.Width / viewport.Height;
        PrepareFrame(camera, instances);

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
        var triCount = RenderPainter(canvas, viewport, camera, instances, projectedList);
        if (CollectStats)
        {
            LastStats = new RenderStats(
                triCount,
                0,
                1,
                (int)MathF.Ceiling(viewport.Width),
                (int)MathF.Ceiling(viewport.Height),
                DepthBuffer: false,
                RenderScale: 1f);
        }
        DrawOverlays(canvas, viewport, camera);
    }

    private void PrepareFrame(Camera camera, IEnumerable<MeshInstance> instances)
    {
        var view = camera.GetViewMatrix();
        if (!Matrix4x4.Invert(view, out _frameInvView))
        {
            _frameInvView = Matrix4x4.Identity;
        }

        _shadowData = EnableShadows ? BuildShadowMap(instances) : null;
    }

    private int RenderPainter(SKCanvas canvas, SKRect viewport, Camera camera, IEnumerable<MeshInstance> instances, IReadOnlyList<(float depth, SKPath path, SKPaint paint)> projectedPaths)
    {
        canvas.Save();
        canvas.ClipRect(viewport);
        using var clearPaint = new SKPaint
        {
            Color = Background,
            BlendMode = SKBlendMode.Src
        };
        canvas.DrawRect(viewport, clearPaint);

        var view = camera.GetViewMatrix();
        var lightInfos = Lights.Select(light => ToView(light, view)).ToList();
        var triangles = RefineForPainter(CollectTriangles(instances, camera, viewport)).ToList();
        var painterItems = new List<PainterItem>(triangles.Count + projectedPaths.Count + Decals.Count);
        const float centroidWeight = 1f / 3f;

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
            var color = Shade(t, centroidWeight, centroidWeight, centroidWeight, lightInfos);

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

        foreach (var decal in PrepareDecalPainterItems(viewport, camera))
        {
            painterItems.Add(decal);
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

            if (item.Kind == PainterItemKind.Path)
            {
                canvas.DrawPath(item.Path!, item.Paint!);
                item.Path!.Dispose();
                continue;
            }

            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true,
                ColorFilter = SKColorFilter.CreateBlendMode(item.Color, SKBlendMode.Modulate),
                Color = new SKColor(255, 255, 255, (byte)(Math.Clamp(item.Opacity, 0f, 1f) * 255f))
            };

            canvas.DrawBitmap(item.Bitmap!, item.SourceRect, item.DestRect, paint);
        }

        canvas.Restore();
        return triangles.Count;
    }

    private List<PainterItem> PrepareDecalPainterItems(SKRect viewport, Camera camera)
    {
        if (Decals.Count == 0)
        {
            return new List<PainterItem>();
        }

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();
        var viewProj = view * projection;
        var items = new List<PainterItem>(Decals.Count);

        bool TryProject(in Vector3 worldPosition, out SKPoint screen)
        {
            var clip = Vector4.Transform(new Vector4(worldPosition, 1f), viewProj);
            if (MathF.Abs(clip.W) < 1e-5f)
            {
                screen = default;
                return false;
            }

            var ndc = clip / clip.W;
            if (ndc.X is < -1f or > 1f || ndc.Y is < -1f or > 1f || ndc.Z is < 0f or > 1f)
            {
                screen = default;
                return false;
            }

            screen = ProjectToScreen(ndc, viewport);
            return true;
        }

        foreach (var decal in Decals)
        {
            if (decal.Atlas == null || decal.Atlas.IsEmpty)
            {
                continue;
            }

            var centerClip = Vector4.Transform(new Vector4(decal.CenterWorld, 1f), viewProj);
            if (MathF.Abs(centerClip.W) < 1e-5f)
            {
                continue;
            }

            var centerNdc = centerClip / centerClip.W;
            if (centerNdc.Z is < 0f or > 1f)
            {
                continue;
            }

            var p0World = decal.CenterWorld - decal.UDirWorld * 0.5f - decal.VDirWorld * 0.5f;
            var p1World = decal.CenterWorld + decal.UDirWorld * 0.5f - decal.VDirWorld * 0.5f;
            var p2World = decal.CenterWorld + decal.UDirWorld * 0.5f + decal.VDirWorld * 0.5f;
            var p3World = decal.CenterWorld - decal.UDirWorld * 0.5f + decal.VDirWorld * 0.5f;

            if (!TryProject(p0World, out var p0))
            {
                continue;
            }
            if (!TryProject(p1World, out var p1))
            {
                continue;
            }
            if (!TryProject(p2World, out var p2))
            {
                continue;
            }
            if (!TryProject(p3World, out var p3))
            {
                continue;
            }

            var minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
            var maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
            var minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
            var maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));
            var dest = new SKRect(minX, minY, maxX, maxY);
            if (dest.Width <= 0f || dest.Height <= 0f)
            {
                continue;
            }

            var source = new SKRect(decal.UvRect.Left, decal.UvRect.Top, decal.UvRect.Right, decal.UvRect.Bottom);
            items.Add(PainterItem.ForBitmap(centerNdc.Z, decal.Atlas, source, dest, decal.Tint ?? SKColors.White, decal.Opacity));
        }

        return items;
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
        if (ndc.X is < -1f or > 1f || ndc.Y is < -1f or > 1f || ndc.Z is < 0f or > 1f)
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

        projected.Sort((a, b) => b.depth.CompareTo(a.depth)); // far-to-near

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
        var localX = screen.X - viewport.Left;
        var localY = screen.Y - viewport.Top;
        var px = (int)MathF.Round(localX * sx);
        var py = (int)MathF.Round(localY * sy);

        if (px < 0 || py < 0 || px >= _bufferWidth || py >= _bufferHeight)
        {
            return false;
        }

        depth = _depthBuffer[py * _bufferWidth + px];
        return true;
    }

    public bool TryGetWorldAtScreen(SKPoint screen, SKRect viewport, Camera camera, out Vector3 worldPosition)
    {
        worldPosition = default;
        if (!TryGetDepthAtScreen(screen, viewport, out var depth))
        {
            return false;
        }
        if (!float.IsFinite(depth) || depth > 1f)
        {
            return false;
        }

        var viewportSize = new Vector2(viewport.Width, viewport.Height);
        var local = new Vector2(screen.X - viewport.Left, screen.Y - viewport.Top);
        return Camera.TryUnproject(camera, local, viewportSize, depth, out worldPosition);
    }

    public bool TryGetScreenBounds(MeshInstance instance, SKRect viewport, Camera camera, out SKRect bounds)
    {
        bounds = default;
        if (!instance.TryGetWorldBounds(out var min, out var max))
        {
            return false;
        }

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();
        var viewProj = view * projection;

        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(min.X, min.Y, min.Z);
        corners[1] = new Vector3(max.X, min.Y, min.Z);
        corners[2] = new Vector3(min.X, max.Y, min.Z);
        corners[3] = new Vector3(max.X, max.Y, min.Z);
        corners[4] = new Vector3(min.X, min.Y, max.Z);
        corners[5] = new Vector3(max.X, min.Y, max.Z);
        corners[6] = new Vector3(min.X, max.Y, max.Z);
        corners[7] = new Vector3(max.X, max.Y, max.Z);

        bool hasAny = false;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            var clip = Vector4.Transform(new Vector4(corners[i], 1f), viewProj);
            if (MathF.Abs(clip.W) < 1e-5f)
            {
                continue;
            }

            var ndc = clip / clip.W;
            if (ndc.Z is < 0f or > 1f)
            {
                continue;
            }

            var screen = ProjectToScreen(ndc, viewport);
            minX = MathF.Min(minX, screen.X);
            minY = MathF.Min(minY, screen.Y);
            maxX = MathF.Max(maxX, screen.X);
            maxY = MathF.Max(maxY, screen.Y);
            hasAny = true;
        }

        if (!hasAny)
        {
            return false;
        }

        bounds = new SKRect(minX, minY, maxX, maxY);
        return true;
    }

    public bool TryPick(SKPoint screen, SKRect viewport, Camera camera, IEnumerable<MeshInstance> instances, out PickResult result)
    {
        result = default;
        if (!TryPickDetailed(screen, viewport, camera, instances, out var detail))
        {
            return false;
        }

        result = new PickResult(detail.Instance, detail.TriangleIndex, detail.Position, detail.Normal, detail.Distance);
        return true;
    }

    public bool TryPickDetailed(SKPoint screen, SKRect viewport, Camera camera, IEnumerable<MeshInstance> instances, out PickDetail result)
    {
        result = default;
        var viewportSize = new Vector2(viewport.Width, viewport.Height);
        var local = new Vector2(screen.X - viewport.Left, screen.Y - viewport.Top);
        if (!Camera.TryBuildRay(camera, local, viewportSize, out var origin, out var direction))
        {
            return false;
        }

        float maxDistance = float.PositiveInfinity;
        if (UseDepthBuffer && TryGetDepthAtScreen(screen, viewport, out var depth) && float.IsFinite(depth) && depth <= 1f &&
            Camera.TryUnproject(camera, local, viewportSize, depth, out var depthPoint))
        {
            maxDistance = Vector3.Dot(depthPoint - origin, direction);
        }

        return TryRaycastDetailed(origin, direction, instances, maxDistance, out result);
    }

    public bool TryRaycast(Vector3 origin, Vector3 direction, IEnumerable<MeshInstance> instances, float maxDistance, out PickResult result)
    {
        result = default;
        if (!TryRaycastDetailed(origin, direction, instances, maxDistance, out var detail))
        {
            return false;
        }

        result = new PickResult(detail.Instance, detail.TriangleIndex, detail.Position, detail.Normal, detail.Distance);
        return true;
    }

    public bool TryRaycast(Vector3 origin, Vector3 direction, IEnumerable<MeshInstance> instances, out PickResult result)
    {
        return TryRaycast(origin, direction, instances, float.PositiveInfinity, out result);
    }

    public bool TryRaycastDetailed(Vector3 origin, Vector3 direction, IEnumerable<MeshInstance> instances, float maxDistance, out PickDetail result)
    {
        result = default;
        if (direction.LengthSquared() < 1e-10f)
        {
            return false;
        }

        var dir = Vector3.Normalize(direction);
        float bestT = maxDistance;
        bool hit = false;
        var bestResult = default(PickDetail);

        var snapshot = SnapshotInstances(instances);
        for (int instanceIndex = 0; instanceIndex < snapshot.Length; instanceIndex++)
        {
            var instance = snapshot[instanceIndex];
            if (!instance.IsVisible)
            {
                continue;
            }

            var mesh = instance.Mesh;
            if (mesh.Vertices.Count == 0 || mesh.Indices.Count == 0)
            {
                continue;
            }

            var center = Vector3.Transform(Vector3.Zero, instance.Transform);
            var maxScale = ExtractMaxScale(instance.Transform);
            var radius = mesh.BoundingRadius * maxScale;
            if (radius > 0f && !TryIntersectSphere(origin, dir, center, radius, bestT))
            {
                continue;
            }

            var material = instance.Material ?? DefaultMaterial;
            var overrides = instance.MaterialOverrides;
            var doubleSided = ResolveDoubleSided(material, overrides);
            if (UseAccelerationStructure)
            {
                var acceleration = ResolvePickAcceleration(mesh);
                bool gridUsed = false;
                if (acceleration == PickAccelerationMode.UniformGrid)
                {
                    var cells = ResolveGridCellsPerAxis(mesh);
                    if (TryRaycastGrid(origin, dir, instance, doubleSided, cells, ref bestT, ref bestResult, out gridUsed))
                    {
                        hit = true;
                        continue;
                    }

                    if (gridUsed)
                    {
                        continue;
                    }
                }

                bool bvhUsed = false;
                if ((acceleration == PickAccelerationMode.Bvh || !gridUsed) &&
                    TryRaycastBvh(origin, dir, instance, doubleSided, ref bestT, ref bestResult, out bvhUsed))
                {
                    hit = true;
                    continue;
                }

                if (bvhUsed)
                {
                    continue;
                }
            }

            var indices = mesh.Indices;
            var vertices = mesh.Vertices;
            for (int i = 0, triIndex = 0; i + 2 < indices.Count; i += 3, triIndex++)
            {
                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];
                var v0 = Vector3.Transform(vertices[i0].Position, instance.Transform);
                var v1 = Vector3.Transform(vertices[i1].Position, instance.Transform);
                var v2 = Vector3.Transform(vertices[i2].Position, instance.Transform);

                if (!TryIntersectTriangle(origin, dir, v0, v1, v2, doubleSided, EnableBackfaceCulling, out var t, out var normal, out var barycentric))
                {
                    continue;
                }

                if (t <= 0f || t >= bestT)
                {
                    continue;
                }

                bestT = t;
                var position = origin + dir * t;
                bestResult = new PickDetail(instance, triIndex, i0, i1, i2, barycentric, position, normal, t);
                hit = true;
            }
        }

        if (!hit)
        {
            return false;
        }

        result = bestResult;
        return true;
    }

    public bool TryRaycastDetailed(Vector3 origin, Vector3 direction, IEnumerable<MeshInstance> instances, out PickDetail result)
    {
        return TryRaycastDetailed(origin, direction, instances, float.PositiveInfinity, out result);
    }

    private static MeshInstance[] SnapshotInstances(IEnumerable<MeshInstance> instances)
    {
        if (instances is MeshInstance[] array)
        {
            return array;
        }

        if (instances is ICollection<MeshInstance> collection)
        {
            var snapshot = new MeshInstance[collection.Count];
            collection.CopyTo(snapshot, 0);
            return snapshot;
        }

        return instances.ToArray();
    }

    private bool TryRaycastBvh(
        Vector3 origin,
        Vector3 direction,
        MeshInstance instance,
        bool doubleSided,
        ref float bestT,
        ref PickDetail bestResult,
        out bool used)
    {
        used = false;
        var mesh = instance.Mesh;
        var bvh = mesh.GetOrBuildBvh(PickBvhLeafSize);
        if (bvh is null || bvh.RootIndex < 0)
        {
            return false;
        }

        if (!Matrix4x4.Invert(instance.Transform, out var inv))
        {
            return false;
        }

        var localDir = Vector3.TransformNormal(direction, inv);
        if (localDir.LengthSquared() < 1e-10f)
        {
            return false;
        }

        used = true;
        localDir = Vector3.Normalize(localDir);
        var localOrigin = Vector3.Transform(origin, inv);
        var ray = new Ray(localOrigin, localDir);

        if (!bvh.TryIntersectRay(ray, float.PositiveInfinity, doubleSided, EnableBackfaceCulling, out var hit))
        {
            return false;
        }

        var worldPosition = Vector3.Transform(hit.Position, instance.Transform);
        var tWorld = Vector3.Dot(worldPosition - origin, direction);
        if (tWorld <= 0f || tWorld >= bestT)
        {
            return false;
        }

        var indices = mesh.Indices;
        var baseIndex = hit.TriangleIndex * 3;
        if ((uint)(baseIndex + 2) >= (uint)indices.Count)
        {
            return false;
        }

        int i0 = indices[baseIndex];
        int i1 = indices[baseIndex + 1];
        int i2 = indices[baseIndex + 2];
        var normalWorld = TransformNormalFromLocal(hit.Normal, inv);

        bestT = tWorld;
        bestResult = new PickDetail(instance, hit.TriangleIndex, i0, i1, i2, hit.Barycentric, worldPosition, normalWorld, tWorld);
        return true;
    }

    private PickAccelerationMode ResolvePickAcceleration(Mesh mesh)
    {
        if (PickAcceleration != PickAccelerationMode.Auto)
        {
            return PickAcceleration;
        }

        var triCount = mesh.Indices.Count / 3;
        if (triCount >= UniformGridAutoTriangleThreshold)
        {
            return PickAccelerationMode.UniformGrid;
        }

        return PickAccelerationMode.Bvh;
    }

    private int ResolveGridCellsPerAxis(Mesh mesh)
    {
        if (UniformGridCellsPerAxis > 0)
        {
            return UniformGridCellsPerAxis;
        }

        var triCount = mesh.Indices.Count / 3;
        if (triCount <= 0)
        {
            return 2;
        }

        var target = MathF.Pow(triCount, 1f / 3f);
        var cells = (int)MathF.Round(target);
        return Math.Clamp(cells, 4, 64);
    }

    private bool TryRaycastGrid(
        Vector3 origin,
        Vector3 direction,
        MeshInstance instance,
        bool doubleSided,
        int cellsPerAxis,
        ref float bestT,
        ref PickDetail bestResult,
        out bool used)
    {
        used = false;
        var mesh = instance.Mesh;
        var grid = mesh.GetOrBuildGrid(cellsPerAxis);
        if (grid is null || grid.CellsX == 0 || grid.CellsY == 0 || grid.CellsZ == 0)
        {
            return false;
        }

        if (!Matrix4x4.Invert(instance.Transform, out var inv))
        {
            return false;
        }

        var localDir = Vector3.TransformNormal(direction, inv);
        if (localDir.LengthSquared() < 1e-10f)
        {
            return false;
        }

        used = true;
        localDir = Vector3.Normalize(localDir);
        var localOrigin = Vector3.Transform(origin, inv);
        var ray = new Ray(localOrigin, localDir);

        if (!grid.TryIntersectRay(ray, float.PositiveInfinity, doubleSided, EnableBackfaceCulling, out var hit))
        {
            return false;
        }

        var worldPosition = Vector3.Transform(hit.Position, instance.Transform);
        var tWorld = Vector3.Dot(worldPosition - origin, direction);
        if (tWorld <= 0f || tWorld >= bestT)
        {
            return false;
        }

        var indices = mesh.Indices;
        var baseIndex = hit.TriangleIndex * 3;
        if ((uint)(baseIndex + 2) >= (uint)indices.Count)
        {
            return false;
        }

        int i0 = indices[baseIndex];
        int i1 = indices[baseIndex + 1];
        int i2 = indices[baseIndex + 2];
        var normalWorld = TransformNormalFromLocal(hit.Normal, inv);

        bestT = tWorld;
        bestResult = new PickDetail(instance, hit.TriangleIndex, i0, i1, i2, hit.Barycentric, worldPosition, normalWorld, tWorld);
        return true;
    }

    private static Vector3 TransformNormalFromLocal(Vector3 normal, Matrix4x4 invWorld)
    {
        var n = Vector3.TransformNormal(normal, Matrix4x4.Transpose(invWorld));
        if (n.LengthSquared() < 1e-8f)
        {
            return normal;
        }

        return Vector3.Normalize(n);
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
        if (_depthBitmap is null || _depthBuffer is null)
        {
            return;
        }

        var depthBitmap = _depthBitmap;
        var depthBuffer = _depthBuffer;

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
                t.Material,
                t.Overrides));
        }

        var preparedDecals = PrepareDecals(camera);
        long pixelsWritten = 0;
        int workersUsed = 1;
        try
        {
            pixelsWritten = RasterizeParallel(scaled, depthBitmap, depthBuffer, width, height, camera, preparedDecals, CollectStats, out workersUsed);
        }
        finally
        {
            DisposePreparedDecals(preparedDecals);
        }

        if (EnableSsao)
        {
            ApplySsao(depthBitmap, depthBuffer, width, height);
        }

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low, IsAntialias = true };
        canvas.DrawBitmap(depthBitmap, new SKRect(0, 0, width, height), viewport, paint);

        if (CollectStats)
        {
            LastStats = new RenderStats(
                scaled.Count,
                pixelsWritten,
                workersUsed,
                width,
                height,
                DepthBuffer: true,
                RenderScale: scale);
        }

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

    private ShadowMapData? BuildShadowMap(IEnumerable<MeshInstance> instances)
    {
        if (!EnableShadows)
        {
            return null;
        }

        if (!TryGetDirectionalLight(Lights, out var light) || light is null)
        {
            return null;
        }

        var snapshot = SnapshotInstances(instances);
        if (!TryGetSceneBounds(snapshot, out var min, out var max))
        {
            return null;
        }

        var lightDir = light.Direction;
        if (lightDir.LengthSquared() < 1e-8f)
        {
            return null;
        }

        lightDir = Vector3.Normalize(lightDir);
        var center = (min + max) * 0.5f;
        var extents = (max - min) * 0.5f;
        var radius = MathF.Max(extents.X, MathF.Max(extents.Y, extents.Z));
        var distance = radius * 3f + 1f;
        var lightPos = center - lightDir * distance;
        var up = MathF.Abs(Vector3.Dot(lightDir, Vector3.UnitY)) > 0.9f ? Vector3.UnitZ : Vector3.UnitY;
        var lightView = Matrix4x4.CreateLookAt(lightPos, center, up);

        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(min.X, min.Y, min.Z);
        corners[1] = new Vector3(max.X, min.Y, min.Z);
        corners[2] = new Vector3(min.X, max.Y, min.Z);
        corners[3] = new Vector3(max.X, max.Y, min.Z);
        corners[4] = new Vector3(min.X, min.Y, max.Z);
        corners[5] = new Vector3(max.X, min.Y, max.Z);
        corners[6] = new Vector3(min.X, max.Y, max.Z);
        corners[7] = new Vector3(max.X, max.Y, max.Z);

        var minLs = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maxLs = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            var ls = Vector3.Transform(corners[i], lightView);
            minLs = Vector3.Min(minLs, ls);
            maxLs = Vector3.Max(maxLs, ls);
        }

        var margin = MathF.Max(0.5f, radius * 0.15f);
        minLs -= new Vector3(margin);
        maxLs += new Vector3(margin);

        var near = MathF.Max(0.01f, -maxLs.Z - margin);
        var far = MathF.Max(near + 0.1f, -minLs.Z + margin);
        var lightProj = Matrix4x4.CreateOrthographicOffCenter(minLs.X, maxLs.X, minLs.Y, maxLs.Y, near, far);
        var lightViewProj = lightView * lightProj;

        int size = Math.Clamp(ShadowMapSize, 64, 4096);
        EnsureShadowBuffers(size);
        if (_shadowDepth == null)
        {
            return null;
        }

        Array.Fill(_shadowDepth, float.PositiveInfinity);
        RasterizeShadowDepth(snapshot, lightViewProj, _shadowWidth, _shadowHeight, _shadowDepth);

        return new ShadowMapData(_shadowDepth, _shadowWidth, _shadowHeight, lightViewProj, lightDir, ShadowBias, ShadowNormalBias, ShadowPcfRadius, ShadowStrength);
    }

    private static bool TryGetDirectionalLight(IReadOnlyList<Light> lights, out Light? light)
    {
        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i].Type == LightType.Directional)
            {
                light = lights[i];
                return true;
            }
        }

        light = null;
        return false;
    }

    private static bool TryGetSceneBounds(MeshInstance[] instances, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;

        for (int i = 0; i < instances.Length; i++)
        {
            var instance = instances[i];
            if (!instance.IsVisible)
            {
                continue;
            }

            if (!instance.TryGetWorldBounds(out var localMin, out var localMax))
            {
                continue;
            }

            min = Vector3.Min(min, localMin);
            max = Vector3.Max(max, localMax);
            any = true;
        }

        return any;
    }

    private void EnsureShadowBuffers(int size)
    {
        if (size <= 0)
        {
            return;
        }

        if (_shadowDepth == null || _shadowWidth != size || _shadowHeight != size)
        {
            _shadowDepth = new float[size * size];
            _shadowWidth = size;
            _shadowHeight = size;
        }
    }

    private static void RasterizeShadowDepth(MeshInstance[] instances, Matrix4x4 lightViewProj, int width, int height, float[] depth)
    {
        const float clipLimit = 1.4f;

        for (int instanceIndex = 0; instanceIndex < instances.Length; instanceIndex++)
        {
            var instance = instances[instanceIndex];
            if (!instance.IsVisible)
            {
                continue;
            }

            var mesh = instance.Mesh;
            var vertices = mesh.Vertices;
            var indices = mesh.Indices;
            if (vertices.Count == 0 || indices.Count == 0)
            {
                continue;
            }

            var worldLight = instance.Transform * lightViewProj;
            var vertexCount = vertices.Count;
            var transformed = ArrayPool<Vector3>.Shared.Rent(vertexCount);
            var valid = ArrayPool<byte>.Shared.Rent(vertexCount);
            try
            {
                for (int v = 0; v < vertexCount; v++)
                {
                    var clip = Vector4.Transform(new Vector4(vertices[v].Position, 1f), worldLight);
                    if (MathF.Abs(clip.W) < 1e-6f)
                    {
                        valid[v] = 0;
                        continue;
                    }

                    var ndc = clip / clip.W;
                    transformed[v] = new Vector3(ndc.X, ndc.Y, ndc.Z);
                    valid[v] = 1;
                }

                for (int i = 0; i + 2 < indices.Count; i += 3)
                {
                    int i0 = indices[i];
                    int i1 = indices[i + 1];
                    int i2 = indices[i + 2];

                    if (valid[i0] == 0 || valid[i1] == 0 || valid[i2] == 0)
                    {
                        continue;
                    }

                    var n0 = transformed[i0];
                    var n1 = transformed[i1];
                    var n2 = transformed[i2];

                    if (MathF.Abs(n0.X) > clipLimit && MathF.Abs(n1.X) > clipLimit && MathF.Abs(n2.X) > clipLimit &&
                        MathF.Abs(n0.Y) > clipLimit && MathF.Abs(n1.Y) > clipLimit && MathF.Abs(n2.Y) > clipLimit)
                    {
                        continue;
                    }

                    if (n0.Z < 0f && n1.Z < 0f && n2.Z < 0f)
                    {
                        continue;
                    }

                    if (n0.Z > 1f && n1.Z > 1f && n2.Z > 1f)
                    {
                        continue;
                    }

                    var p0 = ProjectShadowToScreen(n0, width, height);
                    var p1 = ProjectShadowToScreen(n1, width, height);
                    var p2 = ProjectShadowToScreen(n2, width, height);
                    RasterizeShadowTriangle(p0, p1, p2, n0.Z, n1.Z, n2.Z, depth, width, height);
                }
            }
            finally
            {
                ArrayPool<Vector3>.Shared.Return(transformed);
                ArrayPool<byte>.Shared.Return(valid);
            }
        }
    }

    private static SKPoint ProjectShadowToScreen(Vector3 ndc, int width, int height)
    {
        var x = (ndc.X * 0.5f + 0.5f) * (width - 1);
        var y = (1f - (ndc.Y * 0.5f + 0.5f)) * (height - 1);
        return new SKPoint(x, y);
    }

    private static void RasterizeShadowTriangle(SKPoint p0, SKPoint p1, SKPoint p2, float z0, float z1, float z2, float[] depth, int width, int height)
    {
        var area = Edge(p0, p1, p2);
        if (MathF.Abs(area) < 1e-6f)
        {
            return;
        }

        var minX = (int)MathF.Floor(MathF.Min(p0.X, MathF.Min(p1.X, p2.X)));
        var maxX = (int)MathF.Ceiling(MathF.Max(p0.X, MathF.Max(p1.X, p2.X)));
        var minY = (int)MathF.Floor(MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y)));
        var maxY = (int)MathF.Ceiling(MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y)));

        minX = Math.Clamp(minX, 0, width - 1);
        maxX = Math.Clamp(maxX, 0, width - 1);
        minY = Math.Clamp(minY, 0, height - 1);
        maxY = Math.Clamp(maxY, 0, height - 1);

        if (minX > maxX || minY > maxY)
        {
            return;
        }

        var invArea = 1f / area;
        bool areaPositive = area > 0f;

        var w0StepX = -(p2.Y - p1.Y);
        var w0StepY = (p2.X - p1.X);
        var w1StepX = -(p0.Y - p2.Y);
        var w1StepY = (p0.X - p2.X);
        var w2StepX = -(p1.Y - p0.Y);
        var w2StepY = (p1.X - p0.X);

        var startX = minX + 0.5f;
        var startY = minY + 0.5f;
        var w0Row = Edge(p1, p2, startX, startY);
        var w1Row = Edge(p2, p0, startX, startY);
        var w2Row = Edge(p0, p1, startX, startY);

        for (int y = minY; y <= maxY; y++)
        {
            int rowIndex = y * width;
            var w0 = w0Row;
            var w1 = w1Row;
            var w2 = w2Row;
            for (int x = minX; x <= maxX; x++)
            {
                if (areaPositive)
                {
                    if (w0 < 0 || w1 < 0 || w2 < 0)
                    {
                        w0 += w0StepX;
                        w1 += w1StepX;
                        w2 += w2StepX;
                        continue;
                    }
                }
                else
                {
                    if (w0 > 0 || w1 > 0 || w2 > 0)
                    {
                        w0 += w0StepX;
                        w1 += w1StepX;
                        w2 += w2StepX;
                        continue;
                    }
                }

                var w0n = w0 * invArea;
                var w1n = w1 * invArea;
                var w2n = w2 * invArea;
                var depthValue = w0n * z0 + w1n * z1 + w2n * z2;
                var idx = rowIndex + x;
                if (depthValue < depth[idx])
                {
                    depth[idx] = depthValue;
                }

                w0 += w0StepX;
                w1 += w1StepX;
                w2 += w2StepX;
            }

            w0Row += w0StepY;
            w1Row += w1StepY;
            w2Row += w2StepY;
        }
    }

    private unsafe void ApplySsao(SKBitmap bitmap, float[] depth, int width, int height)
    {
        if (!EnableSsao || depth.Length == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        if (_ssaoBuffer == null || _ssaoWidth != width || _ssaoHeight != height)
        {
            _ssaoBuffer = new float[width * height];
            _ssaoWidth = width;
            _ssaoHeight = height;
        }

        int kernelCount = SsaoKernel.Length;
        int samples = Math.Clamp(SsaoSampleCount, 1, kernelCount);
        var radius = Math.Clamp(SsaoRadius, 1f, 64f);
        var bias = Math.Clamp(SsaoDepthBias, 0f, 0.1f);
        var intensity = Math.Clamp(SsaoIntensity, 0f, 1f);

        for (int y = 0; y < height; y++)
        {
            int rowIndex = y * width;
            for (int x = 0; x < width; x++)
            {
                int idx = rowIndex + x;
                var d = depth[idx];
                if (!float.IsFinite(d) || d > 1f)
                {
                    _ssaoBuffer[idx] = 1f;
                    continue;
                }

                float occluded = 0f;
                int count = 0;
                for (int k = 0; k < samples; k++)
                {
                    var offset = SsaoKernel[k];
                    int sx = (int)MathF.Round(x + offset.X * radius);
                    int sy = (int)MathF.Round(y + offset.Y * radius);
                    if ((uint)sx >= (uint)width || (uint)sy >= (uint)height)
                    {
                        continue;
                    }

                    var sd = depth[sy * width + sx];
                    if (!float.IsFinite(sd) || sd > 1f)
                    {
                        continue;
                    }

                    if (sd < d - bias)
                    {
                        occluded += 1f;
                    }
                    count++;
                }

                float occ = count > 0 ? occluded / count : 0f;
                var ao = 1f - occ * intensity;
                _ssaoBuffer[idx] = Math.Clamp(ao, 0.1f, 1f);
            }
        }

        using var pixmap = bitmap.PeekPixels();
        var ptr = (uint*)pixmap.GetPixels();
        int pixels = width * height;
        for (int i = 0; i < pixels; i++)
        {
            var d = depth[i];
            if (!float.IsFinite(d) || d > 1f)
            {
                continue;
            }

            var ao = _ssaoBuffer[i];
            if (ao >= 0.999f)
            {
                continue;
            }

            var c = ptr[i];
            byte r = (byte)(c & 0xFF);
            byte g = (byte)((c >> 8) & 0xFF);
            byte b = (byte)((c >> 16) & 0xFF);
            byte a = (byte)((c >> 24) & 0xFF);

            r = (byte)Math.Clamp(r * ao, 0f, 255f);
            g = (byte)Math.Clamp(g * ao, 0f, 255f);
            b = (byte)Math.Clamp(b * ao, 0f, 255f);

            ptr[i] = (uint)(r | (g << 8) | (b << 16) | (a << 24));
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
        bool depthTest = UseDepthBuffer && _depthBuffer != null;
        const float depthBias = 0.001f;

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

                bool inSegment = false;
                bool contourHadGap = false;
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
                        if (inSegment)
                        {
                            contourHadGap = true;
                            inSegment = false;
                        }
                        continue;
                    }
                    var ndc = clip / clip.W;
                    if (ndc.X is < -1.2f or > 1.2f || ndc.Y is < -1.2f or > 1.2f || ndc.Z is < 0f or > 1f)
                    {
                        if (inSegment)
                        {
                            contourHadGap = true;
                            inSegment = false;
                        }
                        continue;
                    }

                    var screen = ProjectToScreen(ndc, viewport);
                    if (depthTest && TryGetDepthAtScreen(screen, viewport, out var depth) && ndc.Z > depth + depthBias)
                    {
                        if (inSegment)
                        {
                            contourHadGap = true;
                            inSegment = false;
                        }
                        continue;
                    }

                    if (!inSegment)
                    {
                        screenPath.MoveTo(screen);
                        inSegment = true;
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
                if (measure.IsClosed && inSegment && !contourHadGap)
                {
                    screenPath.Close();
                }

            } while (measure.NextContour());

            if (!hasAny || depthCount == 0)
            {
                screenPath.Dispose();
                continue;
            }

            items.Add(((depthAcc / depthCount) - depthBias, screenPath, proj.Paint));
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
        var o = tri.Overrides;
        return new[]
        {
            new Triangle(tri.A, abV, caV, tri.P0, abP, caP, m, o),
            new Triangle(abV, tri.B, bcV, abP, tri.P1, bcP, m, o),
            new Triangle(caV, bcV, tri.C, caP, bcP, tri.P2, m, o),
            new Triangle(abV, bcV, caV, abP, bcP, caP, m, o)
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

        var snapshot = SnapshotInstances(instances);
        for (int instanceIndex = 0; instanceIndex < snapshot.Length; instanceIndex++)
        {
            var instance = snapshot[instanceIndex];
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
            var overrides = instance.MaterialOverrides;
            var overrideColor = instance.OverrideColor;

            var vertexCount = vertices.Count;
            var transformed = ArrayPool<ClipVertex>.Shared.Rent(vertexCount);
            var valid = ArrayPool<byte>.Shared.Rent(vertexCount);
            try
            {
                for (int v = 0; v < vertexCount; v++)
                {
                    var cv = Transform(vertices[v], worldView, worldViewProjection);
                    if (cv.HasValue)
                    {
                        var value = cv.Value;
                        if (overrideColor.HasValue)
                        {
                            value = value with { Color = overrideColor.Value };
                        }
                        transformed[v] = value;
                        valid[v] = 1;
                    }
                    else
                    {
                        valid[v] = 0;
                    }
                }

                for (var i = 0; i < indices.Count; i += 3)
                {
                    var i0 = indices[i];
                    var i1 = indices[i + 1];
                    var i2 = indices[i + 2];

                    if (valid[i0] == 0 || valid[i1] == 0 || valid[i2] == 0)
                    {
                        continue;
                    }

                    var cv0 = transformed[i0];
                    var cv1 = transformed[i1];
                    var cv2 = transformed[i2];

                    var clipped = ClipToFrustum(cv0, cv1, cv2);
                    foreach (var tri in clipped)
                    {
                        if (EnableBackfaceCulling && !ResolveDoubleSided(material, overrides) && IsBackFacing(tri.A, tri.B, tri.C))
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
                            material,
                            overrides));
                    }
                }
            }
            finally
            {
                ArrayPool<ClipVertex>.Shared.Return(transformed);
                ArrayPool<byte>.Shared.Return(valid);
            }
        }

        return results;
    }

    private unsafe long RasterizeParallel(IReadOnlyList<Triangle> triangles, SKBitmap bitmap, float[] depth, int width, int height, Camera camera, IReadOnlyList<PreparedDecal> decals, bool collectStats, out int workersUsed)
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

        int desiredWorkers = MaxWorkerCount <= 0 ? Environment.ProcessorCount : MaxWorkerCount;
        int workerCount = Math.Clamp(desiredWorkers, 1, Environment.ProcessorCount);
        workersUsed = workerCount;
        if (workerCount <= 1 || triangles.Count < 64)
        {
            return RasterizeBand(triangles, ptr, stride, depth, width, height, 0, height, lightInfos, decals, collectStats);
        }

        int rowsPerWorker = (height + workerCount - 1) / workerCount;
        var bands = new List<Triangle>[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            bands[i] = new List<Triangle>();
        }

        foreach (var tri in triangles)
        {
            var minY = (int)MathF.Floor(MathF.Min(tri.P0.Y, MathF.Min(tri.P1.Y, tri.P2.Y)));
            var maxY = (int)MathF.Ceiling(MathF.Max(tri.P0.Y, MathF.Max(tri.P1.Y, tri.P2.Y)));

            if (maxY < 0 || minY >= height)
            {
                continue;
            }

            minY = Math.Clamp(minY, 0, height - 1);
            maxY = Math.Clamp(maxY, 0, height - 1);

            int bandStart = Math.Clamp(minY / rowsPerWorker, 0, workerCount - 1);
            int bandEnd = Math.Clamp(maxY / rowsPerWorker, 0, workerCount - 1);
            for (int band = bandStart; band <= bandEnd; band++)
            {
                bands[band].Add(tri);
            }
        }

        long[]? counts = collectStats ? new long[workerCount] : null;
        Parallel.For(0, workerCount, workerIndex =>
        {
            int yStart = workerIndex * rowsPerWorker;
            int yEnd = Math.Min(height, yStart + rowsPerWorker);
            if (yStart >= yEnd)
            {
                return;
            }

            var bandTriangles = bands[workerIndex];
            if (bandTriangles.Count == 0)
            {
                return;
            }

            var count = RasterizeBand(bandTriangles, ptr, stride, depth, width, height, yStart, yEnd, lightInfos, decals, collectStats);
            if (counts != null)
            {
                counts[workerIndex] = count;
            }
        });

        if (counts == null)
        {
            return 0;
        }

        long total = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            total += counts[i];
        }

        return total;
    }

    private unsafe long RasterizeBand(IEnumerable<Triangle> triangles, uint* colorPtr, int colorStride, float[] depth, int width, int height, int yStart, int yEnd, List<LightView> lightInfos, IReadOnlyList<PreparedDecal> decals, bool collectStats)
    {
        long pixelsWritten = 0;
        foreach (var tri in triangles)
        {
            var p0 = tri.P0;
            var p1 = tri.P1;
            var p2 = tri.P2;
            var area = Edge(p0, p1, p2);
            if (MathF.Abs(area) < 1e-6f)
            {
                continue;
            }

            var minX = (int)MathF.Floor(MathF.Min(p0.X, MathF.Min(p1.X, p2.X)));
            var maxX = (int)MathF.Ceiling(MathF.Max(p0.X, MathF.Max(p1.X, p2.X)));
            var minY = (int)MathF.Floor(MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y)));
            var maxY = (int)MathF.Ceiling(MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y)));

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

            var w0StepX = -(p2.Y - p1.Y);
            var w0StepY = (p2.X - p1.X);
            var w1StepX = -(p0.Y - p2.Y);
            var w1StepY = (p0.X - p2.X);
            var w2StepX = -(p1.Y - p0.Y);
            var w2StepY = (p1.X - p0.X);

            var startX = minX + 0.5f;
            var startY = minY + 0.5f;
            var w0Row = Edge(p1, p2, startX, startY);
            var w1Row = Edge(p2, p0, startX, startY);
            var w2Row = Edge(p0, p1, startX, startY);

            var z0 = tri.A.Clip.Z;
            var z1 = tri.B.Clip.Z;
            var z2 = tri.C.Clip.Z;
            var v0 = tri.A.ViewPosition;
            var v1 = tri.B.ViewPosition;
            var v2 = tri.C.ViewPosition;
            var n0 = tri.A.ViewNormal;
            var n1 = tri.B.ViewNormal;
            var n2 = tri.C.ViewNormal;
            var c0 = tri.A.Color;
            var c1 = tri.B.Color;
            var c2 = tri.C.Color;
            var uv0 = tri.A.UV;
            var uv1 = tri.B.UV;
            var uv2 = tri.C.UV;
            var baseMaterial = tri.Material ?? DefaultMaterial;
            var resolved = ResolveMaterial(baseMaterial, tri.Overrides);
            bool hasTangentBasis = false;
            Vector3 tangent = default;
            Vector3 bitangent = default;
            if (resolved.NormalTexture != null)
            {
                hasTangentBasis = TryBuildTangentBasis(v0, v1, v2, uv0, uv1, uv2, out tangent, out bitangent);
            }

            for (int y = minY; y <= maxY; y++)
            {
                var rowPtr = colorPtr + y * colorStride;
                var rowDepth = depth.AsSpan(y * width, width);
                var w0 = w0Row;
                var w1 = w1Row;
                var w2 = w2Row;
                for (int x = minX; x <= maxX; x++)
                {
                    if (areaPositive)
                    {
                        if (w0 < 0 || w1 < 0 || w2 < 0)
                        {
                            w0 += w0StepX;
                            w1 += w1StepX;
                            w2 += w2StepX;
                            continue;
                        }
                    }
                    else
                    {
                        if (w0 > 0 || w1 > 0 || w2 > 0)
                        {
                            w0 += w0StepX;
                            w1 += w1StepX;
                            w2 += w2StepX;
                            continue;
                        }
                    }

                    var w0n = w0 * invArea;
                    var w1n = w1 * invArea;
                    var w2n = w2 * invArea;

                    var depthValue = w0n * z0 + w1n * z1 + w2n * z2;
                    ref var depthSlot = ref rowDepth[x];
                    if (depthValue >= depthSlot)
                    {
                        w0 += w0StepX;
                        w1 += w1StepX;
                        w2 += w2StepX;
                        continue;
                    }

                    depthSlot = depthValue;
                    var viewPos = v0 * w0n + v1 * w1n + v2 * w2n;
                    var viewNormal = n0 * w0n + n1 * w1n + n2 * w2n;
                    var vColor = LerpColor(c0, c1, c2, w0n, w1n, w2n);
                    var uv = uv0 * w0n + uv1 * w1n + uv2 * w2n;
                    var color = Shade(resolved, viewPos, viewNormal, vColor, uv, tangent, bitangent, hasTangentBasis, lightInfos);
                    if (decals.Count > 0)
                    {
                        color = ApplyDecals(color, viewPos, decals);
                    }
                    rowPtr[x] = PackColor(color);
                    if (collectStats)
                    {
                        pixelsWritten++;
                    }

                    w0 += w0StepX;
                    w1 += w1StepX;
                    w2 += w2StepX;
                }

                w0Row += w0StepY;
                w1Row += w1StepY;
                w2Row += w2StepY;
            }
        }

        return pixelsWritten;
    }

    private SKColor Shade(Triangle tri, float w0, float w1, float w2, List<LightView> lightsView)
    {
        var vColor = LerpColor(tri.A.Color, tri.B.Color, tri.C.Color, w0, w1, w2);
        var viewPos = tri.A.ViewPosition * w0 + tri.B.ViewPosition * w1 + tri.C.ViewPosition * w2;
        var viewNormal = tri.A.ViewNormal * w0 + tri.B.ViewNormal * w1 + tri.C.ViewNormal * w2;
        var material = tri.Material ?? DefaultMaterial;
        var resolved = ResolveMaterial(material, tri.Overrides);
        var uv = tri.A.UV * w0 + tri.B.UV * w1 + tri.C.UV * w2;
        bool hasTangentBasis = false;
        Vector3 tangent = default;
        Vector3 bitangent = default;
        if (resolved.NormalTexture != null)
        {
            hasTangentBasis = TryBuildTangentBasis(tri.A.ViewPosition, tri.B.ViewPosition, tri.C.ViewPosition, tri.A.UV, tri.B.UV, tri.C.UV, out tangent, out bitangent);
        }

        return Shade(resolved, viewPos, viewNormal, vColor, uv, tangent, bitangent, hasTangentBasis, lightsView);
    }

    private SKColor Shade(
        in ResolvedMaterial material,
        Vector3 viewPos,
        Vector3 viewNormal,
        SKColor vColor,
        Vector2 uv,
        Vector3 tangent,
        Vector3 bitangent,
        bool hasTangentBasis,
        List<LightView> lightsView)
    {
        var uvScaled = uv * material.UvScale + material.UvOffset;
        var baseColor = material.UseVertexColor ? MultiplyColor(material.BaseColor, vColor) : material.BaseColor;
        if (material.BaseColorTexture != null)
        {
            var texColor = material.BaseColorTexture.Sample(uvScaled, material.BaseColorSampler);
            baseColor = MultiplyColor(baseColor, texColor, material.BaseColorTextureStrength);
        }

        var normal = Vector3.Normalize(viewNormal);
        var viewDir = -Vector3.Normalize(viewPos);
        if (material.DoubleSided && Vector3.Dot(normal, viewDir) < 0f)
        {
            normal = -normal;
        }

        var worldPos = Vector3.Transform(viewPos, _frameInvView);
        var worldNormal = Vector3.TransformNormal(normal, _frameInvView);
        if (worldNormal.LengthSquared() > 1e-8f)
        {
            worldNormal = Vector3.Normalize(worldNormal);
        }
        else
        {
            worldNormal = Vector3.UnitY;
        }

        if (material.NormalTexture != null && hasTangentBasis)
        {
            normal = ApplyNormalMap(material, normal, tangent, bitangent, uvScaled);
        }

        var emissive = SampleEmissive(material, uvScaled);
        var environment = SampleEnvironment(worldNormal);

        if (!EnableLighting)
        {
            return AddEmissive(baseColor, emissive);
        }

        return material.ShadingModel == MaterialShadingModel.MetallicRoughness
            ? ShadeMetallicRoughness(material, baseColor, emissive, normal, viewDir, viewPos, uvScaled, lightsView, _shadowData, worldPos, worldNormal, environment)
            : ShadePhong(material, baseColor, emissive, normal, viewDir, viewPos, uvScaled, lightsView, _shadowData, worldPos, worldNormal, environment);
    }

    private static SKColor ShadePhong(
        in ResolvedMaterial material,
        SKColor baseColor,
        Vector3 emissive,
        Vector3 normal,
        Vector3 viewDir,
        Vector3 viewPos,
        Vector2 uv,
        List<LightView> lightsView,
        ShadowMapData? shadowData,
        Vector3 worldPos,
        Vector3 worldNormal,
        Vector3 environment)
    {
        var baseRgb = ColorToVector(baseColor);
        float occlusion = SampleOcclusion(material, uv);
        var diffuseAcc = Vector3.Zero;
        var specAcc = Vector3.Zero;

        foreach (var lightView in lightsView)
        {
            if (!TryComputeLight(lightView, viewPos, out var ldir, out var attenuation))
            {
                continue;
            }

            var light = lightView.Light;
            var lightRgb = ColorToVector(light.Color);

            var ndotl = MathF.Max(0f, Vector3.Dot(normal, -ldir));
            if (ndotl <= 0f)
            {
                continue;
            }

            var shadow = shadowData != null && light.Type == LightType.Directional
                ? SampleShadow(shadowData, worldPos, worldNormal)
                : 1f;
            var diffuse = material.Diffuse * ndotl * light.Intensity * attenuation * shadow;

            var reflect = Vector3.Reflect(ldir, normal);
            var spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(reflect, viewDir)), material.Shininess);
            var specular = material.Specular * spec * light.Intensity * attenuation * shadow;

            diffuseAcc += baseRgb * diffuse * lightRgb;
            specAcc += lightRgb * specular;
        }

        var ambient = baseRgb * material.Ambient * occlusion;
        if (environment != Vector3.Zero)
        {
            ambient += baseRgb * environment * occlusion;
        }
        var rgb = ambient + diffuseAcc + specAcc + emissive;
        return VectorToColor(rgb, baseColor.Alpha);
    }

    private static SKColor ShadeMetallicRoughness(
        in ResolvedMaterial material,
        SKColor baseColor,
        Vector3 emissive,
        Vector3 normal,
        Vector3 viewDir,
        Vector3 viewPos,
        Vector2 uv,
        List<LightView> lightsView,
        ShadowMapData? shadowData,
        Vector3 worldPos,
        Vector3 worldNormal,
        Vector3 environment)
    {
        var baseRgb = ColorToVector(baseColor);
        float occlusion = SampleOcclusion(material, uv);
        GetMetallicRoughness(material, uv, out var metallic, out var roughness);

        metallic = Math.Clamp(metallic, 0f, 1f);
        roughness = Math.Clamp(roughness, 0.04f, 1f);

        var diffuseColor = baseRgb * (1f - metallic);
        var specularColor = Vector3.Lerp(new Vector3(0.04f), baseRgb, metallic);
        float shininess = MathF.Max(2f, (1f - roughness) * (1f - roughness) * 128f);

        var diffuseAcc = Vector3.Zero;
        var specAcc = Vector3.Zero;

        foreach (var lightView in lightsView)
        {
            if (!TryComputeLight(lightView, viewPos, out var ldir, out var attenuation))
            {
                continue;
            }

            var light = lightView.Light;
            var lightRgb = ColorToVector(light.Color);

            var ndotl = MathF.Max(0f, Vector3.Dot(normal, -ldir));
            if (ndotl <= 0f)
            {
                continue;
            }

            var shadow = shadowData != null && light.Type == LightType.Directional
                ? SampleShadow(shadowData, worldPos, worldNormal)
                : 1f;
            var lightTerm = light.Intensity * attenuation * shadow;
            diffuseAcc += diffuseColor * (ndotl * lightTerm) * lightRgb;

            var halfDir = Vector3.Normalize(-ldir + viewDir);
            var ndoth = MathF.Max(0f, Vector3.Dot(normal, halfDir));
            var spec = MathF.Pow(ndoth, shininess);
            specAcc += specularColor * (spec * lightTerm) * lightRgb;
        }

        var ambient = diffuseColor * material.Ambient * occlusion;
        if (environment != Vector3.Zero)
        {
            ambient += diffuseColor * environment * occlusion;
        }
        var rgb = ambient + diffuseAcc + specAcc + emissive;
        return VectorToColor(rgb, baseColor.Alpha);
    }

    private static ResolvedMaterial ResolveMaterial(Material material, MaterialOverrides? overrides)
    {
        return new ResolvedMaterial(
            overrides?.ShadingModel ?? material.ShadingModel,
            overrides?.BaseColor ?? material.BaseColor,
            overrides?.BaseColorTexture ?? material.BaseColorTexture,
            overrides?.BaseColorSampler ?? material.BaseColorSampler,
            overrides?.UvScale ?? material.UvScale,
            overrides?.UvOffset ?? material.UvOffset,
            overrides?.BaseColorTextureStrength ?? material.BaseColorTextureStrength,
            overrides?.Metallic ?? material.Metallic,
            overrides?.Roughness ?? material.Roughness,
            overrides?.MetallicRoughnessTexture ?? material.MetallicRoughnessTexture,
            overrides?.MetallicRoughnessSampler ?? material.MetallicRoughnessSampler,
            overrides?.MetallicRoughnessTextureStrength ?? material.MetallicRoughnessTextureStrength,
            overrides?.NormalTexture ?? material.NormalTexture,
            overrides?.NormalSampler ?? material.NormalSampler,
            overrides?.NormalStrength ?? material.NormalStrength,
            overrides?.EmissiveTexture ?? material.EmissiveTexture,
            overrides?.EmissiveSampler ?? material.EmissiveSampler,
            overrides?.EmissiveColor ?? material.EmissiveColor,
            overrides?.EmissiveStrength ?? material.EmissiveStrength,
            overrides?.OcclusionTexture ?? material.OcclusionTexture,
            overrides?.OcclusionSampler ?? material.OcclusionSampler,
            overrides?.OcclusionStrength ?? material.OcclusionStrength,
            overrides?.Ambient ?? material.Ambient,
            overrides?.Diffuse ?? material.Diffuse,
            overrides?.Specular ?? material.Specular,
            overrides?.Shininess ?? material.Shininess,
            overrides?.UseVertexColor ?? material.UseVertexColor,
            overrides?.DoubleSided ?? material.DoubleSided);
    }

    private static bool ResolveDoubleSided(Material material, MaterialOverrides? overrides)
    {
        return overrides?.DoubleSided ?? material.DoubleSided;
    }

    private Vector3 SampleEnvironment(Vector3 worldNormal)
    {
        if (!EnableImageBasedLighting || EnvironmentIntensity <= 0f)
        {
            return Vector3.Zero;
        }

        var intensity = MathF.Max(0f, EnvironmentIntensity);
        var normal = worldNormal;
        if (normal.LengthSquared() < 1e-6f)
        {
            normal = Vector3.UnitY;
        }
        else
        {
            normal = Vector3.Normalize(normal);
        }

        if (EnvironmentTexture != null)
        {
            float u = 0.5f + MathF.Atan2(normal.Z, normal.X) / (2f * MathF.PI);
            float v = 0.5f - MathF.Asin(Math.Clamp(normal.Y, -1f, 1f)) / MathF.PI;
            var sample = EnvironmentTexture.Sample(new Vector2(u, v), EnvironmentSampler);
            return ColorToVector(sample) * intensity;
        }

        return ColorToVector(EnvironmentColor) * intensity;
    }

    private static bool TryComputeLight(in LightView lightView, Vector3 viewPos, out Vector3 lightDir, out float attenuation)
    {
        var light = lightView.Light;
        attenuation = 1f;
        lightDir = lightView.DirectionView;

        if (light.Type == LightType.Directional)
        {
            return true;
        }

        var toLight = lightView.PositionView - viewPos;
        var dist = toLight.Length();
        if (dist <= 1e-4f)
        {
            return false;
        }

        lightDir = Vector3.Normalize(-toLight);
        attenuation = MathF.Max(0f, 1f - dist / MathF.Max(0.001f, light.Range));
        if (attenuation <= 0f)
        {
            return false;
        }

        if (light.Type == LightType.Spot || light.Type == LightType.Area)
        {
            var forward = lightView.DirectionView;
            if (forward.LengthSquared() > 1e-6f)
            {
                forward = Vector3.Normalize(forward);
            }

            var cosAngle = Vector3.Dot(forward, lightDir);
            if (light.Type == LightType.Spot)
            {
                var inner = MathF.Cos(light.InnerConeAngle);
                var outer = MathF.Cos(light.OuterConeAngle);
                var denom = MathF.Max(1e-4f, inner - outer);
                var spot = Math.Clamp((cosAngle - outer) / denom, 0f, 1f);
                attenuation *= spot * spot;
            }
            else
            {
                var facing = MathF.Max(0f, cosAngle);
                var areaScale = MathF.Max(0.05f, MathF.Sqrt(MathF.Max(0f, light.Size.X * light.Size.Y)));
                attenuation *= facing * areaScale;
            }

            if (attenuation <= 0f)
            {
                return false;
            }
        }

        return true;
    }

    private static float SampleShadow(ShadowMapData shadow, Vector3 worldPos, Vector3 worldNormal)
    {
        var clip = Vector4.Transform(new Vector4(worldPos, 1f), shadow.LightViewProj);
        if (MathF.Abs(clip.W) < 1e-6f)
        {
            return 1f;
        }

        var ndc = clip / clip.W;
        if (ndc.X is < -1f or > 1f || ndc.Y is < -1f or > 1f || ndc.Z is < 0f or > 1f)
        {
            return 1f;
        }

        var sx = (ndc.X * 0.5f + 0.5f) * (shadow.Width - 1);
        var sy = (1f - (ndc.Y * 0.5f + 0.5f)) * (shadow.Height - 1);

        int ix = (int)MathF.Round(sx);
        int iy = (int)MathF.Round(sy);
        if ((uint)ix >= (uint)shadow.Width || (uint)iy >= (uint)shadow.Height)
        {
            return 1f;
        }

        var ndotl = MathF.Max(0f, Vector3.Dot(Vector3.Normalize(worldNormal), -shadow.LightDir));
        var bias = shadow.Bias + shadow.NormalBias * (1f - ndotl);
        var depth = ndc.Z - bias;
        if (depth <= 0f)
        {
            return 1f;
        }

        int radius = Math.Clamp(shadow.PcfRadius, 0, 3);
        if (radius == 0)
        {
            var shadowDepth = shadow.Depth[iy * shadow.Width + ix];
            if (!float.IsFinite(shadowDepth))
            {
                return 1f;
            }

            return depth > shadowDepth ? 1f - shadow.Strength : 1f;
        }

        int samples = 0;
        int occluded = 0;
        for (int y = -radius; y <= radius; y++)
        {
            int py = Math.Clamp(iy + y, 0, shadow.Height - 1);
            int row = py * shadow.Width;
            for (int x = -radius; x <= radius; x++)
            {
                int px = Math.Clamp(ix + x, 0, shadow.Width - 1);
                var shadowDepth = shadow.Depth[row + px];
                if (!float.IsFinite(shadowDepth))
                {
                    samples++;
                    continue;
                }

                if (depth > shadowDepth)
                {
                    occluded++;
                }
                samples++;
            }
        }

        if (samples == 0)
        {
            return 1f;
        }

        var ratio = occluded / (float)samples;
        return 1f - ratio * shadow.Strength;
    }

    private static Vector3 ApplyNormalMap(in ResolvedMaterial material, Vector3 normal, Vector3 tangent, Vector3 bitangent, Vector2 uv)
    {
        if (material.NormalTexture is null)
        {
            return normal;
        }

        var sample = material.NormalTexture.Sample(uv, material.NormalSampler);
        var map = new Vector3(sample.Red / 255f * 2f - 1f, sample.Green / 255f * 2f - 1f, sample.Blue / 255f * 2f - 1f);
        map = Vector3.Normalize(Vector3.Lerp(Vector3.UnitZ, map, Math.Clamp(material.NormalStrength, 0f, 2f)));

        var t = Vector3.Normalize(tangent - normal * Vector3.Dot(normal, tangent));
        var b = Vector3.Normalize(bitangent - normal * Vector3.Dot(normal, bitangent));
        return Vector3.Normalize(t * map.X + b * map.Y + normal * map.Z);
    }

    private static Vector3 SampleEmissive(in ResolvedMaterial material, Vector2 uv)
    {
        var emissive = ColorToVector(material.EmissiveColor);
        if (material.EmissiveTexture != null)
        {
            emissive *= SampleTextureRgb(material.EmissiveTexture, uv, material.EmissiveSampler);
        }

        emissive *= MathF.Max(0f, material.EmissiveStrength);
        return emissive;
    }

    private static float SampleOcclusion(in ResolvedMaterial material, Vector2 uv)
    {
        if (material.OcclusionTexture is null)
        {
            return 1f;
        }

        var sample = material.OcclusionTexture.Sample(uv, material.OcclusionSampler);
        float occlusion = sample.Red / 255f;
        float strength = Math.Clamp(material.OcclusionStrength, 0f, 1f);
        return 1f - strength + occlusion * strength;
    }

    private static void GetMetallicRoughness(in ResolvedMaterial material, Vector2 uv, out float metallic, out float roughness)
    {
        metallic = material.Metallic;
        roughness = material.Roughness;

        if (material.MetallicRoughnessTexture is null)
        {
            return;
        }

        var sample = material.MetallicRoughnessTexture.Sample(uv, material.MetallicRoughnessSampler);
        float strength = Math.Clamp(material.MetallicRoughnessTextureStrength, 0f, 1f);
        float texMetal = sample.Blue / 255f;
        float texRough = sample.Green / 255f;
        metallic = metallic + (texMetal - metallic) * strength;
        roughness = roughness + (texRough - roughness) * strength;
    }

    private static SKColor AddEmissive(SKColor baseColor, Vector3 emissive)
    {
        var rgb = ColorToVector(baseColor) + emissive;
        return VectorToColor(rgb, baseColor.Alpha);
    }

    private static Vector3 SampleTextureRgb(Texture2D texture, Vector2 uv, TextureSampler sampler)
    {
        var sample = texture.Sample(uv, sampler);
        return new Vector3(sample.Red / 255f, sample.Green / 255f, sample.Blue / 255f);
    }

    private static Vector3 ColorToVector(SKColor color)
    {
        return new Vector3(color.Red / 255f, color.Green / 255f, color.Blue / 255f);
    }

    private static SKColor VectorToColor(Vector3 rgb, byte alpha)
    {
        return new SKColor(
            (byte)(Math.Clamp(rgb.X, 0f, 1f) * 255f),
            (byte)(Math.Clamp(rgb.Y, 0f, 1f) * 255f),
            (byte)(Math.Clamp(rgb.Z, 0f, 1f) * 255f),
            alpha);
    }

    private static bool TryBuildTangentBasis(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector2 uv0,
        Vector2 uv1,
        Vector2 uv2,
        out Vector3 tangent,
        out Vector3 bitangent)
    {
        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        var deltaUv1 = uv1 - uv0;
        var deltaUv2 = uv2 - uv0;
        float det = deltaUv1.X * deltaUv2.Y - deltaUv1.Y * deltaUv2.X;
        if (MathF.Abs(det) < 1e-6f)
        {
            tangent = default;
            bitangent = default;
            return false;
        }

        float invDet = 1f / det;
        tangent = (edge1 * deltaUv2.Y - edge2 * deltaUv1.Y) * invDet;
        bitangent = (edge2 * deltaUv1.X - edge1 * deltaUv2.X) * invDet;
        if (tangent.LengthSquared() < 1e-8f || bitangent.LengthSquared() < 1e-8f)
        {
            tangent = default;
            bitangent = default;
            return false;
        }

        tangent = Vector3.Normalize(tangent);
        bitangent = Vector3.Normalize(bitangent);
        return true;
    }

    private static LightView ToView(Light light, Matrix4x4 view)
    {
        return light.Type switch
        {
            LightType.Directional => new LightView(
                light,
                Vector3.Normalize(Vector3.TransformNormal(light.Direction, view)),
                Vector3.Zero),
            LightType.Point => new LightView(
                light,
                Vector3.Zero,
                Vector3.Transform(light.Position, view)),
            LightType.Spot => new LightView(
                light,
                Vector3.Normalize(Vector3.TransformNormal(light.Direction, view)),
                Vector3.Transform(light.Position, view)),
            LightType.Area => new LightView(
                light,
                Vector3.Normalize(Vector3.TransformNormal(light.Direction, view)),
                Vector3.Transform(light.Position, view)),
            _ => new LightView(
                light,
                Vector3.Normalize(Vector3.TransformNormal(light.Direction, view)),
                Vector3.Zero)
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

    private static SKColor MultiplyColor(SKColor a, SKColor b, float strength)
    {
        var mult = MultiplyColor(a, b);
        strength = Math.Clamp(strength, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (mult.Red - a.Red) * strength),
            (byte)(a.Green + (mult.Green - a.Green) * strength),
            (byte)(a.Blue + (mult.Blue - a.Blue) * strength),
            (byte)(a.Alpha + (mult.Alpha - a.Alpha) * strength));
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
            var isBgra = d.IsBgra;
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

                static void Unpack(uint c, bool bgra, out float r, out float g, out float b, out float a)
                {
                    if (bgra)
                    {
                        b = (c & 0xFF) / 255f;
                        g = ((c >> 8) & 0xFF) / 255f;
                        r = ((c >> 16) & 0xFF) / 255f;
                        a = ((c >> 24) & 0xFF) / 255f;
                        return;
                    }

                    r = (c & 0xFF) / 255f;
                    g = ((c >> 8) & 0xFF) / 255f;
                    b = ((c >> 16) & 0xFF) / 255f;
                    a = ((c >> 24) & 0xFF) / 255f;
                }

                Unpack(s00, isBgra, out var r00, out var g00, out var b00, out var a00);
                Unpack(s10, isBgra, out var r10, out var g10, out var b10, out var a10);
                Unpack(s01, isBgra, out var r01, out var g01, out var b01, out var a01);
                Unpack(s11, isBgra, out var r11, out var g11, out var b11, out var a11);

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
        return new ClipVertex(clip, viewPosition, viewNormal, vertex.Color, vertex.UV);
    }

    private static IEnumerable<Triangle> ClipToFrustum(ClipVertex a, ClipVertex b, ClipVertex c)
    {
        var verts = new List<ClipVertex> { a, b, c };
        verts = ClipAgainstPlane(verts, v => v.Clip.X >= -v.Clip.W); // left
        verts = ClipAgainstPlane(verts, v => v.Clip.X <= v.Clip.W);  // right
        verts = ClipAgainstPlane(verts, v => v.Clip.Y >= -v.Clip.W); // bottom
        verts = ClipAgainstPlane(verts, v => v.Clip.Y <= v.Clip.W);  // top
        verts = ClipAgainstPlane(verts, v => v.Clip.Z >= 0f); // near (System.Numerics clip space)
        verts = ClipAgainstPlane(verts, v => v.Clip.Z <= v.Clip.W);  // far

        for (int i = 1; i + 1 < verts.Count; i++)
        {
            yield return new Triangle(verts[0], verts[i], verts[i + 1], default, default, default, default!, null);
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
        var uv = Vector2.Lerp(a.UV, b.UV, t);
        return new ClipVertex(clip, viewPos, viewNormal, color, uv);
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

    private static bool TryIntersectSphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, float maxDistance)
    {
        if (radius <= 0f)
        {
            return false;
        }

        var oc = origin - center;
        var b = Vector3.Dot(oc, direction);
        var c = Vector3.Dot(oc, oc) - radius * radius;
        var discriminant = b * b - c;
        if (discriminant < 0f)
        {
            return false;
        }

        var sqrt = MathF.Sqrt(discriminant);
        var t = -b - sqrt;
        if (t < 0f)
        {
            t = -b + sqrt;
        }

        return t >= 0f && t <= maxDistance;
    }

    private static bool TryIntersectTriangle(
        Vector3 origin,
        Vector3 direction,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        bool doubleSided,
        bool cullBackface,
        out float t,
        out Vector3 normal,
        out Vector3 barycentric)
    {
        const float epsilon = 1e-6f;
        t = 0f;
        normal = default;
        barycentric = default;

        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var pvec = Vector3.Cross(direction, edge2);
        var det = Vector3.Dot(edge1, pvec);

        if (cullBackface && !doubleSided)
        {
            if (det < epsilon)
            {
                return false;
            }
        }
        else
        {
            if (MathF.Abs(det) < epsilon)
            {
                return false;
            }
        }

        var invDet = 1f / det;
        var tvec = origin - v0;
        var u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0f || u > 1f)
        {
            return false;
        }

        var qvec = Vector3.Cross(tvec, edge1);
        var v = Vector3.Dot(direction, qvec) * invDet;
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        t = Vector3.Dot(edge2, qvec) * invDet;
        if (t < 0f)
        {
            return false;
        }

        barycentric = new Vector3(1f - u - v, u, v);
        normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
        if (Vector3.Dot(normal, direction) > 0f)
        {
            normal = -normal;
        }

        return true;
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
