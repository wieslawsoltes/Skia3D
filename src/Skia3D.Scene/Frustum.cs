using System.Numerics;

namespace Skia3D.Scene;

public readonly struct Frustum
{
    private readonly Vector4 _left;
    private readonly Vector4 _right;
    private readonly Vector4 _bottom;
    private readonly Vector4 _top;
    private readonly Vector4 _near;
    private readonly Vector4 _far;

    private Frustum(Vector4 left, Vector4 right, Vector4 bottom, Vector4 top, Vector4 near, Vector4 far)
    {
        _left = left;
        _right = right;
        _bottom = bottom;
        _top = top;
        _near = near;
        _far = far;
    }

    public static Frustum FromViewProjection(Matrix4x4 m)
    {
        var left = new Vector4(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41);
        var right = new Vector4(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
        var bottom = new Vector4(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42);
        var top = new Vector4(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
        var near = new Vector4(m.M13, m.M23, m.M33, m.M43);
        var far = new Vector4(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);

        return new Frustum(
            NormalizePlane(left),
            NormalizePlane(right),
            NormalizePlane(bottom),
            NormalizePlane(top),
            NormalizePlane(near),
            NormalizePlane(far));
    }

    public bool IntersectsSphere(Vector3 center, float radius)
    {
        return TestPlane(_left, center, radius)
            && TestPlane(_right, center, radius)
            && TestPlane(_bottom, center, radius)
            && TestPlane(_top, center, radius)
            && TestPlane(_near, center, radius)
            && TestPlane(_far, center, radius);
    }

    private static bool TestPlane(Vector4 plane, Vector3 center, float radius)
    {
        var dist = plane.X * center.X + plane.Y * center.Y + plane.Z * center.Z + plane.W;
        return dist >= -radius;
    }

    private static Vector4 NormalizePlane(Vector4 plane)
    {
        var n = new Vector3(plane.X, plane.Y, plane.Z);
        var len = n.Length();
        if (len < 1e-6f)
        {
            return plane;
        }

        var inv = 1f / len;
        return new Vector4(plane.X * inv, plane.Y * inv, plane.Z * inv, plane.W * inv);
    }
}
