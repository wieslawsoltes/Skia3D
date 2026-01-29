using Skia3D.Core;
using Skia3D.Rendering;
using SceneGraph = Skia3D.Scene.Scene;
using SkiaSharp;

namespace Skia3D.Runtime;

public sealed class EngineHost
{
    private readonly SceneRenderSystem _sceneSystem;

    public EngineHost(SceneGraph scene, Renderer3D renderer, Camera? camera = null, FrameClock? clock = null)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Camera = camera;
        Engine = new Engine(clock);

        _sceneSystem = new SceneRenderSystem(Scene, Renderer, ResolveCamera);
        Engine.AddSystem(_sceneSystem);
    }

    public Engine Engine { get; }

    public SceneGraph Scene { get; }

    public Renderer3D Renderer { get; }

    public Camera? Camera { get; set; }

    public Func<SceneGraph, Camera?>? CameraSelector { get; set; }

    public bool EnableCulling
    {
        get => _sceneSystem.EnableCulling;
        set => _sceneSystem.EnableCulling = value;
    }

    public bool ParallelUpdate
    {
        get => _sceneSystem.ParallelUpdate;
        set => _sceneSystem.ParallelUpdate = value;
    }

    public bool ParallelCollect
    {
        get => _sceneSystem.ParallelCollect;
        set => _sceneSystem.ParallelCollect = value;
    }

    public bool UseSceneLights
    {
        get => _sceneSystem.UseSceneLights;
        set => _sceneSystem.UseSceneLights = value;
    }

    public Light? FallbackLight
    {
        get => _sceneSystem.FallbackLight;
        set => _sceneSystem.FallbackLight = value;
    }

    public RenderPipeline? RenderPipeline
    {
        get => _sceneSystem.RenderPipeline;
        set => _sceneSystem.RenderPipeline = value;
    }

    public IReadOnlyList<MeshInstance> RenderInstances => _sceneSystem.RenderInstances;

    public void AddSystem(ISystem system)
    {
        Engine.AddSystem(system);
    }

    public bool RemoveSystem(ISystem system, bool shutdown = true)
    {
        return Engine.RemoveSystem(system, shutdown);
    }

    public void Initialize()
    {
        Engine.Initialize();
    }

    public void Shutdown()
    {
        Engine.Shutdown();
    }

    public void Tick()
    {
        Engine.Tick();
    }

    public void Render(SKCanvas canvas, SKRect viewport)
    {
        var camera = ResolveCamera();
        if (camera is null)
        {
            return;
        }

        Engine.Render(new RenderContext(canvas, viewport, camera));
    }

    public void TickAndRender(SKCanvas canvas, SKRect viewport)
    {
        var camera = ResolveCamera();
        Engine.Tick();

        if (camera is null)
        {
            return;
        }

        Engine.Render(new RenderContext(canvas, viewport, camera));
    }

    private Camera? ResolveCamera()
    {
        if (CameraSelector != null)
        {
            return CameraSelector(Scene);
        }

        return Camera;
    }
}
