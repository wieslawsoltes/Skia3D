using System.IO;
using System.Text;
using Skia3D.IO;
using Xunit;

namespace Skia3D.Core.Tests;

public sealed class GltfImporterTests
{
    [Fact]
    public void GltfImporter_Load_AllowsEmptyScene()
    {
        var json = "{\"asset\":{\"version\":\"2.0\"}}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var importer = new GltfImporter();
        var mesh = importer.Load(stream, new MeshLoadOptions());

        Assert.Empty(mesh.Vertices);
        Assert.Empty(mesh.Indices);
    }

    [Fact]
    public void GltfImporter_LoadScene_AllowsEmptyScene()
    {
        var json = "{\"asset\":{\"version\":\"2.0\"}}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var importer = new GltfImporter();
        var result = importer.Load(stream, new SceneLoadOptions());

        Assert.Empty(result.Scene.Roots);
        Assert.Empty(result.Animations);
    }
}
