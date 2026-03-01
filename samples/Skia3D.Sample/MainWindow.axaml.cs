using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Skia3D.Animation;
using Skia3D.Audio;
using Skia3D.Core;
using Skia3D.Editor;
using Skia3D.Geometry;
using Skia3D.IO;
using Skia3D.Modeling;
using Skia3D.Navigation;
using Skia3D.Physics;
using Skia3D.Scene;
using Skia3D.Sample.Services;
using Skia3D.Sample.ViewModels;
using Skia3D.Sample.Controls;
using Skia3D.Vfx;
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
    private readonly VfxPanelService _vfxService;
    private readonly PhysicsPanelService _physicsService;
    private readonly NavigationPanelService _navigationPanelService;
    private readonly AudioPanelService _audioPanelService;
    private readonly InspectorOptionsViewModel _optionsViewModel;
    private readonly StatusHintService _statusHintService;
    private EditorViewportControl? _viewportControlPerspective;
    private EditorViewportControl? _viewportControlTop;
    private EditorViewportControl? _viewportControlFront;
    private EditorViewportControl? _viewportControlLeft;
    private Popup? _quadMenuPopup;
    private bool _viewportAttached;
    private bool _viewportAttachScheduled;
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
    private NavGridComponent? _navGridComponent;
    private int _flowCounter;
    private int _idleCounter;
    private int _groupCounter;
    private int _createCounter;
    private readonly List<Texture2D> _loadedTextures = new();
    private Window? _materialGraphWindow;
    private Window? _scriptConsoleWindow;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(_editor);
        _editorViewModel = _viewModel.Editor;
        _optionsViewModel = _viewModel.CommandPanel.Options;
        DataContext = _viewModel;
        _viewModel.DockLayout.PropertyChanged += OnDockLayoutPropertyChanged;
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

        _viewportService = new EditorViewportService(_editor, _sceneGraph, _renderer, _camera, _orbit, _editorViewModel, _viewModel.StatusBar, enableSubsystems: true);
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
        _viewModel.ViewportContext.PerspectiveViewport = _viewportService;
        _viewModel.ViewportContext.TopViewport = _viewportTopService;
        _viewModel.ViewportContext.FrontViewport = _viewportFrontService;
        _viewModel.ViewportContext.LeftViewport = _viewportLeftService;

        _hierarchyService = new HierarchyPanelService(_editor, _viewportService, _viewModel.CommandPanel.Hierarchy);
        _hierarchyService.SelectionApplied += OnEditorSelectionChanged;
        _commandStateService = new CommandStateService(_editor, _viewModel.CommandPanel.Commands);
        _motionService = new MotionPanelService(_viewportService, _viewModel.CommandPanel.Motion);
        _optionsService = new EditorOptionsService(_editor, _renderer, _viewportManager, _optionsViewModel);
        _materialGraphService = new MaterialGraphService(_editor, _viewModel.Material);
        _constraintService = new ConstraintPanelService(_editor, _viewportService, _viewModel.CommandPanel.Constraints);
        _vfxService = new VfxPanelService(_viewportService, _viewportManager, _viewModel.CommandPanel.Vfx);
        _physicsService = new PhysicsPanelService(_viewportService, _viewportManager, _viewModel.CommandPanel.Physics);
        _navigationPanelService = new NavigationPanelService(_viewportService, _viewportManager, _viewModel.CommandPanel.Navigation);
        _audioPanelService = new AudioPanelService(_viewportService, _viewportManager, _viewModel.CommandPanel.Audio);
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
            SelectAll = SelectAll,
            InvertSelection = InvertSelection,
            ClearScene = ClearSceneAndInvalidate,
            GroupSelection = GroupSelection,
            UngroupSelection = UngroupSelection,
            CreateCube = CreateCube,
            CreateSphere = CreateSphere,
            CreateCylinder = CreateCylinder,
            CreatePyramid = CreatePyramid,
            CreatePlane = CreatePlane,
            CreateGrid = CreateGrid,
            CreateDirectionalLight = CreateDirectionalLight,
            CreatePointLight = CreatePointLight,
            CreateSpotLight = CreateSpotLight,
            CreateCamera = CreateCamera,
            CreateNavGrid = CreateNavGrid,
            CreateFlow = CreateFlow,
            CreateIdleArea = CreateIdleArea,
            ClearNavigation = ClearNavigation,
            OpenMaterialGraph = OpenMaterialGraph,
            OpenScriptConsole = OpenScriptConsole,
            AssignTextureAsync = AssignTextureAsync,
            AssignCheckerTexture = AssignCheckerTexture,
            ClearTextures = ClearTextures,
            Undo = Undo,
            Redo = Redo,
            ClearSelection = ClearSelection,
            DeleteSelection = DeleteSelection,
            DuplicateSelection = DuplicateSelection,
            DetachFaces = DetachFaces,
            ConvertSelectionToVertices = ConvertSelectionToVertices,
            ConvertSelectionToEdges = ConvertSelectionToEdges,
            ConvertSelectionToFaces = ConvertSelectionToFaces,
            TransformSelectionAsync = TransformSelectionAsync,
            SelectEdgeLoop = SelectEdgeLoop,
            SelectEdgeRing = SelectEdgeRing,
            GrowSelection = GrowSelection,
            ShrinkSelection = ShrinkSelection,
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
            AnimationReset = AnimationReset,
            ClearAnimationKeys = ClearAnimationKeys,
            ShowAbout = ShowAbout
        });

        BuildScene(DefaultMeshSegments);
        SetupGroundDecal();
        SetupProjectedPathSample();

        ScheduleViewportAttach();
        AttachControls();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.DockLayout.PropertyChanged -= OnDockLayoutPropertyChanged;
        _viewModel.DockLayout.Dispose();
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
        _vfxService.Dispose();
        _physicsService.Dispose();
        _navigationPanelService.Dispose();
        _audioPanelService.Dispose();
        _statusHintService.Dispose();
        _viewportManager.Dispose();
        _viewportService.Dispose();
        _viewportTopService.Dispose();
        _viewportFrontService.Dispose();
        _viewportLeftService.Dispose();
        if (_materialGraphWindow != null)
        {
            _materialGraphWindow.Close();
            _materialGraphWindow = null;
        }
        if (_scriptConsoleWindow != null)
        {
            _scriptConsoleWindow.Close();
            _scriptConsoleWindow = null;
        }
        foreach (var texture in _loadedTextures)
        {
            texture.Dispose();
        }
        _loadedTextures.Clear();
        _checkerTexture?.Dispose();
    }

    private void AttachViewport()
    {
        if (_viewportAttached)
        {
            return;
        }

        if (!TryAttachViewport())
        {
            ScheduleViewportAttach();
        }
    }

    private bool TryAttachViewport()
    {
        if (!TryResolveViewports(out var perspective, out var top, out var front, out var left))
        {
            return false;
        }

        DetachViewportHandlers();

        _viewportControlPerspective = perspective;
        _viewportControlTop = top;
        _viewportControlFront = front;
        _viewportControlLeft = left;
        _quadMenuPopup = FindQuadMenuPopup();

        _viewportControlPerspective!.QuadMenuRequested += OnQuadMenuRequested;
        _viewportControlTop!.QuadMenuRequested += OnQuadMenuRequested;
        _viewportControlFront!.QuadMenuRequested += OnQuadMenuRequested;
        _viewportControlLeft!.QuadMenuRequested += OnQuadMenuRequested;

        _viewportAttached = true;
        return true;
    }

    private bool TryResolveViewports(out EditorViewportControl? perspective,
        out EditorViewportControl? top,
        out EditorViewportControl? front,
        out EditorViewportControl? left)
    {
        perspective = null;
        top = null;
        front = null;
        left = null;

        var viewportDock = this.GetVisualDescendants().OfType<ViewportDockControl>().FirstOrDefault();
        if (viewportDock != null)
        {
            perspective = viewportDock.FindControl<EditorViewportControl>("ViewportPerspective");
            top = viewportDock.FindControl<EditorViewportControl>("ViewportTop");
            front = viewportDock.FindControl<EditorViewportControl>("ViewportFront");
            left = viewportDock.FindControl<EditorViewportControl>("ViewportLeft");
            if (perspective != null && top != null && front != null && left != null)
            {
                return true;
            }
        }

        var viewports = this.GetVisualDescendants().OfType<EditorViewportControl>().ToList();
        if (viewports.Count == 0)
        {
            return false;
        }

        perspective = FindViewport(viewports, "ViewportPerspective");
        top = FindViewport(viewports, "ViewportTop");
        front = FindViewport(viewports, "ViewportFront");
        left = FindViewport(viewports, "ViewportLeft");
        return perspective != null && top != null && front != null && left != null;
    }

    private Popup? FindQuadMenuPopup()
    {
        var viewportDock = this.GetVisualDescendants().OfType<ViewportDockControl>().FirstOrDefault();
        if (viewportDock != null)
        {
            return viewportDock.FindControl<Popup>("QuadMenuPopup");
        }

        return this.GetVisualDescendants().OfType<Popup>()
            .FirstOrDefault(popup => string.Equals(popup.Name, "QuadMenuPopup", StringComparison.Ordinal));
    }

    private void ScheduleViewportAttach()
    {
        if (_viewportAttached || _viewportAttachScheduled)
        {
            return;
        }

        _viewportAttachScheduled = true;
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(100);
            _viewportAttachScheduled = false;
            AttachViewport();
        }, DispatcherPriority.Background);
    }

    private void DetachViewportHandlers()
    {
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
    }

    private static EditorViewportControl? FindViewport(IEnumerable<EditorViewportControl> viewports, string name)
    {
        return viewports.FirstOrDefault(viewport => string.Equals(viewport.Name, name, StringComparison.Ordinal));
    }

    private void OnDockLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorDockLayoutViewModel.Layout))
        {
            _viewportAttached = false;
            ScheduleViewportAttach();
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

    private void ResetSubsystemState()
    {
        _navGridComponent = null;
        _flowCounter = 0;
        _idleCounter = 0;
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
        ResetSubsystemState();

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

    private NavGridComponent EnsureNavGrid()
    {
        if (_navGridComponent != null)
        {
            return _navGridComponent;
        }

        var originY = _groundNode?.Transform.LocalPosition.Y ?? 0f;
        var origin = new Vector3(-6f, originY, -6f);
        var grid = new NavGrid(12, 12, 1f, origin);

        if (_groundNode != null)
        {
            _navGridComponent = _groundNode.GetComponent<NavGridComponent>();
            if (_navGridComponent == null)
            {
                _navGridComponent = _groundNode.AddComponent(new NavGridComponent(grid));
            }

            return _navGridComponent;
        }

        var navNode = new SceneNode("Nav Grid");
        _navGridComponent = navNode.AddComponent(new NavGridComponent(grid));
        _sceneGraph.AddRoot(navNode);
        _userNodes.Add(navNode);
        return _navGridComponent;
    }

    private void CreateNavGrid()
    {
        EnsureNavGrid();
        _scenePath = null;
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.InvalidateAll();
    }

    private void CreateCube()
    {
        var position = GetNextSpawnPosition();
        var color = new SKColor(90, 168, 255);
        var mesh = MeshFactory.CreateCube(1.6f, color);
        var material = BuildStandardMaterial(color);
        CreatePrimitiveNode(NextName("Cube"), mesh, material, position);
    }

    private void CreateSphere()
    {
        var position = GetNextSpawnPosition();
        var color = new SKColor(80, 220, 180);
        var segments = _optionsService.GetMeshSegments(DefaultMeshSegments);
        var mesh = MeshFactory.CreateSphere(1.2f, Math.Max(8, segments), Math.Max(6, segments / 2), color);
        var material = BuildStandardMaterial(color);
        CreatePrimitiveNode(NextName("Sphere"), mesh, material, position);
    }

    private void CreateCylinder()
    {
        var position = GetNextSpawnPosition();
        var color = new SKColor(220, 200, 120);
        var segments = _optionsService.GetMeshSegments(DefaultMeshSegments);
        var mesh = MeshFactory.CreateCylinder(0.8f, 2.0f, Math.Max(8, segments), color);
        var material = BuildStandardMaterial(color);
        CreatePrimitiveNode(NextName("Cylinder"), mesh, material, position);
    }

    private void CreatePyramid()
    {
        var position = GetNextSpawnPosition();
        var color = new SKColor(255, 140, 120);
        var mesh = MeshFactory.CreatePyramid(1.6f, 2.2f, color);
        var material = BuildStandardMaterial(color);
        CreatePrimitiveNode(NextName("Pyramid"), mesh, material, position);
    }

    private void CreatePlane()
    {
        var position = GetNextSpawnPosition();
        var color = new SKColor(120, 200, 140);
        var mesh = MeshFactory.CreatePlane(3.2f, color);
        var material = BuildStandardMaterial(color);
        material.DoubleSided = true;
        CreatePrimitiveNode(NextName("Plane"), mesh, material, position);
    }

    private void CreateGrid()
    {
        var position = GetNextSpawnPosition();
        var color = new SKColor(140, 155, 175);
        var segments = _optionsService.GetMeshSegments(DefaultMeshSegments);
        var mesh = MeshFactory.CreateGrid(Math.Max(6, segments), 6f, color, twoSided: true);
        var material = BuildStandardMaterial(color);
        material.DoubleSided = true;
        CreatePrimitiveNode(NextName("Grid"), mesh, material, position);
    }

    private void CreateDirectionalLight()
    {
        var position = GetNextSpawnPosition() + new Vector3(0f, 2.5f, 0f);
        var color = new SKColor(255, 244, 214);
        var light = Light.Directional(Vector3.Normalize(new Vector3(-0.4f, -1f, -0.6f)), color, 1f);
        CreateLightNode(NextName("Directional Light"), light, color, position);
    }

    private void CreatePointLight()
    {
        var position = GetNextSpawnPosition() + new Vector3(0f, 2.0f, 0f);
        var color = new SKColor(255, 210, 140);
        var light = Light.Point(position, color, 1f, 8f);
        CreateLightNode(NextName("Point Light"), light, color, position);
    }

    private void CreateSpotLight()
    {
        var position = GetNextSpawnPosition() + new Vector3(0f, 2.8f, 0f);
        var color = new SKColor(160, 210, 255);
        var light = Light.Spot(position, Vector3.Normalize(new Vector3(0f, -1f, 0f)), color, 1f, 10f, 0.35f, 0.7f);
        CreateLightNode(NextName("Spot Light"), light, color, position);
    }

    private void CreateCamera()
    {
        var position = GetNextSpawnPosition() + new Vector3(0f, 1.6f, 2.5f);
        var color = new SKColor(200, 200, 200);
        var mesh = MeshFactory.CreatePyramid(0.6f, 0.8f, color);
        var material = BuildHelperMaterial(color);
        var camera = new Camera
        {
            Position = position,
            Target = Vector3.Zero
        };

        var node = new SceneNode(NextName("Camera"))
        {
            MeshRenderer = new MeshRenderer(mesh, material),
            Camera = new CameraComponent(camera)
        };
        node.Transform.LocalPosition = position;
        _sceneGraph.AddRoot(node);
        _userNodes.Add(node);
        RegisterEditable(node);

        _scenePath = null;
        SelectNode(node);
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.UpdateSceneLights();
        _viewportManager.InvalidateAll();
    }

    private SceneNode CreatePrimitiveNode(string name, Mesh mesh, Material material, Vector3 position)
    {
        var node = new SceneNode(name)
        {
            MeshRenderer = new MeshRenderer(mesh, material)
        };
        node.Transform.LocalPosition = position;
        _sceneGraph.AddRoot(node);
        _userNodes.Add(node);
        RegisterEditable(node);

        _scenePath = null;
        SelectNode(node);
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.UpdateSceneLights();
        _viewportManager.InvalidateAll();
        return node;
    }

    private SceneNode CreateLightNode(string name, Light light, SKColor color, Vector3 position)
    {
        var mesh = MeshFactory.CreateSphere(0.25f, 12, 8, color);
        var material = BuildHelperMaterial(color);

        var node = new SceneNode(name)
        {
            MeshRenderer = new MeshRenderer(mesh, material),
            Light = new LightComponent(light)
        };
        node.Transform.LocalPosition = position;
        _sceneGraph.AddRoot(node);
        _userNodes.Add(node);
        RegisterEditable(node);

        _scenePath = null;
        SelectNode(node);
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.UpdateSceneLights();
        _viewportManager.InvalidateAll();
        return node;
    }

    private static Material BuildStandardMaterial(SKColor color)
    {
        return new Material
        {
            BaseColor = color,
            Ambient = 0.2f,
            Diffuse = 0.85f,
            ShadingModel = MaterialShadingModel.MetallicRoughness,
            Metallic = 0.3f,
            Roughness = 0.6f
        };
    }

    private static Material BuildHelperMaterial(SKColor color)
    {
        return new Material
        {
            BaseColor = color,
            Ambient = 0.25f,
            Diffuse = 0.9f,
            EmissiveColor = color,
            EmissiveStrength = 0.35f,
            ShadingModel = MaterialShadingModel.MetallicRoughness,
            Metallic = 0.1f,
            Roughness = 0.4f
        };
    }

    private Vector3 GetNextSpawnPosition()
    {
        const float spacing = 2.6f;
        var index = _createCounter;
        var row = index / 4;
        var col = index % 4;
        return new Vector3((col - 1.5f) * spacing, 0f, -row * spacing);
    }

    private string NextName(string baseName)
    {
        _createCounter++;
        return $"{baseName} {_createCounter}";
    }

    private void CreateFlow()
    {
        var grid = EnsureNavGrid().Grid;
        var startCorner = _flowCounter % 2 == 0;
        var startCellX = startCorner ? 1 : grid.Width - 2;
        var startCellY = startCorner ? 1 : grid.Height - 2;
        var endCellX = startCorner ? grid.Width - 2 : 1;
        var endCellY = startCorner ? grid.Height - 2 : 1;

        var start = grid.CellToWorld(startCellX, startCellY, 0.2f);
        var destination = grid.CellToWorld(endCellX, endCellY, 0.2f);

        var node = new SceneNode($"Flow Agent {_flowCounter + 1}");
        node.Transform.LocalPosition = start;
        var mesh = MeshFactory.CreateSphere(0.35f, 14, 10, new SKColor(255, 186, 90));
        var material = new Material
        {
            BaseColor = new SKColor(255, 186, 90),
            Ambient = 0.2f,
            Diffuse = 0.85f,
            Metallic = 0.15f,
            Roughness = 0.6f
        };
        node.MeshRenderer = new MeshRenderer(mesh, material);

        var body = node.AddComponent(new RigidBodyComponent
        {
            BodyType = PhysicsBodyType.Kinematic,
            UseGravity = false
        });
        node.AddComponent(new ColliderComponent(new SphereShape(0.35f)) { Body = body });

        var agent = node.AddComponent(new NavAgentComponent
        {
            Speed = 1.6f,
            StoppingDistance = 0.2f
        });
        agent.SetDestination(destination);

        node.AddComponent(new ParticleEmitterComponent
        {
            EmissionRate = 18f,
            Lifetime = 1.1f,
            BaseVelocity = new Vector3(0f, 0.6f, 0f),
            VelocityRandomness = new Vector3(0.2f, 0.4f, 0.2f),
            StartSize = 0.18f,
            EndSize = 0.05f,
            StartColor = new SKColor(255, 210, 140, 180),
            EndColor = new SKColor(255, 120, 60, 0),
            WorldSpace = true
        });

        var source = node.AddComponent(new AudioSourceComponent
        {
            Clip = new AudioClip($"FlowTone {_flowCounter + 1}", 1f),
            Loop = true,
            Volume = 0.3f
        });
        source.Play();

        _sceneGraph.AddRoot(node);
        _userNodes.Add(node);
        RegisterEditable(node);

        _flowCounter++;
        _scenePath = null;
        SelectNode(node);
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.InvalidateAll();
    }

    private void CreateIdleArea()
    {
        var grid = EnsureNavGrid().Grid;
        var safeWidth = Math.Max(1, grid.Width - 4);
        var safeHeight = Math.Max(1, grid.Height - 4);
        var x = 2 + (_idleCounter * 2) % safeWidth;
        var y = 2 + (_idleCounter * 3) % safeHeight;
        var position = grid.CellToWorld(x, y, 0.05f);

        var node = new SceneNode($"Idle Area {_idleCounter + 1}");
        node.Transform.LocalPosition = position;
        var mesh = MeshFactory.CreateCylinder(0.7f, 0.12f, 18, new SKColor(120, 180, 255));
        var material = new Material
        {
            BaseColor = new SKColor(120, 180, 255),
            Ambient = 0.25f,
            Diffuse = 0.7f,
            Metallic = 0.1f,
            Roughness = 0.75f
        };
        node.MeshRenderer = new MeshRenderer(mesh, material);

        var body = node.AddComponent(new RigidBodyComponent { BodyType = PhysicsBodyType.Static });
        node.AddComponent(new ColliderComponent(new BoxShape(new Vector3(1.4f, 0.2f, 1.4f))) { Body = body });

        node.AddComponent(new ParticleEmitterComponent
        {
            EmissionRate = 6f,
            Lifetime = 1.8f,
            BaseVelocity = new Vector3(0f, 0.35f, 0f),
            VelocityRandomness = new Vector3(0.12f, 0.2f, 0.12f),
            StartSize = 0.22f,
            EndSize = 0.06f,
            StartColor = new SKColor(160, 210, 255, 160),
            EndColor = new SKColor(160, 210, 255, 0),
            WorldSpace = true
        });

        var source = node.AddComponent(new AudioSourceComponent
        {
            Clip = new AudioClip($"IdleTone {_idleCounter + 1}", 2f),
            Loop = true,
            Volume = 0.2f
        });
        source.Play();

        _sceneGraph.AddRoot(node);
        _userNodes.Add(node);
        RegisterEditable(node);

        _idleCounter++;
        _scenePath = null;
        SelectNode(node);
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.InvalidateAll();
    }

    private void ClearNavigation()
    {
        var toRemove = new List<SceneNode>();
        foreach (var node in EnumerateSceneNodes(_sceneGraph))
        {
            if (node.Name.StartsWith("Flow Agent ", StringComparison.Ordinal)
                || node.Name.StartsWith("Idle Area ", StringComparison.Ordinal))
            {
                toRemove.Add(node);
            }
        }

        foreach (var node in toRemove)
        {
            RemoveNode(node);
        }

        if (_navGridComponent != null)
        {
            var navNode = _navGridComponent.Node;
            if (navNode != null && !ReferenceEquals(navNode, _groundNode))
            {
                RemoveNode(navNode);
            }
            else if (navNode != null)
            {
                navNode.RemoveComponent(_navGridComponent);
            }

            _navGridComponent = null;
        }

        _flowCounter = 0;
        _idleCounter = 0;
        _scenePath = null;
        _editor.Selection.ClearAll();
        OnEditorSelectionChanged();
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.UpdateSceneLights();
        _viewportManager.InvalidateAll();
    }

    private void SelectNode(SceneNode node)
    {
        _editor.Selection.ClearAll();
        var instance = node.MeshInstance;
        if (instance != null)
        {
            _editor.Selection.ObjectSelection.Add(instance);
            _editor.Selection.Selected = instance;
        }

        OnEditorSelectionChanged();
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
            case Key.Delete:
                _viewModel.Actions.DeleteSelectionCommand.Execute(null);
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

    private bool TryGetEditableSelection(out MeshInstance? instance, out EditableMesh editable)
    {
        instance = _editor.Selection.Selected ?? _editor.Selection.GetFirstSelection();
        if (instance == null || !_editor.Document.TryGetEditableMesh(instance, out editable))
        {
            editable = null!;
            return false;
        }

        if (_editor.Selection.Selected == null)
        {
            _editor.Selection.Selected = instance;
        }

        return true;
    }

    private bool TrySelectAllSubSelection()
    {
        if (!_editor.Mode.EditMode)
        {
            return false;
        }

        if (!TryGetEditableSelection(out _, out var editable))
        {
            return false;
        }

        if (_editor.Mode.VertexSelect)
        {
            var vertices = new List<int>(editable.VertexCount);
            for (int i = 0; i < editable.VertexCount; i++)
            {
                vertices.Add(i);
            }

            _editor.Selection.VertexSelection.ReplaceWith(vertices);
            return true;
        }

        if (_editor.Mode.EdgeSelect)
        {
            _editor.Selection.EdgeSelection.ReplaceWith(GetAllEdges(editable));
            return true;
        }

        if (_editor.Mode.FaceSelect)
        {
            var faces = new List<int>(editable.TriangleCount);
            for (int i = 0; i < editable.TriangleCount; i++)
            {
                faces.Add(i);
            }

            _editor.Selection.FaceSelection.ReplaceWith(faces);
            return true;
        }

        return false;
    }

    private bool TryInvertSubSelection()
    {
        if (!_editor.Mode.EditMode)
        {
            return false;
        }

        if (!TryGetEditableSelection(out _, out var editable))
        {
            return false;
        }

        if (_editor.Mode.VertexSelect)
        {
            var vertices = new HashSet<int>();
            for (int i = 0; i < editable.VertexCount; i++)
            {
                vertices.Add(i);
            }

            vertices.ExceptWith(_editor.Selection.VertexSelection.Items);
            _editor.Selection.VertexSelection.ReplaceWith(vertices);
            return true;
        }

        if (_editor.Mode.EdgeSelect)
        {
            var edges = GetAllEdges(editable);
            edges.ExceptWith(_editor.Selection.EdgeSelection.Items);
            _editor.Selection.EdgeSelection.ReplaceWith(edges);
            return true;
        }

        if (_editor.Mode.FaceSelect)
        {
            var faces = new HashSet<int>();
            for (int i = 0; i < editable.TriangleCount; i++)
            {
                faces.Add(i);
            }

            faces.ExceptWith(_editor.Selection.FaceSelection.Items);
            _editor.Selection.FaceSelection.ReplaceWith(faces);
            return true;
        }

        return false;
    }

    private static HashSet<EdgeKey> GetAllEdges(EditableMesh editable)
    {
        var adjacency = MeshAdjacency.Build(editable);
        return adjacency.Edges.Count == 0
            ? new HashSet<EdgeKey>()
            : new HashSet<EdgeKey>(adjacency.Edges.Keys);
    }

    private void ClearSelection()
    {
        if (_editor.Mode.EditMode && (_editor.Mode.VertexSelect || _editor.Mode.EdgeSelect || _editor.Mode.FaceSelect))
        {
            _editor.Selection.ClearSubSelection();
            OnEditorSelectionChanged();
            return;
        }

        _editor.Selection.ClearAll();
        OnEditorSelectionChanged();
    }

    private void SelectAll()
    {
        if (TrySelectAllSubSelection())
        {
            OnEditorSelectionChanged();
            return;
        }

        MeshInstance? primary = null;
        foreach (var node in EnumerateSceneNodes(_sceneGraph))
        {
            var instance = node.MeshInstance;
            if (instance == null)
            {
                continue;
            }

            _editor.Selection.ObjectSelection.Add(instance);
            primary ??= instance;
        }

        _editor.Selection.Selected = primary;
        OnEditorSelectionChanged();
    }

    private void InvertSelection()
    {
        if (TryInvertSubSelection())
        {
            OnEditorSelectionChanged();
            return;
        }

        var current = new HashSet<MeshInstance>(_editor.Selection.ObjectSelection.Items);
        _editor.Selection.ClearAll();

        MeshInstance? primary = null;
        foreach (var node in EnumerateSceneNodes(_sceneGraph))
        {
            var instance = node.MeshInstance;
            if (instance == null || current.Contains(instance))
            {
                continue;
            }

            _editor.Selection.ObjectSelection.Add(instance);
            primary ??= instance;
        }

        _editor.Selection.Selected = primary;
        OnEditorSelectionChanged();
    }

    private void GrowSelection()
    {
        if (!_editor.Mode.EditMode || !TryGetEditableSelection(out _, out var editable))
        {
            return;
        }

        if (_editor.Mode.VertexSelect)
        {
            var vertices = SelectionOperations.GrowVertices(editable, _editor.Selection.VertexSelection.Items);
            _editor.Selection.VertexSelection.ReplaceWith(vertices);
        }
        else if (_editor.Mode.EdgeSelect)
        {
            var edges = GrowEdges(editable, _editor.Selection.EdgeSelection.Items);
            _editor.Selection.EdgeSelection.ReplaceWith(edges);
        }
        else if (_editor.Mode.FaceSelect)
        {
            var faces = SelectionOperations.GrowFaces(editable, _editor.Selection.FaceSelection.Items);
            _editor.Selection.FaceSelection.ReplaceWith(faces);
        }
        else
        {
            return;
        }

        OnEditorSelectionChanged();
    }

    private void ShrinkSelection()
    {
        if (!_editor.Mode.EditMode || !TryGetEditableSelection(out _, out var editable))
        {
            return;
        }

        if (_editor.Mode.VertexSelect)
        {
            var vertices = SelectionOperations.ShrinkVertices(editable, _editor.Selection.VertexSelection.Items);
            _editor.Selection.VertexSelection.ReplaceWith(vertices);
        }
        else if (_editor.Mode.EdgeSelect)
        {
            var edges = ShrinkEdges(editable, _editor.Selection.EdgeSelection.Items);
            _editor.Selection.EdgeSelection.ReplaceWith(edges);
        }
        else if (_editor.Mode.FaceSelect)
        {
            var faces = SelectionOperations.ShrinkFaces(editable, _editor.Selection.FaceSelection.Items);
            _editor.Selection.FaceSelection.ReplaceWith(faces);
        }
        else
        {
            return;
        }

        OnEditorSelectionChanged();
    }

    private void ConvertSelectionToVertices()
    {
        if (_editor.MeshEdits.ConvertSelectionToVertices())
        {
            _optionsService.SyncEditModeToggles();
        }
    }

    private void ConvertSelectionToEdges()
    {
        if (_editor.MeshEdits.ConvertSelectionToEdges())
        {
            _optionsService.SyncEditModeToggles();
        }
    }

    private void ConvertSelectionToFaces()
    {
        if (_editor.MeshEdits.ConvertSelectionToFaces())
        {
            _optionsService.SyncEditModeToggles();
        }
    }

    private void DuplicateSelection()
    {
        if (_editor.Selection.ObjectSelection.Count == 0)
        {
            return;
        }

        var selectedNodes = new HashSet<SceneNode>();
        foreach (var instance in _editor.Selection.ObjectSelection.Items)
        {
            if (_editor.Document.TryGetNode(instance, out var node))
            {
                selectedNodes.Add(node);
            }
        }

        if (selectedNodes.Count == 0)
        {
            return;
        }

        var rootsToDuplicate = new List<SceneNode>();
        foreach (var node in selectedNodes)
        {
            if (!HasSelectedAncestor(node, selectedNodes))
            {
                rootsToDuplicate.Add(node);
            }
        }

        if (rootsToDuplicate.Count == 0)
        {
            return;
        }

        var existingNames = CollectSceneNames(_sceneGraph);
        var clones = new List<SceneNode>(rootsToDuplicate.Count);
        foreach (var node in rootsToDuplicate)
        {
            bool rename = node.Parent == null;
            var clone = CloneSceneNodeRecursive(node, existingNames, rename);
            if (node.Parent != null)
            {
                node.Parent.AddChild(clone);
            }
            else
            {
                _sceneGraph.AddRoot(clone);
                _userNodes.Add(clone);
            }

            RegisterSceneRecursive(clone);
            clones.Add(clone);
        }

        _scenePath = null;
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.UpdateSceneLights();
        if (clones.Count > 0)
        {
            SelectNode(clones[^1]);
        }

        _viewportManager.InvalidateAll();
    }

    private void DetachFaces()
    {
        if (!_editor.Mode.EditMode || !_editor.Mode.FaceSelect)
        {
            return;
        }

        if (_editor.Selection.FaceSelection.IsEmpty)
        {
            return;
        }

        if (!TryGetEditableSelection(out var instance, out var editable) || instance == null)
        {
            return;
        }

        if (!_editor.Document.TryGetNode(instance, out var sourceNode))
        {
            return;
        }

        var selectedFaces = new HashSet<int>(_editor.Selection.FaceSelection.Items);
        if (!TryBuildDetachedMesh(editable, selectedFaces, out var detachedEditable))
        {
            return;
        }

        RemoveFacesFromMesh(editable, selectedFaces);
        var updatedInstance = _editor.Document.RebuildEditableInstance(instance, editable);
        if (updatedInstance != null && !ReferenceEquals(updatedInstance, instance))
        {
            _editor.Selection.ReplaceInstance(instance, updatedInstance);
        }

        var detachedMesh = EditorDocument.BuildMeshFromEditable(detachedEditable);
        var renderer = sourceNode.MeshRenderer;
        var material = renderer != null ? CloneMaterial(renderer.Material) : Material.Default();
        var newNode = new SceneNode(BuildUniqueName(existingNames: CollectSceneNames(_sceneGraph), sourceNode.Name, "Detached"))
        {
            MeshRenderer = new MeshRenderer(detachedMesh, material)
            {
                MaterialGraph = renderer?.MaterialGraph,
                OverrideColor = renderer?.OverrideColor,
                IsVisible = renderer?.IsVisible ?? true,
                UseLods = renderer?.UseLods ?? true,
                BaseLodScreenFraction = renderer?.BaseLodScreenFraction ?? 0.2f
            }
        };

        newNode.Transform.LocalPosition = sourceNode.Transform.LocalPosition;
        newNode.Transform.LocalRotation = sourceNode.Transform.LocalRotation;
        newNode.Transform.LocalScale = sourceNode.Transform.LocalScale;

        if (sourceNode.Parent != null)
        {
            sourceNode.Parent.AddChild(newNode);
        }
        else
        {
            _sceneGraph.AddRoot(newNode);
            _userNodes.Add(newNode);
        }

        RegisterEditable(newNode);
        _scenePath = null;
        _editor.MeshEdits.ClearCommands();
        SelectNode(newNode);
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.UpdateSceneLights();
        _viewportManager.InvalidateAll();
    }

    private static HashSet<EdgeKey> GrowEdges(EditableMesh editable, IReadOnlyCollection<EdgeKey> edges)
    {
        if (edges.Count == 0)
        {
            return new HashSet<EdgeKey>();
        }

        var adjacency = MeshAdjacency.Build(editable);
        var result = new HashSet<EdgeKey>(edges);
        foreach (var edge in edges)
        {
            if (!adjacency.Edges.TryGetValue(edge, out var faces))
            {
                continue;
            }

            foreach (var face in faces.Faces)
            {
                if ((uint)face >= (uint)adjacency.FaceEdges.Count)
                {
                    continue;
                }

                var faceEdges = adjacency.FaceEdges[face];
                for (int i = 0; i < faceEdges.Length; i++)
                {
                    result.Add(faceEdges[i]);
                }
            }
        }

        return result;
    }

    private static HashSet<EdgeKey> ShrinkEdges(EditableMesh editable, IReadOnlyCollection<EdgeKey> edges)
    {
        if (edges.Count == 0)
        {
            return new HashSet<EdgeKey>();
        }

        var adjacency = MeshAdjacency.Build(editable);
        var current = edges as HashSet<EdgeKey> ?? new HashSet<EdgeKey>(edges);
        var result = new HashSet<EdgeKey>(current);

        foreach (var edge in current)
        {
            if (!adjacency.Edges.TryGetValue(edge, out var faces) || faces.Faces.Length == 0)
            {
                result.Remove(edge);
                continue;
            }

            bool remove = false;
            foreach (var face in faces.Faces)
            {
                if ((uint)face >= (uint)adjacency.FaceEdges.Count)
                {
                    remove = true;
                    break;
                }

                var faceEdges = adjacency.FaceEdges[face];
                for (int i = 0; i < faceEdges.Length; i++)
                {
                    if (!current.Contains(faceEdges[i]))
                    {
                        remove = true;
                        break;
                    }
                }

                if (remove)
                {
                    break;
                }
            }

            if (remove)
            {
                result.Remove(edge);
            }
        }

        return result;
    }

    private void GroupSelection()
    {
        if (_editor.Selection.ObjectSelection.Count < 2)
        {
            return;
        }

        var selectedNodes = new List<SceneNode>();
        foreach (var instance in _editor.Selection.ObjectSelection.Items)
        {
            if (_editor.Document.TryGetNode(instance, out var node) && !selectedNodes.Contains(node))
            {
                selectedNodes.Add(node);
            }
        }

        if (selectedNodes.Count < 2)
        {
            return;
        }

        _sceneGraph.UpdateWorld(parallel: true);

        var commonParent = selectedNodes[0].Parent;
        for (int i = 1; i < selectedNodes.Count; i++)
        {
            if (!ReferenceEquals(selectedNodes[i].Parent, commonParent))
            {
                commonParent = null;
                break;
            }
        }

        var group = new SceneNode($"Group {_groupCounter + 1}");
        _groupCounter++;
        var parentWorld = commonParent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        var center = ComputeSelectionCenter(selectedNodes);
        group.Transform.SetLocalFromWorld(Matrix4x4.CreateTranslation(center), parentWorld);

        if (commonParent != null)
        {
            commonParent.AddChild(group);
        }
        else
        {
            _sceneGraph.AddRoot(group);
            _userNodes.Add(group);
        }

        var groupWorld = group.Transform.LocalMatrix * parentWorld;
        foreach (var node in selectedNodes)
        {
            var world = node.Transform.WorldMatrix;
            group.AddChild(node);
            node.Transform.SetLocalFromWorld(world, groupWorld);
        }

        _sceneGraph.UpdateWorld(parallel: true);
        _scenePath = null;
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        OnEditorSelectionChanged();
        _viewportManager.InvalidateAll();
    }

    private void UngroupSelection()
    {
        var selected = _viewportManager.ActiveViewport.SelectedNode;
        if (selected == null)
        {
            return;
        }

        var group = IsGroupNode(selected) ? selected : selected.Parent;
        if (group == null || !IsGroupNode(group))
        {
            return;
        }

        _sceneGraph.UpdateWorld(parallel: true);

        var parent = group.Parent;
        var parentWorld = parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        var children = new List<SceneNode>(group.Children);
        foreach (var child in children)
        {
            var childWorld = child.Transform.WorldMatrix;
            if (parent != null)
            {
                parent.AddChild(child);
            }
            else
            {
                group.RemoveChild(child);
                _sceneGraph.AddRoot(child);
            }

            child.Transform.SetLocalFromWorld(childWorld, parentWorld);
        }

        if (parent != null)
        {
            parent.RemoveChild(group);
        }
        else
        {
            _sceneGraph.RemoveRoot(group);
            _userNodes.Remove(group);
        }

        _sceneGraph.UpdateWorld(parallel: true);
        _scenePath = null;
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        OnEditorSelectionChanged();
        _viewportManager.InvalidateAll();
    }

    private void DeleteSelection()
    {
        var node = _viewportManager.ActiveViewport.SelectedNode;
        if (node == null)
        {
            return;
        }

        if (node.Parent != null)
        {
            node.Parent.RemoveChild(node);
        }
        else
        {
            _sceneGraph.RemoveRoot(node);
        }

        RemoveKeysRecursive(node);
        _editor.Document.UnregisterSceneRecursive(node);
        _userNodes.Remove(node);

        if (ReferenceEquals(node, _groundNode))
        {
            _groundNode = null;
        }
        if (ReferenceEquals(node, _cubeNode))
        {
            _cubeNode = null;
            SetupSceneAnimation();
        }
        if (ReferenceEquals(node, _lightNode))
        {
            _lightNode = null;
        }

        _editor.Selection.ClearAll();
        _editor.MeshEdits.ClearCommands();
        OnEditorSelectionChanged();
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.UpdateSceneLights();
        _viewportManager.InvalidateAll();
    }

    private void RemoveNode(SceneNode node)
    {
        if (node.Parent != null)
        {
            node.Parent.RemoveChild(node);
        }
        else
        {
            _sceneGraph.RemoveRoot(node);
        }

        RemoveKeysRecursive(node);
        _editor.Document.UnregisterSceneRecursive(node);
        _userNodes.Remove(node);
    }

    private void RemoveKeysRecursive(SceneNode node)
    {
        _motionService.RemoveKeysForNode(node);
        foreach (var child in node.Children)
        {
            RemoveKeysRecursive(child);
        }
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
        _vfxService.Refresh();
        _physicsService.Refresh();
        _navigationPanelService.Refresh();
        _audioPanelService.Refresh();
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

        var previousName = node.Name;
        node.Name = newName;
        _motionService.RenameNode(node, previousName);
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

    private sealed class TransformInput
    {
        public TransformInput(Vector3 translation, Vector3 rotationDegrees, Vector3 scale)
        {
            Translation = translation;
            RotationDegrees = rotationDegrees;
            Scale = scale;
        }

        public Vector3 Translation { get; }

        public Vector3 RotationDegrees { get; }

        public Vector3 Scale { get; }
    }

    private async Task<TransformInput?> ShowTransformDialogAsync()
    {
        var translateX = new TextBox { Text = "0", Width = 64 };
        var translateY = new TextBox { Text = "0", Width = 64 };
        var translateZ = new TextBox { Text = "0", Width = 64 };
        var rotateX = new TextBox { Text = "0", Width = 64 };
        var rotateY = new TextBox { Text = "0", Width = 64 };
        var rotateZ = new TextBox { Text = "0", Width = 64 };
        var scaleX = new TextBox { Text = "1", Width = 64 };
        var scaleY = new TextBox { Text = "1", Width = 64 };
        var scaleZ = new TextBox { Text = "1", Width = 64 };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnSpacing = 6,
            RowSpacing = 6
        };

        grid.Children.Add(new TextBlock { Text = string.Empty });
        grid.Children.Add(new TextBlock { Text = "X", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        grid.Children.Add(new TextBlock { Text = "Y", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        grid.Children.Add(new TextBlock { Text = "Z", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        Grid.SetColumn(grid.Children[1], 1);
        Grid.SetColumn(grid.Children[2], 2);
        Grid.SetColumn(grid.Children[3], 3);

        grid.Children.Add(new TextBlock { Text = "Translate" });
        Grid.SetRow(grid.Children[^1], 1);
        grid.Children.Add(translateX);
        Grid.SetRow(translateX, 1);
        Grid.SetColumn(translateX, 1);
        grid.Children.Add(translateY);
        Grid.SetRow(translateY, 1);
        Grid.SetColumn(translateY, 2);
        grid.Children.Add(translateZ);
        Grid.SetRow(translateZ, 1);
        Grid.SetColumn(translateZ, 3);

        grid.Children.Add(new TextBlock { Text = "Rotate (deg)" });
        Grid.SetRow(grid.Children[^1], 2);
        grid.Children.Add(rotateX);
        Grid.SetRow(rotateX, 2);
        Grid.SetColumn(rotateX, 1);
        grid.Children.Add(rotateY);
        Grid.SetRow(rotateY, 2);
        Grid.SetColumn(rotateY, 2);
        grid.Children.Add(rotateZ);
        Grid.SetRow(rotateZ, 2);
        Grid.SetColumn(rotateZ, 3);

        grid.Children.Add(new TextBlock { Text = "Scale" });
        Grid.SetRow(grid.Children[^1], 3);
        grid.Children.Add(scaleX);
        Grid.SetRow(scaleX, 3);
        Grid.SetColumn(scaleX, 1);
        grid.Children.Add(scaleY);
        Grid.SetRow(scaleY, 3);
        Grid.SetColumn(scaleY, 2);
        grid.Children.Add(scaleZ);
        Grid.SetRow(scaleZ, 3);
        Grid.SetColumn(scaleZ, 3);

        var okButton = new Button { Content = "Apply", Width = 80 };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };
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
        panel.Children.Add(new TextBlock { Text = "Numeric Transform" });
        panel.Children.Add(grid);
        panel.Children.Add(new TextBlock { Text = "Applies to sub-selection when in edit mode.", FontSize = 11, Opacity = 0.7 });
        panel.Children.Add(buttonRow);

        var dialog = new Window
        {
            Title = "Transform Values",
            Width = 360,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        okButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        var result = await dialog.ShowDialog<bool?>(this);
        if (result != true)
        {
            return null;
        }

        if (!TryReadVector3(translateX, translateY, translateZ, 0f, out var translation) ||
            !TryReadVector3(rotateX, rotateY, rotateZ, 0f, out var rotationDegrees) ||
            !TryReadVector3(scaleX, scaleY, scaleZ, 1f, out var scale))
        {
            await new Window
            {
                Title = "Transform Values",
                Width = 360,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBlock { Text = "Invalid numeric input.", Margin = new Avalonia.Thickness(12) }
            }.ShowDialog(this);
            return null;
        }

        return new TransformInput(translation, rotationDegrees, scale);
    }

    private static bool TryReadVector3(TextBox xBox, TextBox yBox, TextBox zBox, float defaultValue, out Vector3 result)
    {
        result = default;

        if (!TryParseFloat(xBox.Text, defaultValue, out var x) ||
            !TryParseFloat(yBox.Text, defaultValue, out var y) ||
            !TryParseFloat(zBox.Text, defaultValue, out var z))
        {
            return false;
        }

        result = new Vector3(x, y, z);
        return true;
    }

    private static bool TryParseFloat(string? text, float defaultValue, out float value)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            value = defaultValue;
            return true;
        }

        return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

    private static Vector3 ComputeSelectionCenter(IReadOnlyList<SceneNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return Vector3.Zero;
        }

        var sum = Vector3.Zero;
        for (int i = 0; i < nodes.Count; i++)
        {
            sum += nodes[i].Transform.WorldMatrix.Translation;
        }

        return sum / nodes.Count;
    }

    private static HashSet<string> CollectSceneNames(SceneGraph scene)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in EnumerateSceneNodes(scene))
        {
            names.Add(node.Name);
        }

        return names;
    }

    private static bool HasSelectedAncestor(SceneNode node, HashSet<SceneNode> selected)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (selected.Contains(parent))
            {
                return true;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static string BuildUniqueName(HashSet<string> existingNames, string baseName, string suffix)
    {
        var candidate = string.IsNullOrWhiteSpace(suffix) ? baseName : $"{baseName} {suffix}";
        if (existingNames.Add(candidate))
        {
            return candidate;
        }

        int index = 2;
        while (true)
        {
            candidate = string.IsNullOrWhiteSpace(suffix)
                ? $"{baseName} {index}"
                : $"{baseName} {suffix} {index}";
            if (existingNames.Add(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static SceneNode CloneSceneNodeRecursive(SceneNode source, HashSet<string> existingNames, bool renameRoot)
    {
        var name = renameRoot ? BuildUniqueName(existingNames, source.Name, "Copy") : source.Name;
        var clone = new SceneNode(name);
        CloneComponents(source, clone);
        clone.Transform.LocalPosition = source.Transform.LocalPosition;
        clone.Transform.LocalRotation = source.Transform.LocalRotation;
        clone.Transform.LocalScale = source.Transform.LocalScale;

        foreach (var child in source.Children)
        {
            var childClone = CloneSceneNodeRecursive(child, existingNames, renameRoot: false);
            clone.AddChild(childClone);
        }

        return clone;
    }

    private static void CloneComponents(SceneNode source, SceneNode clone)
    {
        var rigidBodyMap = new Dictionary<RigidBodyComponent, RigidBodyComponent>();
        var deferredColliders = new List<ColliderComponent>();

        foreach (var component in source.Components)
        {
            switch (component)
            {
                case MeshRenderer renderer:
                    clone.MeshRenderer = CloneMeshRenderer(renderer);
                    break;
                case LightComponent light:
                    clone.Light = new LightComponent(CloneLight(light.Light)) { Enabled = light.Enabled };
                    break;
                case CameraComponent camera:
                    clone.Camera = new CameraComponent(CloneCamera(camera.Camera)) { Enabled = camera.Enabled };
                    break;
                case RigidBodyComponent body:
                    var newBody = new RigidBodyComponent
                    {
                        BodyType = body.BodyType,
                        Mass = body.Mass,
                        Velocity = body.Velocity,
                        AngularVelocity = body.AngularVelocity,
                        UseGravity = body.UseGravity,
                        Restitution = body.Restitution,
                        Friction = body.Friction,
                        LinearDamping = body.LinearDamping,
                        AngularDamping = body.AngularDamping,
                        IsAwake = body.IsAwake
                    };
                    clone.AddComponent(newBody);
                    rigidBodyMap[body] = newBody;
                    break;
                case ColliderComponent collider:
                    deferredColliders.Add(collider);
                    break;
                case NavGridComponent navGrid:
                    clone.AddComponent(new NavGridComponent(navGrid.Grid) { Enabled = navGrid.Enabled });
                    break;
                case NavAgentComponent navAgent:
                    var agent = new NavAgentComponent
                    {
                        Speed = navAgent.Speed,
                        StoppingDistance = navAgent.StoppingDistance
                    };
                    clone.AddComponent(agent);
                    break;
                case ParticleEmitterComponent emitter:
                    var emitterClone = new ParticleEmitterComponent
                    {
                        MaxParticles = emitter.MaxParticles,
                        EmissionRate = emitter.EmissionRate,
                        Lifetime = emitter.Lifetime,
                        BaseVelocity = emitter.BaseVelocity,
                        VelocityRandomness = emitter.VelocityRandomness,
                        StartSize = emitter.StartSize,
                        EndSize = emitter.EndSize,
                        StartColor = emitter.StartColor,
                        EndColor = emitter.EndColor,
                        IsEmitting = emitter.IsEmitting,
                        WorldSpace = emitter.WorldSpace
                    };
                    clone.AddComponent(emitterClone);
                    break;
                case AudioListenerComponent listener:
                    clone.AddComponent(new AudioListenerComponent { Volume = listener.Volume });
                    break;
                case AudioSourceComponent audioSource:
                    var newSource = new AudioSourceComponent
                    {
                        Clip = audioSource.Clip,
                        Loop = audioSource.Loop,
                        Volume = audioSource.Volume,
                        Pitch = audioSource.Pitch,
                        SpatialBlend = audioSource.SpatialBlend,
                        MinDistance = audioSource.MinDistance,
                        MaxDistance = audioSource.MaxDistance,
                        Velocity = audioSource.Velocity
                    };
                    if (audioSource.IsPlaying)
                    {
                        newSource.Play();
                    }
                    clone.AddComponent(newSource);
                    break;
            }
        }

        foreach (var collider in deferredColliders)
        {
            var shape = CloneColliderShape(collider.Shape);
            var newCollider = new ColliderComponent(shape)
            {
                IsTrigger = collider.IsTrigger
            };
            if (collider.Body != null && rigidBodyMap.TryGetValue(collider.Body, out var newBody))
            {
                newCollider.Body = newBody;
            }
            clone.AddComponent(newCollider);
        }
    }

    private static MeshRenderer CloneMeshRenderer(MeshRenderer renderer)
    {
        var mesh = CloneMesh(renderer.Mesh);
        var material = CloneMaterial(renderer.Material);
        var clone = new MeshRenderer(mesh, material)
        {
            MaterialGraph = renderer.MaterialGraph,
            OverrideColor = renderer.OverrideColor,
            IsVisible = renderer.IsVisible,
            UseLods = renderer.UseLods,
            BaseLodScreenFraction = renderer.BaseLodScreenFraction
        };

        if (renderer.Lods.Count > 0)
        {
            var levels = new List<MeshLodLevel>(renderer.Lods.Count);
            for (int i = 0; i < renderer.Lods.Count; i++)
            {
                var level = renderer.Lods[i];
                levels.Add(new MeshLodLevel(CloneMesh(level.Mesh), level.ScreenFraction));
            }
            clone.SetLods(levels);
        }

        return clone;
    }

    private static Mesh CloneMesh(Mesh mesh)
    {
        return new Mesh(mesh.Vertices, mesh.Indices, mesh.Tangents, mesh.Bitangents, mesh.SkinWeights, mesh.SkinIndices, mesh.MorphTargets);
    }

    private static Material CloneMaterial(Material material)
    {
        return new Material
        {
            ShadingModel = material.ShadingModel,
            BaseColor = material.BaseColor,
            BaseColorTexture = material.BaseColorTexture,
            BaseColorSampler = CloneSampler(material.BaseColorSampler),
            UvScale = material.UvScale,
            UvOffset = material.UvOffset,
            BaseColorTextureStrength = material.BaseColorTextureStrength,
            Metallic = material.Metallic,
            Roughness = material.Roughness,
            MetallicRoughnessTexture = material.MetallicRoughnessTexture,
            MetallicRoughnessSampler = CloneSampler(material.MetallicRoughnessSampler),
            MetallicRoughnessTextureStrength = material.MetallicRoughnessTextureStrength,
            NormalTexture = material.NormalTexture,
            NormalSampler = CloneSampler(material.NormalSampler),
            NormalStrength = material.NormalStrength,
            EmissiveTexture = material.EmissiveTexture,
            EmissiveSampler = CloneSampler(material.EmissiveSampler),
            EmissiveColor = material.EmissiveColor,
            EmissiveStrength = material.EmissiveStrength,
            OcclusionTexture = material.OcclusionTexture,
            OcclusionSampler = CloneSampler(material.OcclusionSampler),
            OcclusionStrength = material.OcclusionStrength,
            Ambient = material.Ambient,
            Diffuse = material.Diffuse,
            Specular = material.Specular,
            Shininess = material.Shininess,
            UseVertexColor = material.UseVertexColor,
            DoubleSided = material.DoubleSided
        };
    }

    private static TextureSampler CloneSampler(TextureSampler sampler)
    {
        return new TextureSampler
        {
            WrapU = sampler.WrapU,
            WrapV = sampler.WrapV,
            Filter = sampler.Filter
        };
    }

    private static Light CloneLight(Light light)
    {
        return new Light
        {
            Type = light.Type,
            Direction = light.Direction,
            Position = light.Position,
            Color = light.Color,
            Intensity = light.Intensity,
            Range = light.Range,
            InnerConeAngle = light.InnerConeAngle,
            OuterConeAngle = light.OuterConeAngle,
            Size = light.Size
        };
    }

    private static Camera CloneCamera(Camera camera)
    {
        return new Camera
        {
            Position = camera.Position,
            Target = camera.Target,
            Up = camera.Up,
            FieldOfView = camera.FieldOfView,
            AspectRatio = camera.AspectRatio,
            NearPlane = camera.NearPlane,
            FarPlane = camera.FarPlane,
            OrthographicSize = camera.OrthographicSize,
            ProjectionMode = camera.ProjectionMode
        };
    }

    private static ColliderShape CloneColliderShape(ColliderShape shape)
    {
        switch (shape)
        {
            case SphereShape sphere:
                return new SphereShape(sphere.Radius) { Offset = sphere.Offset };
            case BoxShape box:
                return new BoxShape(box.Size) { Offset = box.Offset };
            default:
                return shape;
        }
    }

    private static bool TryBuildDetachedMesh(EditableMesh source, IReadOnlyCollection<int> selectedFaces, out EditableMesh detached)
    {
        detached = null!;
        if (selectedFaces.Count == 0)
        {
            return false;
        }

        var faceList = new List<int>(selectedFaces);
        faceList.Sort();

        var indices = source.Indices;
        var vertexMap = new Dictionary<int, int>();
        var positions = new List<Vector3>();
        var uvs = new List<Vector2>();
        List<Vector4>? colors = source.HasColors ? new List<Vector4>() : null;
        var newIndices = new List<int>();
        var uvGroups = new List<int>();

        for (int i = 0; i < faceList.Count; i++)
        {
            var face = faceList[i];
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            for (int corner = 0; corner < 3; corner++)
            {
                int oldIndex = indices[baseIndex + corner];
                if (!vertexMap.TryGetValue(oldIndex, out var newIndex))
                {
                    newIndex = positions.Count;
                    vertexMap.Add(oldIndex, newIndex);
                    positions.Add(source.Positions[oldIndex]);
                    uvs.Add(oldIndex < source.UVs.Count ? source.UVs[oldIndex] : Vector2.Zero);
                    colors?.Add(source.Colors![oldIndex]);
                }

                newIndices.Add(newIndex);
            }

            if (face < source.UvFaceGroups.Count)
            {
                uvGroups.Add(source.UvFaceGroups[face]);
            }
        }

        if (newIndices.Count == 0)
        {
            return false;
        }

        detached = new EditableMesh(positions, newIndices, uvs, normals: null, colors: colors, tangents: null);
        if (uvGroups.Count == detached.TriangleCount)
        {
            detached.UvFaceGroups.Clear();
            detached.UvFaceGroups.AddRange(uvGroups);
        }
        else
        {
            detached.EnsureUvFaceGroups();
        }

        foreach (var edge in source.SeamEdges)
        {
            if (vertexMap.TryGetValue(edge.A, out var a) && vertexMap.TryGetValue(edge.B, out var b))
            {
                detached.SeamEdges.Add(new EdgeKey(a, b));
            }
        }
        detached.TrimSeamEdges();
        return true;
    }

    private static void RemoveFacesFromMesh(EditableMesh mesh, IReadOnlyCollection<int> selectedFaces)
    {
        var faceSet = selectedFaces as HashSet<int> ?? new HashSet<int>(selectedFaces);
        var indices = mesh.Indices;
        int triangleCount = indices.Count / 3;
        var newIndices = new List<int>(Math.Max(0, indices.Count - faceSet.Count * 3));
        var newUvGroups = new List<int>(Math.Max(0, mesh.UvFaceGroups.Count - faceSet.Count));

        for (int face = 0; face < triangleCount; face++)
        {
            if (faceSet.Contains(face))
            {
                continue;
            }

            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                break;
            }

            newIndices.Add(indices[baseIndex]);
            newIndices.Add(indices[baseIndex + 1]);
            newIndices.Add(indices[baseIndex + 2]);
            if (face < mesh.UvFaceGroups.Count)
            {
                newUvGroups.Add(mesh.UvFaceGroups[face]);
            }
        }

        mesh.Indices.Clear();
        mesh.Indices.AddRange(newIndices);
        mesh.UvFaceGroups.Clear();
        if (newUvGroups.Count == mesh.Indices.Count / 3)
        {
            mesh.UvFaceGroups.AddRange(newUvGroups);
        }
        else
        {
            mesh.EnsureUvFaceGroups();
        }

        mesh.TrimSeamEdges();
        mesh.InvalidateNormals();
    }

    private static HashSet<int> CollectVerticesFromFaces(EditableMesh mesh, IReadOnlyCollection<int> faces)
    {
        var indices = mesh.Indices;
        var vertices = new HashSet<int>();
        foreach (var face in faces)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            vertices.Add(indices[baseIndex]);
            vertices.Add(indices[baseIndex + 1]);
            vertices.Add(indices[baseIndex + 2]);
        }

        return vertices;
    }

    private bool TryGetTransformSelectionVertices(out EditableMesh editable, out HashSet<int> vertices)
    {
        vertices = new HashSet<int>();
        editable = null!;

        if (!_editor.Mode.EditMode)
        {
            return false;
        }

        if (!TryGetEditableSelection(out _, out editable))
        {
            return false;
        }

        if (_editor.Mode.VertexSelect && !_editor.Selection.VertexSelection.IsEmpty)
        {
            vertices = new HashSet<int>(_editor.Selection.VertexSelection.Items);
            return vertices.Count > 0;
        }

        if (_editor.Mode.EdgeSelect && !_editor.Selection.EdgeSelection.IsEmpty)
        {
            foreach (var edge in _editor.Selection.EdgeSelection.Items)
            {
                vertices.Add(edge.A);
                vertices.Add(edge.B);
            }

            return vertices.Count > 0;
        }

        if (_editor.Mode.FaceSelect && !_editor.Selection.FaceSelection.IsEmpty)
        {
            vertices = CollectVerticesFromFaces(editable, _editor.Selection.FaceSelection.Items);
            return vertices.Count > 0;
        }

        return false;
    }

    private static Vector3 ComputeVertexCenter(EditableMesh mesh, IReadOnlyCollection<int> vertices)
    {
        var positions = mesh.Positions;
        var sum = Vector3.Zero;
        int count = 0;

        foreach (var index in vertices)
        {
            if ((uint)index >= (uint)positions.Count)
            {
                continue;
            }

            sum += positions[index];
            count++;
        }

        return count > 0 ? sum / count : Vector3.Zero;
    }

    private static Quaternion BuildRotation(Vector3 rotationDegrees)
    {
        var radians = rotationDegrees * (MathF.PI / 180f);
        return Quaternion.CreateFromYawPitchRoll(radians.Y, radians.X, radians.Z);
    }

    private static Matrix4x4 BuildSelectionTransform(Vector3 translation, Quaternion rotation, Vector3 scale, Vector3 pivot)
    {
        return Matrix4x4.CreateTranslation(-pivot)
            * Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(rotation)
            * Matrix4x4.CreateTranslation(pivot)
            * Matrix4x4.CreateTranslation(translation);
    }

    private static bool IsGroupNode(SceneNode node)
    {
        return node.MeshRenderer == null
               && node.Light == null
               && node.Camera == null
               && node.Components.Count == 0;
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
        _vfxService.Refresh();
        _physicsService.Refresh();
        _navigationPanelService.Refresh();
        _audioPanelService.Refresh();
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

    private async Task TransformSelectionAsync()
    {
        var input = await ShowTransformDialogAsync();
        if (input == null)
        {
            return;
        }

        var translation = input.Translation;
        var rotationDegrees = input.RotationDegrees;
        var scale = input.Scale;
        if (translation == Vector3.Zero && rotationDegrees == Vector3.Zero && scale == Vector3.One)
        {
            return;
        }

        var rotation = BuildRotation(rotationDegrees);

        if (TryGetTransformSelectionVertices(out var editable, out var vertices))
        {
            var pivot = ComputeVertexCenter(editable, vertices);
            var transform = BuildSelectionTransform(translation, rotation, scale, pivot);
            _editor.MeshEdits.ApplyTransformToVertices(transform, vertices, "Transform Selection");
            return;
        }

        _editor.MeshEdits.ApplyTransformToSelection(translation, rotation, scale, "Transform Selection");
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
        ResetSubsystemState();
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
        _motionService.ClearKeys();
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
            ResetSubsystemState();
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
            _motionService.ClearKeys();
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

    private void ClearAnimationKeys()
    {
        _motionService.ClearKeys();
    }

    private void OpenMaterialGraph()
    {
        if (_materialGraphWindow != null)
        {
            _materialGraphWindow.Activate();
            return;
        }

        var canvas = new MaterialGraphCanvasControl
        {
            DataContext = _viewModel.Material.Canvas,
            Height = 360
        };

        var addPanel = new WrapPanel();
        addPanel.Children.Add(new Button { Content = "Add Color", Command = _viewModel.Material.AddColorNodeCommand, Margin = new Avalonia.Thickness(0, 0, 6, 6) });
        addPanel.Children.Add(new Button { Content = "Add Float", Command = _viewModel.Material.AddFloatNodeCommand, Margin = new Avalonia.Thickness(0, 0, 6, 6) });
        addPanel.Children.Add(new Button { Content = "Add Add", Command = _viewModel.Material.AddAddNodeCommand, Margin = new Avalonia.Thickness(0, 0, 6, 6) });
        addPanel.Children.Add(new Button { Content = "Add Multiply", Command = _viewModel.Material.AddMultiplyNodeCommand, Margin = new Avalonia.Thickness(0, 0, 6, 6) });
        addPanel.Children.Add(new Button { Content = "Add Texture", Command = _viewModel.Material.AddTextureNodeCommand, Margin = new Avalonia.Thickness(0, 0, 6, 6) });
        addPanel.Children.Add(new Button { Content = "Add Sample", Command = _viewModel.Material.AddTextureSampleNodeCommand, Margin = new Avalonia.Thickness(0, 0, 6, 6) });
        addPanel.Children.Add(new Button { Content = "Add Normal Map", Command = _viewModel.Material.AddNormalMapNodeCommand, Margin = new Avalonia.Thickness(0, 0, 6, 6) });

        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(12)
        };
        panel.Children.Add(new TextBlock { Text = "Material Graph" });
        panel.Children.Add(canvas);
        panel.Children.Add(addPanel);

        var window = new Window
        {
            Title = "Material Graph",
            Width = 720,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        window.Closed += (_, _) => _materialGraphWindow = null;
        _materialGraphWindow = window;
        window.Show(this);
    }

    private void OpenScriptConsole()
    {
        if (_scriptConsoleWindow != null)
        {
            _scriptConsoleWindow.Activate();
            return;
        }

        var inputBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 140,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var outputBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            Height = 180,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var runButton = new Button { Content = "Run", Width = 80 };
        runButton.Click += (_, _) => outputBox.Text = ExecuteScript(inputBox.Text ?? string.Empty);

        var clearButton = new Button { Content = "Clear Output", Width = 110 };
        clearButton.Click += (_, _) => outputBox.Text = string.Empty;

        var buttonRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        buttonRow.Children.Add(runButton);
        buttonRow.Children.Add(clearButton);

        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(12)
        };
        panel.Children.Add(new TextBlock { Text = "Script Console" });
        panel.Children.Add(inputBox);
        panel.Children.Add(buttonRow);
        panel.Children.Add(outputBox);

        var window = new Window
        {
            Title = "Script Console",
            Width = 600,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        window.Closed += (_, _) => _scriptConsoleWindow = null;
        _scriptConsoleWindow = window;
        window.Show(this);
    }

    private string ExecuteScript(string script)
    {
        var output = new StringBuilder();
        var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            ExecuteScriptLine(line, output);
        }

        if (output.Length == 0)
        {
            output.AppendLine("No commands executed.");
        }

        return output.ToString();
    }

    private void ExecuteScriptLine(string line, StringBuilder output)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var command = parts[0].ToLowerInvariant();
        switch (command)
        {
            case "help":
                output.AppendLine("Commands: help, clear, reset, select all|none, invert, create cube|sphere|cylinder|pyramid|plane|grid, light directional|point|spot, camera, flow, idle, nav grid, view perspective|top|front|left|right|back|bottom, shading default|wireframe|unlit, grid on|off, wireframe on|off, lighting on|off, pause on|off, play, stop, loop on|off, autokey on|off");
                return;
            case "clear":
                ClearSceneAndInvalidate();
                output.AppendLine("Scene cleared.");
                return;
            case "reset":
                RebuildSampleScene(_optionsService.GetMeshSegments(DefaultMeshSegments));
                output.AppendLine("Scene reset.");
                return;
            case "select":
                if (parts.Length < 2)
                {
                    output.AppendLine("Select expects all or none.");
                    return;
                }

                switch (parts[1].ToLowerInvariant())
                {
                    case "all":
                        SelectAll();
                        output.AppendLine("Selected all.");
                        return;
                    case "none":
                        ClearSelection();
                        output.AppendLine("Selection cleared.");
                        return;
                }

                output.AppendLine($"Unknown select target: {parts[1]}");
                return;
            case "invert":
                InvertSelection();
                output.AppendLine("Selection inverted.");
                return;
            case "create":
                if (parts.Length < 2)
                {
                    output.AppendLine("Create expects a type.");
                    return;
                }

                var createType = parts[1].ToLowerInvariant();
                switch (createType)
                {
                    case "cube":
                        CreateCube();
                        output.AppendLine("Cube created.");
                        return;
                    case "sphere":
                        CreateSphere();
                        output.AppendLine("Sphere created.");
                        return;
                    case "cylinder":
                        CreateCylinder();
                        output.AppendLine("Cylinder created.");
                        return;
                    case "pyramid":
                        CreatePyramid();
                        output.AppendLine("Pyramid created.");
                        return;
                    case "plane":
                        CreatePlane();
                        output.AppendLine("Plane created.");
                        return;
                    case "grid":
                        CreateGrid();
                        output.AppendLine("Grid created.");
                        return;
                    case "flow":
                        CreateFlow();
                        output.AppendLine("Flow agent created.");
                        return;
                    case "idle":
                        CreateIdleArea();
                        output.AppendLine("Idle area created.");
                        return;
                    case "camera":
                        CreateCamera();
                        output.AppendLine("Camera created.");
                        return;
                    case "light":
                        if (parts.Length < 3)
                        {
                            output.AppendLine("Create light expects directional, point, or spot.");
                            return;
                        }

                        ExecuteScriptLine($"light {parts[2]}", output);
                        return;
                }

                output.AppendLine($"Unknown create type: {parts[1]}");
                return;
            case "light":
                if (parts.Length < 2)
                {
                    output.AppendLine("Light expects directional, point, or spot.");
                    return;
                }

                switch (parts[1].ToLowerInvariant())
                {
                    case "directional":
                        CreateDirectionalLight();
                        output.AppendLine("Directional light created.");
                        return;
                    case "point":
                        CreatePointLight();
                        output.AppendLine("Point light created.");
                        return;
                    case "spot":
                        CreateSpotLight();
                        output.AppendLine("Spot light created.");
                        return;
                }

                output.AppendLine($"Unknown light type: {parts[1]}");
                return;
            case "camera":
                CreateCamera();
                output.AppendLine("Camera created.");
                return;
            case "flow":
                CreateFlow();
                output.AppendLine("Flow agent created.");
                return;
            case "idle":
                CreateIdleArea();
                output.AppendLine("Idle area created.");
                return;
            case "nav":
                if (parts.Length > 1 && string.Equals(parts[1], "grid", StringComparison.OrdinalIgnoreCase))
                {
                    CreateNavGrid();
                    output.AppendLine("Nav grid created.");
                    return;
                }

                output.AppendLine("Nav expects grid.");
                return;
            case "view":
                if (parts.Length < 2)
                {
                    output.AppendLine("View expects a view name.");
                    return;
                }

                if (TryGetViewportView(parts[1], out var viewIndex))
                {
                    _optionsViewModel.ViewportViewIndex = viewIndex;
                    output.AppendLine($"View set to {parts[1]}.");
                    return;
                }

                output.AppendLine($"Unknown view: {parts[1]}");
                return;
            case "shading":
                if (parts.Length < 2)
                {
                    output.AppendLine("Shading expects default, wireframe, or unlit.");
                    return;
                }

                if (TryGetViewportShading(parts[1], out var shadingIndex))
                {
                    _optionsViewModel.ViewportShadingIndex = shadingIndex;
                    output.AppendLine($"Shading set to {parts[1]}.");
                    return;
                }

                output.AppendLine($"Unknown shading: {parts[1]}");
                return;
            case "grid":
                if (TryParseOnOff(parts.Length > 1 ? parts[1] : null, out var gridEnabled))
                {
                    _optionsViewModel.GridEnabled = gridEnabled;
                    output.AppendLine($"Grid {(gridEnabled ? "on" : "off")}.");
                }
                else
                {
                    output.AppendLine("Grid expects on or off.");
                }
                return;
            case "wireframe":
                if (TryParseOnOff(parts.Length > 1 ? parts[1] : null, out var wireframeEnabled))
                {
                    _optionsViewModel.WireframeEnabled = wireframeEnabled;
                    output.AppendLine($"Wireframe {(wireframeEnabled ? "on" : "off")}.");
                }
                else
                {
                    output.AppendLine("Wireframe expects on or off.");
                }
                return;
            case "lighting":
                if (TryParseOnOff(parts.Length > 1 ? parts[1] : null, out var lightingEnabled))
                {
                    _optionsViewModel.LightingEnabled = lightingEnabled;
                    output.AppendLine($"Lighting {(lightingEnabled ? "on" : "off")}.");
                }
                else
                {
                    output.AppendLine("Lighting expects on or off.");
                }
                return;
            case "pause":
                if (TryParseOnOff(parts.Length > 1 ? parts[1] : null, out var pauseEnabled))
                {
                    _optionsViewModel.PauseEnabled = pauseEnabled;
                    output.AppendLine($"Pause {(pauseEnabled ? "on" : "off")}.");
                }
                else
                {
                    output.AppendLine("Pause expects on or off.");
                }
                return;
            case "play":
                _viewModel.CommandPanel.Motion.IsPlaying = true;
                output.AppendLine("Playback started.");
                return;
            case "stop":
                _viewModel.CommandPanel.Motion.IsPlaying = false;
                output.AppendLine("Playback stopped.");
                return;
            case "loop":
                if (TryParseOnOff(parts.Length > 1 ? parts[1] : null, out var loopEnabled))
                {
                    _viewModel.CommandPanel.Motion.Loop = loopEnabled;
                    output.AppendLine($"Loop {(loopEnabled ? "on" : "off")}.");
                }
                else
                {
                    output.AppendLine("Loop expects on or off.");
                }
                return;
            case "autokey":
                if (TryParseOnOff(parts.Length > 1 ? parts[1] : null, out var autoKeyEnabled))
                {
                    _viewModel.CommandPanel.Motion.AutoKeyEnabled = autoKeyEnabled;
                    output.AppendLine($"Auto key {(autoKeyEnabled ? "on" : "off")}.");
                }
                else
                {
                    output.AppendLine("Auto key expects on or off.");
                }
                return;
        }

        output.AppendLine($"Unknown command: {line}");
    }

    private static bool TryParseOnOff(string? value, out bool enabled)
    {
        enabled = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
                enabled = true;
                return true;
            case "off":
            case "false":
            case "0":
                enabled = false;
                return true;
        }

        return false;
    }

    private static bool TryGetViewportView(string value, out int index)
    {
        index = 0;
        switch (value.ToLowerInvariant())
        {
            case "perspective":
                index = 0;
                return true;
            case "top":
                index = 1;
                return true;
            case "front":
                index = 2;
                return true;
            case "left":
                index = 3;
                return true;
            case "right":
                index = 4;
                return true;
            case "back":
                index = 5;
                return true;
            case "bottom":
                index = 6;
                return true;
        }

        return false;
    }

    private static bool TryGetViewportShading(string value, out int index)
    {
        index = 0;
        switch (value.ToLowerInvariant())
        {
            case "default":
            case "shaded":
                index = 0;
                return true;
            case "wireframe":
                index = 1;
                return true;
            case "unlit":
            case "flat":
                index = 2;
                return true;
        }

        return false;
    }

    private async Task AssignTextureAsync()
    {
        if (!TryGetSelectedMeshNode(out var node))
        {
            await ShowMessageAsync("Assign Texture", "Select a mesh node to assign a texture.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Texture",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Images") { Patterns = new List<string> { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" } },
                new FilePickerFileType("All files") { Patterns = new List<string> { "*" } }
            }
        });

        if (files is null || files.Count == 0)
        {
            return;
        }

        Texture2D? texture = null;
        try
        {
            var file = files[0];
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                texture = Texture2D.FromFile(path);
            }
            else
            {
                await using var stream = await file.OpenReadAsync();
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    texture = new Texture2D(bitmap);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Assign Texture", $"Failed to load texture: {ex.Message}");
            return;
        }

        if (texture == null)
        {
            await ShowMessageAsync("Assign Texture", "Failed to decode texture.");
            return;
        }

        _loadedTextures.Add(texture);
        ApplyTextureToNode(node, texture);
    }

    private void AssignCheckerTexture()
    {
        if (!TryGetSelectedMeshNode(out var node))
        {
            _ = ShowMessageAsync("Assign Texture", "Select a mesh node to assign a texture.");
            return;
        }

        var texture = Texture2D.CreateCheckerboard(256, 256, new SKColor(80, 85, 95), new SKColor(130, 140, 150), cells: 8);
        _loadedTextures.Add(texture);
        ApplyTextureToNode(node, texture);
    }

    private void ClearTextures()
    {
        if (!TryGetSelectedMeshNode(out var node))
        {
            _ = ShowMessageAsync("Clear Textures", "Select a mesh node to clear textures.");
            return;
        }

        var material = node.MeshRenderer!.Material;
        material.BaseColorTexture = null;
        material.NormalTexture = null;
        material.UseVertexColor = true;
        material.BaseColor = SKColors.White;

        _scenePath = null;
        _viewportManager.InvalidateAll();
    }

    private bool TryGetSelectedMeshNode(out SceneNode node)
    {
        var selected = _viewportManager.ActiveViewport.SelectedNode;
        if (selected?.MeshRenderer == null)
        {
            node = null!;
            return false;
        }

        node = selected;
        return true;
    }

    private void ApplyTextureToNode(SceneNode node, Texture2D texture)
    {
        var material = node.MeshRenderer!.Material;
        material.BaseColorTexture = texture;
        material.BaseColorSampler = new TextureSampler { Filter = TextureFilter.Bilinear };
        material.BaseColorTextureStrength = 1f;
        material.BaseColor = SKColors.White;
        material.UseVertexColor = false;

        _scenePath = null;
        _viewportManager.InvalidateAll();
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var okButton = new Button
        {
            Content = "OK",
            Width = 80
        };

        var buttonRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        buttonRow.Children.Add(okButton);

        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Avalonia.Thickness(12)
        };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(buttonRow);

        var dialog = new Window
        {
            Title = title,
            Width = 360,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private void ShowAbout()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "dev";
        var message = $"Skia3D Sample\nVersion: {version}";
        _ = ShowMessageAsync("About Skia3D Sample", message);
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
        _motionService.ClearKeys();
        OnEditorSelectionChanged();
        _hierarchyService.Rebuild(_sceneGraph);
        _constraintService.Rebuild(_sceneGraph);
        _viewportManager.InvalidateAll();
    }
}
