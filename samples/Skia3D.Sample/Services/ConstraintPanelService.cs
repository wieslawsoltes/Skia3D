using System;
using System.Collections.Generic;
using Skia3D.Editor;
using Skia3D.Scene;
using Skia3D.Sample.ViewModels;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Sample.Services;

public sealed class ConstraintPanelService : IDisposable
{
    private readonly EditorSession _editor;
    private readonly EditorViewportService _viewportService;
    private readonly ConstraintPanelViewModel _viewModel;

    public ConstraintPanelService(EditorSession editor, EditorViewportService viewportService, ConstraintPanelViewModel viewModel)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _viewModel.ApplyRequested += ApplyConstraint;
        _viewModel.ClearRequested += ClearConstraints;
        _viewModel.TargetChanged += Refresh;
        _editor.SelectionService.SelectionChanged += Refresh;
    }

    public void Rebuild(SceneGraph sceneGraph)
    {
        if (sceneGraph == null)
        {
            throw new ArgumentNullException(nameof(sceneGraph));
        }

        _viewModel.Targets.Clear();
        foreach (var root in sceneGraph.Roots)
        {
            CollectTargets(root);
        }

        Refresh();
    }

    private void CollectTargets(SceneNode node)
    {
        var item = new SceneNodeItem(node);
        _viewModel.Targets.Add(item);
        foreach (var child in node.Children)
        {
            CollectTargets(child);
        }
    }

    private void ApplyConstraint()
    {
        var node = _viewportService.SelectedNode;
        var target = _viewModel.SelectedTarget?.Node;
        if (node == null || target == null || ReferenceEquals(node, target))
        {
            return;
        }

        node.Constraints.Clear();
        var weight = (float)_viewModel.Weight;

        switch (_viewModel.ConstraintTypeIndex)
        {
            case 1:
                var parent = new ParentConstraint(target, _viewModel.MaintainOffset) { Weight = weight };
                if (_viewModel.MaintainOffset)
                {
                    parent.CaptureOffset(node);
                }
                node.Constraints.Add(parent);
                break;
            case 2:
                var lookAt = new LookAtConstraint(target) { Weight = weight };
                node.Constraints.Add(lookAt);
                break;
            default:
                break;
        }

        Refresh();
    }

    private void ClearConstraints()
    {
        var node = _viewportService.SelectedNode;
        if (node == null)
        {
            return;
        }

        if (node.Constraints.Count == 0)
        {
            return;
        }

        node.Constraints.Clear();
        Refresh();
    }

    public void Refresh()
    {
        var node = _viewportService.SelectedNode;
        if (node == null)
        {
            _viewModel.SelectionLabel = "Constraint target: none";
            _viewModel.CanApply = false;
            _viewModel.CanClear = false;
            return;
        }

        _viewModel.SelectionLabel = $"Constraint target: {node.Name}";
        var target = _viewModel.SelectedTarget?.Node;
        _viewModel.CanApply = target != null && !ReferenceEquals(target, node);
        _viewModel.CanClear = node.Constraints.Count > 0;
    }

    public void Dispose()
    {
        _viewModel.ApplyRequested -= ApplyConstraint;
        _viewModel.ClearRequested -= ClearConstraints;
        _viewModel.TargetChanged -= Refresh;
        _editor.SelectionService.SelectionChanged -= Refresh;
    }
}
