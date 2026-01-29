using Skia3D.Scene;

namespace Skia3D.Physics;

public sealed class ColliderComponent : SceneComponent
{
    public ColliderComponent(ColliderShape shape)
    {
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));
    }

    public ColliderShape Shape { get; set; }

    public bool IsTrigger { get; set; }

    public RigidBodyComponent? Body { get; set; }
}
