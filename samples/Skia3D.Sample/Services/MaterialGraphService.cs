using System;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Editor;
using Skia3D.Sample.ViewModels;
using Skia3D.ShaderGraph;
using SkiaSharp;
using ShaderGraphModel = Skia3D.ShaderGraph.ShaderGraph;

namespace Skia3D.Sample.Services;

public sealed class MaterialGraphService : IDisposable
{
    private readonly EditorSession _editor;
    private readonly MaterialGraphViewModel _viewModel;

    public MaterialGraphService(EditorSession editor, MaterialGraphViewModel viewModel)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _editor.SelectionService.SelectionChanged += UpdateSelection;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var instance = _editor.Selection.Selected;
        if (instance == null)
        {
            _viewModel.SetMaterial(null, null, "Material: none");
            return;
        }

        if (!_editor.Document.TryGetNode(instance, out var node) || node.MeshRenderer == null)
        {
            _viewModel.SetMaterial(null, null, "Material: none");
            return;
        }

        var renderer = node.MeshRenderer;
        if (renderer.MaterialGraph == null)
        {
            var graph = new ShaderGraphModel();
            SeedFromMaterial(graph, renderer.Material);
            renderer.MaterialGraph = graph;
        }

        var label = $"Material: {node.Name}";
        _viewModel.SetMaterial(renderer.Material, renderer.MaterialGraph, label);
    }

    private static void SeedFromMaterial(ShaderGraphModel graph, Material material)
    {
        var baseColor = ToVector(material.BaseColor);
        var emissive = ToVector(material.EmissiveColor);

        graph.OutputNode.TrySetInputDefault(MaterialOutputNode.BaseColorInput, ShaderValue.Color(baseColor));
        if (material.BaseColorTexture != null)
        {
            graph.OutputNode.TrySetInputDefault(MaterialOutputNode.BaseColorTextureInput,
                ShaderValue.TextureValue(material.BaseColorTexture, material.BaseColorSampler));
        }

        graph.OutputNode.TrySetInputDefault(MaterialOutputNode.MetallicInput, ShaderValue.Float(material.Metallic));
        graph.OutputNode.TrySetInputDefault(MaterialOutputNode.RoughnessInput, ShaderValue.Float(material.Roughness));
        if (material.NormalTexture != null)
        {
            graph.OutputNode.TrySetInputDefault(MaterialOutputNode.NormalTextureInput,
                ShaderValue.TextureValue(material.NormalTexture, material.NormalSampler));
            graph.OutputNode.TrySetInputDefault(MaterialOutputNode.NormalStrengthInput, ShaderValue.Float(material.NormalStrength));
        }

        graph.OutputNode.TrySetInputDefault(MaterialOutputNode.EmissiveInput, ShaderValue.Color(emissive));
    }

    private static Vector4 ToVector(SKColor color)
    {
        return new Vector4(color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f);
    }

    public void Dispose()
    {
        _editor.SelectionService.SelectionChanged -= UpdateSelection;
    }
}
