using Skia3D.Scene;

namespace Skia3D.Animation;

public sealed class AnimationComponent : SceneComponent
{
    public AnimationPlayer? Player { get; set; }

    public AnimationMixer? Mixer { get; set; }

    public void Update(float deltaSeconds)
    {
        if (!Enabled || deltaSeconds <= 0f)
        {
            return;
        }

        if (Mixer != null)
        {
            Mixer.Update(deltaSeconds);
        }
        else
        {
            Player?.Update(deltaSeconds);
        }
    }
}
