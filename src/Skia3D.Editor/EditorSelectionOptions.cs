namespace Skia3D.Editor;

public sealed class EditorSelectionOptions
{
    public SelectionTool Tool { get; set; } = SelectionTool.Click;

    public bool Crossing { get; set; }

    public float PaintRadius { get; set; } = 24f;
}
