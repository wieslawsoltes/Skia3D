using System.Collections.Generic;
using System.IO;

namespace Skia3D.IO;

public interface ISceneImporter
{
    IReadOnlyList<string> Extensions { get; }

    SceneImportResult Load(Stream stream, SceneLoadOptions options);
}
