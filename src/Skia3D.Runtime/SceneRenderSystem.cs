using System.Numerics;
using Skia3D.Core;
using Skia3D.Rendering;
using SceneGraph = Skia3D.Scene.Scene;
using SkiaSharp;

namespace Skia3D.Runtime;

public sealed class SceneRenderSystem : SystemBase
{
    private readonly SceneGraph _scene;
    private readonly Renderer3D _renderer;
    private readonly Func<Camera?> _cameraProvider;
    private IReadOnlyList<MeshInstance> _instances = Array.Empty<MeshInstance>();

    public SceneRenderSystem(SceneGraph scene, Renderer3D renderer, Func<Camera?> cameraProvider)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _cameraProvider = cameraProvider ?? throw new ArgumentNullException(nameof(cameraProvider));
    }

    public bool EnableCulling { get; set; } = true;

    public bool ParallelUpdate { get; set; } = true;

    public bool ParallelCollect { get; set; } = true;

    public bool UseSceneLights { get; set; } = true;

    public Light? FallbackLight { get; set; } = Light.Directional(new Vector3(-0.4f, -1f, -0.6f), new SKColor(255, 255, 255), 1f);

    public IReadOnlyList<MeshInstance> RenderInstances => _instances;

    public RenderPipeline? RenderPipeline { get; set; }

    public override void Update(Engine engine, Time time)
    {
        _scene.UpdateWorld(parallel: ParallelUpdate);

        var camera = _cameraProvider();
        _instances = _scene.CollectMeshInstances(camera, cull: EnableCulling && camera != null, parallel: ParallelCollect);

        if (UseSceneLights)
        {
            SyncLights();
        }
    }

    public override void Render(Engine engine, in RenderContext context)
    {
        if (RenderPipeline != null)
        {
            var frame = new RenderFrame(context.Canvas, context.Viewport, context.Camera, _instances, UseSceneLights ? _renderer.Lights : null);
            RenderPipeline.Render(frame);
            return;
        }

        _renderer.Render(context.Canvas, context.Viewport, context.Camera, _instances);
    }

    private void SyncLights()
    {
        var lights = _scene.CollectLights();
        _renderer.Lights.Clear();

        if (lights.Count > 0)
        {
            _renderer.Lights.AddRange(lights);
            return;
        }

        if (FallbackLight != null)
        {
            _renderer.Lights.Add(FallbackLight);
        }
    }
}
