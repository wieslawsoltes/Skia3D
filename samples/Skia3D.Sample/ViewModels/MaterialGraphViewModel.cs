using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Skia3D.Core;
using Skia3D.Editor;
using Skia3D.ShaderGraph;
using ShaderGraphModel = Skia3D.ShaderGraph.ShaderGraph;
using SkiaSharp;

namespace Skia3D.Sample.ViewModels;

public sealed class MaterialGraphViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<ShaderSourceOption> s_sources = new[]
    {
        new ShaderSourceOption("Constant", ShaderSourceMode.Constant),
        new ShaderSourceOption("Graph", ShaderSourceMode.Graph)
    };

    private Material? _material;
    private ShaderGraphModel? _graph;
    private string _selectionLabel = "Material: none";
    private double _baseColorR = 0.8;
    private double _baseColorG = 0.8;
    private double _baseColorB = 0.8;
    private double _metallic;
    private double _roughness = 0.6;
    private double _emissiveR;
    private double _emissiveG;
    private double _emissiveB;
    private int _baseColorSourceIndex;
    private int _metallicSourceIndex;
    private int _roughnessSourceIndex;
    private int _emissiveSourceIndex;
    private int _baseColorTextureSourceIndex;
    private int _metallicRoughnessTextureSourceIndex;
    private int _normalTextureSourceIndex;
    private int _emissiveTextureSourceIndex;
    private int _occlusionTextureSourceIndex;
    private double _baseColorTextureStrength = 1.0;
    private double _metallicRoughnessTextureStrength = 1.0;
    private double _normalStrength = 1.0;
    private double _emissiveStrength = 1.0;
    private double _occlusionStrength = 1.0;
    private TextureSlotOption? _baseColorTextureSlot;
    private TextureSlotOption? _metallicRoughnessTextureSlot;
    private TextureSlotOption? _normalTextureSlot;
    private TextureSlotOption? _emissiveTextureSlot;
    private TextureSlotOption? _occlusionTextureSlot;
    private int _shadingModelIndex = 1;
    private bool _useVertexColor = true;
    private bool _doubleSided;
    private double _ambient = 0.15;
    private double _diffuse = 0.85;
    private double _specular = 0.2;
    private double _shininess = 16.0;
    private double _uvScaleX = 1.0;
    private double _uvScaleY = 1.0;
    private double _uvOffsetX;
    private double _uvOffsetY;
    private int _previewStamp;
    private bool _isSyncing;
    private bool _isApplying;
    private readonly ObservableCollection<TextureSlotOption> _textureSlotOptions;
    private readonly Dictionary<Texture2D, TextureSlotOption> _customTextureOptions = new();

    public MaterialGraphViewModel()
    {
        Canvas = new ShaderGraphCanvasViewModel();
        Canvas.GraphChanged += OnGraphChanged;
        _textureSlotOptions = new ObservableCollection<TextureSlotOption>();
        _textureSlotOptions.Add(new TextureSlotOption("none", "None", null));
        foreach (var option in TextureLibrary.Options)
        {
            _textureSlotOptions.Add(new TextureSlotOption(option.Id, option.Label, option.Texture));
        }
        AddColorNodeCommand = new DelegateCommand(AddColorNode, CanEditGraph);
        AddFloatNodeCommand = new DelegateCommand(AddFloatNode, CanEditGraph);
        AddAddNodeCommand = new DelegateCommand(AddAddNode, CanEditGraph);
        AddMultiplyNodeCommand = new DelegateCommand(AddMultiplyNode, CanEditGraph);
        AddTextureNodeCommand = new DelegateCommand(AddTextureNode, CanEditGraph);
        AddTextureSampleNodeCommand = new DelegateCommand(AddTextureSampleNode, CanEditGraph);
        AddNormalMapNodeCommand = new DelegateCommand(AddNormalMapNode, CanEditGraph);
    }

    public ShaderGraphCanvasViewModel Canvas { get; }

    public Material? SelectedMaterial => _material;

    public bool HasMaterial => _material != null;

    public int PreviewStamp
    {
        get => _previewStamp;
        private set
        {
            if (_previewStamp == value)
            {
                return;
            }

            _previewStamp = value;
            RaisePropertyChanged();
        }
    }

    public ObservableCollection<TextureSlotOption> TextureSlotOptions => _textureSlotOptions;

    public string SelectionLabel
    {
        get => _selectionLabel;
        set
        {
            if (_selectionLabel == value)
            {
                return;
            }

            _selectionLabel = value;
            RaisePropertyChanged();
        }
    }

    public string BaseColorLabel => $"Base color: {_baseColorR:0.00} {_baseColorG:0.00} {_baseColorB:0.00}";

    public string MetallicLabel => $"Metallic: {_metallic:0.00}";

    public string RoughnessLabel => $"Roughness: {_roughness:0.00}";

    public string EmissiveLabel => $"Emissive: {_emissiveR:0.00} {_emissiveG:0.00} {_emissiveB:0.00}";

    public string BaseColorTextureStrengthLabel => $"Base texture strength: {_baseColorTextureStrength:0.00}";

    public string MetallicRoughnessTextureStrengthLabel => $"Metallic/roughness strength: {_metallicRoughnessTextureStrength:0.00}";

    public string NormalStrengthLabel => $"Normal strength: {_normalStrength:0.00}";

    public string EmissiveStrengthLabel => $"Emissive strength: {_emissiveStrength:0.00}";

    public string OcclusionStrengthLabel => $"Occlusion strength: {_occlusionStrength:0.00}";

    public string AmbientLabel => $"Ambient: {_ambient:0.00}";

    public string DiffuseLabel => $"Diffuse: {_diffuse:0.00}";

    public string SpecularLabel => $"Specular: {_specular:0.00}";

    public string ShininessLabel => $"Shininess: {_shininess:0.00}";

    public string UvScaleLabel => $"UV scale: {_uvScaleX:0.00} {_uvScaleY:0.00}";

    public string UvOffsetLabel => $"UV offset: {_uvOffsetX:0.00} {_uvOffsetY:0.00}";

    public IReadOnlyList<ShaderSourceOption> BaseColorSources => s_sources;

    public IReadOnlyList<ShaderSourceOption> MetallicSources => s_sources;

    public IReadOnlyList<ShaderSourceOption> RoughnessSources => s_sources;

    public IReadOnlyList<ShaderSourceOption> EmissiveSources => s_sources;

    public IReadOnlyList<ShaderSourceOption> TextureSources => s_sources;

    public double BaseColorR
    {
        get => _baseColorR;
        set => SetColorChannel(ref _baseColorR, value, UpdateBaseColor, nameof(BaseColorLabel));
    }

    public double BaseColorG
    {
        get => _baseColorG;
        set => SetColorChannel(ref _baseColorG, value, UpdateBaseColor, nameof(BaseColorLabel));
    }

    public double BaseColorB
    {
        get => _baseColorB;
        set => SetColorChannel(ref _baseColorB, value, UpdateBaseColor, nameof(BaseColorLabel));
    }

    public int BaseColorSourceIndex
    {
        get => _baseColorSourceIndex;
        set => SetSourceIndex(ref _baseColorSourceIndex, value, MaterialOutputNode.BaseColorInput);
    }

    public double Metallic
    {
        get => _metallic;
        set => SetScalar(ref _metallic, value, UpdateMetallic, nameof(MetallicLabel));
    }

    public int MetallicSourceIndex
    {
        get => _metallicSourceIndex;
        set => SetSourceIndex(ref _metallicSourceIndex, value, MaterialOutputNode.MetallicInput);
    }

    public double Roughness
    {
        get => _roughness;
        set => SetScalar(ref _roughness, value, UpdateRoughness, nameof(RoughnessLabel));
    }

    public int RoughnessSourceIndex
    {
        get => _roughnessSourceIndex;
        set => SetSourceIndex(ref _roughnessSourceIndex, value, MaterialOutputNode.RoughnessInput);
    }

    public double EmissiveR
    {
        get => _emissiveR;
        set => SetColorChannel(ref _emissiveR, value, UpdateEmissive, nameof(EmissiveLabel));
    }

    public double EmissiveG
    {
        get => _emissiveG;
        set => SetColorChannel(ref _emissiveG, value, UpdateEmissive, nameof(EmissiveLabel));
    }

    public double EmissiveB
    {
        get => _emissiveB;
        set => SetColorChannel(ref _emissiveB, value, UpdateEmissive, nameof(EmissiveLabel));
    }

    public int EmissiveSourceIndex
    {
        get => _emissiveSourceIndex;
        set => SetSourceIndex(ref _emissiveSourceIndex, value, MaterialOutputNode.EmissiveInput);
    }

    public int BaseColorTextureSourceIndex
    {
        get => _baseColorTextureSourceIndex;
        set
        {
            var previous = _baseColorTextureSourceIndex;
            SetSourceIndex(ref _baseColorTextureSourceIndex, value, MaterialOutputNode.BaseColorTextureInput);
            if (previous != _baseColorTextureSourceIndex)
            {
                RaisePropertyChanged(nameof(IsBaseColorTextureConstant));
            }
        }
    }

    public bool IsBaseColorTextureConstant => BaseColorTextureSourceIndex == 0;

    public int MetallicRoughnessTextureSourceIndex
    {
        get => _metallicRoughnessTextureSourceIndex;
        set
        {
            var previous = _metallicRoughnessTextureSourceIndex;
            SetSourceIndex(ref _metallicRoughnessTextureSourceIndex, value, MaterialOutputNode.MetallicRoughnessTextureInput);
            if (previous != _metallicRoughnessTextureSourceIndex)
            {
                RaisePropertyChanged(nameof(IsMetallicRoughnessTextureConstant));
            }
        }
    }

    public bool IsMetallicRoughnessTextureConstant => MetallicRoughnessTextureSourceIndex == 0;

    public int NormalTextureSourceIndex
    {
        get => _normalTextureSourceIndex;
        set
        {
            var previous = _normalTextureSourceIndex;
            SetSourceIndex(ref _normalTextureSourceIndex, value, MaterialOutputNode.NormalTextureInput);
            if (previous != _normalTextureSourceIndex)
            {
                RaisePropertyChanged(nameof(IsNormalTextureConstant));
            }
        }
    }

    public bool IsNormalTextureConstant => NormalTextureSourceIndex == 0;

    public int EmissiveTextureSourceIndex
    {
        get => _emissiveTextureSourceIndex;
        set
        {
            var previous = _emissiveTextureSourceIndex;
            SetSourceIndex(ref _emissiveTextureSourceIndex, value, MaterialOutputNode.EmissiveTextureInput);
            if (previous != _emissiveTextureSourceIndex)
            {
                RaisePropertyChanged(nameof(IsEmissiveTextureConstant));
            }
        }
    }

    public bool IsEmissiveTextureConstant => EmissiveTextureSourceIndex == 0;

    public int OcclusionTextureSourceIndex
    {
        get => _occlusionTextureSourceIndex;
        set
        {
            var previous = _occlusionTextureSourceIndex;
            SetSourceIndex(ref _occlusionTextureSourceIndex, value, MaterialOutputNode.OcclusionTextureInput);
            if (previous != _occlusionTextureSourceIndex)
            {
                RaisePropertyChanged(nameof(IsOcclusionTextureConstant));
            }
        }
    }

    public bool IsOcclusionTextureConstant => OcclusionTextureSourceIndex == 0;

    public TextureSlotOption? BaseColorTextureSlot
    {
        get => _baseColorTextureSlot;
        set => SetTextureSlot(ref _baseColorTextureSlot, value, MaterialOutputNode.BaseColorTextureInput, GetBaseColorSampler);
    }

    public TextureSlotOption? MetallicRoughnessTextureSlot
    {
        get => _metallicRoughnessTextureSlot;
        set => SetTextureSlot(ref _metallicRoughnessTextureSlot, value, MaterialOutputNode.MetallicRoughnessTextureInput, GetMetallicRoughnessSampler);
    }

    public TextureSlotOption? NormalTextureSlot
    {
        get => _normalTextureSlot;
        set => SetTextureSlot(ref _normalTextureSlot, value, MaterialOutputNode.NormalTextureInput, GetNormalSampler);
    }

    public TextureSlotOption? EmissiveTextureSlot
    {
        get => _emissiveTextureSlot;
        set => SetTextureSlot(ref _emissiveTextureSlot, value, MaterialOutputNode.EmissiveTextureInput, GetEmissiveSampler);
    }

    public TextureSlotOption? OcclusionTextureSlot
    {
        get => _occlusionTextureSlot;
        set => SetTextureSlot(ref _occlusionTextureSlot, value, MaterialOutputNode.OcclusionTextureInput, GetOcclusionSampler);
    }

    public double BaseColorTextureStrength
    {
        get => _baseColorTextureStrength;
        set => SetScalar(ref _baseColorTextureStrength, value, UpdateBaseColorTextureStrength, nameof(BaseColorTextureStrengthLabel));
    }

    public double MetallicRoughnessTextureStrength
    {
        get => _metallicRoughnessTextureStrength;
        set => SetScalar(ref _metallicRoughnessTextureStrength, value, UpdateMetallicRoughnessTextureStrength, nameof(MetallicRoughnessTextureStrengthLabel));
    }

    public double NormalStrength
    {
        get => _normalStrength;
        set => SetScalarRange(ref _normalStrength, value, 0.0, 4.0, UpdateNormalStrength, nameof(NormalStrengthLabel));
    }

    public double EmissiveStrength
    {
        get => _emissiveStrength;
        set => SetScalar(ref _emissiveStrength, value, UpdateEmissiveStrength, nameof(EmissiveStrengthLabel));
    }

    public double OcclusionStrength
    {
        get => _occlusionStrength;
        set => SetScalar(ref _occlusionStrength, value, UpdateOcclusionStrength, nameof(OcclusionStrengthLabel));
    }

    public int ShadingModelIndex
    {
        get => _shadingModelIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (_shadingModelIndex == clamped)
            {
                return;
            }

            _shadingModelIndex = clamped;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsPhongModel));
            RaisePropertyChanged(nameof(IsPbrModel));
            if (_isSyncing || _material == null)
            {
                return;
            }

            _material.ShadingModel = _shadingModelIndex == 0 ? MaterialShadingModel.Phong : MaterialShadingModel.MetallicRoughness;
        }
    }

    public bool IsPhongModel => ShadingModelIndex == 0;

    public bool IsPbrModel => ShadingModelIndex == 1;

    public bool UseVertexColor
    {
        get => _useVertexColor;
        set
        {
            if (_useVertexColor == value)
            {
                return;
            }

            _useVertexColor = value;
            RaisePropertyChanged();
            if (_isSyncing || _material == null)
            {
                return;
            }

            _material.UseVertexColor = value;
        }
    }

    public bool DoubleSided
    {
        get => _doubleSided;
        set
        {
            if (_doubleSided == value)
            {
                return;
            }

            _doubleSided = value;
            RaisePropertyChanged();
            if (_isSyncing || _material == null)
            {
                return;
            }

            _material.DoubleSided = value;
        }
    }

    public double Ambient
    {
        get => _ambient;
        set => SetMaterialScalar(ref _ambient, value, 0.0, 1.0, updated => _material!.Ambient = (float)updated, nameof(AmbientLabel));
    }

    public double Diffuse
    {
        get => _diffuse;
        set => SetMaterialScalar(ref _diffuse, value, 0.0, 1.0, updated => _material!.Diffuse = (float)updated, nameof(DiffuseLabel));
    }

    public double Specular
    {
        get => _specular;
        set => SetMaterialScalar(ref _specular, value, 0.0, 1.0, updated => _material!.Specular = (float)updated, nameof(SpecularLabel));
    }

    public double Shininess
    {
        get => _shininess;
        set => SetMaterialScalar(ref _shininess, value, 1.0, 128.0, updated => _material!.Shininess = (float)updated, nameof(ShininessLabel));
    }

    public double UvScaleX
    {
        get => _uvScaleX;
        set => SetUvScale(ref _uvScaleX, value);
    }

    public double UvScaleY
    {
        get => _uvScaleY;
        set => SetUvScale(ref _uvScaleY, value);
    }

    public double UvOffsetX
    {
        get => _uvOffsetX;
        set => SetUvOffset(ref _uvOffsetX, value);
    }

    public double UvOffsetY
    {
        get => _uvOffsetY;
        set => SetUvOffset(ref _uvOffsetY, value);
    }

    public DelegateCommand AddColorNodeCommand { get; }

    public DelegateCommand AddFloatNodeCommand { get; }

    public DelegateCommand AddAddNodeCommand { get; }

    public DelegateCommand AddMultiplyNodeCommand { get; }

    public DelegateCommand AddTextureNodeCommand { get; }

    public DelegateCommand AddTextureSampleNodeCommand { get; }

    public DelegateCommand AddNormalMapNodeCommand { get; }

    public void SetMaterial(Material? material, ShaderGraphModel? graph, string label)
    {
        _material = material;
        _graph = graph;
        SelectionLabel = label;
        Canvas.SetGraph(graph);
        ResetCustomTextureOptions();
        SyncFromMaterial();
        SyncFromGraph();
        ApplyGraphToMaterial();
        UpdateCommandState();
        RaisePropertyChanged(nameof(SelectedMaterial));
        RaisePropertyChanged(nameof(HasMaterial));
    }

    private void UpdateCommandState()
    {
        AddColorNodeCommand.RaiseCanExecuteChanged();
        AddFloatNodeCommand.RaiseCanExecuteChanged();
        AddAddNodeCommand.RaiseCanExecuteChanged();
        AddMultiplyNodeCommand.RaiseCanExecuteChanged();
        AddTextureNodeCommand.RaiseCanExecuteChanged();
        AddTextureSampleNodeCommand.RaiseCanExecuteChanged();
        AddNormalMapNodeCommand.RaiseCanExecuteChanged();
    }

    private bool CanEditGraph() => _graph != null;

    private void AddColorNode()
    {
        if (_graph == null)
        {
            return;
        }

        var node = new ColorNode(new Vector4(1f, 1f, 1f, 1f));
        Canvas.AddNode(node);
    }

    private void AddFloatNode()
    {
        if (_graph == null)
        {
            return;
        }

        var node = new FloatNode(0.5f);
        Canvas.AddNode(node);
    }

    private void AddAddNode()
    {
        if (_graph == null)
        {
            return;
        }

        Canvas.AddNode(new AddNode());
    }

    private void AddMultiplyNode()
    {
        if (_graph == null)
        {
            return;
        }

        Canvas.AddNode(new MultiplyNode());
    }

    private void AddTextureNode()
    {
        if (_graph == null)
        {
            return;
        }

        var option = TextureLibrary.Default;
        var node = new Texture2DNode(option.Texture)
        {
            Label = option.Label,
            TextureId = option.Id
        };
        Canvas.AddNode(node);
    }

    private void AddTextureSampleNode()
    {
        if (_graph == null)
        {
            return;
        }

        Canvas.AddNode(new TextureSampleNode());
    }

    private void AddNormalMapNode()
    {
        if (_graph == null)
        {
            return;
        }

        Canvas.AddNode(new NormalMapNode());
    }

    private void OnGraphChanged()
    {
        SyncSourceIndices();
        ApplyGraphToMaterial();
    }

    private void SyncFromMaterial()
    {
        _isSyncing = true;
        try
        {
            if (_material == null)
            {
                ShadingModelIndex = 1;
                UseVertexColor = true;
                DoubleSided = false;
                Ambient = 0.15;
                Diffuse = 0.85;
                Specular = 0.2;
                Shininess = 16.0;
                UvScaleX = 1.0;
                UvScaleY = 1.0;
                UvOffsetX = 0.0;
                UvOffsetY = 0.0;
                return;
            }

            ShadingModelIndex = _material.ShadingModel == MaterialShadingModel.Phong ? 0 : 1;
            UseVertexColor = _material.UseVertexColor;
            DoubleSided = _material.DoubleSided;
            Ambient = _material.Ambient;
            Diffuse = _material.Diffuse;
            Specular = _material.Specular;
            Shininess = _material.Shininess;
            UvScaleX = _material.UvScale.X;
            UvScaleY = _material.UvScale.Y;
            UvOffsetX = _material.UvOffset.X;
            UvOffsetY = _material.UvOffset.Y;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void ResetCustomTextureOptions()
    {
        if (_customTextureOptions.Count == 0)
        {
            return;
        }

        for (int i = _textureSlotOptions.Count - 1; i >= 0; i--)
        {
            if (_textureSlotOptions[i].IsCustom)
            {
                _textureSlotOptions.RemoveAt(i);
            }
        }

        _customTextureOptions.Clear();
    }

    private void SyncFromGraph()
    {
        _isSyncing = true;
        try
        {
            if (_graph == null)
            {
                BaseColorR = 0.8;
                BaseColorG = 0.8;
                BaseColorB = 0.8;
                Metallic = 0;
                Roughness = 0.6;
                EmissiveR = 0;
                EmissiveG = 0;
                EmissiveB = 0;
                BaseColorSourceIndex = 0;
                MetallicSourceIndex = 0;
                RoughnessSourceIndex = 0;
                EmissiveSourceIndex = 0;
                BaseColorTextureSourceIndex = 0;
                MetallicRoughnessTextureSourceIndex = 0;
                NormalTextureSourceIndex = 0;
                EmissiveTextureSourceIndex = 0;
                OcclusionTextureSourceIndex = 0;
                BaseColorTextureStrength = 1.0;
                MetallicRoughnessTextureStrength = 1.0;
                NormalStrength = 1.0;
                EmissiveStrength = 1.0;
                OcclusionStrength = 1.0;
                BaseColorTextureSlot = TextureSlotOptions[0];
                MetallicRoughnessTextureSlot = TextureSlotOptions[0];
                NormalTextureSlot = TextureSlotOptions[0];
                EmissiveTextureSlot = TextureSlotOptions[0];
                OcclusionTextureSlot = TextureSlotOptions[0];
                return;
            }

            var output = _graph.OutputNode;
            var baseColor = output.FindInput(MaterialOutputNode.BaseColorInput)?.DefaultValue.AsVector4()
                ?? new Vector4(0.8f, 0.8f, 0.8f, 1f);
            BaseColorR = baseColor.X;
            BaseColorG = baseColor.Y;
            BaseColorB = baseColor.Z;
            Metallic = output.FindInput(MaterialOutputNode.MetallicInput)?.DefaultValue.AsFloat(0f) ?? 0f;
            Roughness = output.FindInput(MaterialOutputNode.RoughnessInput)?.DefaultValue.AsFloat(0.6f) ?? 0.6f;
            var emissive = output.FindInput(MaterialOutputNode.EmissiveInput)?.DefaultValue.AsVector4()
                ?? new Vector4(0f, 0f, 0f, 1f);
            EmissiveR = emissive.X;
            EmissiveG = emissive.Y;
            EmissiveB = emissive.Z;
            BaseColorTextureStrength = output.FindInput(MaterialOutputNode.BaseColorTextureStrengthInput)?.DefaultValue.AsFloat(1f) ?? 1f;
            MetallicRoughnessTextureStrength = output.FindInput(MaterialOutputNode.MetallicRoughnessStrengthInput)?.DefaultValue.AsFloat(1f) ?? 1f;
            NormalStrength = output.FindInput(MaterialOutputNode.NormalStrengthInput)?.DefaultValue.AsFloat(1f) ?? 1f;
            EmissiveStrength = output.FindInput(MaterialOutputNode.EmissiveStrengthInput)?.DefaultValue.AsFloat(1f) ?? 1f;
            OcclusionStrength = output.FindInput(MaterialOutputNode.OcclusionStrengthInput)?.DefaultValue.AsFloat(1f) ?? 1f;
            BaseColorTextureSlot = ResolveTextureSlot(output.FindInput(MaterialOutputNode.BaseColorTextureInput)?.DefaultValue.AsTexture());
            MetallicRoughnessTextureSlot = ResolveTextureSlot(output.FindInput(MaterialOutputNode.MetallicRoughnessTextureInput)?.DefaultValue.AsTexture());
            NormalTextureSlot = ResolveTextureSlot(output.FindInput(MaterialOutputNode.NormalTextureInput)?.DefaultValue.AsTexture());
            EmissiveTextureSlot = ResolveTextureSlot(output.FindInput(MaterialOutputNode.EmissiveTextureInput)?.DefaultValue.AsTexture());
            OcclusionTextureSlot = ResolveTextureSlot(output.FindInput(MaterialOutputNode.OcclusionTextureInput)?.DefaultValue.AsTexture());
            SyncSourceIndices();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void SyncSourceIndices()
    {
        if (_graph == null)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            BaseColorSourceIndex = GetSourceIndex(MaterialOutputNode.BaseColorInput);
            MetallicSourceIndex = GetSourceIndex(MaterialOutputNode.MetallicInput);
            RoughnessSourceIndex = GetSourceIndex(MaterialOutputNode.RoughnessInput);
            EmissiveSourceIndex = GetSourceIndex(MaterialOutputNode.EmissiveInput);
            BaseColorTextureSourceIndex = GetSourceIndex(MaterialOutputNode.BaseColorTextureInput);
            MetallicRoughnessTextureSourceIndex = GetSourceIndex(MaterialOutputNode.MetallicRoughnessTextureInput);
            NormalTextureSourceIndex = GetSourceIndex(MaterialOutputNode.NormalTextureInput);
            EmissiveTextureSourceIndex = GetSourceIndex(MaterialOutputNode.EmissiveTextureInput);
            OcclusionTextureSourceIndex = GetSourceIndex(MaterialOutputNode.OcclusionTextureInput);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private int GetSourceIndex(string inputName)
    {
        if (_graph == null)
        {
            return 0;
        }

        return _graph.TryGetLink(_graph.OutputNode.Id, inputName, out _)
            ? 1
            : 0;
    }

    private void SetColorChannel(ref double field, double value, Action apply, string labelProperty)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (Math.Abs(field - clamped) < 1e-6)
        {
            return;
        }

        field = clamped;
        RaisePropertyChanged();
        RaisePropertyChanged(labelProperty);
        if (_isSyncing)
        {
            return;
        }

        apply();
    }

    private void SetScalar(ref double field, double value, Action apply, string labelProperty)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (Math.Abs(field - clamped) < 1e-6)
        {
            return;
        }

        field = clamped;
        RaisePropertyChanged();
        RaisePropertyChanged(labelProperty);
        if (_isSyncing)
        {
            return;
        }

        apply();
    }

    private void SetScalarRange(ref double field, double value, double min, double max, Action apply, string labelProperty)
    {
        var clamped = Math.Clamp(value, min, max);
        if (Math.Abs(field - clamped) < 1e-6)
        {
            return;
        }

        field = clamped;
        RaisePropertyChanged();
        RaisePropertyChanged(labelProperty);
        if (_isSyncing)
        {
            return;
        }

        apply();
    }

    private void SetMaterialScalar(ref double field, double value, double min, double max, Action<double> apply, string labelProperty)
    {
        var clamped = Math.Clamp(value, min, max);
        if (Math.Abs(field - clamped) < 1e-6)
        {
            return;
        }

        field = clamped;
        RaisePropertyChanged();
        RaisePropertyChanged(labelProperty);
        if (_isSyncing || _material == null)
        {
            return;
        }

        apply(clamped);
    }

    private void SetUvScale(ref double field, double value)
    {
        var clamped = Math.Clamp(value, 0.01, 10.0);
        if (Math.Abs(field - clamped) < 1e-6)
        {
            return;
        }

        field = clamped;
        RaisePropertyChanged();
        RaisePropertyChanged(nameof(UvScaleLabel));
        if (_isSyncing || _material == null)
        {
            return;
        }

        _material.UvScale = new Vector2((float)_uvScaleX, (float)_uvScaleY);
    }

    private void SetUvOffset(ref double field, double value)
    {
        var clamped = Math.Clamp(value, -5.0, 5.0);
        if (Math.Abs(field - clamped) < 1e-6)
        {
            return;
        }

        field = clamped;
        RaisePropertyChanged();
        RaisePropertyChanged(nameof(UvOffsetLabel));
        if (_isSyncing || _material == null)
        {
            return;
        }

        _material.UvOffset = new Vector2((float)_uvOffsetX, (float)_uvOffsetY);
    }

    private void SetTextureSlot(ref TextureSlotOption? field, TextureSlotOption? value, string inputName, Func<TextureSampler> samplerProvider)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        RaisePropertyChanged();
        if (_isSyncing || _graph == null)
        {
            return;
        }

        var sampler = samplerProvider();
        _graph.OutputNode.TrySetInputDefault(inputName, ShaderValue.TextureValue(value?.Texture, sampler));
        ApplyGraphToMaterial();
    }

    private TextureSlotOption ResolveTextureSlot(Texture2D? texture)
    {
        if (texture == null)
        {
            return _textureSlotOptions[0];
        }

        for (int i = 0; i < _textureSlotOptions.Count; i++)
        {
            if (ReferenceEquals(_textureSlotOptions[i].Texture, texture))
            {
                return _textureSlotOptions[i];
            }
        }

        if (_customTextureOptions.TryGetValue(texture, out var existing))
        {
            return existing;
        }

        var index = _customTextureOptions.Count + 1;
        var custom = new TextureSlotOption($"custom-{index}", $"Custom {index}", texture, isCustom: true);
        _customTextureOptions[texture] = custom;
        _textureSlotOptions.Add(custom);
        return custom;
    }

    private TextureSampler GetBaseColorSampler() => _material?.BaseColorSampler ?? new TextureSampler();

    private TextureSampler GetMetallicRoughnessSampler() => _material?.MetallicRoughnessSampler ?? new TextureSampler();

    private TextureSampler GetNormalSampler() => _material?.NormalSampler ?? new TextureSampler();

    private TextureSampler GetEmissiveSampler() => _material?.EmissiveSampler ?? new TextureSampler();

    private TextureSampler GetOcclusionSampler() => _material?.OcclusionSampler ?? new TextureSampler();

    private void SetSourceIndex(ref int field, int value, string inputName)
    {
        var clamped = Math.Clamp(value, 0, s_sources.Count - 1);
        if (field == clamped)
        {
            return;
        }

        field = clamped;
        RaisePropertyChanged();
        if (_isSyncing || _graph == null)
        {
            return;
        }

        var mode = s_sources[field].Mode;
        if (mode == ShaderSourceMode.Constant)
        {
            _graph.ClearLink(_graph.OutputNode.Id, inputName);
            ApplyGraphToMaterial();
        }
    }

    private void UpdateBaseColor()
    {
        if (_graph == null)
        {
            return;
        }

        var color = new Vector4((float)_baseColorR, (float)_baseColorG, (float)_baseColorB, 1f);
        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.BaseColorInput, ShaderValue.Color(color));
        ApplyGraphToMaterial();
    }

    private void UpdateMetallic()
    {
        if (_graph == null)
        {
            return;
        }

        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.MetallicInput, ShaderValue.Float((float)_metallic));
        ApplyGraphToMaterial();
    }

    private void UpdateRoughness()
    {
        if (_graph == null)
        {
            return;
        }

        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.RoughnessInput, ShaderValue.Float((float)_roughness));
        ApplyGraphToMaterial();
    }

    private void UpdateEmissive()
    {
        if (_graph == null)
        {
            return;
        }

        var color = new Vector4((float)_emissiveR, (float)_emissiveG, (float)_emissiveB, 1f);
        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.EmissiveInput, ShaderValue.Color(color));
        ApplyGraphToMaterial();
    }

    private void UpdateBaseColorTextureStrength()
    {
        if (_graph == null)
        {
            return;
        }

        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.BaseColorTextureStrengthInput, ShaderValue.Float((float)_baseColorTextureStrength));
        ApplyGraphToMaterial();
    }

    private void UpdateMetallicRoughnessTextureStrength()
    {
        if (_graph == null)
        {
            return;
        }

        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.MetallicRoughnessStrengthInput, ShaderValue.Float((float)_metallicRoughnessTextureStrength));
        ApplyGraphToMaterial();
    }

    private void UpdateNormalStrength()
    {
        if (_graph == null)
        {
            return;
        }

        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.NormalStrengthInput, ShaderValue.Float((float)_normalStrength));
        ApplyGraphToMaterial();
    }

    private void UpdateEmissiveStrength()
    {
        if (_graph == null)
        {
            return;
        }

        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.EmissiveStrengthInput, ShaderValue.Float((float)_emissiveStrength));
        ApplyGraphToMaterial();
    }

    private void UpdateOcclusionStrength()
    {
        if (_graph == null)
        {
            return;
        }

        _graph.OutputNode.TrySetInputDefault(MaterialOutputNode.OcclusionStrengthInput, ShaderValue.Float((float)_occlusionStrength));
        ApplyGraphToMaterial();
    }

    private void BumpPreviewStamp()
    {
        PreviewStamp = _previewStamp + 1;
    }

    private void ApplyGraphToMaterial()
    {
        if (_isApplying || _material == null || _graph == null)
        {
            return;
        }

        _isApplying = true;
        try
        {
            var result = _graph.Evaluate();
            _material.BaseColor = ToColor(result.BaseColor);
            _material.Metallic = result.Metallic;
            _material.Roughness = result.Roughness;
            _material.EmissiveColor = ToColor(result.Emissive);
            _material.BaseColorTexture = result.BaseColorTexture;
            if (result.BaseColorSampler != null)
            {
                _material.BaseColorSampler = result.BaseColorSampler;
            }

            _material.BaseColorTextureStrength = result.BaseColorTextureStrength;
            _material.MetallicRoughnessTexture = result.MetallicRoughnessTexture;
            if (result.MetallicRoughnessSampler != null)
            {
                _material.MetallicRoughnessSampler = result.MetallicRoughnessSampler;
            }

            _material.MetallicRoughnessTextureStrength = result.MetallicRoughnessStrength;
            _material.NormalTexture = result.NormalTexture;
            if (result.NormalSampler != null)
            {
                _material.NormalSampler = result.NormalSampler;
            }

            _material.NormalStrength = result.NormalStrength;
            _material.EmissiveTexture = result.EmissiveTexture;
            if (result.EmissiveSampler != null)
            {
                _material.EmissiveSampler = result.EmissiveSampler;
            }

            _material.EmissiveStrength = result.EmissiveStrength;
            _material.OcclusionTexture = result.OcclusionTexture;
            if (result.OcclusionSampler != null)
            {
                _material.OcclusionSampler = result.OcclusionSampler;
            }

            _material.OcclusionStrength = result.OcclusionStrength;
            BumpPreviewStamp();
        }
        finally
        {
            _isApplying = false;
        }
    }

    private static SKColor ToColor(Vector4 value)
    {
        var r = (byte)(Math.Clamp(value.X, 0f, 1f) * 255f);
        var g = (byte)(Math.Clamp(value.Y, 0f, 1f) * 255f);
        var b = (byte)(Math.Clamp(value.Z, 0f, 1f) * 255f);
        return new SKColor(r, g, b);
    }
}

public sealed class ShaderGraphCanvasViewModel : ViewModelBase
{
    private ShaderGraphModel? _graph;
    private ShaderPortViewModel? _pendingOutput;
    private double _zoom = 1.0;
    private double _panX = 12.0;
    private double _panY = 12.0;
    private Vector2 _nextPosition = new(24f, 24f);
    private readonly Dictionary<(Guid nodeId, string portName, bool isInput), ShaderPortViewModel> _ports = new();

    public event Action? GraphChanged;

    public ObservableCollection<ShaderNodeViewModel> Nodes { get; } = new();

    public ObservableCollection<ShaderLinkViewModel> Links { get; } = new();

    public double Zoom
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, 0.25, 2.5);
            if (Math.Abs(_zoom - clamped) < 1e-6)
            {
                return;
            }

            _zoom = clamped;
            RaisePropertyChanged();
        }
    }

    public double PanX
    {
        get => _panX;
        set
        {
            if (Math.Abs(_panX - value) < 1e-6)
            {
                return;
            }

            _panX = value;
            RaisePropertyChanged();
        }
    }

    public double PanY
    {
        get => _panY;
        set
        {
            if (Math.Abs(_panY - value) < 1e-6)
            {
                return;
            }

            _panY = value;
            RaisePropertyChanged();
        }
    }

    public void SetGraph(ShaderGraphModel? graph)
    {
        _graph = graph;
        Nodes.Clear();
        Links.Clear();
        _ports.Clear();
        _pendingOutput?.SetPending(false);
        _pendingOutput = null;
        if (_graph == null)
        {
            return;
        }

        bool autoLayout = _graph.Nodes.All(node => node.Position == Vector2.Zero);
        if (autoLayout)
        {
            LayoutDefault();
        }

        foreach (var node in _graph.Nodes)
        {
            var vm = CreateNodeViewModel(node);
            Nodes.Add(vm);
            RegisterPorts(vm);
        }

        BuildLinks();
        UpdateNextPosition();
    }

    public void AddNode(ShaderNode node)
    {
        if (_graph == null)
        {
            return;
        }

        node.Position = _nextPosition;
        _graph.AddNode(node);
        var vm = CreateNodeViewModel(node);
        Nodes.Add(vm);
        RegisterPorts(vm);
        StepNextPosition();
        BuildLinks();
        NotifyGraphChanged();
    }

    public void HandlePortClicked(ShaderPortViewModel port)
    {
        if (_graph == null)
        {
            return;
        }

        if (port.IsInput)
        {
            if (_pendingOutput != null && CanConnect(_pendingOutput.Type, port.Type))
            {
                var link = new ShaderLink(_pendingOutput.NodeId, _pendingOutput.Name, port.NodeId, port.Name);
                _graph.SetLink(link);
                _pendingOutput.SetPending(false);
                _pendingOutput = null;
                BuildLinks();
                NotifyGraphChanged();
                return;
            }

            if (_graph.TryGetLink(port.NodeId, port.Name, out _))
            {
                _graph.ClearLink(port.NodeId, port.Name);
                BuildLinks();
                NotifyGraphChanged();
            }

            return;
        }

        if (_pendingOutput == port)
        {
            port.SetPending(false);
            _pendingOutput = null;
            return;
        }

        _pendingOutput?.SetPending(false);
        _pendingOutput = port;
        port.SetPending(true);
    }

    public void NotifyGraphChanged()
    {
        GraphChanged?.Invoke();
    }

    private static bool CanConnect(ShaderValueType output, ShaderValueType input)
    {
        if (output == ShaderValueType.Texture || input == ShaderValueType.Texture)
        {
            return output == input;
        }

        return true;
    }

    private void RegisterPorts(ShaderNodeViewModel node)
    {
        foreach (var port in node.Inputs)
        {
            _ports[(node.Id, port.Name, true)] = port;
        }

        foreach (var port in node.Outputs)
        {
            _ports[(node.Id, port.Name, false)] = port;
        }
    }

    private void BuildLinks()
    {
        foreach (var port in _ports.Values)
        {
            port.SetConnected(false);
        }

        Links.Clear();
        if (_graph == null)
        {
            return;
        }

        foreach (var link in _graph.Links)
        {
            if (!_ports.TryGetValue((link.FromNode, link.FromPort, false), out var from))
            {
                continue;
            }

            if (!_ports.TryGetValue((link.ToNode, link.ToPort, true), out var to))
            {
                continue;
            }

            from.SetConnected(true);
            to.SetConnected(true);
            Links.Add(new ShaderLinkViewModel(from, to));
        }
    }

    private ShaderNodeViewModel CreateNodeViewModel(ShaderNode node)
    {
        return node switch
        {
            MaterialOutputNode output => new MaterialOutputNodeViewModel(output, this),
            ColorNode color => new ColorNodeViewModel(color, this),
            FloatNode scalar => new FloatNodeViewModel(scalar, this),
            Texture2DNode texture => new TextureNodeViewModel(texture, this),
            NormalMapNode normal => new NormalMapNodeViewModel(normal, this),
            _ => new ShaderNodeViewModel(node, this)
        };
    }

    private void LayoutDefault()
    {
        if (_graph == null)
        {
            return;
        }

        var output = _graph.OutputNode;
        output.Position = new Vector2(360f, 40f);
        float x = 24f;
        float y = 40f;
        float stepY = 140f;
        int index = 0;
        foreach (var node in _graph.Nodes)
        {
            if (ReferenceEquals(node, output))
            {
                continue;
            }

            node.Position = new Vector2(x, y + index * stepY);
            index++;
        }
    }

    private void UpdateNextPosition()
    {
        if (_graph == null || _graph.Nodes.Count == 0)
        {
            _nextPosition = new Vector2(24f, 24f);
            return;
        }

        float maxX = 0f;
        float maxY = 0f;
        foreach (var node in _graph.Nodes)
        {
            maxX = MathF.Max(maxX, node.Position.X);
            maxY = MathF.Max(maxY, node.Position.Y);
        }

        _nextPosition = new Vector2(maxX + 220f, maxY);
    }

    private void StepNextPosition()
    {
        _nextPosition = new Vector2(_nextPosition.X + 220f, _nextPosition.Y);
        if (_nextPosition.X > 480f)
        {
            _nextPosition = new Vector2(24f, _nextPosition.Y + 140f);
        }
    }
}

public class ShaderNodeViewModel : ViewModelBase
{
    public const double DefaultWidth = 180;
    public const double HeaderHeight = 24;
    public const double PortRowHeight = 20;
    public const double DetailRowHeight = 22;

    private double _x;
    private double _y;

    public ShaderNodeViewModel(ShaderNode node, ShaderGraphCanvasViewModel canvas)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Id = node.Id;
        Title = node.Title;
        _x = node.Position.X;
        _y = node.Position.Y;

        int index = 0;
        foreach (var port in node.Inputs)
        {
            Inputs.Add(new ShaderPortViewModel(this, port, true, index++, canvas));
        }

        index = 0;
        foreach (var port in node.Outputs)
        {
            Outputs.Add(new ShaderPortViewModel(this, port, false, index++, canvas));
        }
    }

    public event Action? PositionChanged;

    public ShaderGraphCanvasViewModel Canvas { get; }

    public ShaderNode Node { get; }

    public Guid Id { get; }

    public string Title { get; }

    public ObservableCollection<ShaderPortViewModel> Inputs { get; } = new();

    public ObservableCollection<ShaderPortViewModel> Outputs { get; } = new();

    public virtual int DetailRowCount => 0;

    public bool HasDetails => DetailRowCount > 0;

    public virtual bool IsColorNode => false;

    public virtual bool IsFloatNode => false;

    public virtual bool IsTextureNode => false;

    public virtual bool IsNormalMapNode => false;

    public double NodeWidth => DefaultWidth;

    public double X
    {
        get => _x;
        set
        {
            if (Math.Abs(_x - value) < 0.01)
            {
                return;
            }

            _x = value;
            Node.Position = new Vector2((float)_x, (float)_y);
            RaisePropertyChanged();
            PositionChanged?.Invoke();
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (Math.Abs(_y - value) < 0.01)
            {
                return;
            }

            _y = value;
            Node.Position = new Vector2((float)_x, (float)_y);
            RaisePropertyChanged();
            PositionChanged?.Invoke();
        }
    }

    public double GetPortAnchorX(bool isInput)
    {
        return _x + (isInput ? 0 : NodeWidth);
    }

    public double GetPortAnchorY(int index)
    {
        return _y + HeaderHeight + DetailRowCount * DetailRowHeight + index * PortRowHeight + PortRowHeight * 0.5;
    }
}

public sealed class MaterialOutputNodeViewModel : ShaderNodeViewModel
{
    public MaterialOutputNodeViewModel(MaterialOutputNode node, ShaderGraphCanvasViewModel canvas)
        : base(node, canvas)
    {
    }
}

public sealed class FloatNodeViewModel : ShaderNodeViewModel
{
    private readonly FloatNode _node;
    private double _value;

    public FloatNodeViewModel(FloatNode node, ShaderGraphCanvasViewModel canvas) : base(node, canvas)
    {
        _node = node;
        _value = node.Value;
    }

    public override int DetailRowCount => 1;

    public override bool IsFloatNode => true;

    public double Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, -10.0, 10.0);
            if (Math.Abs(_value - clamped) < 1e-6)
            {
                return;
            }

            _value = clamped;
            _node.Value = (float)clamped;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ValueLabel));
            Canvas.NotifyGraphChanged();
        }
    }

    public string ValueLabel => $"Value: {_value:0.00}";
}

public sealed class ColorNodeViewModel : ShaderNodeViewModel
{
    private readonly ColorNode _node;
    private double _r;
    private double _g;
    private double _b;

    public ColorNodeViewModel(ColorNode node, ShaderGraphCanvasViewModel canvas) : base(node, canvas)
    {
        _node = node;
        _r = node.Color.X;
        _g = node.Color.Y;
        _b = node.Color.Z;
    }

    public override int DetailRowCount => 3;

    public override bool IsColorNode => true;

    public double R
    {
        get => _r;
        set => SetChannel(ref _r, value);
    }

    public double G
    {
        get => _g;
        set => SetChannel(ref _g, value);
    }

    public double B
    {
        get => _b;
        set => SetChannel(ref _b, value);
    }

    public string ColorLabel => $"Color: {_r:0.00} {_g:0.00} {_b:0.00}";

    private void SetChannel(ref double field, double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (Math.Abs(field - clamped) < 1e-6)
        {
            return;
        }

        field = clamped;
        _node.Color = new Vector4((float)_r, (float)_g, (float)_b, 1f);
        RaisePropertyChanged();
        RaisePropertyChanged(nameof(ColorLabel));
        Canvas.NotifyGraphChanged();
    }
}

public sealed class TextureNodeViewModel : ShaderNodeViewModel
{
    private readonly Texture2DNode _node;
    private TextureOption? _selected;

    public TextureNodeViewModel(Texture2DNode node, ShaderGraphCanvasViewModel canvas) : base(node, canvas)
    {
        _node = node;
        Options = new ObservableCollection<TextureOption>(TextureLibrary.Options);
        _selected = ResolveSelection(node);
    }

    public override int DetailRowCount => 1;

    public override bool IsTextureNode => true;

    public ObservableCollection<TextureOption> Options { get; }

    public TextureOption? SelectedOption
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            _selected = value;
            ApplySelection();
            RaisePropertyChanged();
        }
    }

    private TextureOption ResolveSelection(Texture2DNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.TextureId))
        {
            var option = TextureLibrary.GetById(node.TextureId);
            if (option != null)
            {
                return option;
            }
        }

        if (!string.IsNullOrWhiteSpace(node.Label))
        {
            var option = TextureLibrary.Options.FirstOrDefault(item => item.Label == node.Label);
            if (option != null)
            {
                return option;
            }
        }

        return TextureLibrary.Default;
    }

    private void ApplySelection()
    {
        if (_selected == null)
        {
            return;
        }

        _node.Texture = _selected.Texture;
        _node.Label = _selected.Label;
        _node.TextureId = _selected.Id;
        Canvas.NotifyGraphChanged();
    }
}

public sealed class NormalMapNodeViewModel : ShaderNodeViewModel
{
    private readonly NormalMapNode _node;
    private double _strength = 1.0;

    public NormalMapNodeViewModel(NormalMapNode node, ShaderGraphCanvasViewModel canvas) : base(node, canvas)
    {
        _node = node;
        var input = node.FindInput(NormalMapNode.StrengthInput)?.DefaultValue.AsFloat(1f) ?? 1f;
        _strength = input;
    }

    public override int DetailRowCount => 1;

    public override bool IsNormalMapNode => true;

    public double Strength
    {
        get => _strength;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 4.0);
            if (Math.Abs(_strength - clamped) < 1e-6)
            {
                return;
            }

            _strength = clamped;
            _node.TrySetInputDefault(NormalMapNode.StrengthInput, ShaderValue.Float((float)clamped));
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(StrengthLabel));
            Canvas.NotifyGraphChanged();
        }
    }

    public string StrengthLabel => $"Strength: {_strength:0.00}";
}

public sealed class ShaderPortViewModel : ViewModelBase
{
    private bool _isPending;
    private bool _isConnected;

    public ShaderPortViewModel(ShaderNodeViewModel node, ShaderPort port, bool isInput, int index, ShaderGraphCanvasViewModel canvas)
    {
        Node = node;
        Name = port.Name;
        Type = port.Type;
        IsInput = isInput;
        Index = index;
        NodeId = node.Id;
        ClickCommand = new DelegateCommand(() => canvas.HandlePortClicked(this));
    }

    public ShaderNodeViewModel Node { get; }

    public Guid NodeId { get; }

    public string Name { get; }

    public ShaderValueType Type { get; }

    public bool IsInput { get; }

    public int Index { get; }

    public DelegateCommand ClickCommand { get; }

    public string PortFill => PortColor;

    public string PortStroke => _isPending ? "#E8EEF5" : "#0E141B";

    public double PortStrokeThickness => _isPending ? 2.0 : 1.0;

    public string PortColor => Type switch
    {
        ShaderValueType.Float => "#8FD26A",
        ShaderValueType.Vector2 => "#6BC0D8",
        ShaderValueType.Vector3 => "#D8B56B",
        ShaderValueType.Vector4 => "#D88E6B",
        ShaderValueType.Color => "#D8D06B",
        ShaderValueType.Texture => "#6B8FD8",
        _ => "#9AA7B4"
    };

    public void SetPending(bool value)
    {
        if (_isPending == value)
        {
            return;
        }

        _isPending = value;
        RaisePropertyChanged(nameof(PortStroke));
        RaisePropertyChanged(nameof(PortStrokeThickness));
    }

    public void SetConnected(bool value)
    {
        if (_isConnected == value)
        {
            return;
        }

        _isConnected = value;
        RaisePropertyChanged(nameof(PortFill));
    }
}

public sealed class ShaderLinkViewModel : ViewModelBase
{
    private string _pathData = string.Empty;

    public ShaderLinkViewModel(ShaderPortViewModel from, ShaderPortViewModel to)
    {
        From = from;
        To = to;
        Stroke = from.PortColor;
        from.Node.PositionChanged += UpdatePath;
        to.Node.PositionChanged += UpdatePath;
        UpdatePath();
    }

    public ShaderPortViewModel From { get; }

    public ShaderPortViewModel To { get; }

    public string Stroke { get; }

    public string PathData
    {
        get => _pathData;
        private set
        {
            if (_pathData == value)
            {
                return;
            }

            _pathData = value;
            RaisePropertyChanged();
        }
    }

    private void UpdatePath()
    {
        var startX = From.Node.GetPortAnchorX(isInput: false);
        var startY = From.Node.GetPortAnchorY(From.Index);
        var endX = To.Node.GetPortAnchorX(isInput: true);
        var endY = To.Node.GetPortAnchorY(To.Index);
        var offset = Math.Max(40.0, Math.Abs(endX - startX) * 0.5);
        var c1X = startX + offset;
        var c1Y = startY;
        var c2X = endX - offset;
        var c2Y = endY;
        PathData = string.Format(CultureInfo.InvariantCulture,
            "M {0:0.##},{1:0.##} C {2:0.##},{3:0.##} {4:0.##},{5:0.##} {6:0.##},{7:0.##}",
            startX, startY, c1X, c1Y, c2X, c2Y, endX, endY);
    }
}

public sealed class ShaderSourceOption
{
    public ShaderSourceOption(string label, ShaderSourceMode mode)
    {
        Label = label;
        Mode = mode;
    }

    public string Label { get; }

    public ShaderSourceMode Mode { get; }
}

public enum ShaderSourceMode
{
    Constant,
    Graph
}

public sealed class TextureSlotOption
{
    public TextureSlotOption(string id, string label, Texture2D? texture, bool isCustom = false)
    {
        Id = id;
        Label = label;
        Texture = texture;
        IsCustom = isCustom;
    }

    public string Id { get; }

    public string Label { get; }

    public Texture2D? Texture { get; }

    public bool IsCustom { get; }
}

public sealed class TextureOption
{
    public TextureOption(string id, string label, Texture2D texture)
    {
        Id = id;
        Label = label;
        Texture = texture;
    }

    public string Id { get; }

    public string Label { get; }

    public Texture2D Texture { get; }
}

public static class TextureLibrary
{
    private static readonly IReadOnlyList<TextureOption> s_options = new[]
    {
        new TextureOption("checker", "Checkerboard", Texture2D.CreateCheckerboard(128, 128, new SKColor(54, 58, 64), new SKColor(28, 32, 40))),
        new TextureOption("flat-normal", "Flat Normal", CreateSolidTexture(128, 128, new SKColor(128, 128, 255))),
        new TextureOption("neutral-gray", "Neutral Gray", CreateSolidTexture(128, 128, new SKColor(128, 128, 128))),
        new TextureOption("white", "White", CreateSolidTexture(128, 128, new SKColor(235, 235, 235))),
        new TextureOption("black", "Black", CreateSolidTexture(128, 128, new SKColor(20, 20, 20))),
        new TextureOption("noise", "Noise", CreateNoiseTexture(128, 128, 1337))
    };

    public static IReadOnlyList<TextureOption> Options => s_options;

    public static TextureOption Default => s_options[0];

    public static TextureOption? GetById(string id)
    {
        return s_options.FirstOrDefault(option => option.Id == id);
    }

    private static Texture2D CreateSolidTexture(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        return new Texture2D(bitmap);
    }

    private static Texture2D CreateNoiseTexture(int width, int height, int seed)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var rng = new Random(seed);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var value = (byte)rng.Next(40, 220);
                bitmap.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }

        return new Texture2D(bitmap);
    }
}
