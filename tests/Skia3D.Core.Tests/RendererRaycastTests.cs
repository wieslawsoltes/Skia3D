using System.Numerics;
using Skia3D.Core;
using SkiaSharp;
using Xunit;

namespace Skia3D.Core.Tests;

public sealed class RendererRaycastTests
{
    [Fact]
    public void Renderer_Raycast_HitsTriangle()
    {
        var positions = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f)
        };
        // Keep winding front-facing for +Z ray when backface culling is enabled.
        var indices = new[] { 0, 2, 1 };
        var mesh = MeshFactory.CreateFromData(positions, indices, colors: new[] { SKColors.White, SKColors.White, SKColors.White });
        var instance = new MeshInstance(mesh)
        {
            Transform = Matrix4x4.Identity
        };

        var renderer = new Renderer3D();
        var origin = new Vector3(0.25f, 0.25f, -1f);
        var direction = Vector3.UnitZ;

        var hit = renderer.TryRaycastDetailed(origin, direction, new[] { instance }, 10f, out var detail);

        Assert.True(hit);
        Assert.Equal(instance, detail.Instance);
        Assert.True(detail.Distance > 0f);
        Assert.InRange(detail.Position.X, 0f, 1f);
        Assert.InRange(detail.Position.Y, 0f, 1f);
    }
}
