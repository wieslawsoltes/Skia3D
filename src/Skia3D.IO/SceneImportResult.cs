using System.Collections.Generic;
using Skia3D.Animation;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.IO;

public sealed class SceneImportResult
{
    public SceneImportResult(SceneGraph scene, IReadOnlyList<AnimationClip> animations)
    {
        Scene = scene;
        Animations = animations;
    }

    public SceneGraph Scene { get; }

    public IReadOnlyList<AnimationClip> Animations { get; }
}
