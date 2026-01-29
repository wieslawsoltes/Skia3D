using System.Numerics;
using Skia3D.Core;

namespace Skia3D.ShaderGraph;

public enum ShaderValueType
{
    Float,
    Vector2,
    Vector3,
    Vector4,
    Color,
    Texture
}

public readonly struct ShaderValue
{
    public ShaderValue(ShaderValueType type, Vector4 value, Texture2D? texture = null, TextureSampler? sampler = null)
    {
        Type = type;
        Value = value;
        Texture = texture;
        Sampler = sampler;
    }

    public ShaderValueType Type { get; }

    public Vector4 Value { get; }

    public Texture2D? Texture { get; }

    public TextureSampler? Sampler { get; }

    public static ShaderValue Float(float value) => new(ShaderValueType.Float, new Vector4(value, 0f, 0f, 0f));

    public static ShaderValue Vector2(Vector2 value) => new(ShaderValueType.Vector2, new Vector4(value, 0f, 0f));

    public static ShaderValue Vector3(Vector3 value) => new(ShaderValueType.Vector3, new Vector4(value, 0f));

    public static ShaderValue Vector4(Vector4 value) => new(ShaderValueType.Vector4, value);

    public static ShaderValue Color(Vector4 value, Texture2D? texture = null, TextureSampler? sampler = null)
    {
        return new ShaderValue(ShaderValueType.Color, value, texture, sampler);
    }

    public static ShaderValue TextureValue(Texture2D? texture, TextureSampler? sampler = null)
    {
        return new ShaderValue(ShaderValueType.Texture, default, texture, sampler);
    }

    public float AsFloat(float fallback = 0f)
    {
        return Type == ShaderValueType.Float ? Value.X : fallback;
    }

    public Vector4 AsVector4()
    {
        return Type switch
        {
            ShaderValueType.Float => new Vector4(Value.X, Value.X, Value.X, 1f),
            ShaderValueType.Vector2 => new Vector4(Value.X, Value.Y, 0f, 1f),
            ShaderValueType.Vector3 => new Vector4(Value.X, Value.Y, Value.Z, 1f),
            _ => Value
        };
    }

    public Texture2D? AsTexture()
    {
        return Texture;
    }
}
