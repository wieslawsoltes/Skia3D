namespace Skia3D.IO;

public sealed class SceneLoadOptions
{
    public MeshLoadOptions MeshOptions { get; set; } = new();

    public bool LoadMaterials { get; set; } = true;

    public bool LoadAnimations { get; set; } = true;

    public string? SourcePath
    {
        get => MeshOptions.SourcePath;
        set => MeshOptions.SourcePath = value;
    }
}
