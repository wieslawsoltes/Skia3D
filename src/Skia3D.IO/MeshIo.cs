using System;
using System.Collections.Generic;
using System.IO;
using Skia3D.Core;

namespace Skia3D.IO;

public static class MeshIo
{
    private static readonly List<IMeshImporter> Importers = new()
    {
        new ObjImporter(),
        new PlyImporter(),
        new GltfImporter()
    };

    public static IReadOnlyList<IMeshImporter> RegisteredImporters => Importers;

    public static void RegisterImporter(IMeshImporter importer)
    {
        if (importer is null)
        {
            throw new ArgumentNullException(nameof(importer));
        }

        Importers.Add(importer);
    }

    public static Mesh Load(string path, MeshLoadOptions? options = null)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        options ??= new MeshLoadOptions();
        options.SourcePath ??= path;
        var extension = Path.GetExtension(path);
        using var stream = File.OpenRead(path);
        return Load(stream, extension, options);
    }

    public static Mesh Load(Stream stream, string extension, MeshLoadOptions? options = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        options ??= new MeshLoadOptions();
        var importer = ResolveImporter(extension);
        var mesh = importer.Load(stream, options);
        if (options.Processing is null)
        {
            return mesh;
        }

        return MeshProcessing.Apply(mesh, options.Processing);
    }

    private static IMeshImporter ResolveImporter(string extension)
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

        throw new NotSupportedException($"No importer registered for '{normalized}'.");
    }
}
