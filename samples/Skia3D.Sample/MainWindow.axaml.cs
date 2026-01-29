using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Skia3D.Animation;
using Skia3D.Core;
using Skia3D.Editor;
using Skia3D.Geometry;
using Skia3D.IO;
using Skia3D.Modeling;
using Skia3D.Scene;
using Skia3D.Sample.Services;
using Skia3D.Sample.ViewModels;
using Skia3D.Sample.Controls;
using SceneGraph = Skia3D.Scene.Scene;
using SkiaSharp;

namespace Skia3D.Sample;

public partial class MainWindow : Window
{
    private readonly Renderer3D _renderer = new();
    private readonly Camera _camera = new();
    private readonly Camera _cameraTop = new();
    private readonly Camera _cameraFront = new();
    private readonly Camera _cameraLeft = new();
    private readonly OrbitCameraController _orbit;
    private readonly OrbitCameraController _orbitTop;
    private readonly OrbitCameraController _orbitFront;
    private readonly OrbitCameraController _orbitLeft;
    private SceneGraph _sceneGraph = new();
    private readonly List<SceneNode> _userNodes = new();
    private readonly EditorSession _editor = new();
    private readonly MainWindowViewModel _viewModel;
    private readonly EditorViewModel _editorViewModel;
    private readonly EditorViewportService _viewportService;
    private readonly EditorViewportService _viewportTopService;
    private readonly EditorViewportService _viewportFrontService;
    private readonly EditorViewportService _viewportLeftService;
    private readonly ViewportManagerService _viewportManager;
    private readonly CommandStateService _commandStateService;
    private readonly MotionPanelService _motionService;
    private readonly EditorOptionsService _optionsService;
    private readonly MaterialGraphService _materialGraphService;
    private readonly ConstraintPanelService _constraintService;
    private readonly InspectorOptionsViewModel _optionsViewModel;
    private readonly StatusHintService _statusHintService;
    private EditorViewportControl? _viewportControlPerspective;
    private EditorViewportControl? _viewportControlTop;
    private EditorViewportControl? _viewportControlFront;
    private EditorViewportControl? _viewportControlLeft;
    private Popup? _quadMenuPopup;
    private string? _scenePath;
    private Texture2D? _checkerTexture;
    private const int DefaultMeshSegments = 24;
    private SceneNode? _groundNode;
    private SceneNode? _cubeNode;
    private SceneNode? _lightNode;
    private SKBitmap? _groundLabelAtlas;
    private readonly HierarchyPanelService _hierarchyService;
    private readonly Dictionary<SceneNode, bool> _visibilitySnapshot = new();
    private bool _isolateActive;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(_editor);
        _editorViewModel = _viewModel.Editor;
        _optionsViewModel = _viewModel.CommandPanel.Options;
        DataContext = _viewModel;
        _editor.SelectionService.SelectionChanged += OnEditorSelectionChanged;
        _editor.MeshEdits.SelectionChanged += OnEditorSelectionChanged;
        _editor.MeshEdits.CommandStateChanged += OnEditorCommandStateChanged;
        _editor.MeshEdits.MeshEdited += OnEditorMeshEdited;

        _orbit = new OrbitCameraController(_camera)
        {
            Radius = 8f,
            Target = Vector3.Zero,
            Yaw = -0.7f,
            Pitch = -0.5f
        };

        _orbitTop = new OrbitCameraController(_cameraTop)
        {
            Radius = 8f,
            Target = Vector3.Zero,
            Yaw = -MathF.PI / 2f,
            Pitch = MathF.PI / 2f
        };
        _orbitFront = new OrbitCameraController(_cameraFront)
        {
            Radius = 8f,
            Target = Vector3.Zero,
            Yaw = MathF.PI / 2f,
            Pitch = 0f
        };
        _orbitLeft = new OrbitCameraController(_cameraLeft)
        {
            Radius = 8f,
            Target = Vector3.Zero,
            Yaw = MathF.PI,
            Pitch = 0f
        };

        _viewportService = new EditorViewportService(_editor, _sceneGraph, _renderer, _camera, _orbit, _editorViewModel, _viewModel.StatusBar);
        _viewportTopService = new EditorViewportService(_editor, _sceneGraph, _renderer, _cameraTop, _orbitTop, _editorViewModel, _viewModel.StatusBar);
        _viewportFrontService = new EditorViewportService(_editor, _sceneGraph, _renderer, _cameraFront, _orbitFront, _editorViewModel, _viewModel.StatusBar);
        _viewportLeftService = new EditorViewportService(_editor, _sceneGraph, _renderer, _cameraLeft, _orbitLeft, _editorViewModel, _viewModel.StatusBar);
        _viewportManager = new ViewportManagerService();
        _viewportManager.Register(_viewportService, makeActive: true);
        _viewportManager.Register(_viewportTopService);
        _viewportManager.Register(_viewportFrontService);
        _viewportManager.Register(_viewportLeftService);
        _viewportTopService.SetView(1);
        _viewportFrontService.SetView(2);
        _viewportLeftService.SetView(3);

        _hierarchyService = new HierarchyPanelService(_editor, _viewportService, _viewModel.CommandPanel.Hierarchy);
        _hierarchyService.SelectionApplied += OnEditorSelectionChanged;
        _commandStateService = new CommandStateService(_editor, _viewModel.CommandPanel.Commands);
        _motionService = new MotionPanelService(_viewportService, _viewModel.CommandPanel.Motion);
        _optionsService = new EditorOptionsService(_editor, _renderer, _viewportManager, _optionsViewModel);
        _materialGraphService = new MaterialGraphService(_editor, _viewModel.Material);
        _constraintService = new ConstraintPanelService(_editor, _viewportService, _viewModel.CommandPanel.Constraints);
        _optionsService.EditModeApplied += OnEditorSelectionChanged;
        _statusHintService = new StatusHintService(this, _viewModel.StatusBar, _optionsViewModel);
        _viewModel.Actions.Bind(new EditorActionHandlers
        {
            OpenSceneAsync = OpenSceneAsync,
            LoadObjAsync = LoadObjAsync,
            SaveSceneAsync = SaveSceneAsync,
            SaveSceneAsAsync = SaveSceneAsAsync,
            ZoomExtents = ZoomExtents,
            IsolateSelection = IsolateSelection,
            UnhideAll = UnhideAll,
            RenameSelectionAsync = RenameSelectionAsync,
            ClearScene = ClearSceneAndInvalidate,
            Undo = Undo,
            Redo = Redo,
            ClearSelection = ClearSelection,
            SelectEdgeLoop = SelectEdgeLoop,
            SelectEdgeRing = SelectEdgeRing,
            ExtrudeFaces = ExtrudeFaces,
            BevelFaces = BevelFaces,
            InsetFaces = InsetFaces,
            LoopCutEdgeLoop = LoopCutEdgeLoop,
            SplitEdge = SplitEdge,
            BridgeEdges = BridgeEdges,
            BridgeEdgeLoops = BridgeEdgeLoops,
            DissolveEdge = DissolveEdge,
            CollapseEdge = CollapseEdge,
            MergeVertices = MergeVertices,
            DissolveFaces = DissolveFaces,
            WeldVertices = WeldVertices,
            CleanupMesh = CleanupMesh,
            SmoothMesh = SmoothMesh,
            SimplifyMesh = SimplifyMesh,
            NudgePosX = NudgePosX,
            NudgeNegX = NudgeNegX,
            PlanarUv = PlanarUv,
            BoxUv = BoxUv,
            NormalizeUv = NormalizeUv,
            FlipU = FlipU,
            FlipV = FlipV,
            UnwrapUv = UnwrapUv,
            PackUv = PackUv,
            MarkUvSeams = MarkUvSeams,
            ClearUvSeams = ClearUvSeams,
            ClearAllUvSeams = ClearAllUvSeams,
            SelectUvIsland = SelectUvIsland,
            AssignUvGroup = AssignUvGroup,
            ClearUvGroup = ClearUvGroup,
            CenterPivot = CenterPivot,
            ResetTransform = ResetTransform,
            AnimationReset = AnimationReset
        });

        BuildScene(DefaultMeshSegments);
        SetupGroundDecal();
        SetupProjectedPathSample();

        AttachViewport();
        AttachControls();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewportControlPerspective != null)
        {
            _viewportControlPerspective.QuadMenuRequested -= OnQuadMenuRequested;
        }
        if (_viewportControlTop != null)
        {
            _viewportControlTop.QuadMenuRequested -= OnQuadMenuRequested;
        }
        if (_viewportControlFront != null)
        {
            _viewportControlFront.QuadMenuRequested -= OnQuadMenuRequested;
        }
        if (_viewportControlLeft != null)
        {
            _viewportControlLeft.QuadMenuRequested -= OnQuadMenuRequested;
        }
        _hierarchyService.Dispose();
        _motionService.Dispose();
        _optionsService.Dispose();
        _materialGraphService.Dispose();
        _constraintService.Dispose();
        _statusHintService.Dispose();
        _viewportManager.Dispose();
        _viewportService.Dispose();
        _viewportTopService.Dispose();
        _viewportFrontService.Dispose();
        _viewportLeftService.Dispose();
        _checkerTexture?.Dispose();
    }

    private void AttachViewport()
    {
        _viewportControlPerspective = this.FindControl<EditorViewportControl>("ViewportPerspective");
        _viewportControlTop = this.FindControl<EditorViewportControl>("ViewportTop");
        _viewportControlFront = this.FindControl<EditorViewportControl>("ViewportFront");
        _viewportControlLeft = this.FindControl<EditorViewportControl>("ViewportLeft");
        _quadMenuPopup = this.FindControl<Popup>("QuadMenuPopup");

        if (_viewportControlPerspective != null)
        {
            _viewportControlPerspective.BindService(_viewportService);
            _viewportControlPerspective.QuadMenuRequested += OnQuadMenuRequested;
        }
        if (_viewportControlTop != null)
        {
            _viewportControlTop.BindService(_viewportTopService);
            _viewportControlTop.QuadMenuRequested += OnQuadMenuRequested;
        }
        if (_viewportControlFront != null)
        {
            _viewportControlFront.BindService(_viewportFrontService);
            _viewportControlFront.QuadMenuRequested += OnQuadMenuRequested;
        }
        if (_viewportControlLeft != null)
        {
            _viewportControlLeft.BindService(_viewportLeftService);
            _viewportControlLeft.QuadMenuRequested += OnQuadMenuRequested;
        }
    }

    private void OnQuadMenuRequested(object? sender, EventArgs e)
    {
        if (_quadMenuPopup != null && sender is Control control)
        {
            _quadMenuPopup.PlacementTarget = control;
        }

        _viewModel.Actions.IsQuadMenuOpen = true;
    }

    private void AttachControls()
    {
        _optionsService.ApplyOptions();
        _optionsService.ApplyEditOptions();
        _optionsService.ApplyImportOptions();
        _optionsService.ApplyModelingOptions();
        _optionsService.ApplySelectionOptions();
        _optionsService.ApplyViewportView();
        _optionsService.ApplyNavigationMode();
        _optionsService.Attach(RebuildSampleScene, DefaultMeshSegments);
        _commandStateService.Refresh();
        _motionService.Refresh();
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
    }

    private SceneNode CreateRootNode(string name, Mesh mesh, Material material, Vector3 localPosition)
    {
        var node = new SceneNode(name)
        {
            MeshRenderer = new MeshRenderer(mesh, material)
        };
        node.Transform.LocalPosition = localPosition;
        _sceneGraph.AddRoot(node);
        return node;
    }

    private void RegisterInstance(SceneNode node)
    {
        _editor.Document.RegisterInstance(node);
    }

    private void RegisterEditable(SceneNode node)
    {
        _editor.Document.RegisterEditable(node);
    }

    private void RegisterSceneRecursive(SceneNode node)
    {
        _editor.Document.RegisterSceneRecursive(node);
    }

    private static MeshInstance? FindFirstInstance(SceneNode node)
    {
        if (node.MeshInstance != null)
        {
            return node.MeshInstance;
        }

        foreach (var child in node.Children)
        {
            var found = FindFirstInstance(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void BuildScene(int meshSegments)
    {
        _sceneGraph = new SceneGraph();
        for (int i = 0; i < _viewportManager.Viewports.Count; i++)
        {
            _viewportManager.Viewports[i].SceneGraph = _sceneGraph;
        }
        _editor.Document.Clear();
        _groundNode = null;
        _cubeNode = null;
        _lightNode = null;

        var groundColor = new SKColor(115, 125, 135);
        var groundMesh = MeshFactory.CreateGrid(meshSegments, 12f, groundColor, twoSided: true);
        _checkerTexture ??= Texture2D.CreateCheckerboard(256, 256, new SKColor(80, 85, 95), new SKColor(130, 140, 150), cells: 8);
        var groundMaterial = new Material
        {
            BaseColor = SKColors.White,
            BaseColorTexture = _checkerTexture,
            BaseColorSampler = new TextureSampler { Filter = TextureFilter.Bilinear },
            UvScale = new Vector2(4f, 4f),
            ShadingModel = MaterialShadingModel.MetallicRoughness,
            Metallic = 0.0f,
            Roughness = 0.9f,
            Ambient = 0.35f,
            Diffuse = 0.55f,
            DoubleSided = true,
            UseVertexColor = false
        };
        _groundNode = CreateRootNode("Ground", groundMesh, groundMaterial, new Vector3(0f, -1.2f, 0f));
        RegisterInstance(_groundNode);

        var cubeMesh = MeshFactory.CreateCube(2.4f, new SKColor(46, 153, 255));
        var cubeMaterial = new Material
        {
            BaseColor = new SKColor(46, 153, 255),
            Diffuse = 1f,
            Ambient = 0.2f,
            ShadingModel = MaterialShadingModel.MetallicRoughness,
            Metallic = 0.85f,
            Roughness = 0.25f
        };
        _cubeNode = CreateRootNode("Cube", cubeMesh, cubeMaterial, Vector3.Zero);
        RegisterEditable(_cubeNode);

        var pyramidMesh = MeshFactory.CreatePyramid(2f, 2.4f, new SKColor(255, 99, 71));
        var pyramidMaterial = new Material
        {
            BaseColor = new SKColor(255, 140, 120),
            Diffuse = 1f,
            Ambient = 0.2f,
            EmissiveColor = new SKColor(255, 120, 80),
            EmissiveStrength = 0.12f
        };
        var pyramidNode = CreateRootNode("Pyramid", pyramidMesh, pyramidMaterial, new Vector3(2.6f, 0f, -2.2f));
        RegisterEditable(pyramidNode);

        var sphereSlices = Math.Max(8, meshSegments);
        var sphereStacks = Math.Max(6, meshSegments / 2);
        var sphereMesh = MeshFactory.CreateSphere(1.3f, sphereSlices, sphereStacks, new SKColor(80, 220, 180));
        var sphereMaterial = new Material
        {
            BaseColor = new SKColor(80, 220, 180),
            Ambient = 0.2f,
            Diffuse = 0.9f,
            ShadingModel = MaterialShadingModel.MetallicRoughness,
            Metallic = 0.15f,
            Roughness = 0.7f
        };
        var sphereNode = CreateRootNode("Sphere", sphereMesh, sphereMaterial, new Vector3(-2.8f, 0.2f, 1.8f));
        RegisterEditable(sphereNode);

        var cylinderSegments = Math.Max(8, meshSegments);
        var cylinderMesh = MeshFactory.CreateCylinder(0.9f, 2.5f, cylinderSegments, new SKColor(200, 200, 120));
        var cylinderMaterial = new Material
        {
            BaseColor = new SKColor(220, 220, 140),
            Ambient = 0.2f,
            Diffuse = 0.8f,
            ShadingModel = MaterialShadingModel.MetallicRoughness,
            Metallic = 0.6f,
            Roughness = 0.4f
        };
        var cylinderNode = CreateRootNode("Cylinder", cylinderMesh, cylinderMaterial, new Vector3(0f, 0f, -3f));
        RegisterEditable(cylinderNode);

        _lightNode = new SceneNode("Key Light")
        {
            Light = new LightComponent(Light.Directional(new Vector3(-0.4f, -1f, -0.6f), new SKColor(255, 255, 255), 1f))
        };
        _sceneGraph.AddRoot(_lightNode);

        foreach (var user in _userNodes)
        {
            _sceneGraph.AddRoot(user);
            RegisterEditable(user);
        }

        SetupSceneAnimation();
        _sceneGraph.UpdateWorld(parallel: true);
        _viewportManager.UpdateSceneLights();
    }

    private void SetupSceneAnimation()
    {
        if (_cubeNode is null)
        {
            _viewportService.AnimationPlayer = null;
            _viewportService.AnimationMixer = null;
            _viewportService.SpinNode = null;
            return;
        }

        var spinClip = new AnimationClip("CubeSpin");
        var spinTrack = new TransformTrack(_cubeNode.Name);
        spinTrack.RotationKeys.Add(new Keyframe<Quaternion>(0f, Quaternion.Identity));
        spinTrack.RotationKeys.Add(new Keyframe<Quaternion>(4f, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 2f)));
        spinClip.Tracks.Add(spinTrack);
        spinClip.RecalculateDuration();

        var bobClip = new AnimationClip("CubeBob");
        var bobTrack = new TransformTrack(_cubeNode.Name);
        var basePos = _cubeNode.Transform.LocalPosition;
        bobTrack.TranslationKeys.Add(new Keyframe<Vector3>(0f, basePos));
        bobTrack.TranslationKeys.Add(new Keyframe<Vector3>(1f, basePos + new Vector3(0f, 0.35f, 0f)));
        bobTrack.TranslationKeys.Add(new Keyframe<Vector3>(2f, basePos));
        bobClip.Tracks.Add(bobTrack);
        bobClip.RecalculateDuration();

        var spinPlayer = new AnimationPlayer(spinClip.Bind(_sceneGraph)) { Loop = true, IsPlaying = true };
        var bobPlayer = new AnimationPlayer(bobClip.Bind(_sceneGraph)) { Loop = true, IsPlaying = true, Speed = 1f };

        var mixer = new AnimationMixer();
        mixer.AddLayer(spinPlayer, weight: 1f);
        mixer.AddLayer(bobPlayer, weight: 0.6f);
        _viewportService.AnimationMixer = mixer;
        _viewportService.AnimationPlayer = null;
        _viewportService.SpinNode = _cubeNode;
    }

    private void SetupGroundDecal()
    {
        if (_groundNode is null || _groundNode.MeshInstance is null)
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

        if (!DecalBuilder.TryCreatePlanarFrame(_groundNode.Transform.WorldMatrix, 1.4f, 0.9f, 3.6f, out var frame))
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


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
        {
            return;
        }

        var key = e.Key;
        var modifiers = e.KeyModifiers;

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            switch (key)
            {
                case Key.O:
                    _viewModel.Actions.OpenSceneCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Z:
                    if ((modifiers & KeyModifiers.Shift) != 0)
                    {
                        _viewModel.Actions.RedoCommand.Execute(null);
                    }
                    else
                    {
                        _viewModel.Actions.UndoCommand.Execute(null);
                    }
                    e.Handled = true;
                    return;
                case Key.S:
                    if ((modifiers & KeyModifiers.Shift) != 0)
                    {
                        _viewModel.Actions.SaveSceneAsCommand.Execute(null);
                    }
                    else
                    {
                        _viewModel.Actions.SaveSceneCommand.Execute(null);
                    }
                    e.Handled = true;
                    return;
                case Key.Y:
                    _viewModel.Actions.RedoCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        switch (key)
        {
            case Key.Escape:
                _viewModel.Actions.ClearSelectionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F:
                _viewModel.Actions.ZoomExtentsCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.G:
                _optionsViewModel.ToggleGridCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Q:
                _optionsViewModel.SetSelectionToolCommand.Execute(0);
                e.Handled = true;
                break;
            case Key.B:
                _optionsViewModel.SetSelectionToolCommand.Execute(1);
                e.Handled = true;
                break;
            case Key.V:
                _optionsViewModel.SetSelectionToolCommand.Execute(2);
                e.Handled = true;
                break;
            case Key.A:
                _optionsViewModel.SetSelectionToolCommand.Execute(3);
                e.Handled = true;
                break;
            case Key.X:
                _optionsViewModel.ToggleSelectionCrossingCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Z:
                _optionsViewModel.ToggleWireframeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D:
                _optionsViewModel.ToggleDepthCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.L:
                _optionsViewModel.ToggleLightingCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.S:
                _optionsViewModel.ToggleGizmoSnapCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.P:
                _optionsViewModel.TogglePauseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.W:
                _optionsViewModel.SetGizmoModeCommand.Execute(0);
                e.Handled = true;
                break;
            case Key.E:
                _optionsViewModel.SetGizmoModeCommand.Execute(1);
                e.Handled = true;
                break;
            case Key.R:
                _optionsViewModel.SetGizmoModeCommand.Execute(2);
                e.Handled = true;
                break;
            case Key.I:
                _viewModel.Actions.ToggleCommandPanelCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D1:
            case Key.NumPad1:
                _optionsViewModel.SetSelectionModeCommand.Execute(0);
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                _optionsViewModel.SetSelectionModeCommand.Execute(1);
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                _optionsViewModel.SetSelectionModeCommand.Execute(3);
                e.Handled = true;
                break;
            case Key.D4:
            case Key.NumPad4:
                _optionsViewModel.SetSelectionModeCommand.Execute(2);
                e.Handled = true;
                break;
        }
    }


    private void ClearSelection()
    {
        _editor.Selection.ClearAll();
        OnEditorSelectionChanged();
    }

    private void OnEditorSelectionChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnEditorSelectionChanged);
            return;
        }

        _viewportManager.UpdateSelectedNodeFromEditor();
        _editorViewModel.RefreshAll();
        _commandStateService.Refresh();
        _hierarchyService.Refresh();
        _constraintService.Refresh();
        _motionService.Refresh();
        _viewportManager.InvalidateAll();
    }

    private void OnEditorCommandStateChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnEditorCommandStateChanged);
            return;
        }

        _editorViewModel.RefreshAll();
        _commandStateService.Refresh();
        _hierarchyService.Refresh();
        _constraintService.Refresh();
        _motionService.Refresh();
    }

    private void OnEditorMeshEdited()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnEditorMeshEdited);
            return;
        }

        _viewportManager.InvalidateAll();
    }

    private void ZoomExtents()
    {
        _viewportManager.ActiveViewport.ZoomToExtents();
        _viewportManager.ActiveViewport.Invalidate();
    }

    private void IsolateSelection()
    {
        var node = _viewportManager.ActiveViewport.SelectedNode;
        if (node == null)
        {
            return;
        }

        if (_isolateActive)
        {
            RestoreVisibilitySnapshot();
        }

        var visibleNodes = new HashSet<SceneNode>();
        CollectNodeAndChildren(node, visibleNodes);

        _visibilitySnapshot.Clear();
        foreach (var current in EnumerateSceneNodes(_sceneGraph))
        {
            var renderer = current.MeshRenderer;
            if (renderer == null)
            {
                continue;
            }

            _visibilitySnapshot[current] = renderer.IsVisible;
            renderer.IsVisible = visibleNodes.Contains(current);
        }

        _isolateActive = true;
        _viewportManager.InvalidateAll();
        _hierarchyService.Refresh();
    }

    private void UnhideAll()
    {
        if (_isolateActive)
        {
            RestoreVisibilitySnapshot();
            _viewportManager.InvalidateAll();
            _hierarchyService.Refresh();
            return;
        }

        foreach (var current in EnumerateSceneNodes(_sceneGraph))
        {
            var renderer = current.MeshRenderer;
            if (renderer != null)
            {
                renderer.IsVisible = true;
            }
        }

        _viewportManager.InvalidateAll();
        _hierarchyService.Refresh();
    }

    private async Task RenameSelectionAsync()
    {
        var node = _viewportManager.ActiveViewport.SelectedNode;
        if (node == null)
        {
            return;
        }

        var newName = await PromptRenameAsync(node.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        newName = newName.Trim();
        if (string.Equals(newName, node.Name, StringComparison.Ordinal))
        {
            return;
        }

        node.Name = newName;
        _hierarchyService.UpdateNodeName(node);
        _hierarchyService.Refresh();
    }

    private async Task<string?> PromptRenameAsync(string currentName)
    {
        var nameBox = new TextBox
        {
            Text = currentName,
            MinWidth = 220
        };

        var okButton = new Button
        {
            Content = "Rename",
            Width = 80
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };

        var buttonRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        buttonRow.Children.Add(okButton);
        buttonRow.Children.Add(cancelButton);

        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Avalonia.Thickness(12)
        };
        panel.Children.Add(new TextBlock { Text = "New name" });
        panel.Children.Add(nameBox);
        panel.Children.Add(buttonRow);

        var dialog = new Window
        {
            Title = "Rename Node",
            Width = 320,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        okButton.Click += (_, _) => dialog.Close(nameBox.Text?.Trim());
        cancelButton.Click += (_, _) => dialog.Close(null);

        return await dialog.ShowDialog<string?>(this);
    }

    private void RestoreVisibilitySnapshot()
    {
        foreach (var (node, visible) in _visibilitySnapshot)
        {
            var renderer = node.MeshRenderer;
            if (renderer != null)
            {
                renderer.IsVisible = visible;
            }
        }

        _visibilitySnapshot.Clear();
        _isolateActive = false;
    }

    private static void CollectNodeAndChildren(SceneNode node, HashSet<SceneNode> nodes)
    {
        if (!nodes.Add(node))
        {
            return;
        }

        foreach (var child in node.Children)
        {
            CollectNodeAndChildren(child, nodes);
        }
    }

    private static IEnumerable<SceneNode> EnumerateSceneNodes(SceneGraph scene)
    {
        foreach (var root in scene.Roots)
        {
            foreach (var node in EnumerateSceneNodes(root))
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<SceneNode> EnumerateSceneNodes(SceneNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateSceneNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private void ClearSceneAndInvalidate()
    {
        ClearScene();
        _viewportManager.InvalidateAll();
    }

    private void Undo()
    {
        if (_editor.MeshEdits.Undo())
        {
            _editorViewModel.RefreshAll();
            _commandStateService.Refresh();
            _hierarchyService.Refresh();
            _constraintService.Refresh();
            _motionService.Refresh();
            _viewportManager.InvalidateAll();
        }
    }

    private void Redo()
    {
        if (_editor.MeshEdits.Redo())
        {
            _editorViewModel.RefreshAll();
            _commandStateService.Refresh();
            _hierarchyService.Refresh();
            _constraintService.Refresh();
            _motionService.Refresh();
            _viewportManager.InvalidateAll();
        }
    }

    private void CenterPivot()
    {
        if (_editor.MeshEdits.CenterPivot())
        {
            _viewportManager.InvalidateAll();
        }
    }

    private void ResetTransform()
    {
        if (_editor.MeshEdits.ResetTransform())
        {
            _viewportManager.InvalidateAll();
        }
    }

    private void AnimationReset()
    {
        _motionService.Reset();
    }

    private void NudgePosX()
    {
        _editor.MeshEdits.ApplyTransformEdit(Matrix4x4.CreateTranslation(new Vector3(_editor.MeshEdits.NudgeStep, 0f, 0f)), "Nudge +X");
        _viewportManager.InvalidateAll();
    }

    private void NudgeNegX()
    {
        _editor.MeshEdits.ApplyTransformEdit(Matrix4x4.CreateTranslation(new Vector3(-_editor.MeshEdits.NudgeStep, 0f, 0f)), "Nudge -X");
        _viewportManager.InvalidateAll();
    }

    private void ExtrudeFaces()
    {
        _editor.MeshEdits.ExtrudeFaces();
    }

    private void BevelFaces()
    {
        _editor.MeshEdits.BevelFaces();
    }

    private void InsetFaces()
    {
        _editor.MeshEdits.InsetFaces();
    }

    private void SplitEdge()
    {
        _editor.MeshEdits.SplitEdge();
    }

    private void BridgeEdges()
    {
        _editor.MeshEdits.BridgePickedEdges();
    }

    private void BridgeEdgeLoops()
    {
        _editor.MeshEdits.BridgeEdgeLoops();
    }

    private void MergeVertices()
    {
        _editor.MeshEdits.MergeVertices();
    }

    private void DissolveFaces()
    {
        _editor.MeshEdits.DissolveFaces();
    }

    private void DissolveEdge()
    {
        _editor.MeshEdits.DissolvePickedEdge();
    }

    private void CollapseEdge()
    {
        _editor.MeshEdits.CollapsePickedEdge();
    }

    private void LoopCutEdgeLoop()
    {
        _editor.MeshEdits.LoopCutEdgeLoop();
    }

    private void WeldVertices()
    {
        _editor.MeshEdits.WeldVertices();
    }

    private void CleanupMesh()
    {
        _editor.MeshEdits.CleanupMesh();
    }

    private void SmoothMesh()
    {
        _editor.MeshEdits.SmoothMesh();
    }

    private void SimplifyMesh()
    {
        _editor.MeshEdits.SimplifyMesh();
    }

    private void PlanarUv()
    {
        _editor.MeshEdits.PlanarUv();
    }

    private void BoxUv()
    {
        _editor.MeshEdits.BoxUv();
    }

    private void NormalizeUv()
    {
        _editor.MeshEdits.NormalizeUv();
    }

    private void FlipU()
    {
        _editor.MeshEdits.FlipU();
    }

    private void FlipV()
    {
        _editor.MeshEdits.FlipV();
    }

    private void UnwrapUv()
    {
        _editor.MeshEdits.UnwrapUv();
    }

    private void PackUv()
    {
        _editor.MeshEdits.PackUv();
    }

    private void MarkUvSeams()
    {
        _editor.MeshEdits.MarkUvSeams();
    }

    private void ClearUvSeams()
    {
        _editor.MeshEdits.ClearUvSeams();
    }

    private void ClearAllUvSeams()
    {
        _editor.MeshEdits.ClearAllUvSeams();
    }

    private void SelectUvIsland()
    {
        if (_editor.MeshEdits.SelectUvIsland())
        {
            _optionsService.SyncEditModeToggles();
        }
    }

    private void AssignUvGroup(int groupId)
    {
        _editor.MeshEdits.AssignUvGroup(groupId);
    }

    private void ClearUvGroup()
    {
        _editor.MeshEdits.ClearUvGroup();
    }

    private void SelectEdgeLoop()
    {
        if (_editor.MeshEdits.SelectEdgeLoop())
        {
            _optionsService.SyncEditModeToggles();
        }
    }

    private void SelectEdgeRing()
    {
        if (_editor.MeshEdits.SelectEdgeRing())
        {
            _optionsService.SyncEditModeToggles();
        }
    }

    private void ClearScene()
    {
        _renderer.ClearDecals();
        _renderer.ClearProjectedPaths();
        _renderer.ClearOverlays();

        _groundLabelAtlas?.Dispose();
        _groundLabelAtlas = null;
        _groundNode = null;
        _cubeNode = null;
        _lightNode = null;
        _visibilitySnapshot.Clear();
        _isolateActive = false;
        _scenePath = null;

        _sceneGraph = new SceneGraph();
        for (int i = 0; i < _viewportManager.Viewports.Count; i++)
        {
            _viewportManager.Viewports[i].SceneGraph = _sceneGraph;
        }
        _userNodes.Clear();
        _editor.Document.Clear();
        _viewportManager.ResetState();
        _viewportService.SpinNode = null;
        _editor.Selection.ClearAll();
        _editor.MeshEdits.ClearCommands();
        OnEditorSelectionChanged();

        _orbit.Target = Vector3.Zero;
        _orbit.Radius = 8f;
        _orbit.Yaw = -0.7f;
        _orbit.Pitch = -0.5f;
        _orbit.UpdateCamera();

        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
    }

    private async Task OpenSceneAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Scene",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Skia3D Scene") { Patterns = new List<string> { "*.skia3d" } },
                new FilePickerFileType("JSON") { Patterns = new List<string> { "*.json" } },
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
            string json;
            var localPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                json = await File.ReadAllTextAsync(localPath);
                _scenePath = localPath;
            }
            else
            {
                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync();
                _scenePath = null;
            }

            var scene = ScenePackageSerializer.FromJson(json, out var assets);

            _renderer.ClearDecals();
            _renderer.ClearProjectedPaths();
            _renderer.ClearOverlays();
            _groundLabelAtlas?.Dispose();
            _groundLabelAtlas = null;
            _groundNode = null;
            _cubeNode = null;
            _lightNode = null;
            _visibilitySnapshot.Clear();
            _isolateActive = false;

            _sceneGraph = scene;
            for (int i = 0; i < _viewportManager.Viewports.Count; i++)
            {
                _viewportManager.Viewports[i].SceneGraph = _sceneGraph;
            }

            _userNodes.Clear();
            _editor.Document.Clear();
            foreach (var root in _sceneGraph.Roots)
            {
                RegisterSceneRecursive(root);
            }

            ApplyMeshMetadata(assets);

            _editor.Selection.ClearAll();
            _editor.MeshEdits.ClearCommands();
            _viewportService.AnimationMixer = null;
            _viewportService.AnimationPlayer = null;
            _viewportService.SpinNode = null;

            OnEditorSelectionChanged();
            _hierarchyService.Rebuild(_sceneGraph);
            _constraintService.Rebuild(_sceneGraph);
            _viewportManager.UpdateSceneLights();
            _viewportManager.InvalidateAll();
        }
        catch (Exception ex)
        {
            await new Window { Content = new TextBlock { Text = $"Failed to open scene: {ex.Message}" }, Width = 420, Height = 120 }.ShowDialog(this);
        }
    }

    private async Task SaveSceneAsync()
    {
        if (string.IsNullOrWhiteSpace(_scenePath))
        {
            await SaveSceneAsAsync();
            return;
        }

        await SaveSceneToPathAsync(_scenePath);
    }

    private async Task SaveSceneAsAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Scene",
            SuggestedFileName = "scene.skia3d",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Skia3D Scene") { Patterns = new List<string> { "*.skia3d" } },
                new FilePickerFileType("JSON") { Patterns = new List<string> { "*.json" } }
            }
        });

        if (file is null)
        {
            return;
        }

        var json = BuildSceneJson();
        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            await File.WriteAllTextAsync(localPath, json);
            _scenePath = localPath;
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
        await writer.FlushAsync();
        _scenePath = null;
    }

    private async Task SaveSceneToPathAsync(string path)
    {
        var json = BuildSceneJson();
        await File.WriteAllTextAsync(path, json);
    }

    private string BuildSceneJson()
    {
        var edits = BuildMeshEditData();
        return ScenePackageSerializer.ToJson(_sceneGraph, mesh => edits.TryGetValue(mesh, out var data) ? data : null);
    }

    private Dictionary<Mesh, MeshEditData> BuildMeshEditData()
    {
        var edits = new Dictionary<Mesh, MeshEditData>();
        foreach (var pair in _editor.Document.EditableMeshes)
        {
            var mesh = pair.Key.Mesh;
            if (mesh == null)
            {
                continue;
            }

            var editable = pair.Value;
            EdgeData[]? seams = null;
            if (editable.SeamEdges.Count > 0)
            {
                seams = new EdgeData[editable.SeamEdges.Count];
                int index = 0;
                foreach (var edge in editable.SeamEdges)
                {
                    seams[index++] = new EdgeData(edge.A, edge.B);
                }
            }

            int[]? faceGroups = null;
            if (editable.UvFaceGroups.Count > 0)
            {
                faceGroups = editable.UvFaceGroups.ToArray();
            }

            edits[mesh] = new MeshEditData
            {
                SeamEdges = seams,
                UvFaceGroups = faceGroups
            };
        }

        return edits;
    }

    private void ApplyMeshMetadata(ScenePackageAssetLibrary assets)
    {
        foreach (var pair in _editor.Document.EditableMeshes)
        {
            var mesh = pair.Key.Mesh;
            if (mesh == null)
            {
                continue;
            }

            if (!assets.MeshMetadata.TryGetValue(mesh, out var data))
            {
                continue;
            }

            var editable = pair.Value;
            if (data.SeamEdges != null)
            {
                editable.SeamEdges.Clear();
                foreach (var edge in data.SeamEdges)
                {
                    editable.SeamEdges.Add(new EdgeKey(edge.A, edge.B));
                }
            }

            if (data.UvFaceGroups != null && data.UvFaceGroups.Length == editable.TriangleCount)
            {
                editable.UvFaceGroups.Clear();
                editable.UvFaceGroups.AddRange(data.UvFaceGroups);
            }

            editable.EnsureUvFaceGroups();
        }
    }

    private async Task LoadObjAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Mesh",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Mesh files") { Patterns = new List<string> { "*.obj", "*.ply", "*.gltf", "*.glb" } },
                new FilePickerFileType("OBJ") { Patterns = new List<string> { "*.obj" } },
                new FilePickerFileType("PLY") { Patterns = new List<string> { "*.ply" } },
                new FilePickerFileType("glTF") { Patterns = new List<string> { "*.gltf", "*.glb" } },
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
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            _optionsService.ApplyImportOptions();
            var processing = _optionsService.BuildImportOptions();

            var meshOptions = new MeshLoadOptions
            {
                DefaultColor = new SKColor(200, 200, 200),
                GenerateNormals = true,
                Processing = processing
            };
            var localPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                meshOptions.SourcePath = localPath;
            }
            var extension = Path.GetExtension(file.Name);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".obj";
            }

            var sceneOptions = new SceneLoadOptions
            {
                MeshOptions = meshOptions,
                LoadMaterials = true,
                LoadAnimations = true
            };

            var import = SceneIo.Load(stream, extension, sceneOptions);
            var root = new SceneNode($"Import: {file.Name}");
            foreach (var node in import.Scene.Roots)
            {
                root.AddChild(node);
            }

            _sceneGraph.AddRoot(root);
            _userNodes.Add(root);
            RegisterSceneRecursive(root);

            _editor.Selection.ClearAll();
            _editor.MeshEdits.ClearCommands();
            var selected = FindFirstInstance(root);
            if (selected != null)
            {
                _editor.Selection.ObjectSelection.Add(selected);
                _editor.Selection.Selected = selected;
            }

            _viewportService.AnimationMixer = null;
            _viewportService.AnimationPlayer = null;
            _viewportService.SpinNode = null;
            if (import.Animations.Count > 0)
            {
                var clip = import.Animations[0];
                _viewportService.AnimationPlayer = new AnimationPlayer(clip.Bind(_sceneGraph)) { Loop = true, IsPlaying = true };
            }

            _scenePath = null;
            OnEditorSelectionChanged();
            _hierarchyService.Rebuild(_sceneGraph);
            _constraintService.Rebuild(_sceneGraph);
            _viewportManager.InvalidateAll();
        }
        catch (Exception ex)
        {
            await new Window { Content = new TextBlock { Text = $"Failed to load scene: {ex.Message}" }, Width = 400, Height = 120 }.ShowDialog(this);
        }
    }

    private void RebuildSampleScene(int meshSegments)
    {
        BuildScene(meshSegments);
        SetupGroundDecal();
        SetupProjectedPathSample();
        _scenePath = null;
        _editor.Selection.ClearAll();
        _editor.MeshEdits.ClearCommands();
        OnEditorSelectionChanged();
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.InvalidateAll();
    }
}
