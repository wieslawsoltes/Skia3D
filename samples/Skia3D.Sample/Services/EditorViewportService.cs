using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using Skia3D.Animation;
using Skia3D.Core;
using Skia3D.Editor;
using Skia3D.Input;
using Skia3D.Modeling;
using Skia3D.Runtime;
using Skia3D.Scene;
using Skia3D.Sample;
using Skia3D.Sample.ViewModels;
using SkiaSharp;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Sample.Services;

public sealed class EditorViewportService : IDisposable
{
    private const float ClickThreshold = 6f;
    private const int FpsSampleWindow = 20;
    private static readonly Light DefaultLight = Light.Directional(new Vector3(-0.4f, -1f, -0.6f), new SKColor(255, 255, 255), 1f);

    private readonly Renderer3D _renderer;
    private readonly Camera _camera;
    private readonly OrbitCameraController _orbit;
    private readonly EditorSession _editor;
    private readonly EditorViewModel _editorViewModel;
    private readonly StatusBarViewModel _statusBar;
    private readonly DispatcherTimer _timer;
    private readonly AvaloniaInputProvider _inputProvider;
    private readonly ViewportAnimationSystem _animationSystem;

    private SkiaView? _surface;
    private IReadOnlyList<MeshInstance> _renderInstances = Array.Empty<MeshInstance>();
    private SceneNode? _selectedNode;
    private bool _isDragging;
    private bool _isPanning;
    private bool _isGizmoDragging;
    private bool _wasLeftButtonDown;
    private Vector2 _lastPointer;
    private Vector2 _pointerDown;
    private bool _isOrbiting;
    private bool _isZooming;
    private ViewportNavigationMode _navigationMode;
    private int _viewIndex;
    private float _angle;
    private bool _statusUpdatePending;
    private long _lastFrameTimestamp;
    private double _fpsAccum;
    private int _fpsSampleCount;
    private double _fpsAverage;
    private bool _pause;
    private bool _showGrid = true;
    private bool _showPickDebug;
    private bool _showStats;
    private bool _showUvSeams;
    private bool _showUvIslands;
    private bool _uvIslandCacheValid;
    private MeshInstance? _uvIslandInstance;
    private EditableMesh? _uvIslandEditable;
    private readonly List<List<EdgeKey>> _uvIslandEdges = new();
    private AnimationPlayer? _animationPlayer;
    private AnimationMixer? _animationMixer;
    private SceneNode? _spinNode;
    private EngineHost _engineHost = null!;

    private readonly SKPaint _selectionPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        Color = new SKColor(255, 215, 0, 220)
    };

    private readonly SKPaint _vertexPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 140, 60, 220)
    };

    private readonly SKPaint _edgePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        Color = new SKColor(255, 200, 80, 220)
    };

    private readonly SKPaint _uvSeamPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.8f,
        Color = new SKColor(120, 220, 200, 210)
    };

    private readonly SKPaint _uvIslandPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.6f,
        Color = new SKColor(90, 160, 255, 200)
    };

    private static readonly SKColor[] UvIslandPalette =
    {
        new SKColor(90, 160, 255, 200),
        new SKColor(255, 180, 80, 200),
        new SKColor(120, 220, 140, 200),
        new SKColor(220, 120, 210, 200),
        new SKColor(180, 200, 255, 200),
        new SKColor(255, 120, 120, 200)
    };

    private readonly SKPaint _facePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        Color = new SKColor(120, 210, 240, 200)
    };

    private readonly SKPaint _gizmoXPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(240, 90, 80) };
    private readonly SKPaint _gizmoYPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(120, 220, 120) };
    private readonly SKPaint _gizmoZPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(90, 140, 240) };
    private readonly SKPaint _gridMinorPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = new SKColor(60, 70, 85, 120) };
    private readonly SKPaint _gridMajorPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = new SKColor(90, 105, 125, 170) };
    private readonly SKPaint _gridAxisXPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, Color = new SKColor(220, 90, 80, 200) };
    private readonly SKPaint _gridAxisYPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, Color = new SKColor(120, 220, 120, 200) };
    private readonly SKPaint _gridAxisZPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, Color = new SKColor(90, 140, 220, 200) };
    private readonly SKPaint _debugTextPaint = new() { IsAntialias = true, TextSize = 14f, Color = SKColors.White };
    private readonly SKPaint _debugBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0, 0, 0, 140) };
    private readonly SKPaint _marqueeFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(80, 160, 255, 40) };
    private readonly SKPaint _marqueeStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(120, 200, 255, 220) };
    private readonly SKPaint _paintBrushPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = new SKColor(120, 200, 255, 220) };

    public EditorViewportService(EditorSession editor,
        SceneGraph sceneGraph,
        Renderer3D renderer,
        Camera camera,
        OrbitCameraController orbit,
        EditorViewModel editorViewModel,
        StatusBarViewModel statusBar)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _orbit = orbit ?? throw new ArgumentNullException(nameof(orbit));
        _editorViewModel = editorViewModel ?? throw new ArgumentNullException(nameof(editorViewModel));
        _statusBar = statusBar ?? throw new ArgumentNullException(nameof(statusBar));
        _inputProvider = new AvaloniaInputProvider();
        _animationSystem = new ViewportAnimationSystem(this);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += (_, _) => OnFrameTick();
        _timer.Start();

        _editor.MeshEdits.MeshEdited += InvalidateUvIslandCache;
        _editor.SelectionService.SelectionChanged += InvalidateUvIslandCache;

        _engineHost = CreateEngineHost();
    }

    private SceneGraph _sceneGraph;

    public SceneGraph SceneGraph
    {
        get => _sceneGraph;
        set => SetSceneGraph(value);
    }

    private void SetSceneGraph(SceneGraph scene)
    {
        _sceneGraph = scene ?? throw new ArgumentNullException(nameof(scene));
        _engineHost = CreateEngineHost();
    }

    public bool Pause
    {
        get => _pause;
        set => _pause = value;
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set => _showGrid = value;
    }

    public bool ShowUvSeams
    {
        get => _showUvSeams;
        set => _showUvSeams = value;
    }

    public bool ShowUvIslands
    {
        get => _showUvIslands;
        set => _showUvIslands = value;
    }

    public bool ShowPickDebug
    {
        get => _showPickDebug;
        set => _showPickDebug = value;
    }

    public bool ShowStats
    {
        get => _showStats;
        set => _showStats = value;
    }

    public SceneNode? SelectedNode => _selectedNode;

    public AnimationPlayer? AnimationPlayer
    {
        get => _animationPlayer;
        set => _animationPlayer = value;
    }

    public AnimationMixer? AnimationMixer
    {
        get => _animationMixer;
        set => _animationMixer = value;
    }

    public SceneNode? SpinNode
    {
        get => _spinNode;
        set => _spinNode = value;
    }

    public bool IsGizmoDragging => _isGizmoDragging;

    public bool IsActive { get; set; } = true;

    public event EventHandler? Activated;

    public event EventHandler? AnimationTimelineChanged;

    public void Attach(SkiaView surface)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _surface.RenderFrame += OnPaintSurface;
        _inputProvider.Attach(_surface);
        _inputProvider.Input += OnInputEvent;
    }

    public void Detach()
    {
        if (_surface == null)
        {
            return;
        }

        _surface.RenderFrame -= OnPaintSurface;
        _inputProvider.Input -= OnInputEvent;
        _inputProvider.Detach();
        _surface = null;
    }

    public void Invalidate()
    {
        _surface?.InvalidateVisual();
    }

    public void ResetState()
    {
        _renderInstances = Array.Empty<MeshInstance>();
        _selectedNode = null;
        _angle = 0f;
        _lastFrameTimestamp = 0;
        _fpsAccum = 0;
        _fpsSampleCount = 0;
        _fpsAverage = 0;
        _animationPlayer = null;
        _animationMixer = null;
    }

    public void SetNavigationMode(ViewportNavigationMode mode)
    {
        _navigationMode = mode;
    }

    public void SetView(int viewIndex)
    {
        _viewIndex = viewIndex;
        var yaw = _orbit.Yaw;
        var pitch = _orbit.Pitch;
        var up = _camera.Up;
        var projection = CameraProjectionMode.Perspective;

        switch (viewIndex)
        {
            case 1: // Top
                yaw = -MathF.PI / 2f;
                pitch = MathF.PI / 2f;
                up = new Vector3(0f, 0f, -1f);
                projection = CameraProjectionMode.Orthographic;
                break;
            case 2: // Front
                yaw = MathF.PI / 2f;
                pitch = 0f;
                up = Vector3.UnitY;
                projection = CameraProjectionMode.Orthographic;
                break;
            case 3: // Left
                yaw = MathF.PI;
                pitch = 0f;
                up = Vector3.UnitY;
                projection = CameraProjectionMode.Orthographic;
                break;
            case 4: // Right
                yaw = 0f;
                pitch = 0f;
                up = Vector3.UnitY;
                projection = CameraProjectionMode.Orthographic;
                break;
            case 5: // Back
                yaw = -MathF.PI / 2f;
                pitch = 0f;
                up = Vector3.UnitY;
                projection = CameraProjectionMode.Orthographic;
                break;
            case 6: // Bottom
                yaw = -MathF.PI / 2f;
                pitch = -MathF.PI / 2f;
                up = new Vector3(0f, 0f, 1f);
                projection = CameraProjectionMode.Orthographic;
                break;
            default: // Perspective
                yaw = -0.7f;
                pitch = -0.5f;
                up = Vector3.UnitY;
                projection = CameraProjectionMode.Perspective;
                break;
        }

        _camera.ProjectionMode = projection;
        _camera.Up = up;
        if (_camera.ProjectionMode == CameraProjectionMode.Orthographic && _camera.OrthographicSize < 0.1f)
        {
            _camera.OrthographicSize = 6f;
        }

        _orbit.Yaw = yaw;
        _orbit.Pitch = pitch;
        _orbit.UpdateCamera();
    }

    public void UpdateSelectedNodeFromEditor()
    {
        var selected = _editor.Selection.Selected;
        if (selected is not null && _editor.Document.TryGetNode(selected, out var node))
        {
            _selectedNode = node;
            return;
        }

        _selectedNode = null;
    }

    public void UpdateSceneLights()
    {
        _engineHost.UseSceneLights = true;
        var viewport = GetViewport();
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        TickEngine(viewport, cull: true);
    }

    public void ZoomToExtents()
    {
        if (_surface is { Bounds.Width: > 0, Bounds.Height: > 0 })
        {
            _camera.AspectRatio = (float)_surface.Bounds.Width / (float)_surface.Bounds.Height;
        }

        TickEngine(GetViewport(), cull: false);
        var instances = _renderInstances;

        if (instances.Count == 0)
        {
            _orbit.Target = Vector3.Zero;
            _orbit.Radius = 8f;
            _orbit.UpdateCamera();
            return;
        }

        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;

        foreach (var instance in instances)
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

        if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
        {
            FitOrthographicBounds(min, max);
            return;
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

    private void OnFrameTick()
    {
        if (_pause)
        {
            return;
        }

        _surface?.InvalidateVisual();
    }

    private void AdvanceAnimations(float deltaSeconds)
    {
        if (_pause)
        {
            return;
        }

        if (!_isGizmoDragging)
        {
            if (_animationMixer != null)
            {
                _animationMixer.Update(deltaSeconds);
            }
            else if (_animationPlayer != null)
            {
                _animationPlayer.Update(deltaSeconds);
            }
            else if (_spinNode is not null)
            {
                const float spinSpeed = 0.9375f;
                _angle += deltaSeconds * spinSpeed;
                var rotation = Matrix4x4.CreateRotationY(_angle) * Matrix4x4.CreateRotationX(_angle * 0.3f);
                _spinNode.Transform.LocalRotation = Quaternion.CreateFromRotationMatrix(rotation);
            }
        }

        if (_animationPlayer != null || _animationMixer != null)
        {
            AnimationTimelineChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPaintSurface(object? sender, SkiaRenderEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        var viewport = new SKRect(0, 0, info.Width, info.Height);
        TickEngine(viewport, cull: true);
        _engineHost.Render(canvas, viewport);

        DrawEditorGrid(canvas, viewport);
        DrawSelectionOverlay(canvas, viewport);
        DrawSelectionMarquee(canvas);
        DrawPaintSelection(canvas);
        DrawLassoSelection(canvas);
        DrawGizmo(canvas, viewport);
        DrawPickDebug(canvas, viewport);
        DrawFaceSelection(canvas, viewport);
        DrawUvIslands(canvas, viewport);
        DrawUvSeams(canvas, viewport);
        DrawEdgeSelection(canvas, viewport);
        DrawVertexSelection(canvas, viewport);
        DrawStats(canvas);
        UpdateFrameTiming();
        UpdateStatusBar();
    }

    private void OnInputEvent(object? sender, InputEvent e)
    {
        switch (e.Type)
        {
            case InputEventType.Pressed:
                HandlePointerPressed(e);
                break;
            case InputEventType.Released:
                HandlePointerReleased(e);
                break;
            case InputEventType.Moved:
                HandlePointerMoved(e);
                break;
            case InputEventType.Wheel:
                HandlePointerWheel(e);
                break;
        }
    }

    private void HandlePointerPressed(InputEvent e)
    {
        if (_surface is null)
        {
            return;
        }

        Activated?.Invoke(this, EventArgs.Empty);

        var state = e.State;
        var current = state.Position;
        _isDragging = true;
        _isPanning = state.IsButtonDown(InputPointerButton.Middle) || _navigationMode == ViewportNavigationMode.Pan;
        _isOrbiting = _navigationMode == ViewportNavigationMode.Orbit;
        _isZooming = _navigationMode == ViewportNavigationMode.Zoom;
        _wasLeftButtonDown = state.IsButtonDown(InputPointerButton.Left);
        _lastPointer = current;
        _pointerDown = current;
        _isGizmoDragging = false;
        var navActive = _navigationMode != ViewportNavigationMode.None;
        var operation = ResolveSelectionOperation(state.Modifiers);

        if (navActive)
        {
            return;
        }

        if (_wasLeftButtonDown && !_isPanning && _editor.SelectionOptions.Tool != SelectionTool.Click)
        {
            if (!TryGetSelectionContext(out var context, cull: true))
            {
                return;
            }

            var start = ToPoint(current);
            switch (_editor.SelectionOptions.Tool)
            {
                case SelectionTool.Box:
                    _editor.SelectionService.BeginMarqueeSelection(start, operation, context);
                    break;
                case SelectionTool.Paint:
                    _editor.SelectionService.BeginPaintSelection(start, operation, context);
                    break;
                case SelectionTool.Lasso:
                    _editor.SelectionService.BeginLassoSelection(start, operation);
                    break;
            }

            _surface.InvalidateVisual();
            return;
        }

        if (_wasLeftButtonDown)
        {
            if (_editor.Gizmo.ShowGizmo && TryBeginGizmoDrag(current))
            {
                _isGizmoDragging = true;
                _surface.InvalidateVisual();
            }
        }
    }

    private void HandlePointerReleased(InputEvent e)
    {
        if (!_isDragging || _surface is null)
        {
            return;
        }

        var releasePoint = e.State.Position;
        _lastPointer = releasePoint;
        var isLeftRelease = e.Button == InputPointerButton.Left;
        var navActive = _navigationMode != ViewportNavigationMode.None;
        if (isLeftRelease && !navActive)
        {
            var selectionService = _editor.SelectionService;
            if (selectionService.IsLassoSelecting && TryGetSelectionContext(out var lassoContext, cull: true))
            {
                selectionService.UpdateLassoSelection(ToPoint(releasePoint), lassoContext, finalize: true);
            }

            selectionService.EndSelection();

            if (!_isGizmoDragging && !_isPanning && _editor.SelectionOptions.Tool == SelectionTool.Click)
            {
                var dx = releasePoint.X - _pointerDown.X;
                var dy = releasePoint.Y - _pointerDown.Y;
                if (dx * dx + dy * dy <= ClickThreshold * ClickThreshold &&
                    TryGetSelectionContext(out var context, cull: true))
                {
                    var operation = ResolveSelectionOperation(e.State.Modifiers);
                    var toggle = operation != SelectionOperation.Replace;
                    selectionService.TrySelectAt(ToPoint(releasePoint), context, toggle);
                }
            }
        }

        if (_isGizmoDragging)
        {
            _editor.Gizmo.EndDrag();
            _isGizmoDragging = false;
        }

        _isDragging = false;
        _isPanning = false;
        _wasLeftButtonDown = false;
        _isOrbiting = false;
        _isZooming = false;
        _surface.InvalidateVisual();
    }

    private void HandlePointerMoved(InputEvent e)
    {
        if (!_isDragging || _surface is null)
        {
            return;
        }

        var current = e.State.Position;
        var delta = current - _lastPointer;
        _lastPointer = current;

        if (_isGizmoDragging)
        {
            UpdateGizmoDrag(current);
            _surface.InvalidateVisual();
            return;
        }

        if (_navigationMode != ViewportNavigationMode.None)
        {
            if (_isZooming)
            {
                ZoomWithPixels(delta, current);
            }
            else if (_isPanning)
            {
                PanWithPixels(delta);
            }
            else if (_isOrbiting)
            {
                if (_camera.ProjectionMode == CameraProjectionMode.Perspective)
                {
                    const float sensitivity = 0.01f;
                    _orbit.Rotate(delta.X * sensitivity, delta.Y * sensitivity);
                }
            }

            _surface.InvalidateVisual();
            return;
        }

        if (_wasLeftButtonDown && !_isPanning)
        {
            var selectionService = _editor.SelectionService;
            if (selectionService.IsMarqueeSelecting || selectionService.IsPaintSelecting || selectionService.IsLassoSelecting)
            {
                if (TryGetSelectionContext(out var context, cull: true))
                {
                    var screenPoint = ToPoint(current);
                    if (selectionService.IsMarqueeSelecting)
                    {
                        selectionService.UpdateMarqueeSelection(screenPoint, ToPoint(_pointerDown), context);
                    }
                    else if (selectionService.IsPaintSelecting)
                    {
                        selectionService.UpdatePaintSelection(screenPoint, context);
                    }
                    else
                    {
                        selectionService.UpdateLassoSelection(screenPoint, context, finalize: false);
                    }
                }

                _surface.InvalidateVisual();
                return;
            }
        }

        if (_isPanning)
        {
            PanWithPixels(delta);
        }
        else
        {
            if (_camera.ProjectionMode == CameraProjectionMode.Perspective)
            {
                const float sensitivity = 0.01f;
                _orbit.Rotate(delta.X * sensitivity, delta.Y * sensitivity);
            }
        }
        _surface.InvalidateVisual();
    }

    private void HandlePointerWheel(InputEvent e)
    {
        const float zoomSpeed = 0.5f;
        var delta = -e.WheelDelta * zoomSpeed;

        if (_surface is null)
        {
            if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
            {
                ZoomOrthographic(delta);
            }
            else
            {
                _orbit.Zoom(delta);
            }
            _surface?.InvalidateVisual();
            return;
        }

        var bounds = _surface.Bounds;
        var viewportSize = new Vector2((float)bounds.Width, (float)bounds.Height);
        var cursor = e.State.Position;

        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
            {
                ZoomOrthographic(delta);
            }
            else
            {
                _orbit.Zoom(delta);
            }
        }
        else
        {
            _camera.AspectRatio = viewportSize.X / viewportSize.Y;
            if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
            {
                ZoomOrthographic(delta);
            }
            else
            {
                _orbit.ZoomToScreenPoint(cursor, viewportSize, delta);
            }
        }
        _surface?.InvalidateVisual();
    }

    private void PanWithPixels(Vector2 delta)
    {
        var viewportSize = _surface is null
            ? new Vector2(0f, 0f)
            : new Vector2((float)_surface.Bounds.Width, (float)_surface.Bounds.Height);

        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            const float fallback = 0.01f;
            if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
            {
                PanOrthographic(new Vector2(delta.X * fallback, delta.Y * fallback));
            }
            else
            {
                _orbit.Pan(new Vector2(delta.X * fallback, delta.Y * fallback));
            }
            return;
        }

        _camera.AspectRatio = viewportSize.X / viewportSize.Y;
        if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
        {
            var worldPerPixelY = (2f * _camera.OrthographicSize) / viewportSize.Y;
            var worldPerPixelX = worldPerPixelY * (_camera.AspectRatio <= 0f ? 1f : _camera.AspectRatio);
            var pan = new Vector2(delta.X * worldPerPixelX, delta.Y * worldPerPixelY);
            PanOrthographic(pan);
        }
        else
        {
            var dist = _orbit.Radius;
            var vfov = _camera.FieldOfView;
            var worldPerPixelY = (2f * dist * MathF.Tan(vfov * 0.5f)) / viewportSize.Y;
            var worldPerPixelX = worldPerPixelY * (_camera.AspectRatio <= 0f ? 1f : _camera.AspectRatio);

            var pan = new Vector2(delta.X * worldPerPixelX, delta.Y * worldPerPixelY);
            _orbit.Pan(pan);
        }
    }

    private void ZoomWithPixels(Vector2 delta, Vector2 cursor)
    {
        const float zoomSpeed = 0.02f;
        var zoomDelta = delta.Y * zoomSpeed;

        if (_surface is null)
        {
            if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
            {
                ZoomOrthographic(zoomDelta);
            }
            else
            {
                _orbit.Zoom(zoomDelta);
            }
            return;
        }

        var bounds = _surface.Bounds;
        var viewportSize = new Vector2((float)bounds.Width, (float)bounds.Height);
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
            {
                ZoomOrthographic(zoomDelta);
            }
            else
            {
                _orbit.Zoom(zoomDelta);
            }
            return;
        }

        _camera.AspectRatio = viewportSize.X / viewportSize.Y;
        if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
        {
            ZoomOrthographic(zoomDelta);
            return;
        }

        _orbit.ZoomToScreenPoint(cursor, viewportSize, zoomDelta);
    }

    private void PanOrthographic(Vector2 deltaWorld)
    {
        var forward = _camera.Target - _camera.Position;
        if (forward.LengthSquared() < 1e-8f)
        {
            forward = Vector3.UnitZ;
        }
        else
        {
            forward = Vector3.Normalize(forward);
        }

        var right = Vector3.Normalize(Vector3.Cross(forward, _camera.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var offset = (-right * deltaWorld.X) + (up * deltaWorld.Y);

        _camera.Position += offset;
        _camera.Target += offset;
        SyncOrbitFromCamera();
    }

    private void ZoomOrthographic(float delta)
    {
        var size = MathF.Max(0.05f, _camera.OrthographicSize + delta);
        _camera.OrthographicSize = size;
    }

    private void FitOrthographicBounds(Vector3 min, Vector3 max)
    {
        var center = (min + max) * 0.5f;
        var view = _camera.GetViewMatrix();
        var corners = new[]
        {
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z)
        };

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            var viewPos = Vector3.Transform(corners[i], view);
            minX = MathF.Min(minX, viewPos.X);
            maxX = MathF.Max(maxX, viewPos.X);
            minY = MathF.Min(minY, viewPos.Y);
            maxY = MathF.Max(maxY, viewPos.Y);
        }

        var halfWidth = MathF.Max(MathF.Abs(minX), MathF.Abs(maxX));
        var halfHeight = MathF.Max(MathF.Abs(minY), MathF.Abs(maxY));
        var aspect = _camera.AspectRatio <= 0f ? 1f : _camera.AspectRatio;
        var size = MathF.Max(halfHeight, halfWidth / aspect) * 1.2f;
        if (size < 0.05f)
        {
            size = 0.05f;
        }

        var forward = _camera.Target - _camera.Position;
        var distance = forward.Length();
        if (distance < 0.01f)
        {
            distance = 6f;
            forward = new Vector3(0f, 0f, -1f);
        }
        else
        {
            forward = Vector3.Normalize(forward);
        }

        _camera.Target = center;
        _camera.Position = center - forward * distance;
        _camera.OrthographicSize = size;
        SyncOrbitFromCamera();
    }

    private void SyncOrbitFromCamera()
    {
        var delta = _camera.Target - _camera.Position;
        var dist = delta.Length();
        if (dist > 1e-6f)
        {
            _orbit.Radius = dist;
        }

        _orbit.Target = _camera.Target;
    }

    private SKRect GetViewport()
    {
        if (_surface is null)
        {
            return SKRect.Empty;
        }

        var bounds = _surface.Bounds;
        return new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height);
    }

    private void RefreshRenderInstances(SKRect viewport, bool cull)
    {
        TickEngine(viewport, cull);
    }

    private void TickEngine(SKRect viewport, bool cull)
    {
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            _renderInstances = Array.Empty<MeshInstance>();
            return;
        }

        _camera.AspectRatio = viewport.Width / viewport.Height;
        _engineHost.EnableCulling = cull;
        _engineHost.Tick();
        _renderInstances = _engineHost.RenderInstances;
    }

    private EngineHost CreateEngineHost()
    {
        if (_engineHost != null)
        {
            _engineHost.Shutdown();
        }

        var host = new EngineHost(_sceneGraph, _renderer, _camera)
        {
            EnableCulling = true,
            ParallelCollect = true,
            ParallelUpdate = true,
            UseSceneLights = true,
            FallbackLight = DefaultLight
        };

        host.AddSystem(_animationSystem);
        host.Initialize();
        return host;
    }

    private sealed class ViewportAnimationSystem : SystemBase
    {
        private readonly EditorViewportService _owner;

        public ViewportAnimationSystem(EditorViewportService owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public override void Update(Engine engine, Time time)
        {
            _owner.AdvanceAnimations(time.DeltaSeconds);
        }
    }

    private bool TryBeginGizmoDrag(Vector2 screenPoint)
    {
        if (_surface is null || _selectedNode is null)
        {
            return false;
        }

        var selected = _editor.Selection.Selected;
        if (selected is null)
        {
            return false;
        }

        var viewport = GetViewport();
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return false;
        }

        RefreshRenderInstances(viewport, cull: true);
        return _editor.Gizmo.TryBeginDrag(ToPoint(screenPoint), _renderer, _camera, viewport, selected, _selectedNode);
    }

    private void UpdateGizmoDrag(Vector2 screenPoint)
    {
        if (_selectedNode is null)
        {
            return;
        }

        var viewport = GetViewport();
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        _editor.Gizmo.UpdateDrag(ToPoint(screenPoint), _renderer, _camera, viewport, _selectedNode);
    }

    private static SelectionOperation ResolveSelectionOperation(InputModifiers modifiers)
    {
        if ((modifiers & InputModifiers.Control) == InputModifiers.Control || (modifiers & InputModifiers.Command) == InputModifiers.Command)
        {
            return SelectionOperation.Subtract;
        }

        if ((modifiers & InputModifiers.Shift) == InputModifiers.Shift)
        {
            return SelectionOperation.Add;
        }

        return SelectionOperation.Replace;
    }

    private bool TryGetSelectionContext(out EditorSelectionContext context, bool cull)
    {
        context = default;
        var viewport = GetViewport();
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return false;
        }

        RefreshRenderInstances(viewport, cull);
        context = new EditorSelectionContext(_renderer, _camera, viewport, _renderInstances);
        return true;
    }

    private static SKPoint ToPoint(Point point) => new((float)point.X, (float)point.Y);

    private static SKPoint ToPoint(Vector2 point) => new(point.X, point.Y);

    private void DrawSelectionOverlay(SKCanvas canvas, SKRect viewport)
    {
        var selection = _editor.Selection.ObjectSelection;
        if (selection.Count == 0)
        {
            return;
        }

        foreach (var instance in selection.Items)
        {
            if (_renderer.TryGetScreenBounds(instance, viewport, _camera, out var bounds))
            {
                canvas.DrawRect(bounds, _selectionPaint);
            }
        }
    }

    private void DrawSelectionMarquee(SKCanvas canvas)
    {
        var selectionService = _editor.SelectionService;
        if (!selectionService.IsMarqueeSelecting)
        {
            return;
        }

        var rect = selectionService.MarqueeRect;
        canvas.DrawRect(rect, _marqueeFillPaint);
        canvas.DrawRect(rect, _marqueeStrokePaint);
    }

    private void DrawPaintSelection(SKCanvas canvas)
    {
        var selectionService = _editor.SelectionService;
        if (!selectionService.IsPaintSelecting)
        {
            return;
        }

        canvas.DrawCircle(selectionService.PaintCenter, selectionService.PaintRadius, _paintBrushPaint);
    }

    private void DrawLassoSelection(SKCanvas canvas)
    {
        var selectionService = _editor.SelectionService;
        var points = selectionService.LassoPoints;
        if (!selectionService.IsLassoSelecting || points.Count == 0)
        {
            return;
        }

        using var path = new SKPath();
        path.MoveTo(points[0]);
        for (int i = 1; i < points.Count; i++)
        {
            path.LineTo(points[i]);
        }

        if (points.Count > 2)
        {
            path.Close();
            canvas.DrawPath(path, _marqueeFillPaint);
        }

        canvas.DrawPath(path, _marqueeStrokePaint);
    }

    private void DrawGizmo(SKCanvas canvas, SKRect viewport)
    {
        var selected = _editor.Selection.Selected;
        var gizmo = _editor.Gizmo;
        if (selected is null || !gizmo.ShowGizmo)
        {
            return;
        }

        if (!gizmo.TryGetSelectedCenter(selected, out var center))
        {
            return;
        }

        var size = 0.8f;
        if (Matrix4x4.Decompose(selected.Transform, out var scale, out _, out _))
        {
            size = MathF.Max(0.6f, MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z)) * 0.6f);
        }

        if (!_renderer.TryProjectWorld(center, viewport, _camera, out var screenCenter))
        {
            return;
        }

        var xWorld = center + Vector3.UnitX * size;
        var yWorld = center + Vector3.UnitY * size;
        var zWorld = center + Vector3.UnitZ * size;

        if (_renderer.TryProjectWorld(xWorld, viewport, _camera, out var screenX))
        {
            canvas.DrawLine(screenCenter, screenX, _gizmoXPaint);
            if (gizmo.Mode == GizmoMode.Scale)
            {
                DrawGizmoHandleSquare(canvas, screenX, _gizmoXPaint);
            }
        }
        if (_renderer.TryProjectWorld(yWorld, viewport, _camera, out var screenY))
        {
            canvas.DrawLine(screenCenter, screenY, _gizmoYPaint);
            if (gizmo.Mode == GizmoMode.Scale)
            {
                DrawGizmoHandleSquare(canvas, screenY, _gizmoYPaint);
            }
        }
        if (_renderer.TryProjectWorld(zWorld, viewport, _camera, out var screenZ))
        {
            canvas.DrawLine(screenCenter, screenZ, _gizmoZPaint);
            if (gizmo.Mode == GizmoMode.Scale)
            {
                DrawGizmoHandleSquare(canvas, screenZ, _gizmoZPaint);
            }
        }
    }

    private static void DrawGizmoHandleSquare(SKCanvas canvas, SKPoint center, SKPaint paint)
    {
        const float size = 8f;
        var half = size * 0.5f;
        var rect = new SKRect(center.X - half, center.Y - half, center.X + half, center.Y + half);
        canvas.DrawRect(rect, paint);
    }

    private void DrawPickDebug(SKCanvas canvas, SKRect viewport)
    {
        var pick = _editor.Selection.LastPick;
        if (!_showPickDebug || pick is null)
        {
            return;
        }

        var pickValue = pick.Value;
        var bary = pickValue.Barycentric;
        var text = $"Pick: tri {pickValue.TriangleIndex}  bary ({bary.X:0.00}, {bary.Y:0.00}, {bary.Z:0.00})";
        var textWidth = _debugTextPaint.MeasureText(text);
        var margin = 6f;
        var x = 16f;
        var y = viewport.Top + 24f;
        var rect = new SKRect(x - margin, y - 16f, x + textWidth + margin, y + 6f);
        canvas.DrawRect(rect, _debugBgPaint);
        canvas.DrawText(text, x, y, _debugTextPaint);

        if (_renderer.TryProjectWorld(pickValue.Position, viewport, _camera, out var hit))
        {
            canvas.DrawCircle(hit, 4f, _selectionPaint);
        }
    }

    private void DrawFaceSelection(SKCanvas canvas, SKRect viewport)
    {
        var mode = _editor.Mode;
        var selection = _editor.Selection;
        if (!mode.EditMode || !mode.FaceSelect || selection.Selected is null || selection.FaceSelection.IsEmpty)
        {
            return;
        }

        var selected = selection.Selected;
        var mesh = selected.Mesh;
        var indices = mesh.Indices;
        var verts = mesh.Vertices;
        var transform = selected.Transform;

        foreach (var face in selection.FaceSelection.Items)
        {
            var baseIndex = face * 3;
            if (baseIndex + 2 >= indices.Count)
            {
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if ((uint)i0 >= (uint)verts.Count || (uint)i1 >= (uint)verts.Count || (uint)i2 >= (uint)verts.Count)
            {
                continue;
            }

            var w0 = Vector3.Transform(verts[i0].Position, transform);
            var w1 = Vector3.Transform(verts[i1].Position, transform);
            var w2 = Vector3.Transform(verts[i2].Position, transform);

            if (!_renderer.TryProjectWorld(w0, viewport, _camera, out var s0) ||
                !_renderer.TryProjectWorld(w1, viewport, _camera, out var s1) ||
                !_renderer.TryProjectWorld(w2, viewport, _camera, out var s2))
            {
                continue;
            }

            using var path = new SKPath();
            path.MoveTo(s0);
            path.LineTo(s1);
            path.LineTo(s2);
            path.Close();
            canvas.DrawPath(path, _facePaint);
        }
    }

    private void DrawEdgeSelection(SKCanvas canvas, SKRect viewport)
    {
        var mode = _editor.Mode;
        var selection = _editor.Selection;
        if (!mode.EditMode || !mode.EdgeSelect || selection.Selected is null || selection.EdgeSelection.IsEmpty)
        {
            return;
        }

        var selected = selection.Selected;
        foreach (var edge in selection.EdgeSelection.Items)
        {
            if (!TryGetVertexWorld(selected, edge.A, out var w0) ||
                !TryGetVertexWorld(selected, edge.B, out var w1))
            {
                continue;
            }

            if (_renderer.TryProjectWorld(w0, viewport, _camera, out var s0) &&
                _renderer.TryProjectWorld(w1, viewport, _camera, out var s1))
            {
                canvas.DrawLine(s0, s1, _edgePaint);
            }
        }
    }

    private void DrawUvSeams(SKCanvas canvas, SKRect viewport)
    {
        if (!_showUvSeams)
        {
            return;
        }

        var selected = _editor.Selection.Selected;
        if (selected is null)
        {
            return;
        }

        if (!_editor.Document.TryGetEditableMesh(selected, out var editable) || editable.SeamEdges.Count == 0)
        {
            return;
        }

        foreach (var edge in editable.SeamEdges)
        {
            if (!TryGetVertexWorld(selected, edge.A, out var w0) ||
                !TryGetVertexWorld(selected, edge.B, out var w1))
            {
                continue;
            }

            if (_renderer.TryProjectWorld(w0, viewport, _camera, out var s0) &&
                _renderer.TryProjectWorld(w1, viewport, _camera, out var s1))
            {
                canvas.DrawLine(s0, s1, _uvSeamPaint);
            }
        }
    }

    private void DrawUvIslands(SKCanvas canvas, SKRect viewport)
    {
        if (!_showUvIslands)
        {
            return;
        }

        var selected = _editor.Selection.Selected;
        if (selected is null)
        {
            return;
        }

        if (!_editor.Document.TryGetEditableMesh(selected, out var editable))
        {
            return;
        }

        if (!_uvIslandCacheValid ||
            !ReferenceEquals(_uvIslandInstance, selected) ||
            !ReferenceEquals(_uvIslandEditable, editable))
        {
            RebuildUvIslandCache(editable, selected);
        }

        if (_uvIslandEdges.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _uvIslandEdges.Count; i++)
        {
            var edges = _uvIslandEdges[i];
            if (edges.Count == 0)
            {
                continue;
            }

            _uvIslandPaint.Color = UvIslandPalette[i % UvIslandPalette.Length];
            foreach (var edge in edges)
            {
                if (!TryGetVertexWorld(selected, edge.A, out var w0) ||
                    !TryGetVertexWorld(selected, edge.B, out var w1))
                {
                    continue;
                }

                if (_renderer.TryProjectWorld(w0, viewport, _camera, out var s0) &&
                    _renderer.TryProjectWorld(w1, viewport, _camera, out var s1))
                {
                    canvas.DrawLine(s0, s1, _uvIslandPaint);
                }
            }
        }
    }

    private void RebuildUvIslandCache(EditableMesh editable, MeshInstance instance)
    {
        _uvIslandEdges.Clear();
        var islands = UvOperations.BuildUvIslands(editable, editable.SeamEdges, faceSelection: null, angleThresholdDegrees: 180f);
        if (islands.Count > 0)
        {
            var indices = editable.Indices;
            for (int i = 0; i < islands.Count; i++)
            {
                _uvIslandEdges.Add(BuildIslandBoundaryEdges(indices, islands[i]));
            }
        }

        _uvIslandCacheValid = true;
        _uvIslandInstance = instance;
        _uvIslandEditable = editable;
    }

    private void InvalidateUvIslandCache()
    {
        _uvIslandCacheValid = false;
        _uvIslandInstance = null;
        _uvIslandEditable = null;
        _uvIslandEdges.Clear();
    }

    private static List<EdgeKey> BuildIslandBoundaryEdges(IReadOnlyList<int> indices, List<int> faces)
    {
        var counts = new Dictionary<EdgeKey, int>();
        foreach (var face in faces)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            AddEdge(new EdgeKey(i0, i1));
            AddEdge(new EdgeKey(i1, i2));
            AddEdge(new EdgeKey(i2, i0));
        }

        var edges = new List<EdgeKey>();
        foreach (var pair in counts)
        {
            if (pair.Value == 1)
            {
                edges.Add(pair.Key);
            }
        }

        return edges;

        void AddEdge(EdgeKey edge)
        {
            if (counts.TryGetValue(edge, out var count))
            {
                counts[edge] = count + 1;
            }
            else
            {
                counts[edge] = 1;
            }
        }
    }

    private void DrawVertexSelection(SKCanvas canvas, SKRect viewport)
    {
        var mode = _editor.Mode;
        var selection = _editor.Selection;
        if (!mode.EditMode || !mode.VertexSelect || selection.Selected is null || selection.VertexSelection.IsEmpty)
        {
            return;
        }

        var selected = selection.Selected;
        foreach (var index in selection.VertexSelection.Items)
        {
            if (!TryGetVertexWorld(selected, index, out var world))
            {
                continue;
            }

            if (_renderer.TryProjectWorld(world, viewport, _camera, out var screen))
            {
                canvas.DrawCircle(screen, 4f, _vertexPaint);
            }
        }
    }

    private static bool TryGetVertexWorld(MeshInstance instance, int index, out Vector3 world)
    {
        world = default;
        var verts = instance.Mesh.Vertices;
        if ((uint)index >= (uint)verts.Count)
        {
            return false;
        }

        world = Vector3.Transform(verts[index].Position, instance.Transform);
        return true;
    }

    private void DrawEditorGrid(SKCanvas canvas, SKRect viewport)
    {
        if (!_showGrid)
        {
            return;
        }

        var size = 10f;
        if (_camera.ProjectionMode == CameraProjectionMode.Orthographic)
        {
            size = MathF.Max(size, _camera.OrthographicSize * 2f);
        }
        const float spacing = 1f;
        int lines = (int)MathF.Round(size / spacing);

        switch (_viewIndex)
        {
            case 2:
            case 5:
                DrawGridXY(canvas, viewport, size, spacing, lines);
                break;
            case 3:
            case 4:
                DrawGridYZ(canvas, viewport, size, spacing, lines);
                break;
            default:
                DrawGridXZ(canvas, viewport, size, spacing, lines);
                break;
        }
    }

    private void DrawGridXZ(SKCanvas canvas, SKRect viewport, float size, float spacing, int lines)
    {
        const float y = 0f;
        for (int i = -lines; i <= lines; i++)
        {
            float coord = i * spacing;
            bool major = i % 5 == 0;

            var paintZ = major ? _gridMajorPaint : _gridMinorPaint;
            if (i == 0)
            {
                paintZ = _gridAxisZPaint;
            }

            var p0 = new Vector3(coord, y, -size);
            var p1 = new Vector3(coord, y, size);
            DrawGridLine(canvas, viewport, p0, p1, paintZ);

            var paintX = major ? _gridMajorPaint : _gridMinorPaint;
            if (i == 0)
            {
                paintX = _gridAxisXPaint;
            }

            var p2 = new Vector3(-size, y, coord);
            var p3 = new Vector3(size, y, coord);
            DrawGridLine(canvas, viewport, p2, p3, paintX);
        }
    }

    private void DrawGridXY(SKCanvas canvas, SKRect viewport, float size, float spacing, int lines)
    {
        const float z = 0f;
        for (int i = -lines; i <= lines; i++)
        {
            float coord = i * spacing;
            bool major = i % 5 == 0;

            var paintY = major ? _gridMajorPaint : _gridMinorPaint;
            if (i == 0)
            {
                paintY = _gridAxisYPaint;
            }

            var p0 = new Vector3(coord, -size, z);
            var p1 = new Vector3(coord, size, z);
            DrawGridLine(canvas, viewport, p0, p1, paintY);

            var paintX = major ? _gridMajorPaint : _gridMinorPaint;
            if (i == 0)
            {
                paintX = _gridAxisXPaint;
            }

            var p2 = new Vector3(-size, coord, z);
            var p3 = new Vector3(size, coord, z);
            DrawGridLine(canvas, viewport, p2, p3, paintX);
        }
    }

    private void DrawGridYZ(SKCanvas canvas, SKRect viewport, float size, float spacing, int lines)
    {
        const float x = 0f;
        for (int i = -lines; i <= lines; i++)
        {
            float coord = i * spacing;
            bool major = i % 5 == 0;

            var paintZ = major ? _gridMajorPaint : _gridMinorPaint;
            if (i == 0)
            {
                paintZ = _gridAxisZPaint;
            }

            var p0 = new Vector3(x, coord, -size);
            var p1 = new Vector3(x, coord, size);
            DrawGridLine(canvas, viewport, p0, p1, paintZ);

            var paintY = major ? _gridMajorPaint : _gridMinorPaint;
            if (i == 0)
            {
                paintY = _gridAxisYPaint;
            }

            var p2 = new Vector3(x, -size, coord);
            var p3 = new Vector3(x, size, coord);
            DrawGridLine(canvas, viewport, p2, p3, paintY);
        }
    }

    private void DrawGridLine(SKCanvas canvas, SKRect viewport, Vector3 p0, Vector3 p1, SKPaint paint)
    {
        if (_renderer.TryProjectWorld(p0, viewport, _camera, out var s0) &&
            _renderer.TryProjectWorld(p1, viewport, _camera, out var s1))
        {
            canvas.DrawLine(s0, s1, paint);
        }
    }

    private void DrawStats(SKCanvas canvas)
    {
        if (!_showStats || !_renderer.CollectStats)
        {
            return;
        }

        var stats = _renderer.LastStats;
        var lines = new List<string>(2)
        {
            $"Mode: {(stats.DepthBuffer ? "Depth" : "Painter")}  Tri: {stats.Triangles}  Pix: {stats.PixelsWritten}  Workers: {stats.Workers}  Scale: {stats.RenderScale:0.00}"
        };

        var culling = SceneGraph.LastCullingStats;
        if (culling.TotalNodes > 0)
        {
            var state = culling.CullingEnabled ? "On" : "Off";
            lines.Add($"Culling: {state}  Nodes: {culling.TotalNodes}  Mesh: {culling.MeshNodes}  Visible: {culling.VisibleNodes}  Culled: {culling.CulledNodes}");
        }

        float textWidth = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            textWidth = MathF.Max(textWidth, _debugTextPaint.MeasureText(lines[i]));
        }
        var margin = 6f;
        var x = 16f;
        var y = 20f;
        var rect = new SKRect(x - margin, y - 16f, x + textWidth + margin, y + 6f + lines.Count * 16f);
        canvas.DrawRect(rect, _debugBgPaint);

        for (int i = 0; i < lines.Count; i++)
        {
            canvas.DrawText(lines[i], x, y + 16f * i, _debugTextPaint);
        }
    }

    private void UpdateFrameTiming()
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastFrameTimestamp != 0)
        {
            var dt = (now - _lastFrameTimestamp) / (double)Stopwatch.Frequency;
            if (dt > 1e-6)
            {
                var fps = 1.0 / dt;
                _fpsAccum += fps;
                _fpsSampleCount++;
                if (_fpsAverage <= 0)
                {
                    _fpsAverage = fps;
                }
                if (_fpsSampleCount >= FpsSampleWindow)
                {
                    _fpsAverage = _fpsAccum / _fpsSampleCount;
                    _fpsAccum = 0;
                    _fpsSampleCount = 0;
                }
            }
        }

        _lastFrameTimestamp = now;
    }

    private void UpdateStatusBar()
    {
        if (!IsActive)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            if (_statusUpdatePending)
            {
                return;
            }

            _statusUpdatePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _statusUpdatePending = false;
                UpdateStatusBar();
            });
            return;
        }

        _statusBar.SelectionText = _editorViewModel.SelectionStatusText;
        _statusBar.FpsText = _fpsAverage > 0 ? $"FPS: {_fpsAverage:0}" : "FPS: --";

        if (_renderer.CollectStats)
        {
            var stats = _renderer.LastStats;
            var mode = stats.DepthBuffer ? "Depth" : "Painter";
            var tri = FormatCount(stats.Triangles);
            var pix = FormatCount(stats.PixelsWritten);
            var text = $"Render: {mode} | Tri {tri} | Pix {pix} | W {stats.Workers} | Scale {stats.RenderScale:0.00}";

            var culling = SceneGraph.LastCullingStats;
            if (culling.TotalNodes > 0)
            {
                text += $" | Cull {culling.VisibleNodes}/{culling.TotalNodes}";
            }

            _statusBar.RenderText = text;
        }
        else
        {
            _statusBar.RenderText = "Render: stats off";
        }
    }

    private static string FormatCount(long value)
    {
        if (value >= 1_000_000_000)
        {
            return $"{value / 1_000_000_000.0:0.0}B";
        }
        if (value >= 1_000_000)
        {
            return $"{value / 1_000_000.0:0.0}M";
        }
        if (value >= 1_000)
        {
            return $"{value / 1_000.0:0.0}K";
        }

        return value.ToString("0");
    }

    private static bool TryComputeBounds(MeshInstance instance, out Vector3 min, out Vector3 max)
    {
        min = default;
        max = default;

        var mesh = instance.Mesh;
        if (mesh is null || mesh.Vertices.Count == 0)
        {
            return false;
        }

        min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        foreach (var vertex in mesh.Vertices)
        {
            var world = Vector3.Transform(vertex.Position, instance.Transform);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }

        return true;
    }

    public void Dispose()
    {
        Detach();
        _timer.Stop();
        _engineHost.Shutdown();
        _editor.MeshEdits.MeshEdited -= InvalidateUvIslandCache;
        _editor.SelectionService.SelectionChanged -= InvalidateUvIslandCache;
        _selectionPaint.Dispose();
        _vertexPaint.Dispose();
        _edgePaint.Dispose();
        _facePaint.Dispose();
        _gizmoXPaint.Dispose();
        _gizmoYPaint.Dispose();
        _gizmoZPaint.Dispose();
        _gridMinorPaint.Dispose();
        _gridMajorPaint.Dispose();
        _gridAxisXPaint.Dispose();
        _gridAxisZPaint.Dispose();
        _debugTextPaint.Dispose();
        _debugBgPaint.Dispose();
        _marqueeFillPaint.Dispose();
        _marqueeStrokePaint.Dispose();
        _paintBrushPaint.Dispose();
    }
}
