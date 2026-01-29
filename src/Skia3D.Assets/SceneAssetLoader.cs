using System;
using System.Threading;
using System.Threading.Tasks;
using SceneGraph = Skia3D.Scene.Scene;
using Skia3D.IO;

namespace Skia3D.Assets;

public sealed class SceneAssetLoader : IAssetLoader
{
    public bool CanLoad(Type assetType) => assetType == typeof(SceneGraph);

    public Task<AssetLoadResult> LoadAsync(AssetRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new InvalidOperationException("Scene asset requires a source path.");
        }

        var options = request.Options as SceneLoadOptions;
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = request.Source!;
            var result = SceneIo.Load(source, options);
            var scene = result.Scene;

            var dependencies = SceneAssetUtilities.RegisterSceneAssets(request.Manager, scene);
            return new AssetLoadResult(scene, dependencies);
        }, cancellationToken);
    }
}
