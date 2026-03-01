using System.Numerics;
using Skia3D.Core;

namespace Skia3D.ShaderGraph;

public sealed class FloatNode : ShaderNode
{
    public const string OutputName = "Value";

    public FloatNode(float value = 0f, Guid? id = null) : base("Float", id)
    {
        Value = value;
        Outputs.Add(new ShaderPort(OutputName, ShaderValueType.Float, ShaderValue.Float(Value)));
    }

    public float Value { get; set; }

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        return ShaderValue.Float(Value);
    }
}

public sealed class ColorNode : ShaderNode
{
    public const string OutputName = "Color";

    public ColorNode(Vector4 color, Guid? id = null) : base("Color", id)
    {
        Color = color;
        Outputs.Add(new ShaderPort(OutputName, ShaderValueType.Color, ShaderValue.Color(Color)));
    }

    public Vector4 Color { get; set; }

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        return ShaderValue.Color(Color);
    }
}

public sealed class AddNode : ShaderNode
{
    public const string InputA = "A";
    public const string InputB = "B";
    public const string OutputName = "Out";

    public AddNode(Guid? id = null) : base("Add", id)
    {
        Inputs.Add(new ShaderPort(InputA, ShaderValueType.Vector4, ShaderValue.Vector4(Vector4.Zero)));
        Inputs.Add(new ShaderPort(InputB, ShaderValueType.Vector4, ShaderValue.Vector4(Vector4.Zero)));
        Outputs.Add(new ShaderPort(OutputName, ShaderValueType.Vector4, ShaderValue.Vector4(Vector4.Zero)));
    }

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        var a = evaluator.GetInputValue(this, InputA).AsVector4();
        var b = evaluator.GetInputValue(this, InputB).AsVector4();
        return ShaderValue.Vector4(a + b);
    }
}

public sealed class MultiplyNode : ShaderNode
{
    public const string InputA = "A";
    public const string InputB = "B";
    public const string OutputName = "Out";

    public MultiplyNode(Guid? id = null) : base("Multiply", id)
    {
        Inputs.Add(new ShaderPort(InputA, ShaderValueType.Vector4, ShaderValue.Vector4(Vector4.One)));
        Inputs.Add(new ShaderPort(InputB, ShaderValueType.Vector4, ShaderValue.Vector4(Vector4.One)));
        Outputs.Add(new ShaderPort(OutputName, ShaderValueType.Vector4, ShaderValue.Vector4(Vector4.One)));
    }

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        var a = evaluator.GetInputValue(this, InputA).AsVector4();
        var b = evaluator.GetInputValue(this, InputB).AsVector4();
        return ShaderValue.Vector4(a * b);
    }
}

public sealed class MaterialOutputNode : ShaderNode
{
    public const string BaseColorInput = "BaseColor";
    public const string BaseColorTextureInput = "BaseColorTexture";
    public const string BaseColorTextureStrengthInput = "BaseColorTextureStrength";
    public const string MetallicInput = "Metallic";
    public const string RoughnessInput = "Roughness";
    public const string MetallicRoughnessTextureInput = "MetallicRoughnessTexture";
    public const string MetallicRoughnessStrengthInput = "MetallicRoughnessStrength";
    public const string NormalTextureInput = "NormalTexture";
    public const string NormalStrengthInput = "NormalStrength";
    public const string EmissiveInput = "Emissive";
    public const string EmissiveTextureInput = "EmissiveTexture";
    public const string EmissiveStrengthInput = "EmissiveStrength";
    public const string OcclusionTextureInput = "OcclusionTexture";
    public const string OcclusionStrengthInput = "OcclusionStrength";

    public MaterialOutputNode(Guid? id = null) : base("Material Output", id)
    {
        Inputs.Add(new ShaderPort(BaseColorInput, ShaderValueType.Color, ShaderValue.Color(new Vector4(0.8f, 0.8f, 0.8f, 1f))));
        Inputs.Add(new ShaderPort(BaseColorTextureInput, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Inputs.Add(new ShaderPort(BaseColorTextureStrengthInput, ShaderValueType.Float, ShaderValue.Float(1f)));
        Inputs.Add(new ShaderPort(MetallicInput, ShaderValueType.Float, ShaderValue.Float(0f)));
        Inputs.Add(new ShaderPort(RoughnessInput, ShaderValueType.Float, ShaderValue.Float(0.6f)));
        Inputs.Add(new ShaderPort(MetallicRoughnessTextureInput, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Inputs.Add(new ShaderPort(MetallicRoughnessStrengthInput, ShaderValueType.Float, ShaderValue.Float(1f)));
        Inputs.Add(new ShaderPort(NormalTextureInput, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Inputs.Add(new ShaderPort(NormalStrengthInput, ShaderValueType.Float, ShaderValue.Float(1f)));
        Inputs.Add(new ShaderPort(EmissiveInput, ShaderValueType.Color, ShaderValue.Color(new Vector4(0f, 0f, 0f, 1f))));
        Inputs.Add(new ShaderPort(EmissiveTextureInput, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Inputs.Add(new ShaderPort(EmissiveStrengthInput, ShaderValueType.Float, ShaderValue.Float(1f)));
        Inputs.Add(new ShaderPort(OcclusionTextureInput, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Inputs.Add(new ShaderPort(OcclusionStrengthInput, ShaderValueType.Float, ShaderValue.Float(1f)));
    }

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        return ShaderValue.Vector4(Vector4.Zero);
    }
}

public sealed class Texture2DNode : ShaderNode
{
    public const string OutputName = "Texture";

    public Texture2DNode(Texture2D? texture = null, Guid? id = null) : base("Texture2D", id)
    {
        Texture = texture;
        Outputs.Add(new ShaderPort(OutputName, ShaderValueType.Texture, ShaderValue.TextureValue(Texture)));
    }

    public Texture2D? Texture { get; set; }

    public string? TextureId { get; set; }

    public TextureSampler Sampler { get; } = new();

    public string Label { get; set; } = "Texture";

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        return ShaderValue.TextureValue(Texture, Sampler);
    }
}

public sealed class TextureSampleNode : ShaderNode
{
    public const string TextureInput = "Texture";
    public const string UvInput = "UV";
    public const string OutputName = "Color";

    public TextureSampleNode(Guid? id = null) : base("Texture Sample", id)
    {
        Inputs.Add(new ShaderPort(TextureInput, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Inputs.Add(new ShaderPort(UvInput, ShaderValueType.Vector2, ShaderValue.Vector2(Vector2.Zero)));
        Outputs.Add(new ShaderPort(OutputName, ShaderValueType.Color, ShaderValue.Color(Vector4.One)));
    }

    public TextureSampler Sampler { get; } = new();

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        var textureValue = evaluator.GetInputValue(this, TextureInput);
        var texture = textureValue.AsTexture();
        var uvValue = evaluator.GetInputValue(this, UvInput).AsVector4();
        var uv = new Vector2(uvValue.X, uvValue.Y);
        if (texture == null)
        {
            return ShaderValue.Color(Vector4.One);
        }

        var color = texture.Sample(uv, textureValue.Sampler ?? Sampler);
        var vec = new Vector4(color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f);
        return ShaderValue.Color(vec, texture, textureValue.Sampler ?? Sampler);
    }
}

public sealed class NormalMapNode : ShaderNode
{
    public const string TextureInput = "Texture";
    public const string StrengthInput = "Strength";
    public const string OutputTexture = "Texture";
    public const string OutputStrength = "OutStrength";

    public NormalMapNode(Guid? id = null) : base("Normal Map", id)
    {
        Inputs.Add(new ShaderPort(TextureInput, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Inputs.Add(new ShaderPort(StrengthInput, ShaderValueType.Float, ShaderValue.Float(1f)));
        Outputs.Add(new ShaderPort(OutputTexture, ShaderValueType.Texture, ShaderValue.TextureValue(null)));
        Outputs.Add(new ShaderPort(OutputStrength, ShaderValueType.Float, ShaderValue.Float(1f)));
    }

    public override ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator)
    {
        if (outputName == OutputStrength)
        {
            return ShaderValue.Float(evaluator.GetInputValue(this, StrengthInput).AsFloat(1f));
        }

        var textureValue = evaluator.GetInputValue(this, TextureInput);
        return ShaderValue.TextureValue(textureValue.AsTexture(), textureValue.Sampler);
    }
}
