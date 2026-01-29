using System;
using Skia3D.Runtime;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Animation;

public sealed class SceneAnimationSystem : SystemBase
{
    private readonly SceneGraph _scene;

    public SceneAnimationSystem(SceneGraph scene)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    public bool UseFixedUpdate { get; set; }

    public override void Update(Engine engine, Time time)
    {
        if (!UseFixedUpdate)
        {
            Step(time.DeltaSeconds);
        }
    }

    public override void FixedUpdate(Engine engine, Time time)
    {
        if (UseFixedUpdate)
        {
            Step(time.FixedDeltaSeconds);
        }
    }

    private void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var components = _scene.CollectComponents<AnimationComponent>();
        for (int i = 0; i < components.Count; i++)
        {
            components[i].Update(deltaSeconds);
        }

        var animators = _scene.CollectComponents<AnimatorComponent>();
        for (int i = 0; i < animators.Count; i++)
        {
            animators[i].Update(deltaSeconds);
        }
    }
}
