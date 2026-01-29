using Skia3D.Scene;

namespace Skia3D.Animation;

public sealed class AnimatorComponent : SceneComponent
{
    public AnimationStateMachine StateMachine { get; } = new();

    public AnimationParameterSet Parameters => StateMachine.Parameters;

    public void Update(float deltaSeconds)
    {
        if (!Enabled || deltaSeconds <= 0f)
        {
            return;
        }

        StateMachine.Update(deltaSeconds);
    }
}

