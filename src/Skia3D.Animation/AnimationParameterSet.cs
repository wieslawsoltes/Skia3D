using System;
using System.Collections.Generic;

namespace Skia3D.Animation;

public sealed class AnimationParameterSet
{
    private readonly Dictionary<string, float> _floats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _bools = new(StringComparer.Ordinal);
    private readonly HashSet<string> _triggers = new(StringComparer.Ordinal);

    public void SetFloat(string name, float value) => _floats[name] = value;

    public float GetFloat(string name, float defaultValue = 0f)
        => _floats.TryGetValue(name, out var value) ? value : defaultValue;

    public void SetInt(string name, int value) => _ints[name] = value;

    public int GetInt(string name, int defaultValue = 0)
        => _ints.TryGetValue(name, out var value) ? value : defaultValue;

    public void SetBool(string name, bool value) => _bools[name] = value;

    public bool GetBool(string name, bool defaultValue = false)
        => _bools.TryGetValue(name, out var value) ? value : defaultValue;

    public void SetTrigger(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _triggers.Add(name);
        }
    }

    public bool IsTriggerSet(string name) => _triggers.Contains(name);

    public bool ConsumeTrigger(string name) => _triggers.Remove(name);

    public void ClearTrigger(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _triggers.Remove(name);
        }
    }
}

