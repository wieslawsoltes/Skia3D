using System;
using System.Numerics;

namespace Skia3D.Geometry;

public readonly struct SoaVector3
{
    public SoaVector3(float[] x, float[] y, float[] z)
    {
        if (x is null)
        {
            throw new ArgumentNullException(nameof(x));
        }

        if (y is null)
        {
            throw new ArgumentNullException(nameof(y));
        }

        if (z is null)
        {
            throw new ArgumentNullException(nameof(z));
        }

        if (x.Length != y.Length || x.Length != z.Length)
        {
            throw new ArgumentException("SoA arrays must be the same length.");
        }

        X = x;
        Y = y;
        Z = z;
    }

    public float[] X { get; }

    public float[] Y { get; }

    public float[] Z { get; }

    public int Length => X.Length;

    public Vector3 Get(int index) => new(X[index], Y[index], Z[index]);

    public static SoaVector3 From(ReadOnlySpan<Vector3> positions)
    {
        var count = positions.Length;
        var x = new float[count];
        var y = new float[count];
        var z = new float[count];

        for (int i = 0; i < count; i++)
        {
            var p = positions[i];
            x[i] = p.X;
            y[i] = p.Y;
            z[i] = p.Z;
        }

        return new SoaVector3(x, y, z);
    }
}
