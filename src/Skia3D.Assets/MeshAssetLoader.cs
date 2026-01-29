using System;
using System.Threading;
using System.Threading.Tasks;
using Skia3D.Core;
using Skia3D.IO;

namespace Skia3D.Assets;

public sealed class MeshAssetLoader : IAssetLoader
{
    public bool CanLoad(Type assetType) => assetType == typeof(Mesh);

    public Task<AssetLoadResult> LoadAsync(AssetRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new InvalidOperationException("Mesh asset requires a source path.");
        }

        var options = request.Options as MeshLoadOptions;
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mesh = MeshIo.Load(request.Source!, options);
            return AssetLoadResult.From(mesh);
        }, cancellationToken);
    }
}
