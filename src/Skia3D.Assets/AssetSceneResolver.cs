using System;
using Skia3D.Core;
using Skia3D.Scene;

namespace Skia3D.Assets;

public sealed class AssetSceneResolver : ISceneAssetResolver
{
    private readonly AssetManager _manager;

    public AssetSceneResolver(AssetManager manager, bool loadOnResolve = false)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        LoadOnResolve = loadOnResolve;
    }

    public bool LoadOnResolve { get; set; }

    public string? GetMeshId(Mesh mesh)
    {
        return GetId(mesh);
    }

    public Mesh? ResolveMesh(string id)
    {
        return Resolve<Mesh>(id);
    }

    public string? GetMaterialId(Material material)
    {
        return GetId(material);
    }

    public Material? ResolveMaterial(string id)
    {
        return Resolve<Material>(id);
    }

    public string? GetTextureId(Texture2D texture)
    {
        return GetId(texture);
    }

    public Texture2D? ResolveTexture(string id)
    {
        return Resolve<Texture2D>(id);
    }

    private string? GetId<T>(T asset) where T : class
    {
        if (asset == null)
        {
            return null;
        }

        if (_manager.Cache.TryGetId(asset, out var existing))
        {
            return existing.Value;
        }

        var handle = _manager.Register(asset);
        return handle.Id.Value;
    }

    private T? Resolve<T>(string id) where T : class
    {
        if (!AssetId.TryParse(id, out var assetId))
        {
            return null;
        }

        if (_manager.Cache.TryGet(assetId, out T? cached))
        {
            return cached;
        }

        if (!LoadOnResolve)
        {
            return null;
        }

        return _manager.LoadAsync<T>(assetId).GetAwaiter().GetResult();
    }
}
