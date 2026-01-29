using System.Numerics;

namespace Skia3D.Geometry;

public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    public Vector3 GetPoint(float t) => Origin + Direction * t;
}
