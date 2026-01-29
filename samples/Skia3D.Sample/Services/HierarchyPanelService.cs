using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Skia3D.Editor;
using Skia3D.Scene;
using Skia3D.Sample.ViewModels;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Sample.Services;

public sealed class HierarchyPanelService : IDisposable
{
    private readonly EditorSession _editor;
    private readonly EditorViewportService _viewportService;
    private readonly HierarchyPanelViewModel _viewModel;
    private readonly Dictionary<SceneNode, SceneNodeItem> _lookup = new();
    private bool _suppressSelectionChange;

    public HierarchyPanelService(EditorSession editor, EditorViewportService viewportService, HierarchyPanelViewModel viewModel)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.SelectionChanged += OnSelectionChanged;
    }

    public event Action? SelectionApplied;

    public void Rebuild(SceneGraph sceneGraph)
    {
        if (sceneGraph == null)
        {
            throw new ArgumentNullException(nameof(sceneGraph));
        }

        _viewModel.Items.Clear();
        _lookup.Clear();

        foreach (var root in sceneGraph.Roots)
        {
            var item = BuildHierarchyItem(root);
            _viewModel.Items.Add(item);
        }

        Refresh();
    }

    public void Refresh()
    {
        var node = _viewportService.SelectedNode;
        var instance = node?.MeshInstance;
        var hasEditable = instance != null && _editor.Document.EditableMeshes.ContainsKey(instance);

        _viewModel.CanCenterPivot = hasEditable;
        _viewModel.CanResetTransform = node != null;
        _viewModel.CanRename = node != null;
        _viewModel.CanIsolate = node != null;
        _viewModel.CanUnhide = HasHiddenNodes();
        _viewModel.SelectionLabel = node == null ? "Selection: none" : $"Selection: {node.Name}";

        if (node == null)
        {
            _viewModel.PositionLabel = "Position: --";
            _viewModel.RotationLabel = "Rotation: --";
            _viewModel.ScaleLabel = "Scale: --";
        }
        else
        {
            var pos = node.Transform.LocalPosition;
            var rot = node.Transform.LocalRotation;
            var scale = node.Transform.LocalScale;
            var euler = ToEulerDegrees(rot);

            _viewModel.PositionLabel = $"Position: {FormatVector(pos)}";
            _viewModel.RotationLabel = $"Rotation: {FormatVector(euler)}";
            _viewModel.ScaleLabel = $"Scale: {FormatVector(scale)}";
        }

        SceneNodeItem? selectedItem = null;
        if (node != null && _lookup.TryGetValue(node, out var item))
        {
            selectedItem = item;
        }

        _suppressSelectionChange = true;
        _viewModel.SetSelectedItem(selectedItem);
        _suppressSelectionChange = false;
    }

    public void UpdateNodeName(SceneNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (_lookup.TryGetValue(node, out var item))
        {
            item.Name = node.Name;
        }
    }

    private SceneNodeItem BuildHierarchyItem(SceneNode node)
    {
        var item = new SceneNodeItem(node);
        _lookup[node] = item;
        foreach (var child in node.Children)
        {
            item.Children.Add(BuildHierarchyItem(child));
        }

        return item;
    }

    private void OnSelectionChanged(SceneNodeItem? item)
    {
        if (_suppressSelectionChange)
        {
            return;
        }

        _editor.Selection.ClearAll();
        var instance = item?.Node.MeshInstance;
        if (instance != null)
        {
            _editor.Selection.ObjectSelection.Add(instance);
            _editor.Selection.Selected = instance;
        }

        SelectionApplied?.Invoke();
    }

    private bool HasHiddenNodes()
    {
        foreach (var root in _viewportService.SceneGraph.Roots)
        {
            if (HasHiddenNodes(root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHiddenNodes(SceneNode node)
    {
        var renderer = node.MeshRenderer;
        if (renderer != null && !renderer.IsVisible)
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (HasHiddenNodes(child))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatVector(Vector3 value)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.##}, {1:0.##}, {2:0.##}", value.X, value.Y, value.Z);
    }

    private static Vector3 ToEulerDegrees(Quaternion q)
    {
        var sinrCosp = 2f * (q.W * q.X + q.Y * q.Z);
        var cosrCosp = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        var roll = MathF.Atan2(sinrCosp, cosrCosp);

        var sinp = 2f * (q.W * q.Y - q.Z * q.X);
        float pitch;
        if (MathF.Abs(sinp) >= 1f)
        {
            pitch = MathF.CopySign(MathF.PI * 0.5f, sinp);
        }
        else
        {
            pitch = MathF.Asin(sinp);
        }

        var sinyCosp = 2f * (q.W * q.Z + q.X * q.Y);
        var cosyCosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        var yaw = MathF.Atan2(sinyCosp, cosyCosp);

        var radToDeg = 180f / MathF.PI;
        return new Vector3(roll * radToDeg, pitch * radToDeg, yaw * radToDeg);
    }

    public void Dispose()
    {
        _viewModel.SelectionChanged -= OnSelectionChanged;
    }
}
