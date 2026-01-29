using SkiaSharp;

namespace Skia3D.IO;

public sealed class MeshLoadOptions
{
    public SKColor DefaultColor { get; set; } = new SKColor(200, 200, 200);

    public bool GenerateNormals { get; set; } = true;

    public MeshProcessingOptions? Processing { get; set; }

    public string? SourcePath { get; set; }
}
