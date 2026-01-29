using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Skia3D.Core;

namespace Skia3D.ShaderGraph;

public readonly record struct ShaderGraphResult(
    Vector4 BaseColor,
    float Metallic,
    float Roughness,
    Vector4 Emissive,
    Texture2D? BaseColorTexture,
    TextureSampler? BaseColorSampler,
    Texture2D? NormalTexture,
    TextureSampler? NormalSampler,
    float NormalStrength);

public sealed class ShaderPort
{
    public ShaderPort(string name, ShaderValueType type, ShaderValue defaultValue)
    {
        Name = name;
        Type = type;
        DefaultValue = defaultValue;
    }

    public string Name { get; }

    public ShaderValueType Type { get; }

    public ShaderValue DefaultValue { get; set; }
}

public readonly record struct ShaderLink(Guid FromNode, string FromPort, Guid ToNode, string ToPort);

public abstract class ShaderNode
{
    protected ShaderNode(string title, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Title = title;
    }

    public Guid Id { get; }

    public string Title { get; protected set; }

    public Vector2 Position { get; set; }

    public List<ShaderPort> Inputs { get; } = new();

    public List<ShaderPort> Outputs { get; } = new();

    public ShaderPort? FindInput(string name)
    {
        return Inputs.FirstOrDefault(port => port.Name == name);
    }

    public ShaderPort? FindOutput(string name)
    {
        return Outputs.FirstOrDefault(port => port.Name == name);
    }

    public bool TrySetInputDefault(string name, ShaderValue value)
    {
        var port = FindInput(name);
        if (port == null)
        {
            return false;
        }

        port.DefaultValue = value;
        return true;
    }

    public abstract ShaderValue EvaluateOutput(string outputName, ShaderGraphEvaluator evaluator);
}

public sealed class ShaderGraph
{
    private readonly List<ShaderNode> _nodes = new();
    private readonly List<ShaderLink> _links = new();

    public ShaderGraph(Guid? outputNodeId = null)
    {
        OutputNode = new MaterialOutputNode(outputNodeId);
        _nodes.Add(OutputNode);
    }

    public MaterialOutputNode OutputNode { get; }

    public IReadOnlyList<ShaderNode> Nodes => _nodes;

    public IReadOnlyList<ShaderLink> Links => _links;

    public void AddNode(ShaderNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (_nodes.Contains(node))
        {
            return;
        }

        _nodes.Add(node);
    }

    public bool RemoveNode(Guid id)
    {
        var node = FindNode(id);
        if (node == null || node == OutputNode)
        {
            return false;
        }

        _nodes.Remove(node);
        _links.RemoveAll(link => link.FromNode == id || link.ToNode == id);
        return true;
    }

    public void SetLink(ShaderLink link)
    {
        _links.RemoveAll(existing => existing.ToNode == link.ToNode && existing.ToPort == link.ToPort);
        _links.Add(link);
    }

    public void ClearLink(Guid toNode, string toPort)
    {
        _links.RemoveAll(link => link.ToNode == toNode && link.ToPort == toPort);
    }

    public bool TryGetLink(Guid toNode, string toPort, out ShaderLink link)
    {
        for (int i = 0; i < _links.Count; i++)
        {
            var candidate = _links[i];
            if (candidate.ToNode == toNode && candidate.ToPort == toPort)
            {
                link = candidate;
                return true;
            }
        }

        link = default;
        return false;
    }

    public ShaderGraphResult Evaluate()
    {
        var evaluator = new ShaderGraphEvaluator(this);
        var baseColorValue = evaluator.GetInputValue(OutputNode, MaterialOutputNode.BaseColorInput);
        var baseColor = baseColorValue.AsVector4();
        var metallic = evaluator.GetInputValue(OutputNode, MaterialOutputNode.MetallicInput).AsFloat(0f);
        var roughness = evaluator.GetInputValue(OutputNode, MaterialOutputNode.RoughnessInput).AsFloat(0.6f);
        var emissive = evaluator.GetInputValue(OutputNode, MaterialOutputNode.EmissiveInput).AsVector4();

        var baseTextureValue = evaluator.GetInputValue(OutputNode, MaterialOutputNode.BaseColorTextureInput);
        var baseTexture = baseTextureValue.AsTexture() ?? baseColorValue.AsTexture();
        var baseSampler = baseTextureValue.Sampler ?? baseColorValue.Sampler;

        var normalTextureValue = evaluator.GetInputValue(OutputNode, MaterialOutputNode.NormalTextureInput);
        var normalTexture = normalTextureValue.AsTexture();
        var normalSampler = normalTextureValue.Sampler;
        var normalStrength = evaluator.GetInputValue(OutputNode, MaterialOutputNode.NormalStrengthInput).AsFloat(1f);

        return new ShaderGraphResult(baseColor, metallic, roughness, emissive, baseTexture, baseSampler, normalTexture, normalSampler, normalStrength);
    }

    public ShaderNode? FindNode(Guid id)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].Id == id)
            {
                return _nodes[i];
            }
        }

        return null;
    }
}

public sealed class ShaderGraphEvaluator
{
    private readonly ShaderGraph _graph;
    private readonly Dictionary<(Guid, string), ShaderValue> _cache = new();
    private readonly HashSet<(Guid, string)> _stack = new();

    public ShaderGraphEvaluator(ShaderGraph graph)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    public ShaderValue EvaluateOutput(Guid nodeId, string outputName)
    {
        var key = (nodeId, outputName);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_stack.Contains(key))
        {
            return ShaderValue.Float(0f);
        }

        var node = _graph.FindNode(nodeId);
        if (node == null)
        {
            return ShaderValue.Float(0f);
        }

        _stack.Add(key);
        var value = node.EvaluateOutput(outputName, this);
        _stack.Remove(key);
        _cache[key] = value;
        return value;
    }

    public ShaderValue GetInputValue(ShaderNode node, string inputName)
    {
        if (_graph.TryGetLink(node.Id, inputName, out var link))
        {
            return EvaluateOutput(link.FromNode, link.FromPort);
        }

        var port = node.FindInput(inputName);
        return port?.DefaultValue ?? ShaderValue.Float(0f);
    }
}
