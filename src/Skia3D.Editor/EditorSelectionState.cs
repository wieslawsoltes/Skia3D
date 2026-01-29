using Skia3D.Core;
using Skia3D.Modeling;

namespace Skia3D.Editor;

public sealed class EditorSelectionState
{
    public SelectionSet<MeshInstance> ObjectSelection { get; } = new(SelectionKind.Object);

    public SelectionSet<int> VertexSelection { get; } = SelectionSets.Vertices();

    public SelectionSet<EdgeKey> EdgeSelection { get; } = SelectionSets.Edges();

    public SelectionSet<int> FaceSelection { get; } = SelectionSets.Faces();

    public MeshInstance? Selected { get; set; }

    public Renderer3D.PickDetail? LastPick { get; set; }

    public void ClearAll()
    {
        ObjectSelection.Clear();
        VertexSelection.Clear();
        EdgeSelection.Clear();
        FaceSelection.Clear();
        Selected = null;
        LastPick = null;
    }

    public void ClearSubSelection()
    {
        VertexSelection.Clear();
        EdgeSelection.Clear();
        FaceSelection.Clear();
        LastPick = null;
    }

    public MeshInstance? GetFirstSelection()
    {
        foreach (var item in ObjectSelection.Items)
        {
            return item;
        }

        return null;
    }

    public void ReplaceInstance(MeshInstance oldInstance, MeshInstance newInstance)
    {
        if (Selected == oldInstance)
        {
            Selected = newInstance;
        }

        if (ObjectSelection.Items.Remove(oldInstance))
        {
            ObjectSelection.Items.Add(newInstance);
        }

        LastPick = null;
    }
}
