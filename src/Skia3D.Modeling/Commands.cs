using System;
using System.Collections.Generic;
using System.Numerics;

namespace Skia3D.Modeling;

public interface IEditCommand
{
    string Name { get; }

    bool Execute();

    void Undo();
}

public interface IMeshEditCommand : IEditCommand
{
    EditableMesh Mesh { get; }
}

public sealed class CommandStack
{
    private readonly List<IEditCommand> _undo = new();
    private readonly List<IEditCommand> _redo = new();

    public int Capacity { get; set; } = 128;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool Do(IEditCommand command)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (!command.Execute())
        {
            return false;
        }

        _undo.Add(command);
        _redo.Clear();
        Trim();
        return true;
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var index = _undo.Count - 1;
        var command = _undo[index];
        _undo.RemoveAt(index);
        command.Undo();
        _redo.Add(command);
        return true;
    }

    public bool Undo(out IEditCommand? command)
    {
        if (_undo.Count == 0)
        {
            command = null;
            return false;
        }

        var index = _undo.Count - 1;
        command = _undo[index];
        _undo.RemoveAt(index);
        command.Undo();
        _redo.Add(command);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var index = _redo.Count - 1;
        var command = _redo[index];
        _redo.RemoveAt(index);
        if (!command.Execute())
        {
            return false;
        }

        _undo.Add(command);
        Trim();
        return true;
    }

    public bool Redo(out IEditCommand? command)
    {
        if (_redo.Count == 0)
        {
            command = null;
            return false;
        }

        var index = _redo.Count - 1;
        command = _redo[index];
        _redo.RemoveAt(index);
        if (!command.Execute())
        {
            return false;
        }

        _undo.Add(command);
        Trim();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private void Trim()
    {
        if (Capacity <= 0 || _undo.Count <= Capacity)
        {
            return;
        }

        var overflow = _undo.Count - Capacity;
        _undo.RemoveRange(0, overflow);
    }
}

public sealed class TransformVerticesCommand : IMeshEditCommand
{
    private readonly EditableMesh _mesh;
    private readonly Matrix4x4 _transform;
    private readonly int[] _indices;
    private Vector3[]? _before;
    private readonly string _name;

    public TransformVerticesCommand(EditableMesh mesh, Matrix4x4 transform, IReadOnlyCollection<int>? selection = null, string? name = null)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _transform = transform;
        _name = string.IsNullOrWhiteSpace(name) ? "Transform Vertices" : name;

        if (selection is null)
        {
            _indices = new int[_mesh.Positions.Count];
            for (int i = 0; i < _indices.Length; i++)
            {
                _indices[i] = i;
            }
        }
        else
        {
            _indices = new int[selection.Count];
            int i = 0;
            foreach (var index in selection)
            {
                _indices[i++] = index;
            }
        }
    }

    public string Name => _name;

    public EditableMesh Mesh => _mesh;

    public bool Execute()
    {
        if (_mesh.Positions.Count == 0)
        {
            return false;
        }

        if (_before is null)
        {
            _before = new Vector3[_indices.Length];
            for (int i = 0; i < _indices.Length; i++)
            {
                var index = _indices[i];
                if ((uint)index >= (uint)_mesh.Positions.Count)
                {
                    _before[i] = default;
                    continue;
                }

                _before[i] = _mesh.Positions[index];
            }
        }

        for (int i = 0; i < _indices.Length; i++)
        {
            var index = _indices[i];
            if ((uint)index >= (uint)_mesh.Positions.Count)
            {
                continue;
            }

            _mesh.Positions[index] = Vector3.Transform(_mesh.Positions[index], _transform);
        }
        _mesh.InvalidateNormals();

        return true;
    }

    public void Undo()
    {
        if (_before is null)
        {
            return;
        }

        for (int i = 0; i < _indices.Length; i++)
        {
            var index = _indices[i];
            if ((uint)index >= (uint)_mesh.Positions.Count)
            {
                continue;
            }

            _mesh.Positions[index] = _before[i];
        }
        _mesh.InvalidateNormals();
    }
}

public sealed class MeshSnapshotCommand : IMeshEditCommand
{
    private readonly EditableMesh _mesh;
    private readonly Func<EditableMesh, bool> _apply;
    private readonly string _name;
    private MeshSnapshot? _before;

    public MeshSnapshotCommand(EditableMesh mesh, Func<EditableMesh, bool> apply, string? name = null)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _name = string.IsNullOrWhiteSpace(name) ? "Mesh Operation" : name;
    }

    public string Name => _name;

    public EditableMesh Mesh => _mesh;

    public bool Execute()
    {
        _before ??= MeshSnapshot.Capture(_mesh);
        return _apply(_mesh);
    }

    public void Undo()
    {
        if (_before.HasValue)
        {
            _before.Value.Restore(_mesh);
        }
    }
}

public readonly record struct MeshSnapshot(
    Vector3[] Positions,
    int[] Indices,
    Vector2[] UVs,
    Vector3[]? Normals,
    Vector4[]? Colors,
    Vector4[]? Tangents,
    EdgeKey[] SeamEdges,
    int[] UvFaceGroups)
{
    public static MeshSnapshot Capture(EditableMesh mesh)
    {
        mesh.EnsureUvFaceGroups();
        return new MeshSnapshot(
            mesh.Positions.ToArray(),
            mesh.Indices.ToArray(),
            mesh.UVs.ToArray(),
            mesh.HasNormals ? mesh.Normals!.ToArray() : null,
            mesh.HasColors ? mesh.Colors!.ToArray() : null,
            mesh.HasTangents ? mesh.Tangents!.ToArray() : null,
            mesh.SeamEdges.ToArray(),
            mesh.UvFaceGroups.ToArray());
    }

    public void Restore(EditableMesh mesh)
    {
        mesh.Positions.Clear();
        mesh.Positions.AddRange(Positions);
        mesh.Indices.Clear();
        mesh.Indices.AddRange(Indices);
        mesh.UVs.Clear();
        mesh.UVs.AddRange(UVs);
        mesh.SetNormals(Normals);
        mesh.SetColors(Colors);
        mesh.SetTangents(Tangents);
        mesh.SeamEdges.Clear();
        mesh.SeamEdges.UnionWith(SeamEdges);
        mesh.UvFaceGroups.Clear();
        mesh.UvFaceGroups.AddRange(UvFaceGroups);
        mesh.EnsureUvFaceGroups();
    }
}
