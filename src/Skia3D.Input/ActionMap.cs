namespace Skia3D.Input;

public sealed class ActionMap
{
    private readonly Dictionary<string, InputBinding> _bindings = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, InputBinding> Bindings => _bindings;

    public void Bind(string action, InputBinding binding)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action name cannot be null or whitespace.", nameof(action));
        }

        _bindings[action] = binding;
    }

    public bool TryGetBinding(string action, out InputBinding binding)
    {
        return _bindings.TryGetValue(action, out binding);
    }

    public bool IsTriggered(string action, InputEvent inputEvent)
    {
        return _bindings.TryGetValue(action, out var binding) && binding.Matches(inputEvent);
    }
}
