using System.Numerics;
using Skia3D.Geometry;
using Xunit;

namespace Skia3D.Core.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void Aabb_Merge_Encapsulate_ProducesExpectedBounds()
    {
        var a = new Aabb(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f));
        var b = new Aabb(new Vector3(-2f, -1f, 0.5f), new Vector3(0.5f, 2f, 3f));

        var merged = Aabb.Merge(a, b);

        Assert.Equal(new Vector3(-2f, -1f, 0f), merged.Min);
        Assert.Equal(new Vector3(1f, 2f, 3f), merged.Max);

        var encapsulated = merged.Encapsulate(new Vector3(4f, 0f, -1f));
        Assert.Equal(new Vector3(-2f, -1f, -1f), encapsulated.Min);
        Assert.Equal(new Vector3(4f, 2f, 3f), encapsulated.Max);
    }
}
