using System.Numerics;
using Skia3D.Geometry;

namespace Skia3D.Physics;

internal static class PhysicsMath
{
    public static float ExtractMaxScale(Matrix4x4 m)
    {
        var sx = new Vector3(m.M11, m.M12, m.M13).Length();
        var sy = new Vector3(m.M21, m.M22, m.M23).Length();
        var sz = new Vector3(m.M31, m.M32, m.M33).Length();
        return MathF.Max(sx, MathF.Max(sy, sz));
    }

    public static Matrix4x4 ComposeWorld(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        return Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(rotation)
            * Matrix4x4.CreateTranslation(position);
    }

    public static Aabb TransformAabb(Vector3 center, Vector3 halfExtents, Matrix4x4 world)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = center + new Vector3(-halfExtents.X, -halfExtents.Y, -halfExtents.Z);
        corners[1] = center + new Vector3(halfExtents.X, -halfExtents.Y, -halfExtents.Z);
        corners[2] = center + new Vector3(-halfExtents.X, halfExtents.Y, -halfExtents.Z);
        corners[3] = center + new Vector3(halfExtents.X, halfExtents.Y, -halfExtents.Z);
        corners[4] = center + new Vector3(-halfExtents.X, -halfExtents.Y, halfExtents.Z);
        corners[5] = center + new Vector3(halfExtents.X, -halfExtents.Y, halfExtents.Z);
        corners[6] = center + new Vector3(-halfExtents.X, halfExtents.Y, halfExtents.Z);
        corners[7] = center + new Vector3(halfExtents.X, halfExtents.Y, halfExtents.Z);

        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            var worldCorner = Vector3.Transform(corners[i], world);
            min = Vector3.Min(min, worldCorner);
            max = Vector3.Max(max, worldCorner);
        }

        return new Aabb(min, max);
    }

    public static bool RaycastSphere(Ray ray, Vector3 center, float radius, float maxDistance, out float t, out Vector3 normal)
    {
        t = 0f;
        normal = Vector3.Zero;

        var m = ray.Origin - center;
        float b = Vector3.Dot(m, ray.Direction);
        float c = Vector3.Dot(m, m) - radius * radius;

        if (c > 0f && b > 0f)
        {
            return false;
        }

        float discr = b * b - c;
        if (discr < 0f)
        {
            return false;
        }

        float sqrt = MathF.Sqrt(discr);
        float tHit = -b - sqrt;
        if (tHit < 0f)
        {
            tHit = -b + sqrt;
        }

        if (tHit < 0f || tHit > maxDistance)
        {
            return false;
        }

        t = tHit;
        var point = ray.Origin + ray.Direction * t;
        var n = point - center;
        if (n.LengthSquared() > 1e-8f)
        {
            normal = Vector3.Normalize(n);
        }
        else
        {
            normal = Vector3.UnitY;
        }

        return true;
    }

    public static bool RaycastAabb(Ray ray, Aabb aabb, float maxDistance, out float t, out Vector3 normal)
    {
        t = 0f;
        normal = Vector3.Zero;

        float tmin = 0f;
        float tmax = maxDistance;
        Vector3 n = Vector3.Zero;

        if (!RaycastSlab(ray.Origin.X, ray.Direction.X, aabb.Min.X, aabb.Max.X, Vector3.UnitX, ref tmin, ref tmax, ref n))
        {
            return false;
        }
        if (!RaycastSlab(ray.Origin.Y, ray.Direction.Y, aabb.Min.Y, aabb.Max.Y, Vector3.UnitY, ref tmin, ref tmax, ref n))
        {
            return false;
        }
        if (!RaycastSlab(ray.Origin.Z, ray.Direction.Z, aabb.Min.Z, aabb.Max.Z, Vector3.UnitZ, ref tmin, ref tmax, ref n))
        {
            return false;
        }

        float hit = tmin >= 0f ? tmin : tmax;
        if (hit < 0f || hit > maxDistance)
        {
            return false;
        }

        t = hit;
        normal = n.LengthSquared() > 0f ? n : Vector3.UnitY;
        return true;
    }

    private static bool RaycastSlab(float origin, float direction, float min, float max, Vector3 axis, ref float tmin, ref float tmax, ref Vector3 normal)
    {
        const float epsilon = 1e-8f;
        if (MathF.Abs(direction) < epsilon)
        {
            return origin >= min && origin <= max;
        }

        float inv = 1f / direction;
        float t1 = (min - origin) * inv;
        float t2 = (max - origin) * inv;
        Vector3 n1 = -axis;
        Vector3 n2 = axis;

        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
            (n1, n2) = (n2, n1);
        }

        if (t1 > tmin)
        {
            tmin = t1;
            normal = n1;
        }

        if (t2 < tmax)
        {
            tmax = t2;
        }

        return tmin <= tmax;
    }
}
