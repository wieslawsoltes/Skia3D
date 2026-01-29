namespace Skia3D.Geometry;

public readonly record struct Int4(int X, int Y, int Z, int W)
{
    public static Int4 Zero => new(0, 0, 0, 0);
}
