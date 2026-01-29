using System;
using System.Collections.Generic;
using System.IO;

namespace Skia3D.IO;

public static class SceneIo
{
    private static readonly List<ISceneImporter> Importers = new()
    {
        new GltfImporter(),
        new ObjImporter(),
        new PlyImporter()
    };

    public static IReadOnlyList<ISceneImporter> RegisteredImporters => Importers;

    public static void RegisterImporter(ISceneImporter importer)
    {
        if (importer is null)
        {
            throw new ArgumentNullException(nameof(importer));
        }

        Importers.Add(importer);
    }

    public static SceneImportResult Load(string path, SceneLoadOptions? options = null)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        options ??= new SceneLoadOptions();
        options.SourcePath ??= path;
        var extension = Path.GetExtension(path);
        using var stream = File.OpenRead(path);
        return Load(stream, extension, options);
    }

    public static SceneImportResult Load(Stream stream, string extension, SceneLoadOptions? options = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        options ??= new SceneLoadOptions();
        var importer = ResolveImporter(extension);
        return importer.Load(stream, options);
    }

    private static ISceneImporter ResolveImporter(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Missing file extension.", nameof(extension));
        }

        var normalized = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        for (int i = 0; i < Importers.Count; i++)
        {
            var importer = Importers[i];
            var extensions = importer.Extensions;
            for (int j = 0; j < extensions.Count; j++)
            {
                if (string.Equals(extensions[j], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return importer;
                }
            }
        }

        throw new NotSupportedException($"No scene importer registered for '{normalized}'.");
    }
}
