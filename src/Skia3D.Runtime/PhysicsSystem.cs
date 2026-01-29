using System;

namespace Skia3D.Runtime;

public interface IPhysicsWorld
{
    void Step(float fixedDeltaSeconds);
}

public sealed class PhysicsSystem : SystemBase
{
    private readonly IPhysicsWorld _world;

    public PhysicsSystem(IPhysicsWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public override void FixedUpdate(Engine engine, Time time)
    {
        _world.Step(time.FixedDeltaSeconds);
    }
}
