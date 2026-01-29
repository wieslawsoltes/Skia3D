using System;
using Skia3D.Editor;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Services;

public sealed class CommandStateService
{
    private readonly EditorSession _editor;
    private readonly CommandStateViewModel _viewModel;

    public CommandStateService(EditorSession editor, CommandStateViewModel viewModel)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public void Refresh()
    {
        var mode = _editor.Mode;
        var selection = _editor.Selection;
        var enabled = mode.EditMode;
        var selected = selection.Selected;
        var hasEditable = enabled && selected != null && _editor.Document.EditableMeshes.ContainsKey(selected);
        var canEdgeOp = hasEditable && selection.LastPick.HasValue && ReferenceEquals(selection.LastPick.Value.Instance, selected);
        var hasVertexSelection = hasEditable && mode.VertexSelect && selection.VertexSelection.Count > 1;
        var hasEdgeSelection = hasEditable && mode.EdgeSelect && !selection.EdgeSelection.IsEmpty;
        var hasFaceSelection = hasEditable && mode.FaceSelect && !selection.FaceSelection.IsEmpty;
        var canFace = enabled && mode.FaceSelect && !selection.FaceSelection.IsEmpty;
        var canSelectUvIsland = hasEditable
            && enabled
            && selection.LastPick.HasValue
            && ReferenceEquals(selection.LastPick.Value.Instance, selected);
        bool hasSeams = false;
        if (hasEditable && selected != null && _editor.Document.TryGetEditableMesh(selected, out var editable))
        {
            hasSeams = editable.SeamEdges.Count > 0;
        }

        _viewModel.CanExtrudeFaces = canFace;
        _viewModel.CanBevelFaces = canFace;
        _viewModel.CanInsetFaces = canFace;
        _viewModel.CanLoopCutEdgeLoop = canEdgeOp;
        _viewModel.CanSplitEdge = canEdgeOp;
        _viewModel.CanBridgeEdges = canEdgeOp;
        _viewModel.CanBridgeEdgeLoops = hasEdgeSelection || canEdgeOp;
        _viewModel.CanMergeVertices = hasVertexSelection;
        _viewModel.CanDissolveFaces = hasFaceSelection;
        _viewModel.CanDissolveEdge = canEdgeOp;
        _viewModel.CanCollapseEdge = canEdgeOp;
        _viewModel.CanCleanupMesh = enabled;
        _viewModel.CanSmoothMesh = enabled;
        _viewModel.CanSimplifyMesh = enabled;
        _viewModel.CanPlanarUv = hasEditable;
        _viewModel.CanBoxUv = hasEditable;
        _viewModel.CanNormalizeUv = hasEditable;
        _viewModel.CanFlipU = hasEditable;
        _viewModel.CanFlipV = hasEditable;
        _viewModel.CanUnwrapUv = hasEditable;
        _viewModel.CanPackUv = hasEditable;
        _viewModel.CanMarkUvSeams = hasEdgeSelection;
        _viewModel.CanClearUvSeams = hasEdgeSelection;
        _viewModel.CanClearAllUvSeams = hasSeams;
        _viewModel.CanSelectUvIsland = canSelectUvIsland;
        _viewModel.CanAssignUvGroup = hasFaceSelection;
        _viewModel.CanClearUvGroup = hasFaceSelection;
    }
}
