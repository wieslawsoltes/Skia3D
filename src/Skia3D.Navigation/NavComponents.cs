using System.Numerics;
using Skia3D.Scene;

namespace Skia3D.Navigation;

public sealed class NavGridComponent : SceneComponent
{
    public NavGridComponent(NavGrid grid)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    public NavGrid Grid { get; set; }
}

public sealed class NavAgentComponent : SceneComponent
{
    private Vector3? _destination;

    public float Speed { get; set; } = 2f;

    public float StoppingDistance { get; set; } = 0.1f;

    public IReadOnlyList<Vector3> Path => _path;

    public bool HasPath => _path.Count > 0;

    public bool HasDestination => _destination.HasValue;

    public Vector3? Destination => _destination;

    public int CurrentPathIndex { get; internal set; }

    private readonly List<Vector3> _path = new();

    public void SetDestination(Vector3 destination)
    {
        _destination = destination;
        _path.Clear();
        CurrentPathIndex = 0;
    }

    public void ClearDestination()
    {
        _destination = null;
        _path.Clear();
        CurrentPathIndex = 0;
    }

    internal void SetPath(IEnumerable<Vector3> points)
    {
        _path.Clear();
        _path.AddRange(points);
        CurrentPathIndex = 0;
    }
}
