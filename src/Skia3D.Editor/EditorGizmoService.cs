using System;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Modeling;
using Skia3D.Scene;
using SkiaSharp;

namespace Skia3D.Editor;

public sealed class EditorGizmoService
{
    private MeshInstance? _selectedInstance;
    private Vector3 _gizmoStartWorld;
    private Vector3 _gizmoCenterWorld;
    private Matrix4x4 _gizmoStartTransform;
    private float _gizmoDepth;
    private float _gizmoStartRotation;
    private SKPoint _gizmoCenterScreen;
    private SKPoint _gizmoStartScreen;
    private Vector3 _gizmoStartScaleVector;

    public GizmoMode Mode { get; set; } = GizmoMode.Translate;

    public GizmoAxisConstraint AxisConstraint { get; set; } = GizmoAxisConstraint.None;

    public bool SnapEnabled { get; set; }

    public float SnapStep { get; set; } = 0.5f;

    public float RotateSnapDegrees { get; set; } = 15f;

    public float ScaleSnapStep { get; set; } = 0.1f;

    public bool ShowGizmo { get; set; }

    public bool IsDragging { get; private set; }

    public bool TryBeginDrag(SKPoint screenPoint, Renderer3D renderer, Camera camera, SKRect viewport, MeshInstance selected, SceneNode selectedNode)
    {
        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return false;
        }

        if (!renderer.TryGetScreenBounds(selected, viewport, camera, out var bounds))
        {
            return false;
        }

        if (!bounds.Contains(screenPoint.X, screenPoint.Y))
        {
            return false;
        }

        if (!TryGetSelectedCenter(selected, out _gizmoCenterWorld))
        {
            return false;
        }

        _selectedInstance = selected;
        _gizmoStartTransform = selectedNode.Transform.WorldMatrix;

        if (renderer.TryProjectWorld(_gizmoCenterWorld, viewport, camera, out var centerScreen, out var ndcZ))
        {
            _gizmoDepth = ndcZ;
            _gizmoCenterScreen = centerScreen;
            _gizmoStartRotation = MathF.Atan2(screenPoint.Y - centerScreen.Y, screenPoint.X - centerScreen.X);
        }
        else
        {
            _gizmoDepth = 0.5f;
            _gizmoCenterScreen = screenPoint;
            _gizmoStartRotation = 0f;
        }

        _gizmoStartScreen = screenPoint;
        if (renderer.TryGetWorldAtScreen(_gizmoStartScreen, viewport, camera, out var world))
        {
            _gizmoStartWorld = world;
            _gizmoStartScaleVector = _gizmoStartWorld - _gizmoCenterWorld;
            IsDragging = true;
            return true;
        }

        var viewportSize = new Vector2(viewport.Width, viewport.Height);
        var local = new Vector2(screenPoint.X - viewport.Left, screenPoint.Y - viewport.Top);
        if (Camera.TryUnproject(camera, local, viewportSize, _gizmoDepth, out world))
        {
            _gizmoStartWorld = world;
            _gizmoStartScaleVector = _gizmoStartWorld - _gizmoCenterWorld;
            IsDragging = true;
            return true;
        }

        return false;
    }

    public void UpdateDrag(SKPoint screenPoint, Renderer3D renderer, Camera camera, SKRect viewport, SceneNode selectedNode)
    {
        if (!IsDragging)
        {
            return;
        }

        if (viewport.Width <= 0f || viewport.Height <= 0f)
        {
            return;
        }

        if (Mode == GizmoMode.Translate)
        {
            if (!TryGetWorldAtScreen(renderer, camera, viewport, screenPoint, _gizmoDepth, out var world))
            {
                return;
            }

            var delta = world - _gizmoStartWorld;
            delta = ApplyGizmoConstraint(delta, AxisConstraint);
            if (SnapEnabled)
            {
                delta = TransformSnapping.Snap(delta, SnapStep);
            }

            var targetWorld = _gizmoStartTransform * Matrix4x4.CreateTranslation(delta);
            TrySetNodeWorldTransform(selectedNode, targetWorld);
            return;
        }

        if (Mode == GizmoMode.Scale)
        {
            if (!TryGetWorldAtScreen(renderer, camera, viewport, screenPoint, _gizmoDepth, out var world))
            {
                return;
            }

            var scaleVector = ComputeGizmoScale(world - _gizmoCenterWorld);
            var scale = Matrix4x4.CreateTranslation(-_gizmoCenterWorld)
                * Matrix4x4.CreateScale(scaleVector)
                * Matrix4x4.CreateTranslation(_gizmoCenterWorld);
            var targetScale = _gizmoStartTransform * scale;
            TrySetNodeWorldTransform(selectedNode, targetScale);
            return;
        }

        if (!renderer.TryProjectWorld(_gizmoCenterWorld, viewport, camera, out var centerScreen, out _))
        {
            centerScreen = _gizmoCenterScreen;
        }

        var angle = MathF.Atan2(screenPoint.Y - centerScreen.Y, screenPoint.X - centerScreen.X);
        var deltaAngle = angle - _gizmoStartRotation;
        if (SnapEnabled)
        {
            var snapRad = MathF.PI / 180f * RotateSnapDegrees;
            deltaAngle = TransformSnapping.SnapAngleRadians(deltaAngle, snapRad);
        }

        var axis = GetGizmoRotationAxis(AxisConstraint);
        var rotation = Matrix4x4.CreateTranslation(-_gizmoCenterWorld)
            * Matrix4x4.CreateFromAxisAngle(axis, deltaAngle)
            * Matrix4x4.CreateTranslation(_gizmoCenterWorld);
        var targetRotation = _gizmoStartTransform * rotation;
        TrySetNodeWorldTransform(selectedNode, targetRotation);
    }

    public void EndDrag()
    {
        IsDragging = false;
        _selectedInstance = null;
    }

    public bool TryGetSelectedCenter(MeshInstance selected, out Vector3 center)
    {
        if (selected.TryGetWorldBounds(out var min, out var max))
        {
            center = (min + max) * 0.5f;
            return true;
        }

        center = new Vector3(selected.Transform.M41, selected.Transform.M42, selected.Transform.M43);
        return true;
    }

    private static bool TrySetNodeWorldTransform(SceneNode node, Matrix4x4 world)
    {
        var parentWorld = node.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        if (!Matrix4x4.Invert(parentWorld, out var invParent))
        {
            invParent = Matrix4x4.Identity;
        }

        var local = world * invParent;
        if (!Matrix4x4.Decompose(local, out var scale, out var rotation, out var translation))
        {
            return false;
        }

        node.Transform.LocalScale = scale;
        node.Transform.LocalRotation = rotation;
        node.Transform.LocalPosition = translation;
        return true;
    }

    private static Vector3 ApplyGizmoConstraint(Vector3 delta, GizmoAxisConstraint constraint)
    {
        return constraint switch
        {
            GizmoAxisConstraint.X => new Vector3(delta.X, 0f, 0f),
            GizmoAxisConstraint.Y => new Vector3(0f, delta.Y, 0f),
            GizmoAxisConstraint.Z => new Vector3(0f, 0f, delta.Z),
            GizmoAxisConstraint.XY => new Vector3(delta.X, delta.Y, 0f),
            GizmoAxisConstraint.XZ => new Vector3(delta.X, 0f, delta.Z),
            GizmoAxisConstraint.YZ => new Vector3(0f, delta.Y, delta.Z),
            _ => delta
        };
    }

    private Vector3 ComputeGizmoScale(Vector3 currentVector)
    {
        var startVector = _gizmoStartScaleVector;
        var baseScale = GetGizmoReferenceScale();
        if (baseScale <= 1e-4f)
        {
            baseScale = 1f;
        }

        var delta = currentVector - startVector;
        var uniform = ComputeUniformScale(startVector, currentVector, baseScale);

        Vector3 scale = AxisConstraint switch
        {
            GizmoAxisConstraint.X => new Vector3(1f + delta.X / baseScale, 1f, 1f),
            GizmoAxisConstraint.Y => new Vector3(1f, 1f + delta.Y / baseScale, 1f),
            GizmoAxisConstraint.Z => new Vector3(1f, 1f, 1f + delta.Z / baseScale),
            GizmoAxisConstraint.XY => new Vector3(1f + delta.X / baseScale, 1f + delta.Y / baseScale, 1f),
            GizmoAxisConstraint.XZ => new Vector3(1f + delta.X / baseScale, 1f, 1f + delta.Z / baseScale),
            GizmoAxisConstraint.YZ => new Vector3(1f, 1f + delta.Y / baseScale, 1f + delta.Z / baseScale),
            _ => new Vector3(uniform, uniform, uniform)
        };

        scale.X = ClampScale(scale.X);
        scale.Y = ClampScale(scale.Y);
        scale.Z = ClampScale(scale.Z);

        if (SnapEnabled)
        {
            scale = new Vector3(
                SnapScale(scale.X, ScaleSnapStep),
                SnapScale(scale.Y, ScaleSnapStep),
                SnapScale(scale.Z, ScaleSnapStep));
        }

        return scale;
    }

    private float GetGizmoReferenceScale()
    {
        if (_selectedInstance is null)
        {
            return 1f;
        }

        var radius = _selectedInstance.Mesh.BoundingRadius;
        if (radius <= 1e-4f)
        {
            radius = 1f;
        }

        if (Matrix4x4.Decompose(_gizmoStartTransform, out var scale, out _, out _))
        {
            var maxScale = MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
            radius *= MathF.Max(0.1f, maxScale);
        }

        return radius;
    }

    private static Vector3 GetGizmoRotationAxis(GizmoAxisConstraint constraint)
    {
        return constraint switch
        {
            GizmoAxisConstraint.X => Vector3.UnitX,
            GizmoAxisConstraint.Z => Vector3.UnitZ,
            _ => Vector3.UnitY
        };
    }

    private static float ComputeUniformScale(Vector3 startVector, Vector3 currentVector, float baseScale)
    {
        var startLen = startVector.Length();
        var currentLen = currentVector.Length();
        if (startLen > 1e-4f && currentLen > 1e-4f)
        {
            return currentLen / startLen;
        }

        var deltaLen = (currentVector - startVector).Length();
        return 1f + deltaLen / baseScale;
    }

    private static float SnapScale(float value, float step)
    {
        return 1f + TransformSnapping.Snap(value - 1f, step);
    }

    private static float ClampScale(float value)
    {
        return Math.Clamp(value, 0.05f, 25f);
    }

    private static bool TryGetWorldAtScreen(Renderer3D renderer, Camera camera, SKRect viewport, SKPoint screen, float depth, out Vector3 world)
    {
        if (renderer.TryGetWorldAtScreen(screen, viewport, camera, out world))
        {
            return true;
        }

        var viewportSize = new Vector2(viewport.Width, viewport.Height);
        var local = new Vector2(screen.X - viewport.Left, screen.Y - viewport.Top);
        return Camera.TryUnproject(camera, local, viewportSize, depth, out world);
    }
}
