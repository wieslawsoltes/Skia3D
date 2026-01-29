using System;
using System.Threading;
using System.Threading.Tasks;

namespace Skia3D.Assets;

public interface IAssetLoader
{
    bool CanLoad(Type assetType);

    Task<AssetLoadResult> LoadAsync(AssetRequest request, CancellationToken cancellationToken);
}
