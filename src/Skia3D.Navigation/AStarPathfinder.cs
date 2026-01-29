using System.Numerics;

namespace Skia3D.Navigation;

public static class AStarPathfinder
{
    public static List<Vector3> FindPath(NavGrid grid, Vector3 startWorld, Vector3 goalWorld)
    {
        var path = new List<Vector3>();
        if (!grid.TryWorldToCell(startWorld, out var startX, out var startY))
        {
            return path;
        }
        if (!grid.TryWorldToCell(goalWorld, out var goalX, out var goalY))
        {
            return path;
        }

        if (!grid.IsWalkable(startX, startY) || !grid.IsWalkable(goalX, goalY))
        {
            return path;
        }

        var open = new PriorityQueue<Node, float>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), float>();
        var startKey = (startX, startY);
        var goalKey = (goalX, goalY);

        gScore[startKey] = 0f;
        open.Enqueue(new Node(startX, startY), Heuristic(startX, startY, goalX, goalY));

        var closed = new HashSet<(int, int)>();

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            var currentKey = (current.X, current.Y);
            if (closed.Contains(currentKey))
            {
                continue;
            }

            if (currentKey == goalKey)
            {
                ReconstructPath(grid, cameFrom, currentKey, path);
                return path;
            }

            closed.Add(currentKey);

            foreach (var neighbor in GetNeighbors(grid, current.X, current.Y))
            {
                var neighborKey = (neighbor.X, neighbor.Y);
                if (closed.Contains(neighborKey))
                {
                    continue;
                }

                float tentative = gScore[currentKey] + neighbor.Cost;
                if (gScore.TryGetValue(neighborKey, out var existing) && tentative >= existing)
                {
                    continue;
                }

                cameFrom[neighborKey] = currentKey;
                gScore[neighborKey] = tentative;
                float f = tentative + Heuristic(neighbor.X, neighbor.Y, goalX, goalY);
                open.Enqueue(new Node(neighbor.X, neighbor.Y), f);
            }
        }

        return path;
    }

    private static float Heuristic(int x, int y, int gx, int gy)
    {
        float dx = gx - x;
        float dy = gy - y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static IEnumerable<Neighbor> GetNeighbors(NavGrid grid, int x, int y)
    {
        var dirs = Directions;
        for (int i = 0; i < dirs.Length; i++)
        {
            int nx = x + dirs[i].dx;
            int ny = y + dirs[i].dy;
            if (!grid.InBounds(nx, ny) || !grid.IsWalkable(nx, ny))
            {
                continue;
            }

            float cost = (dirs[i].dx == 0 || dirs[i].dy == 0) ? 1f : 1.4142f;
            yield return new Neighbor(nx, ny, cost);
        }
    }

    private static void ReconstructPath(NavGrid grid, Dictionary<(int, int), (int, int)> cameFrom, (int, int) current, List<Vector3> path)
    {
        var stack = new Stack<(int, int)>();
        stack.Push(current);

        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            stack.Push(current);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            path.Add(grid.CellToWorld(node.Item1, node.Item2));
        }
    }

    private readonly record struct Node(int X, int Y);

    private readonly record struct Neighbor(int X, int Y, float Cost);

    private static readonly (int dx, int dy)[] Directions =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1)
    };
}
