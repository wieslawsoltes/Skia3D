namespace Skia3D.Editor;

public sealed class EditorSession
{
    public EditorSession()
    {
        Document = new EditorDocument();
        Selection = new EditorSelectionState();
        Mode = new EditorEditModeState();
        SelectionOptions = new EditorSelectionOptions();
        MeshEditOptions = new EditorMeshEditOptions();

        SelectionService = new EditorSelectionService(Selection, Document, Mode, SelectionOptions);
        MeshEdits = new EditorMeshEditService(Document, Selection, Mode, MeshEditOptions);
        Gizmo = new EditorGizmoService();
    }

    public EditorDocument Document { get; }

    public EditorSelectionState Selection { get; }

    public EditorEditModeState Mode { get; }

    public EditorSelectionOptions SelectionOptions { get; }

    public EditorMeshEditOptions MeshEditOptions { get; }

    public EditorSelectionService SelectionService { get; }

    public EditorMeshEditService MeshEdits { get; }

    public EditorGizmoService Gizmo { get; }
}
