namespace Skia3D.Assets;

public readonly record struct AssetHandle<T>(AssetId Id)
{
    public bool IsValid => Id.IsValid;

    public static AssetHandle<T> Invalid => default;
}
