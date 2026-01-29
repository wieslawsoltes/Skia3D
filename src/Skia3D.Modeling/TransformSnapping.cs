using System;
using System.Numerics;

namespace Skia3D.Modeling;

public static class TransformSnapping
{
    public static float Snap(float value, float step)
    {
        if (step <= 1e-6f)
        {
            return value;
        }

        return MathF.Round(value / step) * step;
    }

    public static Vector3 Snap(Vector3 value, float step)
    {
        return new Vector3(
            Snap(value.X, step),
            Snap(value.Y, step),
            Snap(value.Z, step));
    }

    public static float SnapAngleRadians(float radians, float stepRadians)
    {
        if (stepRadians <= 1e-6f)
        {
            return radians;
        }

        return MathF.Round(radians / stepRadians) * stepRadians;
    }

    public static float SnapAngleDegrees(float degrees, float stepDegrees)
    {
        if (stepDegrees <= 1e-6f)
        {
            return degrees;
        }

        return MathF.Round(degrees / stepDegrees) * stepDegrees;
    }
}
