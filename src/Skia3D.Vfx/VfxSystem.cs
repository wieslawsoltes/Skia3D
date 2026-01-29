using System;
using Skia3D.Core;
using Skia3D.Runtime;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Vfx;

public sealed class VfxSystem : SystemBase
{
    private readonly SceneGraph _scene;
    private readonly Func<SceneGraph, Camera?>? _cameraSelector;

    public VfxSystem(SceneGraph scene, Func<SceneGraph, Camera?>? cameraSelector = null)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _cameraSelector = cameraSelector;
    }

    public override void Update(Engine engine, Time time)
    {
        var emitters = _scene.CollectComponents<ParticleEmitterComponent>();
        for (int i = 0; i < emitters.Count; i++)
        {
            emitters[i].UpdateEmitter(time.DeltaSeconds);
        }
    }

    public override void Render(Engine engine, in RenderContext context)
    {
        var camera = _cameraSelector != null ? _cameraSelector(_scene) : context.Camera;
        if (camera == null)
        {
            return;
        }

        var emitters = _scene.CollectComponents<ParticleEmitterComponent>();
        for (int i = 0; i < emitters.Count; i++)
        {
            emitters[i].BuildBillboards(camera);
        }
    }
}
