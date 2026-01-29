using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Skia3D.Core;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Assets;

public sealed class AssetManager
{
    private readonly List<IAssetLoader> _loaders = new();

    public AssetManager(bool registerDefaultLoaders = true)
    {
        if (registerDefaultLoaders)
        {
            RegisterLoader(new MeshAssetLoader());
            RegisterLoader(new TextureAssetLoader());
            RegisterLoader(new SceneAssetLoader());
        }
    }

    public AssetRegistry Registry { get; } = new();

    public AssetCache Cache { get; } = new();

    public void RegisterLoader(IAssetLoader loader)
    {
        if (loader == null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        _loaders.Add(loader);
    }

    public AssetHandle<T> Register<T>(string? source = null, IEnumerable<AssetId>? dependencies = null)
    {
        var record = Registry.Register(typeof(T), null, source, dependencies);
        return new AssetHandle<T>(record.Id);
    }

    public AssetHandle<T> Register<T>(T asset, string? source = null, IEnumerable<AssetId>? dependencies = null) where T : class
    {
        if (asset == null)
        {
            throw new ArgumentNullException(nameof(asset));
        }

        if (Cache.TryGetId(asset, out var existing))
        {
            return new AssetHandle<T>(existing);
        }

        var record = Registry.Register(typeof(T), null, source, dependencies);
        Cache.Store(record.Id, asset);
        return new AssetHandle<T>(record.Id);
    }

    public bool TryGetCached<T>(AssetHandle<T> handle, out T? asset) where T : class
    {
        if (!handle.IsValid)
        {
            asset = null;
            return false;
        }

        return Cache.TryGet(handle.Id, out asset);
    }

    public Task<T?> LoadAsync<T>(AssetHandle<T> handle, object? options = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!handle.IsValid)
        {
            return Task.FromResult<T?>(null);
        }

        return LoadAsync<T>(handle.Id, options, cancellationToken);
    }

    public Task<T?> LoadAsync<T>(string source, object? options = null, CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source path cannot be empty.", nameof(source));
        }

        var existing = Registry.TryGetBySource(source);
        var id = existing ?? Register<T>(source).Id;
        return LoadAsync<T>(id, options, cancellationToken);
    }

    public async Task<T?> LoadAsync<T>(AssetId id, object? options = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!Registry.TryGet(id, out var record))
        {
            record = Registry.Register(typeof(T), id, null, null);
        }

        Registry.AddRef(id);

        if (Cache.TryGet(id, out T? cached))
        {
            return cached;
        }

        var loader = ResolveLoader(typeof(T));
        var request = new AssetRequest(id, typeof(T), record.Source, options, this, Registry, Cache);
        var result = await loader.LoadAsync(request, cancellationToken).ConfigureAwait(false);

        Cache.Store(id, result.Asset);
        Registry.SetDependencies(id, result.Dependencies);
        foreach (var dependency in result.Dependencies)
        {
            Registry.AddRef(dependency);
        }

        return result.Asset as T;
    }

    public void Release(AssetId id)
    {
        Release(id, recursive: true, visited: null);
    }

    public void Release(AssetHandle<SceneGraph> handle)
    {
        if (handle.IsValid)
        {
            Release(handle.Id);
        }
    }

    private void Release(AssetId id, bool recursive, HashSet<AssetId>? visited)
    {
        if (visited == null)
        {
            visited = new HashSet<AssetId>();
        }

        if (!visited.Add(id))
        {
            return;
        }

        if (!Registry.TryGet(id, out var record))
        {
            return;
        }

        var remaining = Registry.Release(id);
        if (remaining > 0)
        {
            return;
        }

        Cache.Remove(id, dispose: true);
        if (recursive)
        {
            foreach (var dependency in record.Dependencies)
            {
                Release(dependency, recursive: true, visited);
            }
        }
    }

    private IAssetLoader ResolveLoader(Type assetType)
    {
        for (int i = 0; i < _loaders.Count; i++)
        {
            if (_loaders[i].CanLoad(assetType))
            {
                return _loaders[i];
            }
        }

        throw new InvalidOperationException($"No asset loader registered for '{assetType.Name}'.");
    }
}
