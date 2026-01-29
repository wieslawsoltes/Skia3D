namespace Skia3D.Editor;

public enum GizmoMode
{
    Translate,
    Rotate,
    Scale
}

public enum GizmoAxisConstraint
{
    None,
    X,
    Y,
    Z,
    XY,
    XZ,
    YZ
}

public enum SelectionTool
{
    Click,
    Box,
    Paint,
    Lasso
}

public enum SelectionOperation
{
    Replace,
    Add,
    Subtract
}
