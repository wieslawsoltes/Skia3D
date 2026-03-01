using System;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using Skia3D.Audio;
using Skia3D.Navigation;
using Skia3D.Physics;
using Skia3D.Scene;
using Skia3D.Sample.ViewModels;
using Skia3D.Vfx;
using SkiaSharp;

namespace Skia3D.Sample.Services;

public sealed class VfxPanelService : IDisposable
{
    private readonly EditorViewportService _viewportService;
    private readonly ViewportManagerService _viewportManager;
    private readonly VfxPanelViewModel _viewModel;
    private bool _updating;

    public VfxPanelService(EditorViewportService viewportService, ViewportManagerService viewportManager, VfxPanelViewModel viewModel)
    {
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewportManager = viewportManager ?? throw new ArgumentNullException(nameof(viewportManager));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _viewModel.AddEmitterRequested += AddEmitter;
        _viewModel.RemoveEmitterRequested += RemoveEmitter;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged += OnActiveViewportChanged;
    }

    public void Refresh()
    {
        var node = GetSelectedNode();
        var emitter = node?.GetComponent<ParticleEmitterComponent>();

        _updating = true;
        try
        {
            _viewModel.HasSelection = node != null;
            _viewModel.SelectionLabel = node == null ? "Selection: none" : $"Selection: {node.Name}";
            _viewModel.HasEmitter = emitter != null;

            if (emitter == null)
            {
                return;
            }

            _viewModel.EmitterEnabled = emitter.Enabled;
            _viewModel.IsEmitting = emitter.IsEmitting;
            _viewModel.WorldSpace = emitter.WorldSpace;
            _viewModel.MaxParticles = emitter.MaxParticles;
            _viewModel.EmissionRate = emitter.EmissionRate;
            _viewModel.Lifetime = emitter.Lifetime;
            _viewModel.StartSize = emitter.StartSize;
            _viewModel.EndSize = emitter.EndSize;
            _viewModel.BaseVelX = emitter.BaseVelocity.X;
            _viewModel.BaseVelY = emitter.BaseVelocity.Y;
            _viewModel.BaseVelZ = emitter.BaseVelocity.Z;
            _viewModel.VelRandX = emitter.VelocityRandomness.X;
            _viewModel.VelRandY = emitter.VelocityRandomness.Y;
            _viewModel.VelRandZ = emitter.VelocityRandomness.Z;
            ColorToChannels(emitter.StartColor, out var sr, out var sg, out var sb, out var sa);
            ColorToChannels(emitter.EndColor, out var er, out var eg, out var eb, out var ea);
            _viewModel.StartColorR = sr;
            _viewModel.StartColorG = sg;
            _viewModel.StartColorB = sb;
            _viewModel.StartColorA = sa;
            _viewModel.EndColorR = er;
            _viewModel.EndColorG = eg;
            _viewModel.EndColorB = eb;
            _viewModel.EndColorA = ea;
        }
        finally
        {
            _updating = false;
        }
    }

    private void AddEmitter()
    {
        var node = GetSelectedNode();
        if (node == null || node.GetComponent<ParticleEmitterComponent>() != null)
        {
            return;
        }

        node.AddComponent(new ParticleEmitterComponent());
        Refresh();
        _viewportManager.InvalidateAll();
    }

    private void RemoveEmitter()
    {
        var node = GetSelectedNode();
        var emitter = node?.GetComponent<ParticleEmitterComponent>();
        if (node == null || emitter == null)
        {
            return;
        }

        node.RemoveComponent(emitter);
        Refresh();
        _viewportManager.InvalidateAll();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updating || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        var emitter = GetSelectedNode()?.GetComponent<ParticleEmitterComponent>();
        if (emitter == null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(VfxPanelViewModel.EmitterEnabled):
                emitter.Enabled = _viewModel.EmitterEnabled;
                break;
            case nameof(VfxPanelViewModel.IsEmitting):
                emitter.IsEmitting = _viewModel.IsEmitting;
                break;
            case nameof(VfxPanelViewModel.WorldSpace):
                emitter.WorldSpace = _viewModel.WorldSpace;
                break;
            case nameof(VfxPanelViewModel.MaxParticles):
                emitter.MaxParticles = Math.Max(1, (int)Math.Round(_viewModel.MaxParticles));
                break;
            case nameof(VfxPanelViewModel.EmissionRate):
                emitter.EmissionRate = MathF.Max(0f, (float)_viewModel.EmissionRate);
                break;
            case nameof(VfxPanelViewModel.Lifetime):
                emitter.Lifetime = MathF.Max(0.05f, (float)_viewModel.Lifetime);
                break;
            case nameof(VfxPanelViewModel.StartSize):
                emitter.StartSize = MathF.Max(0.001f, (float)_viewModel.StartSize);
                break;
            case nameof(VfxPanelViewModel.EndSize):
                emitter.EndSize = MathF.Max(0.001f, (float)_viewModel.EndSize);
                break;
            case nameof(VfxPanelViewModel.BaseVelX):
            case nameof(VfxPanelViewModel.BaseVelY):
            case nameof(VfxPanelViewModel.BaseVelZ):
                emitter.BaseVelocity = new Vector3((float)_viewModel.BaseVelX, (float)_viewModel.BaseVelY, (float)_viewModel.BaseVelZ);
                break;
            case nameof(VfxPanelViewModel.VelRandX):
            case nameof(VfxPanelViewModel.VelRandY):
            case nameof(VfxPanelViewModel.VelRandZ):
                emitter.VelocityRandomness = new Vector3((float)_viewModel.VelRandX, (float)_viewModel.VelRandY, (float)_viewModel.VelRandZ);
                break;
            case nameof(VfxPanelViewModel.StartColorR):
            case nameof(VfxPanelViewModel.StartColorG):
            case nameof(VfxPanelViewModel.StartColorB):
            case nameof(VfxPanelViewModel.StartColorA):
                emitter.StartColor = ChannelsToColor(_viewModel.StartColorR, _viewModel.StartColorG, _viewModel.StartColorB, _viewModel.StartColorA);
                break;
            case nameof(VfxPanelViewModel.EndColorR):
            case nameof(VfxPanelViewModel.EndColorG):
            case nameof(VfxPanelViewModel.EndColorB):
            case nameof(VfxPanelViewModel.EndColorA):
                emitter.EndColor = ChannelsToColor(_viewModel.EndColorR, _viewModel.EndColorG, _viewModel.EndColorB, _viewModel.EndColorA);
                break;
            default:
                return;
        }

        _viewportManager.InvalidateAll();
    }

    private void OnActiveViewportChanged(EditorViewportService viewport)
    {
        Refresh();
    }

    private SceneNode? GetSelectedNode()
    {
        return _viewportManager.ActiveViewport.SelectedNode ?? _viewportService.SelectedNode;
    }

    private static void ColorToChannels(SKColor color, out double r, out double g, out double b, out double a)
    {
        r = color.Red / 255.0;
        g = color.Green / 255.0;
        b = color.Blue / 255.0;
        a = color.Alpha / 255.0;
    }

    private static SKColor ChannelsToColor(double r, double g, double b, double a)
    {
        return new SKColor(
            (byte)Math.Clamp(r * 255.0, 0, 255),
            (byte)Math.Clamp(g * 255.0, 0, 255),
            (byte)Math.Clamp(b * 255.0, 0, 255),
            (byte)Math.Clamp(a * 255.0, 0, 255));
    }

    public void Dispose()
    {
        _viewModel.AddEmitterRequested -= AddEmitter;
        _viewModel.RemoveEmitterRequested -= RemoveEmitter;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged -= OnActiveViewportChanged;
    }
}

public sealed class PhysicsPanelService : IDisposable
{
    private readonly EditorViewportService _viewportService;
    private readonly ViewportManagerService _viewportManager;
    private readonly PhysicsPanelViewModel _viewModel;
    private bool _updating;

    public PhysicsPanelService(EditorViewportService viewportService, ViewportManagerService viewportManager, PhysicsPanelViewModel viewModel)
    {
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewportManager = viewportManager ?? throw new ArgumentNullException(nameof(viewportManager));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _viewModel.AddRigidBodyRequested += AddRigidBody;
        _viewModel.RemoveRigidBodyRequested += RemoveRigidBody;
        _viewModel.AddColliderRequested += AddCollider;
        _viewModel.RemoveColliderRequested += RemoveCollider;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged += OnActiveViewportChanged;
    }

    public void Refresh()
    {
        var node = GetSelectedNode();
        var body = node?.GetComponent<RigidBodyComponent>();
        var collider = node?.GetComponent<ColliderComponent>();

        _updating = true;
        try
        {
            _viewModel.HasSelection = node != null;
            _viewModel.SelectionLabel = node == null ? "Selection: none" : $"Selection: {node.Name}";

            _viewModel.HasRigidBody = body != null;
            if (body != null)
            {
                _viewModel.BodyEnabled = body.Enabled;
                _viewModel.BodyTypeIndex = body.BodyType switch
                {
                    PhysicsBodyType.Static => 0,
                    PhysicsBodyType.Dynamic => 1,
                    PhysicsBodyType.Kinematic => 2,
                    _ => 1
                };
                _viewModel.Mass = body.Mass;
                _viewModel.UseGravity = body.UseGravity;
                _viewModel.Restitution = body.Restitution;
                _viewModel.Friction = body.Friction;
                _viewModel.LinearDamping = body.LinearDamping;
                _viewModel.AngularDamping = body.AngularDamping;
                _viewModel.IsAwake = body.IsAwake;
            }

            _viewModel.HasCollider = collider != null;
            if (collider != null)
            {
                _viewModel.ColliderEnabled = collider.Enabled;
                _viewModel.IsTrigger = collider.IsTrigger;
                switch (collider.Shape)
                {
                    case SphereShape sphere:
                        _viewModel.ColliderShapeIndex = 0;
                        _viewModel.ColliderRadius = sphere.Radius;
                        _viewModel.ColliderOffsetX = sphere.Offset.X;
                        _viewModel.ColliderOffsetY = sphere.Offset.Y;
                        _viewModel.ColliderOffsetZ = sphere.Offset.Z;
                        break;
                    case BoxShape box:
                        _viewModel.ColliderShapeIndex = 1;
                        _viewModel.ColliderSizeX = box.Size.X;
                        _viewModel.ColliderSizeY = box.Size.Y;
                        _viewModel.ColliderSizeZ = box.Size.Z;
                        _viewModel.ColliderOffsetX = box.Offset.X;
                        _viewModel.ColliderOffsetY = box.Offset.Y;
                        _viewModel.ColliderOffsetZ = box.Offset.Z;
                        break;
                }
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void AddRigidBody()
    {
        var node = GetSelectedNode();
        if (node == null || node.GetComponent<RigidBodyComponent>() != null)
        {
            return;
        }

        node.AddComponent(new RigidBodyComponent());
        Refresh();
    }

    private void RemoveRigidBody()
    {
        var node = GetSelectedNode();
        var body = node?.GetComponent<RigidBodyComponent>();
        if (node == null || body == null)
        {
            return;
        }

        node.RemoveComponent(body);
        Refresh();
    }

    private void AddCollider()
    {
        var node = GetSelectedNode();
        if (node == null || node.GetComponent<ColliderComponent>() != null)
        {
            return;
        }

        node.AddComponent(new ColliderComponent(CreateShape()));
        Refresh();
    }

    private void RemoveCollider()
    {
        var node = GetSelectedNode();
        var collider = node?.GetComponent<ColliderComponent>();
        if (node == null || collider == null)
        {
            return;
        }

        node.RemoveComponent(collider);
        Refresh();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updating || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        var node = GetSelectedNode();
        if (node == null)
        {
            return;
        }

        var body = node.GetComponent<RigidBodyComponent>();
        var collider = node.GetComponent<ColliderComponent>();

        switch (e.PropertyName)
        {
            case nameof(PhysicsPanelViewModel.BodyEnabled):
                if (body != null)
                {
                    body.Enabled = _viewModel.BodyEnabled;
                }
                break;
            case nameof(PhysicsPanelViewModel.BodyTypeIndex):
                if (body != null)
                {
                    body.BodyType = _viewModel.BodyTypeIndex switch
                    {
                        0 => PhysicsBodyType.Static,
                        2 => PhysicsBodyType.Kinematic,
                        _ => PhysicsBodyType.Dynamic
                    };
                }
                break;
            case nameof(PhysicsPanelViewModel.Mass):
                if (body != null)
                {
                    body.Mass = (float)_viewModel.Mass;
                }
                break;
            case nameof(PhysicsPanelViewModel.UseGravity):
                if (body != null)
                {
                    body.UseGravity = _viewModel.UseGravity;
                }
                break;
            case nameof(PhysicsPanelViewModel.Restitution):
                if (body != null)
                {
                    body.Restitution = (float)_viewModel.Restitution;
                }
                break;
            case nameof(PhysicsPanelViewModel.Friction):
                if (body != null)
                {
                    body.Friction = (float)_viewModel.Friction;
                }
                break;
            case nameof(PhysicsPanelViewModel.LinearDamping):
                if (body != null)
                {
                    body.LinearDamping = (float)_viewModel.LinearDamping;
                }
                break;
            case nameof(PhysicsPanelViewModel.AngularDamping):
                if (body != null)
                {
                    body.AngularDamping = (float)_viewModel.AngularDamping;
                }
                break;
            case nameof(PhysicsPanelViewModel.IsAwake):
                if (body != null)
                {
                    body.IsAwake = _viewModel.IsAwake;
                }
                break;
            case nameof(PhysicsPanelViewModel.ColliderEnabled):
                if (collider != null)
                {
                    collider.Enabled = _viewModel.ColliderEnabled;
                }
                break;
            case nameof(PhysicsPanelViewModel.IsTrigger):
                if (collider != null)
                {
                    collider.IsTrigger = _viewModel.IsTrigger;
                }
                break;
            case nameof(PhysicsPanelViewModel.ColliderShapeIndex):
            case nameof(PhysicsPanelViewModel.ColliderRadius):
            case nameof(PhysicsPanelViewModel.ColliderSizeX):
            case nameof(PhysicsPanelViewModel.ColliderSizeY):
            case nameof(PhysicsPanelViewModel.ColliderSizeZ):
            case nameof(PhysicsPanelViewModel.ColliderOffsetX):
            case nameof(PhysicsPanelViewModel.ColliderOffsetY):
            case nameof(PhysicsPanelViewModel.ColliderOffsetZ):
                if (collider != null)
                {
                    ApplyColliderShape(collider);
                }
                break;
            default:
                return;
        }

        _viewportManager.InvalidateAll();
    }

    private ColliderShape CreateShape()
    {
        return _viewModel.ColliderShapeIndex switch
        {
            1 => new BoxShape(new Vector3((float)_viewModel.ColliderSizeX, (float)_viewModel.ColliderSizeY, (float)_viewModel.ColliderSizeZ))
            {
                Offset = new Vector3((float)_viewModel.ColliderOffsetX, (float)_viewModel.ColliderOffsetY, (float)_viewModel.ColliderOffsetZ)
            },
            _ => new SphereShape((float)_viewModel.ColliderRadius)
            {
                Offset = new Vector3((float)_viewModel.ColliderOffsetX, (float)_viewModel.ColliderOffsetY, (float)_viewModel.ColliderOffsetZ)
            }
        };
    }

    private void ApplyColliderShape(ColliderComponent collider)
    {
        if (_viewModel.ColliderShapeIndex == 0)
        {
            var sphere = collider.Shape as SphereShape ?? new SphereShape((float)_viewModel.ColliderRadius);
            sphere.Radius = (float)_viewModel.ColliderRadius;
            sphere.Offset = new Vector3((float)_viewModel.ColliderOffsetX, (float)_viewModel.ColliderOffsetY, (float)_viewModel.ColliderOffsetZ);
            collider.Shape = sphere;
            return;
        }

        var box = collider.Shape as BoxShape ?? new BoxShape(new Vector3((float)_viewModel.ColliderSizeX, (float)_viewModel.ColliderSizeY, (float)_viewModel.ColliderSizeZ));
        box.Size = new Vector3((float)_viewModel.ColliderSizeX, (float)_viewModel.ColliderSizeY, (float)_viewModel.ColliderSizeZ);
        box.Offset = new Vector3((float)_viewModel.ColliderOffsetX, (float)_viewModel.ColliderOffsetY, (float)_viewModel.ColliderOffsetZ);
        collider.Shape = box;
    }

    private void OnActiveViewportChanged(EditorViewportService viewport)
    {
        Refresh();
    }

    private SceneNode? GetSelectedNode()
    {
        return _viewportManager.ActiveViewport.SelectedNode ?? _viewportService.SelectedNode;
    }

    public void Dispose()
    {
        _viewModel.AddRigidBodyRequested -= AddRigidBody;
        _viewModel.RemoveRigidBodyRequested -= RemoveRigidBody;
        _viewModel.AddColliderRequested -= AddCollider;
        _viewModel.RemoveColliderRequested -= RemoveCollider;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged -= OnActiveViewportChanged;
    }
}

public sealed class NavigationPanelService : IDisposable
{
    private readonly EditorViewportService _viewportService;
    private readonly ViewportManagerService _viewportManager;
    private readonly NavigationPanelViewModel _viewModel;
    private bool _updating;

    public NavigationPanelService(EditorViewportService viewportService, ViewportManagerService viewportManager, NavigationPanelViewModel viewModel)
    {
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewportManager = viewportManager ?? throw new ArgumentNullException(nameof(viewportManager));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _viewModel.AddGridRequested += AddGrid;
        _viewModel.RemoveGridRequested += RemoveGrid;
        _viewModel.ApplyGridRequested += ApplyGrid;
        _viewModel.BakeGridRequested += ApplyGrid;
        _viewModel.AddAgentRequested += AddAgent;
        _viewModel.RemoveAgentRequested += RemoveAgent;
        _viewModel.ApplyDestinationRequested += ApplyDestination;
        _viewModel.ClearDestinationRequested += ClearDestination;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged += OnActiveViewportChanged;
    }

    public void Refresh()
    {
        var node = GetSelectedNode();
        var grid = node?.GetComponent<NavGridComponent>();
        var agent = node?.GetComponent<NavAgentComponent>();

        _updating = true;
        try
        {
            _viewModel.HasSelection = node != null;
            _viewModel.SelectionLabel = node == null ? "Selection: none" : $"Selection: {node.Name}";

            _viewModel.HasNavGrid = grid != null;
            if (grid != null)
            {
                _viewModel.GridWidth = grid.Grid.Width;
                _viewModel.GridHeight = grid.Grid.Height;
                _viewModel.CellSize = grid.Grid.CellSize;
                _viewModel.OriginX = grid.Grid.Origin.X;
                _viewModel.OriginY = grid.Grid.Origin.Y;
                _viewModel.OriginZ = grid.Grid.Origin.Z;
            }

            _viewModel.HasNavAgent = agent != null;
            if (agent != null)
            {
                _viewModel.AgentSpeed = agent.Speed;
                _viewModel.AgentStoppingDistance = agent.StoppingDistance;

                if (agent.Destination is Vector3 destination)
                {
                    _viewModel.DestinationX = destination.X.ToString("0.###", CultureInfo.InvariantCulture);
                    _viewModel.DestinationY = destination.Y.ToString("0.###", CultureInfo.InvariantCulture);
                    _viewModel.DestinationZ = destination.Z.ToString("0.###", CultureInfo.InvariantCulture);
                }

                _viewModel.PathLabel = agent.HasPath
                    ? $"Path: {agent.Path.Count} points"
                    : (agent.HasDestination ? "Path: computing" : "Path: none");
            }
            else
            {
                _viewModel.PathLabel = "Path: none";
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void AddGrid()
    {
        var node = GetSelectedNode();
        if (node == null || node.GetComponent<NavGridComponent>() != null)
        {
            return;
        }

        node.AddComponent(new NavGridComponent(CreateGrid()));
        Refresh();
    }

    private void RemoveGrid()
    {
        var node = GetSelectedNode();
        var grid = node?.GetComponent<NavGridComponent>();
        if (node == null || grid == null)
        {
            return;
        }

        node.RemoveComponent(grid);
        Refresh();
    }

    private void ApplyGrid()
    {
        var grid = GetSelectedNode()?.GetComponent<NavGridComponent>();
        if (grid == null)
        {
            return;
        }

        grid.Grid = CreateGrid();
        Refresh();
        _viewportManager.InvalidateAll();
    }

    private void AddAgent()
    {
        var node = GetSelectedNode();
        if (node == null || node.GetComponent<NavAgentComponent>() != null)
        {
            return;
        }

        node.AddComponent(new NavAgentComponent());
        Refresh();
    }

    private void RemoveAgent()
    {
        var node = GetSelectedNode();
        var agent = node?.GetComponent<NavAgentComponent>();
        if (node == null || agent == null)
        {
            return;
        }

        node.RemoveComponent(agent);
        Refresh();
    }

    private void ApplyDestination()
    {
        var agent = GetSelectedNode()?.GetComponent<NavAgentComponent>();
        if (agent == null)
        {
            return;
        }

        if (!TryParseVector(_viewModel.DestinationX, _viewModel.DestinationY, _viewModel.DestinationZ, out var destination))
        {
            return;
        }

        agent.SetDestination(destination);
        Refresh();
    }

    private void ClearDestination()
    {
        var agent = GetSelectedNode()?.GetComponent<NavAgentComponent>();
        if (agent == null)
        {
            return;
        }

        agent.ClearDestination();
        Refresh();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updating || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        var agent = GetSelectedNode()?.GetComponent<NavAgentComponent>();
        if (agent == null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(NavigationPanelViewModel.AgentSpeed):
                agent.Speed = (float)_viewModel.AgentSpeed;
                break;
            case nameof(NavigationPanelViewModel.AgentStoppingDistance):
                agent.StoppingDistance = (float)_viewModel.AgentStoppingDistance;
                break;
            default:
                return;
        }

        _viewportManager.InvalidateAll();
    }

    private NavGrid CreateGrid()
    {
        return new NavGrid(
            Math.Max(1, (int)Math.Round(_viewModel.GridWidth)),
            Math.Max(1, (int)Math.Round(_viewModel.GridHeight)),
            MathF.Max(0.01f, (float)_viewModel.CellSize),
            new Vector3((float)_viewModel.OriginX, (float)_viewModel.OriginY, (float)_viewModel.OriginZ));
    }

    private static bool TryParseVector(string xText, string yText, string zText, out Vector3 result)
    {
        var style = NumberStyles.Float;
        var culture = CultureInfo.InvariantCulture;
        if (float.TryParse(xText, style, culture, out var x)
            && float.TryParse(yText, style, culture, out var y)
            && float.TryParse(zText, style, culture, out var z))
        {
            result = new Vector3(x, y, z);
            return true;
        }

        result = default;
        return false;
    }

    private void OnActiveViewportChanged(EditorViewportService viewport)
    {
        Refresh();
    }

    private SceneNode? GetSelectedNode()
    {
        return _viewportManager.ActiveViewport.SelectedNode ?? _viewportService.SelectedNode;
    }

    public void Dispose()
    {
        _viewModel.AddGridRequested -= AddGrid;
        _viewModel.RemoveGridRequested -= RemoveGrid;
        _viewModel.ApplyGridRequested -= ApplyGrid;
        _viewModel.BakeGridRequested -= ApplyGrid;
        _viewModel.AddAgentRequested -= AddAgent;
        _viewModel.RemoveAgentRequested -= RemoveAgent;
        _viewModel.ApplyDestinationRequested -= ApplyDestination;
        _viewModel.ClearDestinationRequested -= ClearDestination;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged -= OnActiveViewportChanged;
    }
}

public sealed class AudioPanelService : IDisposable
{
    private readonly EditorViewportService _viewportService;
    private readonly ViewportManagerService _viewportManager;
    private readonly AudioPanelViewModel _viewModel;
    private bool _updating;

    public AudioPanelService(EditorViewportService viewportService, ViewportManagerService viewportManager, AudioPanelViewModel viewModel)
    {
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewportManager = viewportManager ?? throw new ArgumentNullException(nameof(viewportManager));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _viewModel.AddListenerRequested += AddListener;
        _viewModel.RemoveListenerRequested += RemoveListener;
        _viewModel.AddSourceRequested += AddSource;
        _viewModel.RemoveSourceRequested += RemoveSource;
        _viewModel.PlayRequested += Play;
        _viewModel.StopRequested += Stop;
        _viewModel.CreateClipRequested += CreateClip;
        _viewModel.ClearClipRequested += ClearClip;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged += OnActiveViewportChanged;
    }

    public void Refresh()
    {
        var node = GetSelectedNode();
        var listener = node?.GetComponent<AudioListenerComponent>();
        var source = node?.GetComponent<AudioSourceComponent>();

        _updating = true;
        try
        {
            _viewModel.HasSelection = node != null;
            _viewModel.SelectionLabel = node == null ? "Selection: none" : $"Selection: {node.Name}";

            _viewModel.HasListener = listener != null;
            if (listener != null)
            {
                _viewModel.ListenerEnabled = listener.Enabled;
                _viewModel.ListenerVolume = listener.Volume;
            }

            _viewModel.HasSource = source != null;
            if (source != null)
            {
                _viewModel.SourceEnabled = source.Enabled;
                _viewModel.SourceLoop = source.Loop;
                _viewModel.SourceVolume = source.Volume;
                _viewModel.SourcePitch = source.Pitch;
                _viewModel.SourceSpatialBlend = source.SpatialBlend;
                _viewModel.SourceMinDistance = source.MinDistance;
                _viewModel.SourceMaxDistance = source.MaxDistance;
                _viewModel.ClipLabel = source.Clip == null
                    ? "Clip: none"
                    : $"Clip: {source.Clip.Name} ({source.Clip.DurationSeconds:0.##}s)";
                _viewModel.PlaybackLabel = source.IsPlaying
                    ? $"Playback: playing ({source.Time:0.##}s)"
                    : "Playback: stopped";
                if (source.Clip != null)
                {
                    _viewModel.ClipDuration = source.Clip.DurationSeconds;
                }
            }
            else
            {
                _viewModel.ClipLabel = "Clip: none";
                _viewModel.PlaybackLabel = "Playback: stopped";
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void AddListener()
    {
        var node = GetSelectedNode();
        if (node == null || node.GetComponent<AudioListenerComponent>() != null)
        {
            return;
        }

        node.AddComponent(new AudioListenerComponent());
        Refresh();
    }

    private void RemoveListener()
    {
        var node = GetSelectedNode();
        var listener = node?.GetComponent<AudioListenerComponent>();
        if (node == null || listener == null)
        {
            return;
        }

        node.RemoveComponent(listener);
        Refresh();
    }

    private void AddSource()
    {
        var node = GetSelectedNode();
        if (node == null || node.GetComponent<AudioSourceComponent>() != null)
        {
            return;
        }

        node.AddComponent(new AudioSourceComponent());
        Refresh();
    }

    private void RemoveSource()
    {
        var node = GetSelectedNode();
        var source = node?.GetComponent<AudioSourceComponent>();
        if (node == null || source == null)
        {
            return;
        }

        node.RemoveComponent(source);
        Refresh();
    }

    private void Play()
    {
        var source = GetSelectedNode()?.GetComponent<AudioSourceComponent>();
        source?.Play();
        Refresh();
    }

    private void Stop()
    {
        var source = GetSelectedNode()?.GetComponent<AudioSourceComponent>();
        source?.Stop();
        Refresh();
    }

    private void CreateClip()
    {
        var source = GetSelectedNode()?.GetComponent<AudioSourceComponent>();
        if (source == null)
        {
            return;
        }

        source.Clip = new AudioClip("Generated Clip", (float)_viewModel.ClipDuration);
        Refresh();
    }

    private void ClearClip()
    {
        var source = GetSelectedNode()?.GetComponent<AudioSourceComponent>();
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.Clip = null;
        Refresh();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updating || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        var node = GetSelectedNode();
        var listener = node?.GetComponent<AudioListenerComponent>();
        var source = node?.GetComponent<AudioSourceComponent>();

        switch (e.PropertyName)
        {
            case nameof(AudioPanelViewModel.ListenerEnabled):
                if (listener != null)
                {
                    listener.Enabled = _viewModel.ListenerEnabled;
                }
                break;
            case nameof(AudioPanelViewModel.ListenerVolume):
                if (listener != null)
                {
                    listener.Volume = (float)_viewModel.ListenerVolume;
                }
                break;
            case nameof(AudioPanelViewModel.SourceEnabled):
                if (source != null)
                {
                    source.Enabled = _viewModel.SourceEnabled;
                }
                break;
            case nameof(AudioPanelViewModel.SourceLoop):
                if (source != null)
                {
                    source.Loop = _viewModel.SourceLoop;
                }
                break;
            case nameof(AudioPanelViewModel.SourceVolume):
                if (source != null)
                {
                    source.Volume = (float)_viewModel.SourceVolume;
                }
                break;
            case nameof(AudioPanelViewModel.SourcePitch):
                if (source != null)
                {
                    source.Pitch = (float)_viewModel.SourcePitch;
                }
                break;
            case nameof(AudioPanelViewModel.SourceSpatialBlend):
                if (source != null)
                {
                    source.SpatialBlend = (float)_viewModel.SourceSpatialBlend;
                }
                break;
            case nameof(AudioPanelViewModel.SourceMinDistance):
                if (source != null)
                {
                    source.MinDistance = (float)_viewModel.SourceMinDistance;
                }
                break;
            case nameof(AudioPanelViewModel.SourceMaxDistance):
                if (source != null)
                {
                    source.MaxDistance = (float)_viewModel.SourceMaxDistance;
                }
                break;
            default:
                return;
        }

        _viewportManager.InvalidateAll();
    }

    private void OnActiveViewportChanged(EditorViewportService viewport)
    {
        Refresh();
    }

    private SceneNode? GetSelectedNode()
    {
        return _viewportManager.ActiveViewport.SelectedNode ?? _viewportService.SelectedNode;
    }

    public void Dispose()
    {
        _viewModel.AddListenerRequested -= AddListener;
        _viewModel.RemoveListenerRequested -= RemoveListener;
        _viewModel.AddSourceRequested -= AddSource;
        _viewModel.RemoveSourceRequested -= RemoveSource;
        _viewModel.PlayRequested -= Play;
        _viewModel.StopRequested -= Stop;
        _viewModel.CreateClipRequested -= CreateClip;
        _viewModel.ClearClipRequested -= ClearClip;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewportManager.ActiveViewportChanged -= OnActiveViewportChanged;
    }
}
