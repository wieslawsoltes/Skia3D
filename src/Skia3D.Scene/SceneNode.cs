using System;
using System.Collections.Generic;
using System.Numerics;
using Skia3D.Core;

namespace Skia3D.Scene;

public sealed class SceneNode
{
    private readonly List<SceneNode> _children = new();
    private readonly List<SceneComponent> _components = new();

    public SceneNode(string name = "Node")
    {
        Name = name;
    }

    public string Name { get; set; }

    public SceneNode? Parent { get; private set; }

    public IReadOnlyList<SceneNode> Children => _children;

    public Transform Transform { get; } = new();

    public MeshRenderer? MeshRenderer
    {
        get => GetComponent<MeshRenderer>();
        set => ReplaceComponent(value);
    }

    public MeshInstance? MeshInstance => MeshRenderer?.ActiveInstance;

    public LightComponent? Light
    {
        get => GetComponent<LightComponent>();
        set => ReplaceComponent(value);
    }

    public CameraComponent? Camera
    {
        get => GetComponent<CameraComponent>();
        set => ReplaceComponent(value);
    }

    public List<TransformConstraint> Constraints { get; } = new();

    public IReadOnlyList<SceneComponent> Components => _components;

    public event Action<SceneNode, SceneComponent>? ComponentAdded;

    public event Action<SceneNode, SceneComponent>? ComponentRemoved;

    public bool HasWorldBounds { get; private set; }

    public Vector3 WorldBoundsMin { get; private set; }

    public Vector3 WorldBoundsMax { get; private set; }

    public bool HasWorldSphere { get; private set; }

    public Vector3 WorldCenter { get; private set; }

    public float WorldRadius { get; private set; }

    public void AddChild(SceneNode child)
    {
        if (child == this || child.Parent == this)
        {
            return;
        }

        child.Parent?._children.Remove(child);
        child.Parent = this;
        _children.Add(child);
        child.Transform.MarkDirty();
    }

    public bool RemoveChild(SceneNode child)
    {
        if (child is null)
        {
            return false;
        }

        if (_children.Remove(child))
        {
            child.Parent = null;
            child.Transform.MarkDirty();
            return true;
        }

        return false;
    }

    public T AddComponent<T>(T component) where T : SceneComponent
    {
        if (component is null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        if (component.Node == this)
        {
            return component;
        }

        if (component.Node != null)
        {
            component.Node.RemoveComponent(component);
        }

        _components.Add(component);
        component.Attach(this);
        ComponentAdded?.Invoke(this, component);
        return component;
    }

    public bool RemoveComponent(SceneComponent component)
    {
        if (component is null)
        {
            return false;
        }

        if (!_components.Remove(component))
        {
            return false;
        }

        component.Detach(this);
        ComponentRemoved?.Invoke(this, component);
        return true;
    }

    public T? GetComponent<T>() where T : SceneComponent
    {
        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    public IReadOnlyList<T> GetComponents<T>() where T : SceneComponent
    {
        var list = new List<T>();
        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T typed)
            {
                list.Add(typed);
            }
        }

        return list;
    }

    private void ReplaceComponent<T>(T? component) where T : SceneComponent
    {
        var existing = GetComponent<T>();
        if (existing == component)
        {
            return;
        }

        if (existing != null)
        {
            RemoveComponent(existing);
        }

        if (component != null)
        {
            AddComponent(component);
        }
    }

    internal void UpdateWorldRecursive(bool parentDirty, Matrix4x4 parentWorld)
    {
        var dirty = parentDirty || Transform.WorldDirty;
        if (Constraints.Count > 0)
        {
            ApplyConstraints(parentWorld);
            dirty = true;
        }
        Transform.UpdateWorld(parentWorld, parentDirty);
        UpdateWorldBounds();

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].UpdateWorldRecursive(dirty, Transform.WorldMatrix);
        }
    }

    internal void UpdateWorldAndBounds(Matrix4x4 parentWorld, bool parentDirty)
    {
        UpdateWorldAndBounds(parentWorld, parentDirty, applyConstraints: true);
    }

    internal void UpdateWorldAndBounds(Matrix4x4 parentWorld, bool parentDirty, bool applyConstraints)
    {
        if (applyConstraints && Constraints.Count > 0)
        {
            ApplyConstraints(parentWorld);
            parentDirty = true;
        }

        Transform.UpdateWorld(parentWorld, parentDirty);
        UpdateWorldBounds();
    }

    private void ApplyConstraints(Matrix4x4 parentWorld)
    {
        for (int i = 0; i < Constraints.Count; i++)
        {
            Constraints[i].Apply(this, parentWorld);
        }
    }

    private void UpdateWorldBounds()
    {
        var renderer = MeshRenderer;
        var mesh = renderer?.Skin?.SkinnedMesh ?? renderer?.Mesh;
        if (mesh is null || !mesh.HasBounds)
        {
            HasWorldBounds = false;
            HasWorldSphere = false;
            WorldRadius = 0f;
            return;
        }

        var world = Transform.WorldMatrix;
        WorldCenter = Vector3.Transform(Vector3.Zero, world);
        WorldRadius = mesh.BoundingRadius * ExtractMaxScale(world);
        HasWorldSphere = WorldRadius > 0f;

        var localMin = mesh.BoundsMin;
        var localMax = mesh.BoundsMax;
        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(localMin.X, localMin.Y, localMin.Z);
        corners[1] = new Vector3(localMax.X, localMin.Y, localMin.Z);
        corners[2] = new Vector3(localMin.X, localMax.Y, localMin.Z);
        corners[3] = new Vector3(localMax.X, localMax.Y, localMin.Z);
        corners[4] = new Vector3(localMin.X, localMin.Y, localMax.Z);
        corners[5] = new Vector3(localMax.X, localMin.Y, localMax.Z);
        corners[6] = new Vector3(localMin.X, localMax.Y, localMax.Z);
        corners[7] = new Vector3(localMax.X, localMax.Y, localMax.Z);

        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            var worldCorner = Vector3.Transform(corners[i], world);
            min = Vector3.Min(min, worldCorner);
            max = Vector3.Max(max, worldCorner);
        }

        WorldBoundsMin = min;
        WorldBoundsMax = max;
        HasWorldBounds = true;
    }

    private static float ExtractMaxScale(Matrix4x4 m)
    {
        var sx = new Vector3(m.M11, m.M12, m.M13).Length();
        var sy = new Vector3(m.M21, m.M22, m.M23).Length();
        var sz = new Vector3(m.M31, m.M32, m.M33).Length();
        return MathF.Max(sx, MathF.Max(sy, sz));
    }
}
