using System;
using Skia3D.Runtime;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Physics;

public sealed class ScenePhysicsSystem : SystemBase
{
    private readonly SceneGraph _scene;
    private readonly PhysicsWorld _world;

    public ScenePhysicsSystem(SceneGraph scene, PhysicsWorld world)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public bool UpdateSceneWorld { get; set; } = true;

    public override void FixedUpdate(Engine engine, Time time)
    {
        if (UpdateSceneWorld)
        {
            _scene.UpdateWorld(parallel: false);
        }

        _world.SyncFromScene(_scene);
        _world.Step(time.FixedDeltaSeconds);
    }
}
