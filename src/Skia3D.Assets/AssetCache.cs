using System;
using System.Collections.Generic;

namespace Skia3D.Assets;

public sealed class AssetCache
{
    private readonly object _sync = new();
    private readonly Dictionary<AssetId, object> _assets = new();
    private readonly Dictionary<object, AssetId> _reverse = new(ReferenceEqualityComparer.Instance);

    public bool TryGet<T>(AssetId id, out T? asset) where T : class
    {
        lock (_sync)
        {
            if (_assets.TryGetValue(id, out var value) && value is T typed)
            {
                asset = typed;
                return true;
            }
        }

        asset = null;
        return false;
    }

    public bool TryGetId<T>(T asset, out AssetId id) where T : class
    {
        lock (_sync)
        {
            return _reverse.TryGetValue(asset, out id);
        }
    }

    public void Store(AssetId id, object asset)
    {
        if (asset == null)
        {
            throw new ArgumentNullException(nameof(asset));
        }

        lock (_sync)
        {
            _assets[id] = asset;
            _reverse[asset] = id;
        }
    }

    public bool Remove(AssetId id, bool dispose)
    {
        object? asset;
        lock (_sync)
        {
            if (!_assets.TryGetValue(id, out asset))
            {
                return false;
            }

            _assets.Remove(id);
            _reverse.Remove(asset);
        }

        if (dispose && asset is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return true;
    }
}
