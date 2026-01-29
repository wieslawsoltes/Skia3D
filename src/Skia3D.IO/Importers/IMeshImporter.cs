using System.Collections.Generic;
using System.IO;
using Skia3D.Core;

namespace Skia3D.IO;

public interface IMeshImporter
{
    IReadOnlyList<string> Extensions { get; }

    Mesh Load(Stream stream, MeshLoadOptions options);
}
