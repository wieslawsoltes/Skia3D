using System.Collections.Generic;
using System.Numerics;

namespace Skia3D.Modeling;

public sealed class TransformStack
{
    private readonly List<Matrix4x4> _stack = new();

    public int Count => _stack.Count;

    public Matrix4x4 Current { get; private set; } = Matrix4x4.Identity;

    public void Clear()
    {
        _stack.Clear();
        Current = Matrix4x4.Identity;
    }

    public void Push(Matrix4x4 transform)
    {
        _stack.Add(transform);
        Recompute();
    }

    public bool TryPop(out Matrix4x4 transform)
    {
        if (_stack.Count == 0)
        {
            transform = default;
            return false;
        }

        var index = _stack.Count - 1;
        transform = _stack[index];
        _stack.RemoveAt(index);
        Recompute();
        return true;
    }

    private void Recompute()
    {
        var combined = Matrix4x4.Identity;
        for (int i = 0; i < _stack.Count; i++)
        {
            combined = combined * _stack[i];
        }

        Current = combined;
    }
}
