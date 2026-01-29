using System;
using System.Threading;
using System.Threading.Tasks;
using Skia3D.Core;

namespace Skia3D.Assets;

public sealed class TextureAssetLoader : IAssetLoader
{
    public bool CanLoad(Type assetType) => assetType == typeof(Texture2D);

    public Task<AssetLoadResult> LoadAsync(AssetRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new InvalidOperationException("Texture asset requires a source path.");
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var texture = Texture2D.FromFile(request.Source!);
            return AssetLoadResult.From(texture);
        }, cancellationToken);
    }
}
