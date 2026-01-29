using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Modeling;
using SkiaSharp;

namespace Skia3D.Editor;

public sealed class EditorSelectionService
{
    private const float ClickThreshold = 6f;
    private const float LassoPointSpacing = 4f;

    private readonly EditorSelectionState _selection;
    private readonly EditorDocument _document;
    private readonly EditorEditModeState _mode;
    private readonly EditorSelectionOptions _options;

    private bool _isMarqueeSelecting;
    private bool _isPaintSelecting;
    private bool _isLassoSelecting;
    private SKRect _marqueeRect;
    private SKPoint _paintCenter;
    private SelectionOperation _selectionOperation;
    private readonly List<SKPoint> _lassoPoints = new();
    private SKRect _lassoBounds;

    public EditorSelectionService(EditorSelectionState selection, EditorDocument document, EditorEditModeState mode, EditorSelectionOptions options)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _mode = mode ?? throw new ArgumentNullException(nameof(mode));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public event Action? SelectionChanged;

    public bool IsMarqueeSelecting => _isMarqueeSelecting;

    public bool IsPaintSelecting => _isPaintSelecting;

    public bool IsLassoSelecting => _isLassoSelecting;

    public SKRect MarqueeRect => _marqueeRect;

    public SKPoint PaintCenter => _paintCenter;

    public float PaintRadius => _options.PaintRadius;

    public IReadOnlyList<SKPoint> LassoPoints => _lassoPoints;

    public SKRect LassoBounds => _lassoBounds;

    public void BeginMarqueeSelection(SKPoint start, SelectionOperation operation, EditorSelectionContext context)
    {
        _isMarqueeSelecting = true;
        _selectionOperation = operation;
        _marqueeRect = CreateRect(start, start);
        ApplyMarqueeSelection(_marqueeRect, context);
    }

    public void UpdateMarqueeSelection(SKPoint current, SKPoint start, EditorSelectionContext context)
    {
        if (!_isMarqueeSelecting)
        {
            return;
        }

        _marqueeRect = CreateRect(start, current);
        ApplyMarqueeSelection(_marqueeRect, context);
    }

    public void BeginPaintSelection(SKPoint start, SelectionOperation operation, EditorSelectionContext context)
    {
        _isPaintSelecting = true;
        _paintCenter = start;
        _selectionOperation = operation == SelectionOperation.Replace ? SelectionOperation.Add : operation;

        if (operation == SelectionOperation.Replace)
        {
            ClearSelectionForPaint();
        }

        ApplyPaintSelection(_paintCenter, context);
    }

    public void UpdatePaintSelection(SKPoint current, EditorSelectionContext context)
    {
        if (!_isPaintSelecting)
        {
            return;
        }

        _paintCenter = current;
        ApplyPaintSelection(_paintCenter, context);
    }

    public void BeginLassoSelection(SKPoint start, SelectionOperation operation)
    {
        _isLassoSelecting = true;
        _selectionOperation = operation;
        _lassoPoints.Clear();
        _lassoPoints.Add(start);
        _lassoBounds = CreateRect(start, start);
    }

    public void UpdateLassoSelection(SKPoint current, EditorSelectionContext context, bool finalize)
    {
        if (!_isLassoSelecting)
        {
            return;
        }

        AddLassoPoint(current);

        if (IsLassoClick())
        {
            if (finalize)
            {
                ApplyClickSelection(current, _selectionOperation, context);
            }
            return;
        }

        ApplyLassoSelection(_lassoPoints, context);
    }

    public void EndSelection()
    {
        _isMarqueeSelecting = false;
        _isPaintSelecting = false;
        _isLassoSelecting = false;
    }

    public void TrySelectAt(SKPoint screenPoint, EditorSelectionContext context, bool toggle)
    {
        if (_mode.EditMode)
        {
            if (_mode.VertexSelect)
            {
                TrySelectVertexAt(screenPoint, context, toggle);
                return;
            }

            if (_mode.EdgeSelect)
            {
                TrySelectEdgeAt(screenPoint, context, toggle);
                return;
            }

            if (_mode.FaceSelect)
            {
                TrySelectFaceAt(screenPoint, context, toggle);
                return;
            }
        }

        TrySelectObjectAt(screenPoint, context, toggle);
    }

    public string BuildSelectionSummary(bool compact)
    {
        if (_selection.ObjectSelection.Count == 0)
        {
            return compact ? "Sel: none" : "Selection: none";
        }

        var prefix = compact ? "Sel: " : "Selection: ";
        if (_mode.VertexSelect)
        {
            return $"{prefix}{_selection.ObjectSelection.Count} obj, {_selection.VertexSelection.Count} vert";
        }

        if (_mode.EdgeSelect)
        {
            return $"{prefix}{_selection.ObjectSelection.Count} obj, {_selection.EdgeSelection.Count} edge";
        }

        if (_mode.FaceSelect)
        {
            return $"{prefix}{_selection.ObjectSelection.Count} obj, {_selection.FaceSelection.Count} face";
        }

        return $"{prefix}{_selection.ObjectSelection.Count} obj";
    }

    public string BuildSelectionStatusText()
    {
        var summary = BuildSelectionSummary(compact: true);
        var tool = _options.Tool switch
        {
            SelectionTool.Box => "Box",
            SelectionTool.Paint => "Paint",
            SelectionTool.Lasso => "Lasso",
            _ => "Click"
        };

        var mode = _options.Crossing ? "Cross" : "Window";
        return $"{summary} | Tool: {tool} ({mode}) | Q Click · B Box · V Paint · A Lasso · X Crossing | Shift add · Ctrl subtract";
    }

    private void ApplyMarqueeSelection(SKRect rect, EditorSelectionContext context)
    {
        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (_mode.EditMode && (_mode.VertexSelect || _mode.EdgeSelect || _mode.FaceSelect))
        {
            if (_selection.Selected is null || !_document.TryGetEditableMesh(_selection.Selected, out _))
            {
                if (_selectionOperation == SelectionOperation.Replace)
                {
                    _selection.VertexSelection.Clear();
                    _selection.EdgeSelection.Clear();
                    _selection.FaceSelection.Clear();
                    SelectionChanged?.Invoke();
                }
                return;
            }

            if (_mode.VertexSelect)
            {
                var hits = CollectVerticesInRect(_selection.Selected, viewport, context, rect);
                ApplyVertexSelection(hits, _selectionOperation, trim: true);
            }
            else if (_mode.EdgeSelect)
            {
                var hits = CollectEdgesInRect(_selection.Selected, viewport, context, rect, _options.Crossing);
                ApplyEdgeSelection(hits, _selectionOperation);
            }
            else if (_mode.FaceSelect)
            {
                var hits = CollectFacesInRect(_selection.Selected, viewport, context, rect, _options.Crossing);
                ApplyFaceSelection(hits, _selectionOperation);
            }

            return;
        }

        var objects = CollectObjectsInRect(viewport, context, rect, _options.Crossing);
        ApplyObjectSelectionResults(objects, _selectionOperation);
    }

    private void ApplyPaintSelection(SKPoint center, EditorSelectionContext context)
    {
        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (_mode.EditMode && (_mode.VertexSelect || _mode.EdgeSelect || _mode.FaceSelect))
        {
            if (_selection.Selected is null || !_document.TryGetEditableMesh(_selection.Selected, out _))
            {
                return;
            }

            if (_mode.VertexSelect)
            {
                var hits = CollectVerticesInCircle(_selection.Selected, viewport, context, center, _options.PaintRadius);
                ApplyVertexSelection(hits, _selectionOperation, trim: true);
            }
            else if (_mode.EdgeSelect)
            {
                var hits = CollectEdgesInCircle(_selection.Selected, viewport, context, center, _options.PaintRadius);
                ApplyEdgeSelection(hits, _selectionOperation);
            }
            else if (_mode.FaceSelect)
            {
                var hits = CollectFacesInCircle(_selection.Selected, viewport, context, center, _options.PaintRadius);
                ApplyFaceSelection(hits, _selectionOperation);
            }

            return;
        }

        var objects = CollectObjectsInCircle(viewport, context, center, _options.PaintRadius);
        ApplyObjectSelectionResults(objects, _selectionOperation);
    }

    private void ApplyLassoSelection(IReadOnlyList<SKPoint> polygon, EditorSelectionContext context)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        var bounds = _lassoBounds;

        if (_mode.EditMode && (_mode.VertexSelect || _mode.EdgeSelect || _mode.FaceSelect))
        {
            if (_selection.Selected is null || !_document.TryGetEditableMesh(_selection.Selected, out _))
            {
                if (_selectionOperation == SelectionOperation.Replace)
                {
                    _selection.VertexSelection.Clear();
                    _selection.EdgeSelection.Clear();
                    _selection.FaceSelection.Clear();
                    SelectionChanged?.Invoke();
                }
                return;
            }

            if (_mode.VertexSelect)
            {
                var hits = CollectVerticesInPolygon(_selection.Selected, viewport, context, polygon, bounds);
                ApplyVertexSelection(hits, _selectionOperation, trim: true);
            }
            else if (_mode.EdgeSelect)
            {
                var hits = CollectEdgesInPolygon(_selection.Selected, viewport, context, polygon, bounds, _options.Crossing);
                ApplyEdgeSelection(hits, _selectionOperation);
            }
            else if (_mode.FaceSelect)
            {
                var hits = CollectFacesInPolygon(_selection.Selected, viewport, context, polygon, bounds, _options.Crossing);
                ApplyFaceSelection(hits, _selectionOperation);
            }

            return;
        }

        var objects = CollectObjectsInPolygon(viewport, context, polygon, bounds, _options.Crossing);
        ApplyObjectSelectionResults(objects, _selectionOperation);
    }

    private void ApplyClickSelection(SKPoint screenPoint, SelectionOperation operation, EditorSelectionContext context)
    {
        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (context.Renderer.TryPickDetailed(screenPoint, viewport, context.Camera, context.Instances, out var pick))
        {
            if (_mode.EditMode && (_mode.VertexSelect || _mode.EdgeSelect || _mode.FaceSelect))
            {
                if (!ReferenceEquals(_selection.Selected, pick.Instance))
                {
                    ApplyObjectSelection(pick.Instance, pick, toggle: false);
                }

                if (!_document.TryGetEditableMesh(pick.Instance, out _))
                {
                    if (operation == SelectionOperation.Replace)
                    {
                        _selection.VertexSelection.Clear();
                        _selection.EdgeSelection.Clear();
                        _selection.FaceSelection.Clear();
                        SelectionChanged?.Invoke();
                    }
                    return;
                }

                if (_mode.VertexSelect)
                {
                    var hits = new HashSet<int> { EditorSelectionUtils.GetBestVertexIndex(pick) };
                    ApplyVertexSelection(hits, operation, trim: true);
                }
                else if (_mode.EdgeSelect)
                {
                    var hits = new HashSet<EdgeKey> { EditorSelectionUtils.GetPickedEdge(pick) };
                    ApplyEdgeSelection(hits, operation);
                }
                else if (_mode.FaceSelect)
                {
                    var hits = new HashSet<int> { pick.TriangleIndex };
                    ApplyFaceSelection(hits, operation);
                }

                _selection.LastPick = pick;
                return;
            }

            var objects = new HashSet<MeshInstance> { pick.Instance };
            ApplyObjectSelectionResults(objects, operation);
        }
        else if (operation == SelectionOperation.Replace)
        {
            if (_mode.EditMode && (_mode.VertexSelect || _mode.EdgeSelect || _mode.FaceSelect))
            {
                _selection.VertexSelection.Clear();
                _selection.EdgeSelection.Clear();
                _selection.FaceSelection.Clear();
                SelectionChanged?.Invoke();
            }
            else
            {
                ClearSelection();
            }
        }
    }

    private void ClearSelectionForPaint()
    {
        if (_mode.EditMode && (_mode.VertexSelect || _mode.EdgeSelect || _mode.FaceSelect))
        {
            _selection.VertexSelection.Clear();
            _selection.EdgeSelection.Clear();
            _selection.FaceSelection.Clear();
        }
        else
        {
            _selection.ObjectSelection.Clear();
            _selection.Selected = null;
            _selection.LastPick = null;
        }

        SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        _selection.ClearAll();
        SelectionChanged?.Invoke();
    }

    private void ApplyObjectSelectionResults(HashSet<MeshInstance> hits, SelectionOperation operation)
    {
        if (operation == SelectionOperation.Replace)
        {
            _selection.ObjectSelection.Clear();
            _selection.VertexSelection.Clear();
            _selection.EdgeSelection.Clear();
            _selection.FaceSelection.Clear();
            foreach (var instance in hits)
            {
                _selection.ObjectSelection.Add(instance);
            }

            _selection.Selected = hits.Count > 0 ? hits.First() : null;
            _selection.LastPick = null;
        }
        else if (operation == SelectionOperation.Add)
        {
            foreach (var instance in hits)
            {
                _selection.ObjectSelection.Add(instance);
            }

            if (_selection.Selected is null && hits.Count > 0)
            {
                _selection.Selected = hits.First();
            }
        }
        else if (operation == SelectionOperation.Subtract)
        {
            foreach (var instance in hits)
            {
                _selection.ObjectSelection.Remove(instance);
            }

            if (_selection.Selected != null && !_selection.ObjectSelection.Contains(_selection.Selected))
            {
                _selection.Selected = _selection.GetFirstSelection();
                _selection.LastPick = null;
            }
        }

        if (_selection.ObjectSelection.Count == 0)
        {
            _selection.Selected = null;
            _selection.LastPick = null;
            _selection.VertexSelection.Clear();
            _selection.EdgeSelection.Clear();
            _selection.FaceSelection.Clear();
        }

        SelectionChanged?.Invoke();
    }

    private void ApplyVertexSelection(HashSet<int> hits, SelectionOperation operation, bool trim)
    {
        _selection.EdgeSelection.Clear();
        if (operation == SelectionOperation.Replace)
        {
            _selection.VertexSelection.Clear();
            foreach (var index in hits)
            {
                _selection.VertexSelection.Add(index);
            }
        }
        else if (operation == SelectionOperation.Add)
        {
            foreach (var index in hits)
            {
                _selection.VertexSelection.Add(index);
            }
        }
        else if (operation == SelectionOperation.Subtract)
        {
            foreach (var index in hits)
            {
                _selection.VertexSelection.Remove(index);
            }
        }

        if (trim)
        {
            TrimVertexSelection();
        }

        _selection.LastPick = null;
        SelectionChanged?.Invoke();
    }

    private void ApplyEdgeSelection(HashSet<EdgeKey> hits, SelectionOperation operation)
    {
        _selection.VertexSelection.Clear();
        _selection.FaceSelection.Clear();
        if (operation == SelectionOperation.Replace)
        {
            _selection.EdgeSelection.Clear();
            foreach (var edge in hits)
            {
                _selection.EdgeSelection.Add(edge);
            }
        }
        else if (operation == SelectionOperation.Add)
        {
            foreach (var edge in hits)
            {
                _selection.EdgeSelection.Add(edge);
            }
        }
        else if (operation == SelectionOperation.Subtract)
        {
            foreach (var edge in hits)
            {
                _selection.EdgeSelection.Remove(edge);
            }
        }

        TrimEdgeSelection();
        _selection.LastPick = null;
        SelectionChanged?.Invoke();
    }

    private void ApplyFaceSelection(HashSet<int> hits, SelectionOperation operation)
    {
        _selection.EdgeSelection.Clear();
        if (operation == SelectionOperation.Replace)
        {
            _selection.FaceSelection.Clear();
            foreach (var index in hits)
            {
                _selection.FaceSelection.Add(index);
            }
        }
        else if (operation == SelectionOperation.Add)
        {
            foreach (var index in hits)
            {
                _selection.FaceSelection.Add(index);
            }
        }
        else if (operation == SelectionOperation.Subtract)
        {
            foreach (var index in hits)
            {
                _selection.FaceSelection.Remove(index);
            }
        }

        TrimFaceSelection();
        _selection.LastPick = null;
        SelectionChanged?.Invoke();
    }

    private HashSet<MeshInstance> CollectObjectsInRect(SKRect viewport, EditorSelectionContext context, SKRect rect, bool crossing)
    {
        var results = new HashSet<MeshInstance>();
        foreach (var instance in context.Instances)
        {
            if (!context.Renderer.TryGetScreenBounds(instance, viewport, context.Camera, out var bounds))
            {
                continue;
            }

            if (crossing)
            {
                if (RectIntersects(rect, bounds))
                {
                    results.Add(instance);
                }
            }
            else if (RectContains(rect, bounds))
            {
                results.Add(instance);
            }
        }

        return results;
    }

    private HashSet<MeshInstance> CollectObjectsInCircle(SKRect viewport, EditorSelectionContext context, SKPoint center, float radius)
    {
        var results = new HashSet<MeshInstance>();
        foreach (var instance in context.Instances)
        {
            if (!context.Renderer.TryGetScreenBounds(instance, viewport, context.Camera, out var bounds))
            {
                continue;
            }

            if (CircleIntersectsRect(center, radius, bounds))
            {
                results.Add(instance);
            }
        }

        return results;
    }

    private HashSet<MeshInstance> CollectObjectsInPolygon(SKRect viewport, EditorSelectionContext context, IReadOnlyList<SKPoint> polygon, SKRect polygonBounds, bool crossing)
    {
        var results = new HashSet<MeshInstance>();
        foreach (var instance in context.Instances)
        {
            if (!context.Renderer.TryGetScreenBounds(instance, viewport, context.Camera, out var bounds))
            {
                continue;
            }

            if (!RectIntersects(polygonBounds, bounds))
            {
                continue;
            }

            if (crossing)
            {
                if (PolygonIntersectsRect(polygon, polygonBounds, bounds))
                {
                    results.Add(instance);
                }
            }
            else if (RectInsidePolygon(polygon, bounds))
            {
                results.Add(instance);
            }
        }

        return results;
    }

    private HashSet<int> CollectVerticesInRect(MeshInstance instance, SKRect viewport, EditorSelectionContext context, SKRect rect)
    {
        var results = new HashSet<int>();
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int i = 0; i < verts.Count; i++)
        {
            var world = Vector3.Transform(verts[i].Position, transform);
            if (context.Renderer.TryProjectWorld(world, viewport, context.Camera, out var screen))
            {
                if (RectContains(rect, screen))
                {
                    results.Add(i);
                }
            }
        }

        return results;
    }

    private HashSet<int> CollectVerticesInPolygon(MeshInstance instance, SKRect viewport, EditorSelectionContext context, IReadOnlyList<SKPoint> polygon, SKRect polygonBounds)
    {
        var results = new HashSet<int>();
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int i = 0; i < verts.Count; i++)
        {
            var world = Vector3.Transform(verts[i].Position, transform);
            if (context.Renderer.TryProjectWorld(world, viewport, context.Camera, out var screen))
            {
                if (!RectContains(polygonBounds, screen))
                {
                    continue;
                }

                if (PointInPolygon(polygon, screen))
                {
                    results.Add(i);
                }
            }
        }

        return results;
    }

    private HashSet<int> CollectVerticesInCircle(MeshInstance instance, SKRect viewport, EditorSelectionContext context, SKPoint center, float radius)
    {
        var results = new HashSet<int>();
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;
        float radiusSq = radius * radius;

        for (int i = 0; i < verts.Count; i++)
        {
            var world = Vector3.Transform(verts[i].Position, transform);
            if (context.Renderer.TryProjectWorld(world, viewport, context.Camera, out var screen))
            {
                var dx = screen.X - center.X;
                var dy = screen.Y - center.Y;
                if ((dx * dx + dy * dy) <= radiusSq)
                {
                    results.Add(i);
                }
            }
        }

        return results;
    }

    private HashSet<EdgeKey> CollectEdgesInRect(MeshInstance instance, SKRect viewport, EditorSelectionContext context, SKRect rect, bool crossing)
    {
        var results = new HashSet<EdgeKey>();
        var visited = new HashSet<EdgeKey>();
        var indices = instance.Mesh.Indices;
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int face = 0; face * 3 + 2 < indices.Count; face++)
        {
            int i0 = indices[face * 3];
            int i1 = indices[face * 3 + 1];
            int i2 = indices[face * 3 + 2];

            if ((uint)i0 >= (uint)verts.Count || (uint)i1 >= (uint)verts.Count || (uint)i2 >= (uint)verts.Count)
            {
                continue;
            }

            AddEdgeCandidate(i0, i1);
            AddEdgeCandidate(i1, i2);
            AddEdgeCandidate(i2, i0);
        }

        return results;

        void AddEdgeCandidate(int a, int b)
        {
            var edge = new EdgeKey(a, b);
            if (!visited.Add(edge))
            {
                return;
            }

            if (!TryProjectEdge(verts, transform, viewport, context, edge, out var s0, out var s1))
            {
                return;
            }

            bool hit = crossing
                ? SegmentIntersectsRect(s0, s1, rect)
                : RectContains(rect, s0) && RectContains(rect, s1);
            if (hit)
            {
                results.Add(edge);
            }
        }
    }

    private HashSet<EdgeKey> CollectEdgesInPolygon(
        MeshInstance instance,
        SKRect viewport,
        EditorSelectionContext context,
        IReadOnlyList<SKPoint> polygon,
        SKRect polygonBounds,
        bool crossing)
    {
        var results = new HashSet<EdgeKey>();
        var visited = new HashSet<EdgeKey>();
        var indices = instance.Mesh.Indices;
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int face = 0; face * 3 + 2 < indices.Count; face++)
        {
            int i0 = indices[face * 3];
            int i1 = indices[face * 3 + 1];
            int i2 = indices[face * 3 + 2];

            if ((uint)i0 >= (uint)verts.Count || (uint)i1 >= (uint)verts.Count || (uint)i2 >= (uint)verts.Count)
            {
                continue;
            }

            AddEdgeCandidate(i0, i1);
            AddEdgeCandidate(i1, i2);
            AddEdgeCandidate(i2, i0);
        }

        return results;

        void AddEdgeCandidate(int a, int b)
        {
            var edge = new EdgeKey(a, b);
            if (!visited.Add(edge))
            {
                return;
            }

            if (!TryProjectEdge(verts, transform, viewport, context, edge, out var s0, out var s1))
            {
                return;
            }

            var edgeBounds = CreateRect(s0, s1);
            if (!RectIntersects(polygonBounds, edgeBounds))
            {
                return;
            }

            bool hit = crossing
                ? PointInPolygon(polygon, s0) || PointInPolygon(polygon, s1) || SegmentIntersectsPolygon(polygon, s0, s1)
                : PointInPolygon(polygon, s0) && PointInPolygon(polygon, s1);
            if (hit)
            {
                results.Add(edge);
            }
        }
    }

    private HashSet<EdgeKey> CollectEdgesInCircle(
        MeshInstance instance,
        SKRect viewport,
        EditorSelectionContext context,
        SKPoint center,
        float radius)
    {
        var results = new HashSet<EdgeKey>();
        var visited = new HashSet<EdgeKey>();
        var indices = instance.Mesh.Indices;
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int face = 0; face * 3 + 2 < indices.Count; face++)
        {
            int i0 = indices[face * 3];
            int i1 = indices[face * 3 + 1];
            int i2 = indices[face * 3 + 2];

            if ((uint)i0 >= (uint)verts.Count || (uint)i1 >= (uint)verts.Count || (uint)i2 >= (uint)verts.Count)
            {
                continue;
            }

            AddEdgeCandidate(i0, i1);
            AddEdgeCandidate(i1, i2);
            AddEdgeCandidate(i2, i0);
        }

        return results;

        void AddEdgeCandidate(int a, int b)
        {
            var edge = new EdgeKey(a, b);
            if (!visited.Add(edge))
            {
                return;
            }

            if (!TryProjectEdge(verts, transform, viewport, context, edge, out var s0, out var s1))
            {
                return;
            }

            bool hit = CircleContainsPoint(center, radius, s0) && CircleContainsPoint(center, radius, s1);
            if (!hit)
            {
                hit = SegmentIntersectsCircle(center, radius, s0, s1);
            }

            if (hit)
            {
                results.Add(edge);
            }
        }
    }

    private HashSet<int> CollectFacesInRect(MeshInstance instance, SKRect viewport, EditorSelectionContext context, SKRect rect, bool crossing)
    {
        var results = new HashSet<int>();
        var indices = instance.Mesh.Indices;
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int face = 0; face * 3 + 2 < indices.Count; face++)
        {
            int i0 = indices[face * 3];
            int i1 = indices[face * 3 + 1];
            int i2 = indices[face * 3 + 2];

            if ((uint)i0 >= (uint)verts.Count || (uint)i1 >= (uint)verts.Count || (uint)i2 >= (uint)verts.Count)
            {
                continue;
            }

            if (!TryProjectTriangle(verts, transform, viewport, context, i0, i1, i2, out var s0, out var s1, out var s2))
            {
                continue;
            }

            if (crossing)
            {
                var bounds = CreateRect(s0, s1, s2);
                if (RectIntersects(rect, bounds))
                {
                    results.Add(face);
                }
            }
            else if (RectContains(rect, s0) && RectContains(rect, s1) && RectContains(rect, s2))
            {
                results.Add(face);
            }
        }

        return results;
    }

    private HashSet<int> CollectFacesInPolygon(MeshInstance instance, SKRect viewport, EditorSelectionContext context, IReadOnlyList<SKPoint> polygon, SKRect polygonBounds, bool crossing)
    {
        var results = new HashSet<int>();
        var indices = instance.Mesh.Indices;
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int face = 0; face * 3 + 2 < indices.Count; face++)
        {
            int i0 = indices[face * 3];
            int i1 = indices[face * 3 + 1];
            int i2 = indices[face * 3 + 2];

            if ((uint)i0 >= (uint)verts.Count || (uint)i1 >= (uint)verts.Count || (uint)i2 >= (uint)verts.Count)
            {
                continue;
            }

            if (!TryProjectTriangle(verts, transform, viewport, context, i0, i1, i2, out var s0, out var s1, out var s2))
            {
                continue;
            }

            if (crossing)
            {
                if (PolygonIntersectsTriangle(polygon, polygonBounds, s0, s1, s2))
                {
                    results.Add(face);
                }
            }
            else if (PointInPolygon(polygon, s0) && PointInPolygon(polygon, s1) && PointInPolygon(polygon, s2))
            {
                results.Add(face);
            }
        }

        return results;
    }

    private HashSet<int> CollectFacesInCircle(MeshInstance instance, SKRect viewport, EditorSelectionContext context, SKPoint center, float radius)
    {
        var results = new HashSet<int>();
        var indices = instance.Mesh.Indices;
        var verts = instance.Mesh.Vertices;
        var transform = instance.Transform;

        for (int face = 0; face * 3 + 2 < indices.Count; face++)
        {
            int i0 = indices[face * 3];
            int i1 = indices[face * 3 + 1];
            int i2 = indices[face * 3 + 2];

            if ((uint)i0 >= (uint)verts.Count || (uint)i1 >= (uint)verts.Count || (uint)i2 >= (uint)verts.Count)
            {
                continue;
            }

            if (!TryProjectTriangle(verts, transform, viewport, context, i0, i1, i2, out var s0, out var s1, out var s2))
            {
                continue;
            }

            if (CircleContainsPoint(center, radius, s0) ||
                CircleContainsPoint(center, radius, s1) ||
                CircleContainsPoint(center, radius, s2))
            {
                results.Add(face);
                continue;
            }

            var bounds = CreateRect(s0, s1, s2);
            if (CircleIntersectsRect(center, radius, bounds))
            {
                results.Add(face);
            }
        }

        return results;
    }

    private static bool TryProjectTriangle(IReadOnlyList<Vertex> verts, Matrix4x4 transform, SKRect viewport, EditorSelectionContext context, int i0, int i1, int i2, out SKPoint s0, out SKPoint s1, out SKPoint s2)
    {
        s0 = default;
        s1 = default;
        s2 = default;

        var w0 = Vector3.Transform(verts[i0].Position, transform);
        var w1 = Vector3.Transform(verts[i1].Position, transform);
        var w2 = Vector3.Transform(verts[i2].Position, transform);

        if (!context.Renderer.TryProjectWorld(w0, viewport, context.Camera, out s0))
        {
            return false;
        }

        if (!context.Renderer.TryProjectWorld(w1, viewport, context.Camera, out s1))
        {
            return false;
        }

        if (!context.Renderer.TryProjectWorld(w2, viewport, context.Camera, out s2))
        {
            return false;
        }

        return true;
    }

    private static bool TryProjectEdge(
        IReadOnlyList<Vertex> verts,
        Matrix4x4 transform,
        SKRect viewport,
        EditorSelectionContext context,
        EdgeKey edge,
        out SKPoint s0,
        out SKPoint s1)
    {
        s0 = default;
        s1 = default;

        if ((uint)edge.A >= (uint)verts.Count || (uint)edge.B >= (uint)verts.Count)
        {
            return false;
        }

        var w0 = Vector3.Transform(verts[edge.A].Position, transform);
        var w1 = Vector3.Transform(verts[edge.B].Position, transform);

        if (!context.Renderer.TryProjectWorld(w0, viewport, context.Camera, out s0))
        {
            return false;
        }

        if (!context.Renderer.TryProjectWorld(w1, viewport, context.Camera, out s1))
        {
            return false;
        }

        return true;
    }

    private static bool RectContains(SKRect rect, SKPoint point)
    {
        return point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom;
    }

    private static bool RectContains(SKRect rect, SKRect other)
    {
        return other.Left >= rect.Left && other.Right <= rect.Right && other.Top >= rect.Top && other.Bottom <= rect.Bottom;
    }

    private static bool RectIntersects(SKRect a, SKRect b)
    {
        return a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;
    }

    private static bool CircleContainsPoint(SKPoint center, float radius, SKPoint point)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return (dx * dx + dy * dy) <= radius * radius;
    }

    private static bool CircleIntersectsRect(SKPoint center, float radius, SKRect rect)
    {
        var closestX = Math.Clamp(center.X, rect.Left, rect.Right);
        var closestY = Math.Clamp(center.Y, rect.Top, rect.Bottom);
        var dx = center.X - closestX;
        var dy = center.Y - closestY;
        return (dx * dx + dy * dy) <= radius * radius;
    }

    private static bool SegmentIntersectsRect(SKPoint a, SKPoint b, SKRect rect)
    {
        if (RectContains(rect, a) || RectContains(rect, b))
        {
            return true;
        }

        var tl = new SKPoint(rect.Left, rect.Top);
        var tr = new SKPoint(rect.Right, rect.Top);
        var br = new SKPoint(rect.Right, rect.Bottom);
        var bl = new SKPoint(rect.Left, rect.Bottom);

        return SegmentsIntersect(a, b, tl, tr) ||
               SegmentsIntersect(a, b, tr, br) ||
               SegmentsIntersect(a, b, br, bl) ||
               SegmentsIntersect(a, b, bl, tl);
    }

    private static bool SegmentIntersectsCircle(SKPoint center, float radius, SKPoint a, SKPoint b)
    {
        var abX = b.X - a.X;
        var abY = b.Y - a.Y;
        var abLenSq = abX * abX + abY * abY;
        if (abLenSq <= 1e-6f)
        {
            return CircleContainsPoint(center, radius, a);
        }

        var acX = center.X - a.X;
        var acY = center.Y - a.Y;
        var t = (acX * abX + acY * abY) / abLenSq;
        t = Math.Clamp(t, 0f, 1f);
        var closest = new SKPoint(a.X + abX * t, a.Y + abY * t);
        return CircleContainsPoint(center, radius, closest);
    }

    private static SKRect ExpandRect(SKRect rect, SKPoint point)
    {
        return new SKRect(
            MathF.Min(rect.Left, point.X),
            MathF.Min(rect.Top, point.Y),
            MathF.Max(rect.Right, point.X),
            MathF.Max(rect.Bottom, point.Y));
    }

    private static bool PointInPolygon(IReadOnlyList<SKPoint> polygon, SKPoint point)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            bool intersect = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                             (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + 1e-6f) + pi.X);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool RectInsidePolygon(IReadOnlyList<SKPoint> polygon, SKRect rect)
    {
        var tl = new SKPoint(rect.Left, rect.Top);
        var tr = new SKPoint(rect.Right, rect.Top);
        var br = new SKPoint(rect.Right, rect.Bottom);
        var bl = new SKPoint(rect.Left, rect.Bottom);
        return PointInPolygon(polygon, tl) &&
               PointInPolygon(polygon, tr) &&
               PointInPolygon(polygon, br) &&
               PointInPolygon(polygon, bl);
    }

    private static bool PolygonIntersectsRect(IReadOnlyList<SKPoint> polygon, SKRect polygonBounds, SKRect rect)
    {
        if (!RectIntersects(polygonBounds, rect))
        {
            return false;
        }

        var tl = new SKPoint(rect.Left, rect.Top);
        var tr = new SKPoint(rect.Right, rect.Top);
        var br = new SKPoint(rect.Right, rect.Bottom);
        var bl = new SKPoint(rect.Left, rect.Bottom);

        if (PointInPolygon(polygon, tl) || PointInPolygon(polygon, tr) ||
            PointInPolygon(polygon, br) || PointInPolygon(polygon, bl))
        {
            return true;
        }

        for (int i = 0; i < polygon.Count; i++)
        {
            if (RectContains(rect, polygon[i]))
            {
                return true;
            }
        }

        var rectPoints = new[] { tl, tr, br, bl };
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            for (int r = 0; r < rectPoints.Length; r++)
            {
                var c = rectPoints[r];
                var d = rectPoints[(r + 1) % rectPoints.Length];
                if (SegmentsIntersect(a, b, c, d))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PolygonIntersectsTriangle(IReadOnlyList<SKPoint> polygon, SKRect polygonBounds, SKPoint a, SKPoint b, SKPoint c)
    {
        var triBounds = CreateRect(a, b, c);
        if (!RectIntersects(polygonBounds, triBounds))
        {
            return false;
        }

        if (PointInPolygon(polygon, a) || PointInPolygon(polygon, b) || PointInPolygon(polygon, c))
        {
            return true;
        }

        for (int i = 0; i < polygon.Count; i++)
        {
            if (PointInTriangle(polygon[i], a, b, c))
            {
                return true;
            }
        }

        return SegmentIntersectsPolygon(polygon, a, b) ||
               SegmentIntersectsPolygon(polygon, b, c) ||
               SegmentIntersectsPolygon(polygon, c, a);
    }

    private static bool SegmentIntersectsPolygon(IReadOnlyList<SKPoint> polygon, SKPoint a, SKPoint b)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            var c = polygon[i];
            var d = polygon[(i + 1) % polygon.Count];
            if (SegmentsIntersect(a, b, c, d))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(SKPoint p1, SKPoint p2, SKPoint q1, SKPoint q2)
    {
        var o1 = Orientation(p1, p2, q1);
        var o2 = Orientation(p1, p2, q2);
        var o3 = Orientation(q1, q2, p1);
        var o4 = Orientation(q1, q2, p2);

        if (MathF.Abs(o1) <= 1e-6f && OnSegment(p1, p2, q1))
        {
            return true;
        }

        if (MathF.Abs(o2) <= 1e-6f && OnSegment(p1, p2, q2))
        {
            return true;
        }

        if (MathF.Abs(o3) <= 1e-6f && OnSegment(q1, q2, p1))
        {
            return true;
        }

        if (MathF.Abs(o4) <= 1e-6f && OnSegment(q1, q2, p2))
        {
            return true;
        }

        return (o1 > 0f && o2 < 0f || o1 < 0f && o2 > 0f) &&
               (o3 > 0f && o4 < 0f || o3 < 0f && o4 > 0f);
    }

    private static float Orientation(SKPoint a, SKPoint b, SKPoint c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    private static bool OnSegment(SKPoint a, SKPoint b, SKPoint p)
    {
        return p.X >= MathF.Min(a.X, b.X) - 1e-6f &&
               p.X <= MathF.Max(a.X, b.X) + 1e-6f &&
               p.Y >= MathF.Min(a.Y, b.Y) - 1e-6f &&
               p.Y <= MathF.Max(a.Y, b.Y) + 1e-6f;
    }

    private static bool PointInTriangle(SKPoint p, SKPoint a, SKPoint b, SKPoint c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);

        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;

        return !(hasNeg && hasPos);
    }

    private static float Sign(SKPoint p1, SKPoint p2, SKPoint p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    private static float PolygonArea(IReadOnlyList<SKPoint> polygon)
    {
        if (polygon.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            area += polygon[j].X * polygon[i].Y - polygon[i].X * polygon[j].Y;
        }

        return area * 0.5f;
    }

    private static SKRect CreateRect(SKPoint a, SKPoint b)
    {
        return new SKRect(
            MathF.Min(a.X, b.X),
            MathF.Min(a.Y, b.Y),
            MathF.Max(a.X, b.X),
            MathF.Max(a.Y, b.Y));
    }

    private static SKRect CreateRect(SKPoint a, SKPoint b, SKPoint c)
    {
        var minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
        var minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
        var maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
        var maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
        return new SKRect(minX, minY, maxX, maxY);
    }

    private void AddLassoPoint(SKPoint point)
    {
        if (_lassoPoints.Count == 0)
        {
            _lassoPoints.Add(point);
            _lassoBounds = CreateRect(point, point);
            return;
        }

        var last = _lassoPoints[_lassoPoints.Count - 1];
        if (DistanceSquared(last, point) < LassoPointSpacing * LassoPointSpacing)
        {
            _lassoPoints[_lassoPoints.Count - 1] = point;
            _lassoBounds = ExpandRect(_lassoBounds, point);
            return;
        }

        _lassoPoints.Add(point);
        _lassoBounds = ExpandRect(_lassoBounds, point);
    }

    private bool IsLassoClick()
    {
        if (_lassoPoints.Count < 3)
        {
            return true;
        }

        var area = MathF.Abs(PolygonArea(_lassoPoints));
        return area <= ClickThreshold * ClickThreshold;
    }

    private static float DistanceSquared(SKPoint a, SKPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private void TrySelectObjectAt(SKPoint screenPoint, EditorSelectionContext context, bool toggle)
    {
        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (context.Renderer.TryPickDetailed(screenPoint, viewport, context.Camera, context.Instances, out var pick))
        {
            ApplyObjectSelection(pick.Instance, pick, toggle);
        }
        else if (!toggle)
        {
            ClearSelection();
        }
    }

    private void TrySelectVertexAt(SKPoint screenPoint, EditorSelectionContext context, bool toggle)
    {
        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (context.Renderer.TryPickDetailed(screenPoint, viewport, context.Camera, context.Instances, out var pick))
        {
            _selection.EdgeSelection.Clear();
            if (!ReferenceEquals(_selection.Selected, pick.Instance))
            {
                ApplyObjectSelection(pick.Instance, pick, toggle: false);
            }

            if (!_document.TryGetEditableMesh(pick.Instance, out _))
            {
                _selection.VertexSelection.Clear();
                SelectionChanged?.Invoke();
                return;
            }

            var vertexIndex = EditorSelectionUtils.GetBestVertexIndex(pick);
            if (toggle)
            {
                _selection.VertexSelection.Toggle(vertexIndex);
            }
            else
            {
                _selection.VertexSelection.Clear();
                _selection.VertexSelection.Add(vertexIndex);
            }

            _selection.LastPick = pick;
            TrimVertexSelection();
            SelectionChanged?.Invoke();
        }
        else if (!toggle)
        {
            _selection.VertexSelection.Clear();
            SelectionChanged?.Invoke();
        }
    }

    private void TrySelectEdgeAt(SKPoint screenPoint, EditorSelectionContext context, bool toggle)
    {
        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (context.Renderer.TryPickDetailed(screenPoint, viewport, context.Camera, context.Instances, out var pick))
        {
            if (!ReferenceEquals(_selection.Selected, pick.Instance))
            {
                ApplyObjectSelection(pick.Instance, pick, toggle: false);
            }

            if (!_document.TryGetEditableMesh(pick.Instance, out _))
            {
                _selection.EdgeSelection.Clear();
                SelectionChanged?.Invoke();
                return;
            }

            var edge = EditorSelectionUtils.GetPickedEdge(pick);
            if (toggle)
            {
                _selection.EdgeSelection.Toggle(edge);
            }
            else
            {
                _selection.EdgeSelection.Clear();
                _selection.EdgeSelection.Add(edge);
            }

            _selection.VertexSelection.Clear();
            _selection.FaceSelection.Clear();
            _selection.LastPick = pick;
            TrimEdgeSelection();
            SelectionChanged?.Invoke();
        }
        else if (!toggle)
        {
            _selection.EdgeSelection.Clear();
            SelectionChanged?.Invoke();
        }
    }

    private void TrySelectFaceAt(SKPoint screenPoint, EditorSelectionContext context, bool toggle)
    {
        var viewport = context.Viewport;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (context.Renderer.TryPickDetailed(screenPoint, viewport, context.Camera, context.Instances, out var pick))
        {
            _selection.EdgeSelection.Clear();
            if (!ReferenceEquals(_selection.Selected, pick.Instance))
            {
                ApplyObjectSelection(pick.Instance, pick, toggle: false);
            }

            if (!_document.TryGetEditableMesh(pick.Instance, out _))
            {
                _selection.FaceSelection.Clear();
                SelectionChanged?.Invoke();
                return;
            }

            if (toggle)
            {
                _selection.FaceSelection.Toggle(pick.TriangleIndex);
            }
            else
            {
                _selection.FaceSelection.Clear();
                _selection.FaceSelection.Add(pick.TriangleIndex);
            }

            _selection.LastPick = pick;
            TrimFaceSelection();
            SelectionChanged?.Invoke();
        }
        else if (!toggle)
        {
            _selection.FaceSelection.Clear();
            SelectionChanged?.Invoke();
        }
    }

    private void ApplyObjectSelection(MeshInstance instance, Renderer3D.PickDetail? pick, bool toggle)
    {
        if (!toggle)
        {
            _selection.ObjectSelection.Clear();
            _selection.VertexSelection.Clear();
            _selection.EdgeSelection.Clear();
            _selection.FaceSelection.Clear();
            _selection.ObjectSelection.Add(instance);
            _selection.Selected = instance;
        }
        else
        {
            var previous = _selection.Selected;
            var added = _selection.ObjectSelection.Toggle(instance);
            if (added)
            {
                _selection.Selected = instance;
                if (!ReferenceEquals(previous, instance))
                {
                    _selection.VertexSelection.Clear();
                    _selection.EdgeSelection.Clear();
                    _selection.FaceSelection.Clear();
                }
                _selection.LastPick = pick;
            }
            else if (_selection.Selected == instance)
            {
                _selection.Selected = _selection.GetFirstSelection();
                _selection.VertexSelection.Clear();
                _selection.EdgeSelection.Clear();
                _selection.FaceSelection.Clear();
                _selection.LastPick = null;
            }
        }

        if (!toggle)
        {
            _selection.LastPick = pick;
        }

        if (_selection.ObjectSelection.Count == 0)
        {
            _selection.Selected = null;
            _selection.LastPick = null;
            _selection.VertexSelection.Clear();
            _selection.EdgeSelection.Clear();
            _selection.FaceSelection.Clear();
        }

        SelectionChanged?.Invoke();
    }

    private void TrimVertexSelection()
    {
        if (_selection.Selected is null || _selection.VertexSelection.IsEmpty)
        {
            _selection.VertexSelection.Clear();
            return;
        }

        var count = _selection.Selected.Mesh.Vertices.Count;
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

    private void TrimEdgeSelection()
    {
        if (_selection.Selected is null || _selection.EdgeSelection.IsEmpty)
        {
            _selection.EdgeSelection.Clear();
            return;
        }

        var count = _selection.Selected.Mesh.Vertices.Count;
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

    private void TrimFaceSelection()
    {
        if (_selection.Selected is null || _selection.FaceSelection.IsEmpty)
        {
            _selection.FaceSelection.Clear();
            return;
        }

        var count = _selection.Selected.Mesh.Indices.Count / 3;
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
}
