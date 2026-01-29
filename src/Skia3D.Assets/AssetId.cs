using System;

namespace Skia3D.Assets;

public readonly record struct AssetId(string Value)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public static AssetId New() => new(Guid.NewGuid().ToString("N"));

    public static bool TryParse(string? value, out AssetId id)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            id = default;
            return false;
        }

        id = new AssetId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}
