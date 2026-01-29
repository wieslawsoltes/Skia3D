using System;
using System.Collections.Generic;
using Skia3D.Core;
using SceneGraph = Skia3D.Scene.Scene;
using Skia3D.Scene;

namespace Skia3D.Assets;

public static class SceneAssetUtilities
{
    public static IReadOnlyList<AssetId> RegisterSceneAssets(AssetManager manager, SceneGraph scene)
    {
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }

        if (scene == null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        var ids = new HashSet<AssetId>();
        foreach (var root in scene.Roots)
        {
            CollectNode(manager, root, ids);
        }

        return new List<AssetId>(ids);
    }

    private static void CollectNode(AssetManager manager, SceneNode node, HashSet<AssetId> ids)
    {
        if (node == null)
        {
            return;
        }

        var renderer = node.MeshRenderer;
        if (renderer != null)
        {
            if (renderer.Mesh != null)
            {
                ids.Add(Register(manager, renderer.Mesh));
            }

            if (renderer.Material != null)
            {
                ids.Add(Register(manager, renderer.Material));
                CollectMaterialTextures(manager, renderer.Material, ids);
            }
        }

        foreach (var child in node.Children)
        {
            CollectNode(manager, child, ids);
        }
    }

    private static void CollectMaterialTextures(AssetManager manager, Material material, HashSet<AssetId> ids)
    {
        if (material.BaseColorTexture != null)
        {
            ids.Add(Register(manager, material.BaseColorTexture));
        }

        if (material.MetallicRoughnessTexture != null)
        {
            ids.Add(Register(manager, material.MetallicRoughnessTexture));
        }

        if (material.NormalTexture != null)
        {
            ids.Add(Register(manager, material.NormalTexture));
        }

        if (material.EmissiveTexture != null)
        {
            ids.Add(Register(manager, material.EmissiveTexture));
        }

        if (material.OcclusionTexture != null)
        {
            ids.Add(Register(manager, material.OcclusionTexture));
        }
    }

    private static AssetId Register<T>(AssetManager manager, T asset) where T : class
    {
        if (manager.Cache.TryGetId(asset, out var existing))
        {
            return existing;
        }

        return manager.Register(asset).Id;
    }
}
