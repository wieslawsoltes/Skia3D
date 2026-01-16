using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Skia3D.Core;
using SkiaSharp;

namespace Skia3D.Sample;

public partial class MainWindow : Window
{
    private readonly Renderer3D _renderer = new();
    private readonly Camera _camera = new();
    private readonly OrbitCameraController _orbit;
    private readonly List<MeshInstance> _scene = new();
    private readonly List<MeshInstance> _userMeshes = new();
    private readonly DispatcherTimer _timer;
    private const int DefaultMeshSegments = 24;
    private MeshInstance? _ground;
    private SKBitmap? _groundLabelAtlas;
    private SkiaView? _surface;
    private CheckBox? _depthToggle;
    private CheckBox? _lightingToggle;
    private CheckBox? _wireframeToggle;
    private CheckBox? _pauseToggle;
    private Slider? _meshPrecisionSlider;
    private TextBlock? _meshPrecisionLabel;
    private Slider? _subdivisionSlider;
    private TextBlock? _subdivisionLabel;
    private bool _isDragging;
    private bool _isPanning;
    private Point _lastPointer;
    private float _angle;

    public MainWindow()
    {
        InitializeComponent();

        _orbit = new OrbitCameraController(_camera)
        {
            Radius = 8f,
            Target = Vector3.Zero,
            Yaw = -0.7f,
            Pitch = -0.5f
        };

        BuildScene(DefaultMeshSegments);
        SetupGroundDecal();
        SetupProjectedPathSample();

        AttachSurface();
        AttachControls();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += (_, _) => Animate();
        _timer.Start();
    }

    private void AttachSurface()
    {
        _surface = this.FindControl<SkiaView>("Surface");
        if (_surface != null)
        {
            _surface.RenderFrame += OnPaintSurface;
            _surface.PointerPressed += OnPointerPressed;
            _surface.PointerReleased += OnPointerReleased;
            _surface.PointerMoved += OnPointerMoved;
            _surface.PointerWheelChanged += OnPointerWheelChanged;
        }
    }

    private void AttachControls()
    {
        _depthToggle = this.FindControl<CheckBox>("DepthToggle");
        _lightingToggle = this.FindControl<CheckBox>("LightingToggle");
        _wireframeToggle = this.FindControl<CheckBox>("WireframeToggle");
        _pauseToggle = this.FindControl<CheckBox>("PauseToggle");
        _meshPrecisionSlider = this.FindControl<Slider>("MeshPrecisionSlider");
        _meshPrecisionLabel = this.FindControl<TextBlock>("MeshPrecisionLabel");
        _subdivisionSlider = this.FindControl<Slider>("SubdivisionSlider");
        _subdivisionLabel = this.FindControl<TextBlock>("SubdivisionLabel");

        ApplyOptions();
    }

    private void BuildScene(int meshSegments)
    {
        _scene.Clear();
        var groundColor = new SKColor(115, 125, 135);
        _ground = new MeshInstance(MeshFactory.CreateGrid(meshSegments, 12f, groundColor, twoSided: true))
        {
            Transform = Matrix4x4.CreateTranslation(new Vector3(0f, -1.2f, 0f)),
            Material = new Material { BaseColor = new SKColor(145, 155, 165), Ambient = 0.35f, Diffuse = 0.55f, DoubleSided = true }
        };

        var cube = new MeshInstance(MeshFactory.CreateCube(2.4f, new SKColor(46, 153, 255)))
        {
            Material = new Material { BaseColor = new SKColor(46, 153, 255), Diffuse = 1f, Ambient = 0.2f }
        };

        var pyramid = new MeshInstance(MeshFactory.CreatePyramid(2f, 2.4f, new SKColor(255, 99, 71)))
        {
            Transform = Matrix4x4.CreateTranslation(new Vector3(2.6f, 0f, -2.2f)),
            Material = new Material { BaseColor = new SKColor(255, 140, 120), Diffuse = 1f, Ambient = 0.2f }
        };

        var sphereSlices = Math.Max(8, meshSegments);
        var sphereStacks = Math.Max(6, meshSegments / 2);
        var sphere = new MeshInstance(MeshFactory.CreateSphere(1.3f, sphereSlices, sphereStacks, new SKColor(80, 220, 180)))
        {
            Transform = Matrix4x4.CreateTranslation(new Vector3(-2.8f, 0.2f, 1.8f)),
            Material = new Material { BaseColor = new SKColor(80, 220, 180), Ambient = 0.2f, Diffuse = 0.9f }
        };

        var cylinderSegments = Math.Max(8, meshSegments);
        var cylinder = new MeshInstance(MeshFactory.CreateCylinder(0.9f, 2.5f, cylinderSegments, new SKColor(200, 200, 120)))
        {
            Transform = Matrix4x4.CreateTranslation(new Vector3(0f, 0f, -3f)),
            Material = new Material { BaseColor = new SKColor(220, 220, 140), Ambient = 0.2f, Diffuse = 0.8f }
        };

        _scene.Add(_ground);
        _scene.Add(cube);
        _scene.Add(pyramid);
        _scene.Add(sphere);
        _scene.Add(cylinder);

        foreach (var user in _userMeshes)
        {
            _scene.Add(user);
        }
    }

    private void SetupGroundDecal()
    {
        if (_ground is null)
        {
            return;
        }

        _groundLabelAtlas?.Dispose();

        var atlas = DecalBuilder.BuildFromDraw(
            width: 256,
            height: 128,
            clear: SKColors.Transparent,
            draw: gc =>
            {
                using var textPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(235, 235, 240),
                    TextSize = 32f,
                    Typeface = SKTypeface.FromFamilyName("Helvetica", SKFontStyle.Bold)
                };

                const string label = "Skia3D Ground";
                var layout = DecalBuilder.BuildTextPath(label, textPaint, 16f, 12f, 12f, 12f);
                using var path = layout.Path;

                using var bgPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(0, 0, 0, 140)
                };

                gc.DrawRoundRect(new SKRoundRect(layout.PaddedBounds, 12f, 12f), bgPaint);
                gc.DrawPath(path, textPaint);

                using var deco = new SKPath();
                var underY = layout.PaddedBounds.Bottom + 6f;
                deco.MoveTo(layout.PaddedBounds.Left + 4f, underY);
                deco.CubicTo(layout.PaddedBounds.Left + layout.PaddedBounds.Width * 0.3f, underY + 8f, layout.PaddedBounds.Left + layout.PaddedBounds.Width * 0.7f, underY - 6f, layout.PaddedBounds.Right - 4f, underY + 4f);
                using var decoPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    Color = new SKColor(120, 200, 255, 200),
                    StrokeWidth = 3f
                };
                gc.DrawPath(deco, decoPaint);
            });

        _groundLabelAtlas = atlas.Atlas;

        if (!DecalBuilder.TryCreatePlanarFrame(_ground.Transform, 1.4f, 0.9f, 3.6f, out var frame))
        {
            return;
        }

        _renderer.ClearDecals();
        _renderer.AddDecal(DecalBuilder.CreatePlanarDecal(atlas, frame, SKColors.White, 0.95f));
    }

    private void SetupProjectedPathSample()
    {
        _renderer.ClearProjectedPaths();
        _renderer.ClearOverlays();

        var textPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(245, 245, 250),
            TextSize = 26f,
            Typeface = SKTypeface.FromFamilyName("Helvetica", SKFontStyle.Bold)
        };

        var layout = DecalBuilder.BuildTextPath("Projected vector", textPaint, 0f, 0f, 0f, 0f);

        // Map onto the cube's top face (local +Y), small scale and slight lift.
        var scale = Matrix4x4.CreateScale(0.02f, 0.02f, 0.02f);
        var lift = Matrix4x4.CreateTranslation(new Vector3(0f, 1.25f, 0f));
        var world = scale * lift;
        _renderer.AddProjectedPath(layout.Path, textPaint, world);

        // Also map a second label near a ground corner, slightly above the plane.
        var cornerPaint = textPaint.Clone();
        cornerPaint.Color = new SKColor(255, 210, 120);
        var cornerLayout = DecalBuilder.BuildTextPath("Corner marker", cornerPaint, 0f, 0f, 0f, 0f);

        var cornerScale = Matrix4x4.CreateScale(0.02f, 0.02f, 0.02f);
        var cornerRotate = Matrix4x4.CreateRotationY(0.6f);
        var cornerLift = Matrix4x4.CreateTranslation(new Vector3(-5.0f, -1.0f, -5.0f));
        var cornerWorld = cornerScale * cornerRotate * cornerLift;
        _renderer.AddProjectedPath(cornerLayout.Path, cornerPaint, cornerWorld);
    }

    private void Animate()
    {
        if (_pauseToggle?.IsChecked == true)
        {
            return;
        }

        _angle += 0.015f;
        if (_scene.Count > 1)
        {
            var rotation = Matrix4x4.CreateRotationY(_angle) * Matrix4x4.CreateRotationX(_angle * 0.3f);
            _scene[1].Transform = rotation;
        }

        _surface?.InvalidateVisual();
    }

    private void OnPaintSurface(object? sender, SkiaRenderEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        var viewport = new SKRect(0, 0, info.Width, info.Height);
        _renderer.Render(canvas, viewport, _camera, _scene);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _isPanning = e.GetCurrentPoint(_surface).Properties.IsMiddleButtonPressed;
        _lastPointer = e.GetPosition(_surface);
        e.Pointer.Capture(_surface);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _isPanning = false;
        e.Pointer.Capture(null);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _surface is null)
        {
            return;
        }

        var current = e.GetPosition(_surface);
        var delta = current - _lastPointer;
        _lastPointer = current;

        if (_isPanning)
        {
            PanWithPixels(delta);
        }
        else
        {
            const float sensitivity = 0.01f;
            _orbit.Rotate((float)delta.X * sensitivity, (float)delta.Y * sensitivity);
        }
        _surface.InvalidateVisual();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        const float zoomSpeed = 0.5f;
        var delta = -(float)e.Delta.Y * zoomSpeed;

        if (_surface is null)
        {
            _orbit.Zoom(delta);
            _surface?.InvalidateVisual();
            return;
        }

        var bounds = _surface.Bounds;
        var viewportSize = new Vector2((float)bounds.Width, (float)bounds.Height);
        var cursor = e.GetPosition(_surface);

        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            _orbit.Zoom(delta);
        }
        else
        {
            _camera.AspectRatio = viewportSize.X / viewportSize.Y;
            _orbit.ZoomToScreenPoint(new Vector2((float)cursor.X, (float)cursor.Y), viewportSize, delta);
        }
        _surface?.InvalidateVisual();
    }

    private void PanWithPixels(Avalonia.Vector delta)
    {
        var viewportSize = _surface is null
            ? new Vector2(0f, 0f)
            : new Vector2((float)_surface.Bounds.Width, (float)_surface.Bounds.Height);

        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            const float fallback = 0.01f;
            _orbit.Pan(new Vector2((float)(delta.X * fallback), (float)(delta.Y * fallback)));
            return;
        }

        _camera.AspectRatio = viewportSize.X / viewportSize.Y;
        var dist = _orbit.Radius;
        var vfov = _camera.FieldOfView;
        var worldPerPixelY = (2f * dist * MathF.Tan(vfov * 0.5f)) / viewportSize.Y;
        var worldPerPixelX = worldPerPixelY * (_camera.AspectRatio <= 0f ? 1f : _camera.AspectRatio);

        var pan = new Vector2((float)(delta.X * worldPerPixelX), (float)(delta.Y * worldPerPixelY));
        _orbit.Pan(pan);
    }

    private void OnOptionsChanged(object? sender, RoutedEventArgs e)
    {
        ApplyOptions();
        _surface?.InvalidateVisual();
    }

    private void OnZoomExtents(object? sender, RoutedEventArgs e)
    {
        ZoomToExtents();
        _surface?.InvalidateVisual();
    }

    private void OnClearScene(object? sender, RoutedEventArgs e)
    {
        ClearScene();
        _surface?.InvalidateVisual();
    }

    private void OnMeshPrecisionChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var segments = GetMeshSegments();
        UpdateMeshPrecisionLabel(segments);
        RebuildSampleScene(segments);
    }

    private void ApplyOptions()
    {
        _renderer.UseDepthBuffer = _depthToggle?.IsChecked ?? true;
        _renderer.EnableLighting = _lightingToggle?.IsChecked ?? true;
        _renderer.ShowWireframe = _wireframeToggle?.IsChecked ?? false;

        if (_meshPrecisionSlider != null)
        {
            var segments = GetMeshSegments();
            UpdateMeshPrecisionLabel(segments);
        }

        if (_subdivisionSlider != null)
        {
            var samples = (int)MathF.Round((float)_subdivisionSlider.Value);
            samples = Math.Clamp(samples, 4, 512);
            _renderer.ProjectedPathSamples = samples;
            UpdateSubdivisionLabel(samples);
        }
    }

    private void OnSubdivisionChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        ApplyOptions();
        _surface?.InvalidateVisual();
    }

    private void UpdateSubdivisionLabel(int samples)
    {
        if (_subdivisionLabel != null)
        {
            _subdivisionLabel.Text = $"Path subdivision: {samples}";
        }
    }

    private void ZoomToExtents()
    {
        if (_surface is { Bounds.Width: > 0, Bounds.Height: > 0 })
        {
            _camera.AspectRatio = (float)_surface.Bounds.Width / (float)_surface.Bounds.Height;
        }

        if (_scene.Count == 0)
        {
            _orbit.Target = Vector3.Zero;
            _orbit.Radius = 8f;
            _orbit.UpdateCamera();
            return;
        }

        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;

        foreach (var instance in _scene)
        {
            if (instance is null || !instance.IsVisible)
            {
                continue;
            }

            if (!TryComputeBounds(instance, out var bMin, out var bMax))
            {
                continue;
            }

            min = Vector3.Min(min, bMin);
            max = Vector3.Max(max, bMax);
            any = true;
        }

        if (!any)
        {
            _orbit.Target = Vector3.Zero;
            _orbit.Radius = 8f;
            _orbit.UpdateCamera();
            return;
        }

        var centerBounds = (min + max) * 0.5f;
        var halfSize = (max - min) * 0.5f;
        var extentRadius = halfSize.Length();
        if (extentRadius < 1e-3f)
        {
            extentRadius = 0.5f;
        }

        var halfVFov = MathF.Max(0.01f, _camera.FieldOfView * 0.5f);
        var halfHFov = MathF.Atan(MathF.Tan(halfVFov) * (_camera.AspectRatio <= 0f ? 1f : _camera.AspectRatio));
        var distV = extentRadius / MathF.Tan(halfVFov);
        var distH = extentRadius / MathF.Tan(MathF.Max(0.01f, halfHFov));
        var distance = MathF.Max(distV, distH) * 1.2f;

        _orbit.Target = centerBounds;
        _orbit.Radius = Math.Max(0.5f, distance);
        _orbit.UpdateCamera();
    }

    private void ClearScene()
    {
        _renderer.ClearDecals();
        _renderer.ClearProjectedPaths();
        _renderer.ClearOverlays();

        _groundLabelAtlas?.Dispose();
        _groundLabelAtlas = null;
        _ground = null;

        _scene.Clear();
        _userMeshes.Clear();
        _angle = 0f;

        _orbit.Target = Vector3.Zero;
        _orbit.Radius = 8f;
        _orbit.Yaw = -0.7f;
        _orbit.Pitch = -0.5f;
        _orbit.UpdateCamera();
    }

    private async void OnLoadObj(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load OBJ",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("OBJ") { Patterns = new List<string> { "*.obj" } },
                new FilePickerFileType("All files") { Patterns = new List<string> { "*" } }
            }
        });

        if (files is null || files.Count == 0)
        {
            return;
        }

        var file = files[0];
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();
            var mesh = MeshFactory.LoadObj(text, new SKColor(200, 200, 200));
            var instance = new MeshInstance(mesh)
            {
                Material = new Material { BaseColor = new SKColor(200, 200, 200), Diffuse = 0.9f, Ambient = 0.15f }
            };
            _scene.Add(instance);
            _userMeshes.Add(instance);
            _surface?.InvalidateVisual();
        }
        catch (Exception ex)
        {
            await new Window { Content = new TextBlock { Text = $"Failed to load OBJ: {ex.Message}" }, Width = 400, Height = 120 }.ShowDialog(this);
        }
    }

    private static (Vector3 center, float radius) ComputeBoundingSphere(MeshInstance instance)
    {
        if (!Matrix4x4.Decompose(instance.Transform, out var scale, out _, out var translation))
        {
            return (new Vector3(instance.Transform.M41, instance.Transform.M42, instance.Transform.M43), instance.Mesh.BoundingRadius);
        }

        var maxScale = Math.Max(scale.X, Math.Max(scale.Y, scale.Z));
        var radius = instance.Mesh.BoundingRadius * (maxScale > 0f ? maxScale : 1f);
        return (translation, radius);
    }

    private static bool TryComputeBounds(MeshInstance instance, out Vector3 min, out Vector3 max)
    {
        var verts = instance.Mesh.Vertices;
        if (verts.Count == 0)
        {
            min = max = default;
            return false;
        }

        min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        foreach (var v in verts)
        {
            var p = Vector3.Transform(v.Position, instance.Transform);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return true;
    }

    private int GetMeshSegments()
    {
        if (_meshPrecisionSlider == null)
        {
            return DefaultMeshSegments;
        }

        var segments = (int)MathF.Round((float)_meshPrecisionSlider.Value);
        return Math.Clamp(segments, 4, 128);
    }

    private void UpdateMeshPrecisionLabel(int segments)
    {
        if (_meshPrecisionLabel != null)
        {
            _meshPrecisionLabel.Text = $"Mesh precision: {segments}";
        }
    }

    private void RebuildSampleScene(int meshSegments)
    {
        BuildScene(meshSegments);
        SetupGroundDecal();
        SetupProjectedPathSample();
        _surface?.InvalidateVisual();
    }
}