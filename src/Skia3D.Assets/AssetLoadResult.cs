using System;
using System.Collections.Generic;

namespace Skia3D.Assets;

public sealed record AssetLoadResult(object Asset, IReadOnlyList<AssetId> Dependencies)
{
    public static AssetLoadResult From(object asset, IEnumerable<AssetId>? dependencies = null)
    {
        var list = dependencies == null ? new List<AssetId>() : new List<AssetId>(dependencies);
        return new AssetLoadResult(asset, list);
    }
}
