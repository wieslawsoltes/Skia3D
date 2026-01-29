using System;
using System.Collections.Generic;

namespace Skia3D.Modeling;

public enum SelectionKind
{
    Object,
    Vertex,
    Edge,
    Face
}

public readonly struct EdgeKey : IEquatable<EdgeKey>
{
    public EdgeKey(int a, int b)
    {
        if (a <= b)
        {
            A = a;
            B = b;
        }
        else
        {
            A = b;
            B = a;
        }
    }

    public int A { get; }

    public int B { get; }

    public bool Equals(EdgeKey other) => A == other.A && B == other.B;

    public override bool Equals(object? obj) => obj is EdgeKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(A, B);
}

public sealed class SelectionSet<T>
    where T : notnull
{
    public SelectionSet(SelectionKind kind)
    {
        Kind = kind;
    }

    public SelectionKind Kind { get; }

    public HashSet<T> Items { get; } = new();

    public int Count => Items.Count;

    public bool IsEmpty => Items.Count == 0;

    public bool Add(T item) => Items.Add(item);

    public bool Remove(T item) => Items.Remove(item);

    public void Clear() => Items.Clear();

    public bool Contains(T item) => Items.Contains(item);

    public bool Toggle(T item)
    {
        if (Items.Contains(item))
        {
            Items.Remove(item);
            return false;
        }

        Items.Add(item);
        return true;
    }

    public void ReplaceWith(IEnumerable<T> items)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }
}

public static class SelectionSets
{
    public static SelectionSet<int> Objects() => new(SelectionKind.Object);

    public static SelectionSet<int> Vertices() => new(SelectionKind.Vertex);

    public static SelectionSet<EdgeKey> Edges() => new(SelectionKind.Edge);

    public static SelectionSet<int> Faces() => new(SelectionKind.Face);
}
