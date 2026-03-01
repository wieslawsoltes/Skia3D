using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Geometry;
using Skia3D.Modeling;
using Skia3D.Scene;

namespace Skia3D.Editor;

public sealed class EditorMeshEditService
{
    private readonly EditorDocument _document;
    private readonly EditorSelectionState _selection;
    private readonly EditorEditModeState _mode;
    private readonly CommandStack _commands = new();
    private EdgeKey? _bridgeAnchor;
    private MeshInstance? _bridgeAnchorInstance;
    private EdgeKey? _bridgeLoopAnchor;
    private MeshInstance? _bridgeLoopAnchorInstance;

    public EditorMeshEditService(EditorDocument document, EditorSelectionState selection, EditorEditModeState mode, EditorMeshEditOptions options)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _mode = mode ?? throw new ArgumentNullException(nameof(mode));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public event Action? MeshEdited;

    public event Action? SelectionChanged;

    public event Action? CommandStateChanged;

    public EditorMeshEditOptions Options { get; }

    public float NudgeStep { get; set; } = 0.2f;

    public float WeldTolerance { get; set; } = 0.02f;

    public float ExtrudeDistance { get; set; } = 0.6f;

    public float BevelInset { get; set; } = 0.2f;

    public float BevelHeight { get; set; } = 0.25f;

    public float InsetDistance { get; set; } = 0.2f;

    public bool CanUndo => _commands.UndoCount > 0;

    public bool CanRedo => _commands.RedoCount > 0;

    public bool Undo()
    {
        if (_commands.Undo(out var command))
        {
            if (command is IMeshEditCommand meshCommand)
            {
                var clearSubSelection = command is MeshSnapshotCommand;
                RebuildMeshForEditable(meshCommand.Mesh, clearSubSelection);
            }

            CommandStateChanged?.Invoke();
            MeshEdited?.Invoke();
            return true;
        }

        return false;
    }

    public bool Redo()
    {
        if (_commands.Redo(out var command))
        {
            if (command is IMeshEditCommand meshCommand)
            {
                var clearSubSelection = command is MeshSnapshotCommand;
                RebuildMeshForEditable(meshCommand.Mesh, clearSubSelection);
            }

            CommandStateChanged?.Invoke();
            MeshEdited?.Invoke();
            return true;
        }

        return false;
    }

    public void ClearCommands()
    {
        _commands.Clear();
        CommandStateChanged?.Invoke();
    }

    public bool ApplyTransformEdit(Matrix4x4 transform, string name)
    {
        if (!_mode.EditMode)
        {
            return false;
        }

        if (!TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        IReadOnlyCollection<int>? selection = null;
        if (_mode.VertexSelect)
        {
            if (_selection.VertexSelection.IsEmpty)
            {
                return false;
            }

            selection = _selection.VertexSelection.Items;
        }

        if (_mode.VertexSelect && Options.ProportionalEnabled && selection != null)
        {
            var command = new MeshSnapshotCommand(editable, mesh =>
            {
                return ProportionalEditing.ApplyTransform(mesh, transform, selection, Options.ProportionalRadius, Options.ProportionalFalloff) > 0;
            }, name);

            if (_commands.Do(command))
            {
                RebuildEditableInstance(instance, editable, clearSubSelection: false);
                CommandStateChanged?.Invoke();
                return true;
            }

            return false;
        }

        var simpleCommand = new TransformVerticesCommand(editable, transform, selection, name);
        if (_commands.Do(simpleCommand))
        {
            RebuildEditableInstance(instance, editable, clearSubSelection: false);
            CommandStateChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool ApplyTransformToVertices(Matrix4x4 transform, IReadOnlyCollection<int> vertices, string name)
    {
        if (!_mode.EditMode)
        {
            return false;
        }

        if (vertices == null || vertices.Count == 0)
        {
            return false;
        }

        if (!TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        if (Options.ProportionalEnabled)
        {
            var command = new MeshSnapshotCommand(editable, mesh =>
            {
                return ProportionalEditing.ApplyTransform(mesh, transform, vertices, Options.ProportionalRadius, Options.ProportionalFalloff) > 0;
            }, name);

            if (_commands.Do(command))
            {
                RebuildEditableInstance(instance, editable, clearSubSelection: false);
                CommandStateChanged?.Invoke();
                return true;
            }

            return false;
        }

        var simpleCommand = new TransformVerticesCommand(editable, transform, vertices, name);
        if (_commands.Do(simpleCommand))
        {
            RebuildEditableInstance(instance, editable, clearSubSelection: false);
            CommandStateChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool ApplyTransformToSelection(Vector3 translation, Quaternion rotation, Vector3 scale, string name)
    {
        if (_selection.ObjectSelection.Count == 0)
        {
            return false;
        }

        var nodes = new List<SceneNode>();
        foreach (var instance in _selection.ObjectSelection.Items)
        {
            if (_document.TryGetNode(instance, out var node) && !nodes.Contains(node))
            {
                nodes.Add(node);
            }
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        var command = new MultiNodeTransformCommand(nodes, translation, rotation, scale, name);
        if (_commands.Do(command))
        {
            CommandStateChanged?.Invoke();
            MeshEdited?.Invoke();
            return true;
        }

        return false;
    }

    public bool CenterPivot()
    {
        if (!TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        if (!_document.TryGetNode(instance, out var node))
        {
            return false;
        }

        var command = new CenterPivotCommand(this, instance, editable, node);
        if (_commands.Do(command))
        {
            CommandStateChanged?.Invoke();
            MeshEdited?.Invoke();
            return true;
        }

        return false;
    }

    public bool ResetTransform()
    {
        var selected = _selection.Selected;
        if (selected is null)
        {
            return false;
        }

        if (!_document.TryGetNode(selected, out var node))
        {
            return false;
        }

        var command = new NodeTransformCommand(node, Vector3.Zero, Quaternion.Identity, Vector3.One, "Reset Transform");
        if (_commands.Do(command))
        {
            CommandStateChanged?.Invoke();
            MeshEdited?.Invoke();
            return true;
        }

        return false;
    }

    public bool ExtrudeFaces()
    {
        if (!_mode.FaceSelect || _selection.FaceSelection.IsEmpty)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            MeshOperations.ExtrudeFaces(mesh, _selection.FaceSelection.Items, ExtrudeDistance, keepBase: true);
            return true;
        }, "Extrude Faces", clearSubSelection: true);
    }

    public bool BevelFaces()
    {
        if (!_mode.FaceSelect || _selection.FaceSelection.IsEmpty)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            MeshOperations.BevelFaces(mesh, _selection.FaceSelection.Items, BevelInset, BevelHeight, keepBase: true);
            return true;
        }, "Bevel Faces", clearSubSelection: true);
    }

    public bool InsetFaces()
    {
        if (!_mode.FaceSelect || _selection.FaceSelection.IsEmpty)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.InsetFaces(mesh, _selection.FaceSelection.Items, InsetDistance, keepBase: true) > 0;
        }, "Inset Faces", clearSubSelection: true);
    }

    public bool SplitEdge()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out _))
        {
            return false;
        }

        var pick = _selection.LastPick.Value;
        if (!ReferenceEquals(pick.Instance, instance))
        {
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(pick);
        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.SplitEdge(mesh, edge, 0.5f) >= 0;
        }, "Split Edge", clearSubSelection: true);
    }

    public bool LoopCutEdgeLoop()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        var pick = _selection.LastPick.Value;
        if (!ReferenceEquals(pick.Instance, instance))
        {
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(pick);
        var edges = SelectionOperations.EdgeLoop(editable, edge);
        if (edges.Count == 0)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.LoopCutEdges(mesh, edges, 0.5f) > 0;
        }, "Loop Cut", clearSubSelection: true);
    }

    public bool BridgePickedEdges()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out _))
        {
            ClearBridgeAnchor();
            return false;
        }

        var pick = _selection.LastPick.Value;
        if (!ReferenceEquals(pick.Instance, instance))
        {
            ClearBridgeAnchor();
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(pick);
        if (_bridgeAnchor is null || !ReferenceEquals(_bridgeAnchorInstance, instance))
        {
            _bridgeAnchor = edge;
            _bridgeAnchorInstance = instance;
            return false;
        }

        var anchor = _bridgeAnchor.Value;
        if (anchor.Equals(edge))
        {
            return false;
        }

        var bridged = ApplyMeshEdit(mesh =>
        {
            return MeshOperations.BridgeEdges(mesh, anchor, edge, flip: false) > 0;
        }, "Bridge Edges", clearSubSelection: true);

        ClearBridgeAnchor();
        return bridged;
    }

    public bool BridgeEdgeLoops()
    {
        if (!_mode.EditMode || !TryGetEditableSelection(out var instance, out var editable))
        {
            ClearBridgeLoopAnchor();
            return false;
        }

        if (TryBridgeSelectedEdgeLoops(editable, out var loopA, out var loopB))
        {
            var bridged = ApplyMeshEdit(mesh =>
            {
                return MeshOperations.BridgeEdgePairs(mesh, loopA, loopB, flip: false) > 0;
            }, "Bridge Edge Loops", clearSubSelection: true);

            ClearBridgeLoopAnchor();
            return bridged;
        }

        if (_selection.LastPick is null)
        {
            ClearBridgeLoopAnchor();
            return false;
        }

        var pick = _selection.LastPick.Value;
        if (!ReferenceEquals(pick.Instance, instance))
        {
            ClearBridgeLoopAnchor();
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(pick);
        if (_bridgeLoopAnchor is null || !ReferenceEquals(_bridgeLoopAnchorInstance, instance))
        {
            _bridgeLoopAnchor = edge;
            _bridgeLoopAnchorInstance = instance;
            return false;
        }

        var anchor = _bridgeLoopAnchor.Value;
        if (anchor.Equals(edge))
        {
            return false;
        }

        var loopASet = SelectionOperations.EdgeLoop(editable, anchor);
        var loopBSet = SelectionOperations.EdgeLoop(editable, edge);
        if (loopASet.Count == 0 || loopBSet.Count == 0)
        {
            ClearBridgeLoopAnchor();
            return false;
        }

        if (!TryOrderEdgeLoop(loopASet, anchor, out loopA) ||
            !TryOrderEdgeLoop(loopBSet, edge, out loopB))
        {
            ClearBridgeLoopAnchor();
            return false;
        }

        if (loopA.Count != loopB.Count)
        {
            ClearBridgeLoopAnchor();
            return false;
        }

        var bridgedPicked = ApplyMeshEdit(mesh =>
        {
            return MeshOperations.BridgeEdgePairs(mesh, loopA, loopB, flip: false) > 0;
        }, "Bridge Edge Loops", clearSubSelection: true);

        ClearBridgeLoopAnchor();
        return bridgedPicked;
    }

    public bool MergeVertices()
    {
        if (!_mode.EditMode || !_mode.VertexSelect || _selection.VertexSelection.Count < 2)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.MergeVertices(mesh, _selection.VertexSelection.Items, removeDegenerateTriangles: true) > 0;
        }, "Merge Vertices", clearSubSelection: true);
    }

    public bool DissolveFaces()
    {
        if (!_mode.EditMode || !_mode.FaceSelect || _selection.FaceSelection.IsEmpty)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.DissolveFaces(mesh, _selection.FaceSelection.Items, removeUnusedVertices: true) > 0;
        }, "Dissolve Faces", clearSubSelection: true);
    }

    public bool DissolvePickedEdge()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out _))
        {
            return false;
        }

        var pick = _selection.LastPick.Value;
        if (!ReferenceEquals(pick.Instance, instance))
        {
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(pick);
        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.DissolveEdges(mesh, new[] { edge }, removeUnusedVertices: true) > 0;
        }, "Dissolve Edge", clearSubSelection: true);
    }

    public bool CollapsePickedEdge()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out _))
        {
            return false;
        }

        var pick = _selection.LastPick.Value;
        if (!ReferenceEquals(pick.Instance, instance))
        {
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(pick);
        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.CollapseEdge(mesh, edge, 0.5f, removeUnusedVertices: true) > 0;
        }, "Collapse Edge", clearSubSelection: true);
    }

    public bool WeldVertices()
    {
        return ApplyMeshEdit(mesh =>
        {
            MeshOperations.WeldVertices(mesh, WeldTolerance);
            return true;
        }, "Weld Vertices", clearSubSelection: true);
    }

    public bool CleanupMesh()
    {
        return ApplyMeshEdit(mesh =>
        {
            var removed = MeshOperations.RemoveDegenerateTriangles(mesh, 1e-8f, removeUnusedVertices: true);
            MeshOperations.RemoveUnusedVertices(mesh);
            return removed > 0;
        }, "Cleanup Mesh", clearSubSelection: true);
    }

    public bool SmoothMesh()
    {
        return ApplyMeshEdit(mesh =>
        {
            return MeshOperations.Smooth(mesh, Options.SmoothIterations, Options.SmoothStrength, preserveBoundary: true);
        }, "Smooth Mesh", clearSubSelection: false);
    }

    public bool SimplifyMesh()
    {
        return ApplyMeshEdit(mesh =>
        {
            var options = new MeshSimplifyOptions
            {
                TargetRatio = Options.SimplifyRatio,
                RecalculateNormals = true
            };
            return MeshOperations.Simplify(mesh, options);
        }, "Simplify Mesh", clearSubSelection: true);
    }

    public bool PlanarUv()
    {
        return ApplyMeshEdit(mesh =>
        {
            var selection = GetUvSelectionVertices(mesh);
            if (!TryComputePlanarProjection(mesh, selection, out var normal, out var origin, out var scale, out var offset))
            {
                return false;
            }

            UvOperations.ProjectPlanar(mesh, normal, origin, scale, offset, selection);
            return true;
        }, "Planar UV", clearSubSelection: false);
    }

    public bool BoxUv()
    {
        return ApplyMeshEdit(mesh =>
        {
            var selection = GetUvSelectionVertices(mesh);
            if (!TryGetSelectionBounds(mesh, selection, out var min, out var max))
            {
                return false;
            }

            var center = (min + max) * 0.5f;
            var size = max - min;
            var scale = new Vector2(Options.UvScale, Options.UvScale);
            var offset = new Vector2(0.5f, 0.5f);
            UvOperations.ProjectBox(mesh, center, size, scale, offset, selection);
            return true;
        }, "Box UV", clearSubSelection: false);
    }

    public bool NormalizeUv()
    {
        return ApplyMeshEdit(mesh =>
        {
            var selection = GetUvSelectionVertices(mesh);
            UvOperations.NormalizeUVs(mesh, selection);
            return true;
        }, "Normalize UV", clearSubSelection: false);
    }

    public bool FlipU()
    {
        return ApplyMeshEdit(mesh =>
        {
            var selection = GetUvSelectionVertices(mesh);
            UvOperations.FlipU(mesh, selection);
            return true;
        }, "Flip U", clearSubSelection: false);
    }

    public bool FlipV()
    {
        return ApplyMeshEdit(mesh =>
        {
            var selection = GetUvSelectionVertices(mesh);
            UvOperations.FlipV(mesh, selection);
            return true;
        }, "Flip V", clearSubSelection: false);
    }

    public bool MarkUvSeams()
    {
        if (!_mode.EditMode || !_mode.EdgeSelect || _selection.EdgeSelection.IsEmpty)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            int added = 0;
            foreach (var edge in _selection.EdgeSelection.Items)
            {
                if (mesh.SeamEdges.Add(edge))
                {
                    added++;
                }
            }

            return added > 0;
        }, "Mark UV Seam", clearSubSelection: false);
    }

    public bool ClearUvSeams()
    {
        if (!_mode.EditMode || !_mode.EdgeSelect || _selection.EdgeSelection.IsEmpty)
        {
            return false;
        }

        return ApplyMeshEdit(mesh =>
        {
            int removed = 0;
            foreach (var edge in _selection.EdgeSelection.Items)
            {
                if (mesh.SeamEdges.Remove(edge))
                {
                    removed++;
                }
            }

            return removed > 0;
        }, "Clear UV Seam", clearSubSelection: false);
    }

    public bool ClearAllUvSeams()
    {
        return ApplyMeshEdit(mesh =>
        {
            if (mesh.SeamEdges.Count == 0)
            {
                return false;
            }

            mesh.SeamEdges.Clear();
            return true;
        }, "Clear All UV Seams", clearSubSelection: false);
    }

    public bool AssignUvGroup(int groupId)
    {
        if (!_mode.EditMode || !_mode.FaceSelect || _selection.FaceSelection.IsEmpty)
        {
            return false;
        }

        groupId = Math.Max(0, groupId);
        return ApplyMeshEdit(mesh =>
        {
            mesh.EnsureUvFaceGroups();
            bool changed = false;
            foreach (var face in _selection.FaceSelection.Items)
            {
                if ((uint)face >= (uint)mesh.UvFaceGroups.Count)
                {
                    continue;
                }

                if (mesh.UvFaceGroups[face] != groupId)
                {
                    mesh.UvFaceGroups[face] = groupId;
                    changed = true;
                }
            }

            return changed;
        }, groupId == 0 ? "Clear UV Group" : "Set UV Group", clearSubSelection: false);
    }

    public bool ClearUvGroup()
    {
        return AssignUvGroup(0);
    }

    public bool UnwrapUv()
    {
        return ApplyMeshEdit(mesh =>
        {
            var faces = GetUvSelectionFaces();
            var seamSet = new HashSet<EdgeKey>(mesh.SeamEdges);
            if (_mode.EdgeSelect && !_selection.EdgeSelection.IsEmpty)
            {
                foreach (var edge in _selection.EdgeSelection.Items)
                {
                    seamSet.Add(edge);
                }
            }

            var options = new UvUnwrapOptions(Options.UvUnwrapMethod, Options.UvUnwrapAngle, Options.UvScale);
            return UvOperations.Unwrap(mesh, options, seamSet, faces);
        }, "Unwrap UV", clearSubSelection: true);
    }

    public bool PackUv()
    {
        return ApplyMeshEdit(mesh =>
        {
            var options = new UvPackOptions(
                Options.UvPackPadding,
                Options.UvPackRotate,
                Options.UvPackPreserveTexelDensity,
                Options.UvPackTexelDensity,
                Options.UvPackUseGroups);
            return UvOperations.PackIslands(mesh, options, mesh.SeamEdges, mesh.UvFaceGroups, faceSelection: null);
        }, "Pack UV", clearSubSelection: false);
    }

    public bool SelectUvIsland()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        if (!ReferenceEquals(_selection.LastPick.Value.Instance, instance))
        {
            return false;
        }

        int faceIndex = _selection.LastPick.Value.TriangleIndex;
        if ((uint)faceIndex >= (uint)editable.TriangleCount)
        {
            return false;
        }

        var islands = UvOperations.BuildUvIslands(editable, editable.SeamEdges, faceSelection: null, angleThresholdDegrees: 180f);
        foreach (var island in islands)
        {
            if (!island.Contains(faceIndex))
            {
                continue;
            }

            _selection.FaceSelection.ReplaceWith(island);
            _mode.EditMode = true;
            _mode.VertexSelect = false;
            _mode.EdgeSelect = false;
            _mode.FaceSelect = true;
            SelectionChanged?.Invoke();
            return true;
        }

        return false;
    }

    private void ClearBridgeAnchor()
    {
        _bridgeAnchor = null;
        _bridgeAnchorInstance = null;
    }

    private void ClearBridgeLoopAnchor()
    {
        _bridgeLoopAnchor = null;
        _bridgeLoopAnchorInstance = null;
    }

    public bool SelectEdgeLoop()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        if (!ReferenceEquals(_selection.LastPick.Value.Instance, instance))
        {
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(_selection.LastPick.Value);
        var edges = SelectionOperations.EdgeLoop(editable, edge);
        if (edges.Count == 0)
        {
            return false;
        }

        if (_mode.EdgeSelect)
        {
            _selection.EdgeSelection.ReplaceWith(edges);
            _mode.EditMode = true;
            _mode.VertexSelect = false;
            _mode.FaceSelect = false;
            SelectionChanged?.Invoke();
            return true;
        }

        var vertices = new HashSet<int>();
        foreach (var edgeItem in edges)
        {
            vertices.Add(edgeItem.A);
            vertices.Add(edgeItem.B);
        }

        _selection.VertexSelection.ReplaceWith(vertices);
        _mode.EditMode = true;
        _mode.VertexSelect = true;
        _mode.FaceSelect = false;
        SelectionChanged?.Invoke();
        return true;
    }

    public bool SelectEdgeRing()
    {
        if (!_mode.EditMode || _selection.LastPick is null || !TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        if (!ReferenceEquals(_selection.LastPick.Value.Instance, instance))
        {
            return false;
        }

        var edge = EditorSelectionUtils.GetPickedEdge(_selection.LastPick.Value);
        var edges = SelectionOperations.EdgeRing(editable, edge);
        if (edges.Count == 0)
        {
            return false;
        }

        if (_mode.EdgeSelect)
        {
            _selection.EdgeSelection.ReplaceWith(edges);
            _mode.EditMode = true;
            _mode.VertexSelect = false;
            _mode.FaceSelect = false;
            SelectionChanged?.Invoke();
            return true;
        }

        var vertices = new HashSet<int>();
        foreach (var edgeItem in edges)
        {
            vertices.Add(edgeItem.A);
            vertices.Add(edgeItem.B);
        }

        _selection.VertexSelection.ReplaceWith(vertices);
        _mode.EditMode = true;
        _mode.VertexSelect = true;
        _mode.FaceSelect = false;
        SelectionChanged?.Invoke();
        return true;
    }

    public bool ConvertSelectionToVertices()
    {
        if (!_mode.EditMode || !TryGetEditableSelection(out _, out var editable))
        {
            return false;
        }

        HashSet<int> vertices;
        if (!_selection.VertexSelection.IsEmpty)
        {
            vertices = new HashSet<int>(_selection.VertexSelection.Items);
        }
        else if (!_selection.EdgeSelection.IsEmpty)
        {
            vertices = new HashSet<int>();
            foreach (var edge in _selection.EdgeSelection.Items)
            {
                vertices.Add(edge.A);
                vertices.Add(edge.B);
            }
        }
        else if (!_selection.FaceSelection.IsEmpty)
        {
            vertices = CollectVerticesFromFaces(editable, _selection.FaceSelection.Items);
        }
        else
        {
            return false;
        }

        if (vertices.Count == 0)
        {
            return false;
        }

        _selection.VertexSelection.ReplaceWith(vertices);
        _selection.EdgeSelection.Clear();
        _selection.FaceSelection.Clear();
        _mode.EditMode = true;
        _mode.VertexSelect = true;
        _mode.EdgeSelect = false;
        _mode.FaceSelect = false;
        SelectionChanged?.Invoke();
        return true;
    }

    public bool ConvertSelectionToEdges()
    {
        if (!_mode.EditMode || !TryGetEditableSelection(out _, out var editable))
        {
            return false;
        }

        HashSet<EdgeKey> edges;
        if (!_selection.EdgeSelection.IsEmpty)
        {
            edges = new HashSet<EdgeKey>(_selection.EdgeSelection.Items);
        }
        else if (!_selection.FaceSelection.IsEmpty)
        {
            edges = CollectEdgesFromFaces(editable, _selection.FaceSelection.Items);
        }
        else if (!_selection.VertexSelection.IsEmpty)
        {
            edges = CollectEdgesFromVertices(editable, _selection.VertexSelection.Items);
        }
        else
        {
            return false;
        }

        if (edges.Count == 0)
        {
            return false;
        }

        _selection.EdgeSelection.ReplaceWith(edges);
        _selection.VertexSelection.Clear();
        _selection.FaceSelection.Clear();
        _mode.EditMode = true;
        _mode.VertexSelect = false;
        _mode.EdgeSelect = true;
        _mode.FaceSelect = false;
        SelectionChanged?.Invoke();
        return true;
    }

    public bool ConvertSelectionToFaces()
    {
        if (!_mode.EditMode || !TryGetEditableSelection(out _, out var editable))
        {
            return false;
        }

        HashSet<int> faces;
        if (!_selection.FaceSelection.IsEmpty)
        {
            faces = new HashSet<int>(_selection.FaceSelection.Items);
        }
        else if (!_selection.EdgeSelection.IsEmpty)
        {
            faces = CollectFacesFromEdges(editable, _selection.EdgeSelection.Items);
        }
        else if (!_selection.VertexSelection.IsEmpty)
        {
            faces = CollectFacesFromVertices(editable, _selection.VertexSelection.Items);
        }
        else
        {
            return false;
        }

        if (faces.Count == 0)
        {
            return false;
        }

        _selection.FaceSelection.ReplaceWith(faces);
        _selection.VertexSelection.Clear();
        _selection.EdgeSelection.Clear();
        _mode.EditMode = true;
        _mode.VertexSelect = false;
        _mode.EdgeSelect = false;
        _mode.FaceSelect = true;
        SelectionChanged?.Invoke();
        return true;
    }

    private bool TryBridgeSelectedEdgeLoops(EditableMesh editable, out List<EdgeKey> loopA, out List<EdgeKey> loopB)
    {
        loopA = new List<EdgeKey>();
        loopB = new List<EdgeKey>();

        if (!_mode.EdgeSelect || _selection.EdgeSelection.Count < 2)
        {
            return false;
        }

        var components = SplitEdgeSelectionIntoComponents(_selection.EdgeSelection.Items);
        if (components.Count != 2)
        {
            return false;
        }

        var startA = GetFirstEdge(components[0]);
        var startB = GetFirstEdge(components[1]);
        if (!TryOrderEdgeLoop(components[0], startA, out loopA) ||
            !TryOrderEdgeLoop(components[1], startB, out loopB))
        {
            return false;
        }

        if (loopA.Count != loopB.Count)
        {
            return false;
        }

        loopB = AlignEdgeLoopPairs(editable, loopA, loopB);
        return loopA.Count == loopB.Count && loopA.Count > 0;
    }

    private static List<HashSet<EdgeKey>> SplitEdgeSelectionIntoComponents(IReadOnlyCollection<EdgeKey> edges)
    {
        var all = edges as HashSet<EdgeKey> ?? new HashSet<EdgeKey>(edges);
        var adjacency = BuildEdgeAdjacency(all);
        var visited = new HashSet<EdgeKey>();
        var components = new List<HashSet<EdgeKey>>();

        foreach (var edge in all)
        {
            if (visited.Contains(edge))
            {
                continue;
            }

            var component = new HashSet<EdgeKey>();
            var stack = new Stack<EdgeKey>();
            stack.Push(edge);
            visited.Add(edge);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                component.Add(current);

                AddNeighbors(current.A);
                AddNeighbors(current.B);
            }

            components.Add(component);

            void AddNeighbors(int vertex)
            {
                if (!adjacency.TryGetValue(vertex, out var neighbors))
                {
                    return;
                }

                foreach (var neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                    {
                        stack.Push(neighbor);
                    }
                }
            }
        }

        return components;
    }

    private static Dictionary<int, List<EdgeKey>> BuildEdgeAdjacency(HashSet<EdgeKey> edges)
    {
        var map = new Dictionary<int, List<EdgeKey>>();
        foreach (var edge in edges)
        {
            AddEdge(edge.A, edge);
            AddEdge(edge.B, edge);
        }

        return map;

        void AddEdge(int vertex, EdgeKey edge)
        {
            if (!map.TryGetValue(vertex, out var list))
            {
                list = new List<EdgeKey>();
                map[vertex] = list;
            }

            list.Add(edge);
        }
    }

    private static EdgeKey GetFirstEdge(HashSet<EdgeKey> edges)
    {
        foreach (var edge in edges)
        {
            return edge;
        }

        return default;
    }

    private static bool TryOrderEdgeLoop(HashSet<EdgeKey> edges, EdgeKey start, out List<EdgeKey> ordered)
    {
        ordered = new List<EdgeKey>(edges.Count);
        if (edges.Count == 0)
        {
            return false;
        }

        var adjacency = BuildEdgeAdjacency(edges);
        foreach (var pair in adjacency)
        {
            if (pair.Value.Count == 1)
            {
                var edge = pair.Value[0];
                var startVertex = pair.Key;
                var nextVertex = edge.A == startVertex ? edge.B : edge.A;
                if (TryOrderEdgeLoopDirection(edges, adjacency, edge, startVertex, nextVertex, out ordered))
                {
                    return true;
                }
            }
        }

        if (TryOrderEdgeLoopDirection(edges, adjacency, start, start.A, start.B, out ordered))
        {
            return true;
        }

        return TryOrderEdgeLoopDirection(edges, adjacency, start, start.B, start.A, out ordered);
    }

    private static bool TryOrderEdgeLoopDirection(
        HashSet<EdgeKey> edges,
        Dictionary<int, List<EdgeKey>> adjacency,
        EdgeKey startEdge,
        int prevVertex,
        int currentVertex,
        out List<EdgeKey> ordered)
    {
        ordered = new List<EdgeKey>(edges.Count);
        var used = new HashSet<EdgeKey>();
        ordered.Add(startEdge);
        used.Add(startEdge);

        while (ordered.Count < edges.Count)
        {
            if (!adjacency.TryGetValue(currentVertex, out var candidates))
            {
                return false;
            }

            EdgeKey nextEdge = default;
            int nextVertex = -1;
            bool hasNext = false;
            EdgeKey fallback = default;
            int fallbackVertex = -1;
            bool hasFallback = false;

            foreach (var candidate in candidates)
            {
                if (used.Contains(candidate))
                {
                    continue;
                }

                int other = candidate.A == currentVertex ? candidate.B : candidate.A;
                if (other != prevVertex)
                {
                    nextEdge = candidate;
                    nextVertex = other;
                    hasNext = true;
                    break;
                }

                if (!hasFallback)
                {
                    fallback = candidate;
                    fallbackVertex = other;
                    hasFallback = true;
                }
            }

            if (!hasNext)
            {
                if (!hasFallback)
                {
                    return false;
                }

                nextEdge = fallback;
                nextVertex = fallbackVertex;
            }

            ordered.Add(nextEdge);
            used.Add(nextEdge);
            prevVertex = currentVertex;
            currentVertex = nextVertex;
        }

        return true;
    }

    private static List<EdgeKey> AlignEdgeLoopPairs(EditableMesh mesh, List<EdgeKey> loopA, List<EdgeKey> loopB)
    {
        int count = loopA.Count;
        if (count == 0 || loopB.Count != count)
        {
            return loopB;
        }

        var positions = mesh.Positions;
        var centersA = new Vector3[count];
        var centersB = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            centersA[i] = GetEdgeCenter(positions, loopA[i]);
            centersB[i] = GetEdgeCenter(positions, loopB[i]);
        }

        float bestScore = float.PositiveInfinity;
        int bestOffset = 0;
        bool bestReverse = false;

        for (int offset = 0; offset < count; offset++)
        {
            float score = 0f;
            for (int i = 0; i < count; i++)
            {
                int j = (offset + i) % count;
                var d = centersA[i] - centersB[j];
                score += d.LengthSquared();
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestOffset = offset;
                bestReverse = false;
            }
        }

        for (int offset = 0; offset < count; offset++)
        {
            float score = 0f;
            for (int i = 0; i < count; i++)
            {
                int j = count - 1 - ((offset + i) % count);
                var d = centersA[i] - centersB[j];
                score += d.LengthSquared();
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestOffset = offset;
                bestReverse = true;
            }
        }

        var aligned = new List<EdgeKey>(count);
        for (int i = 0; i < count; i++)
        {
            int j = bestReverse
                ? count - 1 - ((bestOffset + i) % count)
                : (bestOffset + i) % count;
            aligned.Add(loopB[j]);
        }

        return aligned;
    }

    private static Vector3 GetEdgeCenter(IReadOnlyList<Vector3> positions, EdgeKey edge)
    {
        if ((uint)edge.A >= (uint)positions.Count || (uint)edge.B >= (uint)positions.Count)
        {
            return Vector3.Zero;
        }

        return (positions[edge.A] + positions[edge.B]) * 0.5f;
    }

    private bool ApplyMeshEdit(Func<EditableMesh, bool> apply, string name, bool clearSubSelection)
    {
        if (!_mode.EditMode)
        {
            return false;
        }

        if (!TryGetEditableSelection(out var instance, out var editable))
        {
            return false;
        }

        var command = new MeshSnapshotCommand(editable, apply, name);
        if (_commands.Do(command))
        {
            editable.TrimSeamEdges();
            editable.EnsureUvFaceGroups();
            RebuildEditableInstance(instance, editable, clearSubSelection);
            CommandStateChanged?.Invoke();
            MeshEdited?.Invoke();
            return true;
        }

        return false;
    }

    private bool TryGetEditableSelection(out MeshInstance instance, out EditableMesh editable)
    {
        instance = null!;
        editable = null!;

        var selected = _selection.Selected;
        if (selected is null)
        {
            return false;
        }

        if (!_document.TryGetEditableMesh(selected, out var editableValue))
        {
            return false;
        }

        instance = selected;
        editable = editableValue;
        return true;
    }

    private void RebuildMeshForEditable(EditableMesh editable, bool clearSubSelection)
    {
        var targetInstance = _document.RebuildMeshForEditable(editable);
        if (targetInstance is null)
        {
            return;
        }

        if (_selection.Selected is not null && !ReferenceEquals(_selection.Selected, targetInstance))
        {
            _selection.ReplaceInstance(_selection.Selected, targetInstance);
        }

        UpdateSelectionAfterRebuild(targetInstance, clearSubSelection);
    }

    private MeshInstance? RebuildEditableInstance(MeshInstance instance, EditableMesh editable, bool clearSubSelection)
    {
        var targetInstance = _document.RebuildEditableInstance(instance, editable);
        if (targetInstance is null)
        {
            return null;
        }

        if (!ReferenceEquals(targetInstance, instance))
        {
            _selection.ReplaceInstance(instance, targetInstance);
        }

        UpdateSelectionAfterRebuild(targetInstance, clearSubSelection);
        return targetInstance;
    }

    private void UpdateSelectionAfterRebuild(MeshInstance targetInstance, bool clearSubSelection)
    {
        if (clearSubSelection || _selection.Selected != targetInstance)
        {
            _selection.VertexSelection.Clear();
            _selection.EdgeSelection.Clear();
            _selection.FaceSelection.Clear();
        }
        else
        {
            TrimVertexSelection(targetInstance);
            TrimEdgeSelection(targetInstance);
            TrimFaceSelection(targetInstance);
        }

        SelectionChanged?.Invoke();
    }

    private void TrimVertexSelection(MeshInstance instance)
    {
        if (_selection.VertexSelection.IsEmpty)
        {
            return;
        }

        var count = instance.Mesh.Vertices.Count;
        if (count <= 0)
        {
            _selection.VertexSelection.Clear();
            return;
        }

        List<int>? remove = null;
        foreach (var index in _selection.VertexSelection.Items)
        {
            if ((uint)index >= (uint)count)
            {
                remove ??= new List<int>();
                remove.Add(index);
            }
        }

        if (remove is null)
        {
            return;
        }

        foreach (var index in remove)
        {
            _selection.VertexSelection.Remove(index);
        }
    }

    private void TrimFaceSelection(MeshInstance instance)
    {
        if (_selection.FaceSelection.IsEmpty)
        {
            return;
        }

        var count = instance.Mesh.Indices.Count / 3;
        if (count <= 0)
        {
            _selection.FaceSelection.Clear();
            return;
        }

        List<int>? remove = null;
        foreach (var index in _selection.FaceSelection.Items)
        {
            if ((uint)index >= (uint)count)
            {
                remove ??= new List<int>();
                remove.Add(index);
            }
        }

        if (remove is null)
        {
            return;
        }

        foreach (var index in remove)
        {
            _selection.FaceSelection.Remove(index);
        }
    }

    private void TrimEdgeSelection(MeshInstance instance)
    {
        if (_selection.EdgeSelection.IsEmpty)
        {
            return;
        }

        var count = instance.Mesh.Vertices.Count;
        if (count <= 0)
        {
            _selection.EdgeSelection.Clear();
            return;
        }

        List<EdgeKey>? remove = null;
        foreach (var edge in _selection.EdgeSelection.Items)
        {
            if ((uint)edge.A >= (uint)count || (uint)edge.B >= (uint)count)
            {
                remove ??= new List<EdgeKey>();
                remove.Add(edge);
            }
        }

        if (remove is null)
        {
            return;
        }

        foreach (var edge in remove)
        {
            _selection.EdgeSelection.Remove(edge);
        }
    }

    private sealed class CenterPivotCommand : IEditCommand
    {
        private readonly EditorMeshEditService _owner;
        private MeshInstance _instance;
        private readonly EditableMesh _editable;
        private readonly SceneNode _node;
        private MeshSnapshot? _before;
        private MeshSnapshot? _after;
        private Vector3 _beforePosition;
        private Quaternion _beforeRotation;
        private Vector3 _beforeScale;
        private Vector3 _afterPosition;
        private bool _initialized;

        public CenterPivotCommand(EditorMeshEditService owner, MeshInstance instance, EditableMesh editable, SceneNode node)
        {
            _owner = owner;
            _instance = instance;
            _editable = editable;
            _node = node;
        }

        public string Name => "Center Pivot";

        public bool Execute()
        {
            if (!_initialized)
            {
                _before = MeshSnapshot.Capture(_editable);
                _beforePosition = _node.Transform.LocalPosition;
                _beforeRotation = _node.Transform.LocalRotation;
                _beforeScale = _node.Transform.LocalScale;

                if (!TryComputeBounds(_before.Value.Positions, out var min, out var max))
                {
                    return false;
                }

                var center = (min + max) * 0.5f;
                if (center.LengthSquared() < 1e-8f)
                {
                    return false;
                }

                var positions = new Vector3[_before.Value.Positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = _before.Value.Positions[i] - center;
                }

                _after = new MeshSnapshot(
                    positions,
                    _before.Value.Indices,
                    _before.Value.UVs,
                    _before.Value.Normals,
                    _before.Value.Colors,
                    _before.Value.Tangents,
                    _before.Value.SeamEdges,
                    _before.Value.UvFaceGroups);
                var rotationScale = Matrix4x4.CreateScale(_beforeScale)
                    * Matrix4x4.CreateFromQuaternion(_beforeRotation);
                var offset = Vector3.Transform(center, rotationScale);
                _afterPosition = _beforePosition + offset;
                _initialized = true;
            }

            ApplySnapshot(_after);
            _node.Transform.LocalPosition = _afterPosition;
            _node.Transform.LocalRotation = _beforeRotation;
            _node.Transform.LocalScale = _beforeScale;
            return true;
        }

        public void Undo()
        {
            ApplySnapshot(_before);
            _node.Transform.LocalPosition = _beforePosition;
            _node.Transform.LocalRotation = _beforeRotation;
            _node.Transform.LocalScale = _beforeScale;
        }

        private void ApplySnapshot(MeshSnapshot? snapshot)
        {
            if (!snapshot.HasValue)
            {
                return;
            }

            snapshot.Value.Restore(_editable);
            var updated = _owner.RebuildEditableInstance(_instance, _editable, clearSubSelection: false);
            if (updated != null)
            {
                _instance = updated;
            }
        }

        private static bool TryComputeBounds(Vector3[] positions, out Vector3 min, out Vector3 max)
        {
            if (positions.Length == 0)
            {
                min = max = default;
                return false;
            }

            min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            return true;
        }
    }

    private sealed class NodeTransformCommand : IEditCommand
    {
        private readonly SceneNode _node;
        private readonly Vector3 _targetPosition;
        private readonly Quaternion _targetRotation;
        private readonly Vector3 _targetScale;
        private readonly string _name;
        private bool _initialized;
        private Vector3 _beforePosition;
        private Quaternion _beforeRotation;
        private Vector3 _beforeScale;

        public NodeTransformCommand(SceneNode node, Vector3 position, Quaternion rotation, Vector3 scale, string name)
        {
            _node = node;
            _targetPosition = position;
            _targetRotation = rotation;
            _targetScale = scale;
            _name = name;
        }

        public string Name => _name;

        public bool Execute()
        {
            if (!_initialized)
            {
                _beforePosition = _node.Transform.LocalPosition;
                _beforeRotation = _node.Transform.LocalRotation;
                _beforeScale = _node.Transform.LocalScale;
                _initialized = true;
            }

            Apply(_targetPosition, _targetRotation, _targetScale);
            return true;
        }

        public void Undo()
        {
            Apply(_beforePosition, _beforeRotation, _beforeScale);
        }

        private void Apply(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            _node.Transform.LocalPosition = position;
            _node.Transform.LocalRotation = rotation;
            _node.Transform.LocalScale = scale;
        }
    }

    private sealed class MultiNodeTransformCommand : IEditCommand
    {
        private readonly SceneNode[] _nodes;
        private readonly Vector3 _translation;
        private readonly Quaternion _rotation;
        private readonly Vector3 _scale;
        private readonly string _name;
        private bool _initialized;
        private Vector3[]? _beforePositions;
        private Quaternion[]? _beforeRotations;
        private Vector3[]? _beforeScales;
        private Vector3[]? _afterPositions;
        private Quaternion[]? _afterRotations;
        private Vector3[]? _afterScales;

        public MultiNodeTransformCommand(IReadOnlyList<SceneNode> nodes, Vector3 translation, Quaternion rotation, Vector3 scale, string name)
        {
            if (nodes is null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            _nodes = new SceneNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                _nodes[i] = nodes[i] ?? throw new ArgumentException("Node list contains null.", nameof(nodes));
            }

            _translation = translation;
            _rotation = rotation;
            _scale = scale;
            _name = string.IsNullOrWhiteSpace(name) ? "Transform Selection" : name;
        }

        public string Name => _name;

        public bool Execute()
        {
            if (!_initialized)
            {
                int count = _nodes.Length;
                _beforePositions = new Vector3[count];
                _beforeRotations = new Quaternion[count];
                _beforeScales = new Vector3[count];
                _afterPositions = new Vector3[count];
                _afterRotations = new Quaternion[count];
                _afterScales = new Vector3[count];

                for (int i = 0; i < count; i++)
                {
                    var node = _nodes[i];
                    _beforePositions[i] = node.Transform.LocalPosition;
                    _beforeRotations[i] = node.Transform.LocalRotation;
                    _beforeScales[i] = node.Transform.LocalScale;
                    _afterPositions[i] = _beforePositions[i] + _translation;
                    _afterRotations[i] = Quaternion.Normalize(_rotation * _beforeRotations[i]);
                    _afterScales[i] = new Vector3(
                        _beforeScales[i].X * _scale.X,
                        _beforeScales[i].Y * _scale.Y,
                        _beforeScales[i].Z * _scale.Z);
                }

                _initialized = true;
            }

            Apply(_afterPositions!, _afterRotations!, _afterScales!);
            return true;
        }

        public void Undo()
        {
            if (!_initialized)
            {
                return;
            }

            Apply(_beforePositions!, _beforeRotations!, _beforeScales!);
        }

        private void Apply(Vector3[] positions, Quaternion[] rotations, Vector3[] scales)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                node.Transform.LocalPosition = positions[i];
                node.Transform.LocalRotation = rotations[i];
                node.Transform.LocalScale = scales[i];
            }
        }
    }

    private IReadOnlyCollection<int>? GetUvSelectionVertices(EditableMesh mesh)
    {
        if (_mode.VertexSelect && !_selection.VertexSelection.IsEmpty)
        {
            return _selection.VertexSelection.Items;
        }

        if (_mode.FaceSelect && !_selection.FaceSelection.IsEmpty)
        {
            return CollectVerticesFromFaces(mesh, _selection.FaceSelection.Items);
        }

        return null;
    }

    private IReadOnlyCollection<int>? GetUvSelectionFaces()
    {
        if (_mode.FaceSelect && !_selection.FaceSelection.IsEmpty)
        {
            return _selection.FaceSelection.Items;
        }

        return null;
    }

    private bool TryComputePlanarProjection(
        EditableMesh mesh,
        IReadOnlyCollection<int>? selection,
        out Vector3 normal,
        out Vector3 origin,
        out Vector2 scale,
        out Vector2 offset)
    {
        normal = Vector3.UnitY;
        origin = Vector3.Zero;
        scale = Vector2.One;
        offset = Vector2.Zero;

        if (_mode.FaceSelect && !_selection.FaceSelection.IsEmpty)
        {
            normal = ComputeAverageFaceNormal(mesh, _selection.FaceSelection.Items);
        }

        if (normal.LengthSquared() < 1e-8f)
        {
            normal = Vector3.UnitY;
        }

        if (!TryGetSelectionBounds(mesh, selection, out var min, out var max))
        {
            return false;
        }

        origin = (min + max) * 0.5f;

        var n = Vector3.Normalize(normal);
        var up = MathF.Abs(n.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var uAxis = Vector3.Normalize(Vector3.Cross(up, n));
        var vAxis = Vector3.Normalize(Vector3.Cross(n, uAxis));

        var positions = mesh.Positions;
        float minU = float.PositiveInfinity;
        float maxU = float.NegativeInfinity;
        float minV = float.PositiveInfinity;
        float maxV = float.NegativeInfinity;
        bool any = false;

        if (selection is null)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                var local = positions[i] - origin;
                var u = Vector3.Dot(local, uAxis);
                var v = Vector3.Dot(local, vAxis);
                minU = MathF.Min(minU, u);
                maxU = MathF.Max(maxU, u);
                minV = MathF.Min(minV, v);
                maxV = MathF.Max(maxV, v);
                any = true;
            }
        }
        else
        {
            foreach (var index in selection)
            {
                if ((uint)index >= (uint)positions.Count)
                {
                    continue;
                }

                var local = positions[index] - origin;
                var u = Vector3.Dot(local, uAxis);
                var v = Vector3.Dot(local, vAxis);
                minU = MathF.Min(minU, u);
                maxU = MathF.Max(maxU, u);
                minV = MathF.Min(minV, v);
                maxV = MathF.Max(maxV, v);
                any = true;
            }
        }

        if (!any)
        {
            return false;
        }

        var rangeU = maxU - minU;
        var rangeV = maxV - minV;
        var invU = rangeU > 1e-6f ? 1f / rangeU : 1f;
        var invV = rangeV > 1e-6f ? 1f / rangeV : 1f;
        scale = new Vector2(invU * Options.UvScale, invV * Options.UvScale);
        offset = new Vector2(-minU * scale.X, -minV * scale.Y);
        return true;
    }

    private static HashSet<int> CollectVerticesFromFaces(EditableMesh mesh, IReadOnlyCollection<int> faces)
    {
        var indices = mesh.Indices;
        var vertices = new HashSet<int>();
        foreach (var face in faces)
        {
            var baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            vertices.Add(indices[baseIndex]);
            vertices.Add(indices[baseIndex + 1]);
            vertices.Add(indices[baseIndex + 2]);
        }

        return vertices;
    }

    private static HashSet<EdgeKey> CollectEdgesFromFaces(EditableMesh mesh, IReadOnlyCollection<int> faces)
    {
        var adjacency = MeshAdjacency.Build(mesh);
        var edges = new HashSet<EdgeKey>();
        foreach (var face in faces)
        {
            if ((uint)face >= (uint)adjacency.FaceEdges.Count)
            {
                continue;
            }

            var faceEdges = adjacency.FaceEdges[face];
            for (int i = 0; i < faceEdges.Length; i++)
            {
                edges.Add(faceEdges[i]);
            }
        }

        return edges;
    }

    private static HashSet<EdgeKey> CollectEdgesFromVertices(EditableMesh mesh, IReadOnlyCollection<int> vertices)
    {
        if (vertices.Count == 0)
        {
            return new HashSet<EdgeKey>();
        }

        var vertexSet = vertices as HashSet<int> ?? new HashSet<int>(vertices);
        var adjacency = MeshAdjacency.Build(mesh);
        var edges = new HashSet<EdgeKey>();
        foreach (var edge in adjacency.Edges.Keys)
        {
            if (vertexSet.Contains(edge.A) && vertexSet.Contains(edge.B))
            {
                edges.Add(edge);
            }
        }

        return edges;
    }

    private static HashSet<int> CollectFacesFromVertices(EditableMesh mesh, IReadOnlyCollection<int> vertices)
    {
        if (vertices.Count == 0)
        {
            return new HashSet<int>();
        }

        var vertexSet = vertices as HashSet<int> ?? new HashSet<int>(vertices);
        var indices = mesh.Indices;
        var triangleCount = indices.Count / 3;
        var faces = new HashSet<int>();

        for (int face = 0; face < triangleCount; face++)
        {
            int baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                break;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];
            if (vertexSet.Contains(i0) && vertexSet.Contains(i1) && vertexSet.Contains(i2))
            {
                faces.Add(face);
            }
        }

        return faces;
    }

    private static HashSet<int> CollectFacesFromEdges(EditableMesh mesh, IReadOnlyCollection<EdgeKey> edges)
    {
        if (edges.Count == 0)
        {
            return new HashSet<int>();
        }

        var edgeSet = edges as HashSet<EdgeKey> ?? new HashSet<EdgeKey>(edges);
        var adjacency = MeshAdjacency.Build(mesh);
        var faces = new HashSet<int>();

        for (int face = 0; face < adjacency.FaceEdges.Count; face++)
        {
            var faceEdges = adjacency.FaceEdges[face];
            if (faceEdges.Length == 0)
            {
                continue;
            }

            bool all = true;
            for (int i = 0; i < faceEdges.Length; i++)
            {
                if (!edgeSet.Contains(faceEdges[i]))
                {
                    all = false;
                    break;
                }
            }

            if (all)
            {
                faces.Add(face);
            }
        }

        return faces;
    }

    private static bool TryGetSelectionBounds(EditableMesh mesh, IReadOnlyCollection<int>? selection, out Vector3 min, out Vector3 max)
    {
        var positions = mesh.Positions;
        min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;

        if (selection is null)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                min = Vector3.Min(min, positions[i]);
                max = Vector3.Max(max, positions[i]);
                any = true;
            }

            return any;
        }

        foreach (var index in selection)
        {
            if ((uint)index >= (uint)positions.Count)
            {
                continue;
            }

            min = Vector3.Min(min, positions[index]);
            max = Vector3.Max(max, positions[index]);
            any = true;
        }

        return any;
    }

    private static Vector3 ComputeAverageFaceNormal(EditableMesh mesh, IReadOnlyCollection<int> faces)
    {
        if (faces.Count == 0)
        {
            return Vector3.UnitY;
        }

        var positions = mesh.Positions;
        var indices = mesh.Indices;
        var normal = Vector3.Zero;

        foreach (var face in faces)
        {
            var baseIndex = face * 3;
            if ((uint)(baseIndex + 2) >= (uint)indices.Count)
            {
                continue;
            }

            int i0 = indices[baseIndex];
            int i1 = indices[baseIndex + 1];
            int i2 = indices[baseIndex + 2];

            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            normal += Vector3.Cross(p1 - p0, p2 - p0);
        }

        if (normal.LengthSquared() > 1e-8f)
        {
            return Vector3.Normalize(normal);
        }

        return Vector3.UnitY;
    }
}
