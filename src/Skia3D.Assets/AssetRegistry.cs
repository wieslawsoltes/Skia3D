using System;
using System.Collections.Generic;

namespace Skia3D.Assets;

public sealed class AssetRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<AssetId, AssetRecord> _records = new();
    private readonly Dictionary<string, AssetId> _sourceIndex = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<AssetId, AssetRecord> Records => _records;

    public AssetRecord Register(Type assetType, AssetId? id = null, string? source = null, IEnumerable<AssetId>? dependencies = null)
    {
        if (assetType == null)
        {
            throw new ArgumentNullException(nameof(assetType));
        }

        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(source) && _sourceIndex.TryGetValue(source, out var existingId) && _records.TryGetValue(existingId, out var existing))
            {
                return existing;
            }

            var resolvedId = id.HasValue && id.Value.IsValid ? id.Value : AssetId.New();
            if (_records.TryGetValue(resolvedId, out var record))
            {
                if (!string.IsNullOrWhiteSpace(source))
                {
                    record.Source = source;
                    _sourceIndex[source] = resolvedId;
                }

                if (dependencies != null)
                {
                    record.Dependencies.Clear();
                    record.Dependencies.AddRange(dependencies);
                }

                return record;
            }

            record = new AssetRecord(resolvedId, assetType)
            {
                Source = source
            };

            if (dependencies != null)
            {
                record.Dependencies.AddRange(dependencies);
            }

            _records[resolvedId] = record;
            if (!string.IsNullOrWhiteSpace(source))
            {
                _sourceIndex[source] = resolvedId;
            }

            return record;
        }
    }

    public bool TryGet(AssetId id, out AssetRecord record)
    {
        lock (_sync)
        {
            return _records.TryGetValue(id, out record!);
        }
    }

    public AssetRecord? Get(AssetId id)
    {
        return TryGet(id, out var record) ? record : null;
    }

    public AssetId? TryGetBySource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        lock (_sync)
        {
            return _sourceIndex.TryGetValue(source, out var id) ? id : null;
        }
    }

    public void SetDependencies(AssetId id, IEnumerable<AssetId> dependencies)
    {
        lock (_sync)
        {
            if (_records.TryGetValue(id, out var record))
            {
                record.Dependencies.Clear();
                record.Dependencies.AddRange(dependencies);
            }
        }
    }

    public void AddDependency(AssetId id, AssetId dependency)
    {
        lock (_sync)
        {
            if (_records.TryGetValue(id, out var record))
            {
                record.Dependencies.Add(dependency);
            }
        }
    }

    public void AddRef(AssetId id)
    {
        lock (_sync)
        {
            if (_records.TryGetValue(id, out var record))
            {
                record.RefCount++;
            }
        }
    }

    public int Release(AssetId id)
    {
        lock (_sync)
        {
            if (_records.TryGetValue(id, out var record))
            {
                record.RefCount = Math.Max(0, record.RefCount - 1);
                return record.RefCount;
            }
        }

        return 0;
    }
}
