using System.Numerics;
using Skia3D.Runtime;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Audio;

public sealed class AudioSystem : SystemBase
{
    private readonly SceneGraph _scene;
    private readonly IAudioBackend _backend;

    public AudioSystem(SceneGraph scene, IAudioBackend? backend = null)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _backend = backend ?? new NullAudioBackend();
    }

    public float GlobalVolume { get; set; } = 1f;

    public override void Initialize(Engine engine)
    {
        _backend.Initialize();
    }

    public override void Shutdown(Engine engine)
    {
        _backend.Shutdown();
    }

    public override void Update(Engine engine, Time time)
    {
        var listeners = _scene.CollectComponents<AudioListenerComponent>();
        var sources = _scene.CollectComponents<AudioSourceComponent>();

        if (listeners.Count > 0)
        {
            var listener = listeners[0];
            if (listener.Node != null)
            {
                var world = listener.Node.Transform.WorldMatrix;
                var forward = new Vector3(world.M31, world.M32, world.M33);
                if (forward.LengthSquared() > 1e-8f)
                {
                    forward = Vector3.Normalize(forward);
                }
                else
                {
                    forward = Vector3.UnitZ;
                }

                var up = new Vector3(world.M21, world.M22, world.M23);
                if (up.LengthSquared() > 1e-8f)
                {
                    up = Vector3.Normalize(up);
                }
                else
                {
                    up = Vector3.UnitY;
                }

                var state = new AudioListenerState(world.Translation, forward, up, listener.Volume * GlobalVolume);
                _backend.UpdateListener(state);

                for (int i = 0; i < sources.Count; i++)
                {
                    UpdateSource(listeners[0], sources[i], time.DeltaSeconds);
                }

                return;
            }
        }

        for (int i = 0; i < sources.Count; i++)
        {
            sources[i].Advance(time.DeltaSeconds);
        }
    }

    private void UpdateSource(AudioListenerComponent listener, AudioSourceComponent source, float deltaSeconds)
    {
        source.Advance(deltaSeconds);
        if (!source.IsPlaying || source.Node == null)
        {
            return;
        }

        var listenerPos = listener.Node?.Transform.WorldMatrix.Translation ?? Vector3.Zero;
        var sourcePos = source.Node.Transform.WorldMatrix.Translation;
        var distance = Vector3.Distance(listenerPos, sourcePos);
        float attenuation = ComputeAttenuation(distance, source.MinDistance, source.MaxDistance);
        float volume = source.Volume * attenuation * GlobalVolume;

        var state = new AudioSourceState(source, sourcePos, source.Velocity, volume, source.Pitch, attenuation);
        _backend.UpdateSource(state);
    }

    private static float ComputeAttenuation(float distance, float minDistance, float maxDistance)
    {
        minDistance = MathF.Max(0.01f, minDistance);
        maxDistance = MathF.Max(minDistance, maxDistance);
        if (distance <= minDistance)
        {
            return 1f;
        }

        if (distance >= maxDistance)
        {
            return 0f;
        }

        float t = (distance - minDistance) / (maxDistance - minDistance);
        return 1f - t;
    }
}
