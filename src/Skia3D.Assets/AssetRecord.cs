using System;
using System.Collections.Generic;

namespace Skia3D.Assets;

public sealed class AssetRecord
{
    public AssetRecord(AssetId id, Type assetType)
    {
        Id = id;
        AssetType = assetType ?? throw new ArgumentNullException(nameof(assetType));
    }

    public AssetId Id { get; }

    public Type AssetType { get; }

    public string? Source { get; set; }

    public List<AssetId> Dependencies { get; } = new();

    public int RefCount { get; internal set; }

    public object? Metadata { get; set; }
}
