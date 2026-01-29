using System.Numerics;
using Skia3D.Geometry;

namespace Skia3D.Navigation;

public sealed class NavGrid
{
    private readonly bool[] _walkable;

    public NavGrid(int width, int height, float cellSize, Vector3 origin)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        CellSize = MathF.Max(0.01f, cellSize);
        Origin = origin;
        _walkable = new bool[Width * Height];
        Array.Fill(_walkable, true);
    }

    public int Width { get; }

    public int Height { get; }

    public float CellSize { get; }

    public Vector3 Origin { get; }

    public bool IsWalkable(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return false;
        }

        return _walkable[y * Width + x];
    }

    public void SetWalkable(int x, int y, bool walkable)
    {
        if (!InBounds(x, y))
        {
            return;
        }

        _walkable[y * Width + x] = walkable;
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public bool TryWorldToCell(Vector3 world, out int x, out int y)
    {
        var local = world - Origin;
        x = (int)MathF.Floor(local.X / CellSize);
        y = (int)MathF.Floor(local.Z / CellSize);
        return InBounds(x, y);
    }

    public Vector3 CellToWorld(int x, int y, float height = 0f)
    {
        return Origin + new Vector3((x + 0.5f) * CellSize, height, (y + 0.5f) * CellSize);
    }

    public Aabb GetBounds(float height = 0f)
    {
        var size = new Vector3(Width * CellSize, height, Height * CellSize);
        return new Aabb(Origin, Origin + size);
    }
}

public static class NavGridBuilder
{
    public static NavGrid BuildFromBounds(Aabb bounds, float cellSize)
    {
        var size = bounds.Size;
        int width = Math.Max(1, (int)MathF.Ceiling(size.X / cellSize));
        int height = Math.Max(1, (int)MathF.Ceiling(size.Z / cellSize));
        return new NavGrid(width, height, cellSize, bounds.Min);
    }
}
