using System.Numerics;
using Skia3D.Runtime;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Navigation;

public sealed class NavigationSystem : SystemBase
{
    private readonly SceneGraph _scene;

    public NavigationSystem(SceneGraph scene)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    public override void Update(Engine engine, Time time)
    {
        var grids = _scene.CollectComponents<NavGridComponent>();
        if (grids.Count == 0)
        {
            return;
        }

        var grid = grids[0].Grid;
        var agents = _scene.CollectComponents<NavAgentComponent>();

        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            if (agent.Node == null || !agent.HasDestination)
            {
                continue;
            }

            if (!agent.HasPath)
            {
                var path = AStarPathfinder.FindPath(grid, agent.Node.Transform.WorldMatrix.Translation, agent.Destination!.Value);
                agent.SetPath(path);
            }

            if (!agent.HasPath)
            {
                continue;
            }

            AdvanceAlongPath(agent, time.DeltaSeconds);
        }
    }

    private static void AdvanceAlongPath(NavAgentComponent agent, float deltaSeconds)
    {
        if (agent.Node == null || agent.CurrentPathIndex >= agent.Path.Count)
        {
            return;
        }

        var currentWorld = agent.Node.Transform.WorldMatrix.Translation;
        var target = agent.Path[agent.CurrentPathIndex];
        var toTarget = target - currentWorld;
        float distance = toTarget.Length();

        if (distance <= agent.StoppingDistance)
        {
            agent.CurrentPathIndex++;
            if (agent.CurrentPathIndex >= agent.Path.Count)
            {
                agent.ClearDestination();
            }
            return;
        }

        var direction = distance > 1e-6f ? toTarget / distance : Vector3.Zero;
        float step = agent.Speed * MathF.Max(0f, deltaSeconds);
        var newPos = currentWorld + direction * MathF.Min(step, distance);

        var parentWorld = agent.Node.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        if (Matrix4x4.Invert(parentWorld, out var invParent))
        {
            var local = Vector3.Transform(newPos, invParent);
            agent.Node.Transform.LocalPosition = local;
        }
        else
        {
            agent.Node.Transform.LocalPosition = newPos;
        }
    }
}
