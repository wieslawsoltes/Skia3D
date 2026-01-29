using Skia3D.Core;
using Skia3D.Modeling;

namespace Skia3D.Editor;

public static class EditorSelectionUtils
{
    public static int GetBestVertexIndex(Renderer3D.PickDetail pick)
    {
        var bary = pick.Barycentric;
        if (bary.X >= bary.Y && bary.X >= bary.Z)
        {
            return pick.VertexIndex0;
        }

        if (bary.Y >= bary.Z)
        {
            return pick.VertexIndex1;
        }

        return pick.VertexIndex2;
    }

    public static EdgeKey GetPickedEdge(Renderer3D.PickDetail pick)
    {
        var bary = pick.Barycentric;
        if (bary.X <= bary.Y && bary.X <= bary.Z)
        {
            return new EdgeKey(pick.VertexIndex1, pick.VertexIndex2);
        }

        if (bary.Y <= bary.Z)
        {
            return new EdgeKey(pick.VertexIndex0, pick.VertexIndex2);
        }

        return new EdgeKey(pick.VertexIndex0, pick.VertexIndex1);
    }
}
