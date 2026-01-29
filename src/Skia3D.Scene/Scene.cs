using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Skia3D.Core;

namespace Skia3D.Scene;

public readonly record struct SceneCullingStats(
    int TotalNodes,
    int MeshNodes,
    int VisibleNodes,
    int CulledNodes,
    bool CullingEnabled);

public sealed class Scene
{
    private readonly List<SceneNode> _roots = new();
    private readonly List<MeshInstance> _instances = new();

    public IReadOnlyList<SceneNode> Roots => _roots;

    public SceneCullingStats LastCullingStats { get; private set; }

    public int MaxWorkerCount { get; set; }

    public void AddRoot(SceneNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.Parent != null)
        {
            node.Parent.RemoveChild(node);
        }

        if (!_roots.Contains(node))
        {
            _roots.Add(node);
        }
    }

    public bool RemoveRoot(SceneNode node) => _roots.Remove(node);

    public void UpdateWorld(bool parallel = false)
    {
        if (!parallel)
        {
            for (int i = 0; i < _roots.Count; i++)
            {
                _roots[i].UpdateWorldRecursive(parentDirty: true, parentWorld: Matrix4x4.Identity);
            }
            return;
        }

        ApplyConstraintsSequential();
        UpdateWorldParallel();
    }

    public IReadOnlyList<MeshInstance> CollectMeshInstances(Camera? camera = null, bool cull = false, bool parallel = false)
    {
        if (!parallel)
        {
            _instances.Clear();
            Frustum frustum = default;
            bool useCulling = cull && camera != null;
            if (useCulling)
            {
                var viewProj = camera!.GetViewProjectionMatrix();
                frustum = Frustum.FromViewProjection(viewProj);
            }

            var counter = new CullingCounter();
            foreach (var root in _roots)
            {
            CollectMeshInstancesRecursive(root, camera, useCulling, frustum, ref counter);
            }

            LastCullingStats = new SceneCullingStats(counter.TotalNodes, counter.MeshNodes, counter.VisibleNodes, counter.CulledNodes, useCulling);
            return _instances;
        }

        return CollectMeshInstancesParallel(camera, cull);
    }

    public IReadOnlyList<Light> CollectLights()
    {
        var lights = new List<Light>();
        foreach (var root in _roots)
        {
            CollectLightsRecursive(root, lights);
        }

        return lights;
    }

    public IReadOnlyList<T> CollectComponents<T>(bool includeDisabled = false) where T : SceneComponent
    {
        var list = new List<T>();
        foreach (var node in EnumerateNodes())
        {
            var components = node.Components;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T typed && (includeDisabled || typed.Enabled))
                {
                    list.Add(typed);
                }
            }
        }

        return list;
    }

    public IEnumerable<SceneNode> EnumerateNodes()
    {
        if (_roots.Count == 0)
        {
            yield break;
        }

        var stack = new Stack<SceneNode>(_roots.Count);
        for (int i = _roots.Count - 1; i >= 0; i--)
        {
            stack.Push(_roots[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;

            var children = node.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }

    private void UpdateWorldParallel()
    {
        if (_roots.Count == 0)
        {
            return;
        }

        var levels = BuildLevels();
        var options = CreateParallelOptions();
        for (int depth = 0; depth < levels.Count; depth++)
        {
            var level = levels[depth];
            Parallel.For(0, level.Count, options, i =>
            {
                var node = level[i];
                var parentWorld = node.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
                node.UpdateWorldAndBounds(parentWorld, parentDirty: true, applyConstraints: false);
            });
        }
    }

    private void ApplyConstraintsSequential()
    {
        if (_roots.Count == 0)
        {
            return;
        }

        var stack = new Stack<SceneNode>(_roots.Count);
        for (int i = _roots.Count - 1; i >= 0; i--)
        {
            stack.Push(_roots[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Constraints.Count > 0)
            {
                var parentWorld = node.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
                node.UpdateWorldAndBounds(parentWorld, parentDirty: true, applyConstraints: true);
            }

            var children = node.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }

    private IReadOnlyList<MeshInstance> CollectMeshInstancesParallel(Camera? camera, bool cull)
    {
        _instances.Clear();
        Frustum frustum = default;
        bool useCulling = cull && camera != null;
        if (useCulling)
        {
            var viewProj = camera!.GetViewProjectionMatrix();
            frustum = Frustum.FromViewProjection(viewProj);
        }

        var nodes = FlattenNodes();
        if (nodes.Count == 0)
        {
            LastCullingStats = new SceneCullingStats(0, 0, 0, 0, useCulling);
            return _instances;
        }

        var results = new MeshInstance?[nodes.Count];
        var options = CreateParallelOptions();
        int meshNodes = 0;
        int visibleNodes = 0;
        int culledNodes = 0;
        Parallel.For(0, nodes.Count, options, () => new CullingCounter(), (i, _, local) =>
        {
            var node = nodes[i];
            var renderer = node.MeshRenderer;
            if (renderer is { IsVisible: true, Enabled: true })
            {
                var instance = renderer.GetInstance(camera, node.Transform.WorldMatrix);

                if (!useCulling || IsVisible(node, frustum))
                {
                    results[i] = instance;
                    local.VisibleNodes++;
                }
                else
                {
                    local.CulledNodes++;
                }

                local.MeshNodes++;
            }
            return local;
        }, local =>
        {
            if (local.MeshNodes == 0)
            {
                return;
            }

            System.Threading.Interlocked.Add(ref meshNodes, local.MeshNodes);
            System.Threading.Interlocked.Add(ref visibleNodes, local.VisibleNodes);
            System.Threading.Interlocked.Add(ref culledNodes, local.CulledNodes);
        });

        for (int i = 0; i < results.Length; i++)
        {
            var instance = results[i];
            if (instance != null)
            {
                _instances.Add(instance);
            }
        }

        LastCullingStats = new SceneCullingStats(nodes.Count, meshNodes, visibleNodes, culledNodes, useCulling);
        return _instances;
    }

    private List<List<SceneNode>> BuildLevels()
    {
        var levels = new List<List<SceneNode>>();
        var queue = new Queue<(SceneNode node, int depth)>();
        for (int i = 0; i < _roots.Count; i++)
        {
            queue.Enqueue((_roots[i], 0));
        }

        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();
            while (levels.Count <= depth)
            {
                levels.Add(new List<SceneNode>());
            }

            levels[depth].Add(node);
            var children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                queue.Enqueue((children[i], depth + 1));
            }
        }

        return levels;
    }

    private List<SceneNode> FlattenNodes()
    {
        var nodes = new List<SceneNode>();
        for (int i = 0; i < _roots.Count; i++)
        {
            FlattenRecursive(_roots[i], nodes);
        }

        return nodes;
    }

    private static void FlattenRecursive(SceneNode node, List<SceneNode> nodes)
    {
        nodes.Add(node);
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            FlattenRecursive(children[i], nodes);
        }
    }

    private void CollectMeshInstancesRecursive(SceneNode node, Camera? camera, bool cull, Frustum frustum, ref CullingCounter counter)
    {
        counter.TotalNodes++;
        var renderer = node.MeshRenderer;
        if (renderer is { IsVisible: true, Enabled: true })
        {
            var instance = renderer.GetInstance(camera, node.Transform.WorldMatrix);

            if (!cull || IsVisible(node, frustum))
            {
                _instances.Add(instance);
                counter.VisibleNodes++;
            }
            else
            {
                counter.CulledNodes++;
            }

            counter.MeshNodes++;
        }

        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            CollectMeshInstancesRecursive(children[i], camera, cull, frustum, ref counter);
        }
    }

    private void CollectLightsRecursive(SceneNode node, List<Light> lights)
    {
        if (node.Light is { IsEnabled: true } light)
        {
            lights.Add(light.Light);
        }

        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            CollectLightsRecursive(children[i], lights);
        }
    }

    private static bool IsVisible(SceneNode node, Frustum frustum)
    {
        var renderer = node.MeshRenderer;
        var mesh = renderer?.Skin?.SkinnedMesh ?? renderer?.Mesh;
        if (mesh is null)
        {
            return false;
        }

        if (node.HasWorldSphere)
        {
            return frustum.IntersectsSphere(node.WorldCenter, node.WorldRadius);
        }

        var center = Vector3.Transform(Vector3.Zero, node.Transform.WorldMatrix);
        var radius = mesh.BoundingRadius * ExtractMaxScale(node.Transform.WorldMatrix);
        return frustum.IntersectsSphere(center, radius);
    }

    private static float ExtractMaxScale(Matrix4x4 m)
    {
        var sx = new Vector3(m.M11, m.M12, m.M13).Length();
        var sy = new Vector3(m.M21, m.M22, m.M23).Length();
        var sz = new Vector3(m.M31, m.M32, m.M33).Length();
        return MathF.Max(sx, MathF.Max(sy, sz));
    }

    private ParallelOptions CreateParallelOptions()
    {
        int desired = MaxWorkerCount <= 0 ? Environment.ProcessorCount : MaxWorkerCount;
        int workerCount = Math.Clamp(desired, 1, Environment.ProcessorCount);
        return new ParallelOptions { MaxDegreeOfParallelism = workerCount };
    }

    private struct CullingCounter
    {
        public int TotalNodes;
        public int MeshNodes;
        public int VisibleNodes;
        public int CulledNodes;
    }
}
