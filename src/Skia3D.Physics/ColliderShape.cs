using System.Numerics;
using Skia3D.Geometry;

namespace Skia3D.Physics;

public abstract class ColliderShape
{
    public Vector3 Offset { get; set; }

    public abstract Aabb ComputeAabb(Matrix4x4 world);
}

public sealed class SphereShape : ColliderShape
{
    public SphereShape(float radius)
    {
        Radius = MathF.Max(0f, radius);
    }

    public float Radius { get; set; }

    public override Aabb ComputeAabb(Matrix4x4 world)
    {
        GetWorldSphere(world, out var center, out var radius);
        var extents = new Vector3(radius, radius, radius);
        return new Aabb(center - extents, center + extents);
    }

    public void GetWorldSphere(Matrix4x4 world, out Vector3 center, out float radius)
    {
        center = Vector3.Transform(Offset, world);
        var scale = PhysicsMath.ExtractMaxScale(world);
        radius = Radius * scale;
    }
}

public sealed class BoxShape : ColliderShape
{
    public BoxShape(Vector3 size)
    {
        Size = size;
    }

    public Vector3 Size { get; set; }

    public override Aabb ComputeAabb(Matrix4x4 world)
    {
        var half = Size * 0.5f;
        return PhysicsMath.TransformAabb(Offset, half, world);
    }
}
