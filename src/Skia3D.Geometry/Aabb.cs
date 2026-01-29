using System.Numerics;

namespace Skia3D.Geometry;

public readonly record struct Aabb(Vector3 Min, Vector3 Max)
{
    public static Aabb Empty => new(
        new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
        new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));

    public bool IsValid => Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;

    public Vector3 Center => (Min + Max) * 0.5f;

    public Vector3 Size => Max - Min;

    public Aabb Encapsulate(Vector3 point)
    {
        if (!IsValid)
        {
            return new Aabb(point, point);
        }

        return new Aabb(Vector3.Min(Min, point), Vector3.Max(Max, point));
    }

    public static Aabb Merge(Aabb a, Aabb b)
    {
        if (!a.IsValid)
        {
            return b;
        }

        if (!b.IsValid)
        {
            return a;
        }

        return new Aabb(Vector3.Min(a.Min, b.Min), Vector3.Max(a.Max, b.Max));
    }
}
