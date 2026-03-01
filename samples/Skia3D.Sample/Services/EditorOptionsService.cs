using System;
using System.ComponentModel;
using Skia3D.Core;
using Skia3D.Editor;
using Skia3D.IO;
using Skia3D.Modeling;
using Skia3D.Rendering;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Services;

public sealed class EditorOptionsService : IDisposable
{
    private readonly EditorSession _editor;
    private readonly Renderer3D _renderer;
    private readonly ViewportManagerService _viewportManager;
    private readonly InspectorOptionsViewModel _optionsViewModel;
    private bool _isApplying;
    private bool _isAttached;
    private int _defaultMeshSegments;
    private Action<int>? _meshPrecisionChanged;

    public EditorOptionsService(EditorSession editor,
        Renderer3D renderer,
        ViewportManagerService viewportManager,
        InspectorOptionsViewModel optionsViewModel)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _viewportManager = viewportManager ?? throw new ArgumentNullException(nameof(viewportManager));
        _optionsViewModel = optionsViewModel ?? throw new ArgumentNullException(nameof(optionsViewModel));
    }

    public event Action? EditModeApplied;

    public void Attach(Action<int> rebuildSampleScene, int defaultMeshSegments)
    {
        if (_isAttached)
        {
            return;
        }

        _meshPrecisionChanged = rebuildSampleScene ?? throw new ArgumentNullException(nameof(rebuildSampleScene));
        _defaultMeshSegments = defaultMeshSegments;
        _optionsViewModel.PropertyChanged += OnOptionsPropertyChanged;
        _isAttached = true;
    }

    public void ApplyOptions()
    {
        _renderer.UseDepthBuffer = _optionsViewModel.DepthEnabled;
        _renderer.EnableLighting = _optionsViewModel.LightingEnabled;
        _renderer.EnableImageBasedLighting = _optionsViewModel.ImageBasedLightingEnabled;
        _renderer.EnableBackfaceCulling = _optionsViewModel.BackfaceCullingEnabled;
        _renderer.ShowWireframe = _optionsViewModel.WireframeEnabled;
        _renderer.EnableShadows = _optionsViewModel.ShadowEnabled;
        _renderer.EnableSsao = _optionsViewModel.SsaoEnabled;
        var renderScale = (float)Math.Clamp(_optionsViewModel.RenderScale, 0.25, 1.0);
        _renderer.DepthRenderScale = renderScale;
        _optionsViewModel.RenderScale = renderScale;
        var maxWorkers = Math.Max(1, Environment.ProcessorCount);
        var workerCount = Math.Clamp(_optionsViewModel.RenderWorkerCount, 1, maxWorkers);
        _renderer.MaxWorkerCount = workerCount;
        _optionsViewModel.RenderWorkerCount = workerCount;
        var environmentIntensity = (float)Math.Clamp(_optionsViewModel.EnvironmentIntensity, 0.0, 2.0);
        _renderer.EnvironmentIntensity = environmentIntensity;
        _optionsViewModel.EnvironmentIntensity = environmentIntensity;
        var shadowMapSize = Math.Clamp(_optionsViewModel.ShadowMapSize, 64, 4096);
        _renderer.ShadowMapSize = shadowMapSize;
        _optionsViewModel.ShadowMapSize = shadowMapSize;

        var shadowBias = (float)Math.Clamp(_optionsViewModel.ShadowBias, 0.0, 0.02);
        _renderer.ShadowBias = shadowBias;
        _optionsViewModel.ShadowBias = shadowBias;

        var shadowNormalBias = (float)Math.Clamp(_optionsViewModel.ShadowNormalBias, 0.0, 0.02);
        _renderer.ShadowNormalBias = shadowNormalBias;
        _optionsViewModel.ShadowNormalBias = shadowNormalBias;

        var shadowStrength = (float)Math.Clamp(_optionsViewModel.ShadowStrength, 0.0, 1.0);
        _renderer.ShadowStrength = shadowStrength;
        _optionsViewModel.ShadowStrength = shadowStrength;

        var shadowPcfRadius = Math.Clamp(_optionsViewModel.ShadowPcfRadius, 0, 6);
        _renderer.ShadowPcfRadius = shadowPcfRadius;
        _optionsViewModel.ShadowPcfRadius = shadowPcfRadius;

        var ssaoRadius = (float)Math.Clamp(_optionsViewModel.SsaoRadius, 1.0, 64.0);
        _renderer.SsaoRadius = ssaoRadius;
        _optionsViewModel.SsaoRadius = ssaoRadius;

        var ssaoIntensity = (float)Math.Clamp(_optionsViewModel.SsaoIntensity, 0.0, 1.0);
        _renderer.SsaoIntensity = ssaoIntensity;
        _optionsViewModel.SsaoIntensity = ssaoIntensity;

        var ssaoDepthBias = (float)Math.Clamp(_optionsViewModel.SsaoDepthBias, 0.0, 0.1);
        _renderer.SsaoDepthBias = ssaoDepthBias;
        _optionsViewModel.SsaoDepthBias = ssaoDepthBias;

        var ssaoSamples = Math.Clamp(_optionsViewModel.SsaoSampleCount, 1, 32);
        _renderer.SsaoSampleCount = ssaoSamples;
        _optionsViewModel.SsaoSampleCount = ssaoSamples;

        var toneIndex = ClampIndex(_optionsViewModel.ToneMappingIndex, 0, 2, value => _optionsViewModel.ToneMappingIndex = value);
        var toneMapping = toneIndex switch
        {
            2 => ToneMappingMode.Aces,
            1 => ToneMappingMode.Reinhard,
            _ => ToneMappingMode.None
        };

        var exposure = (float)Math.Clamp(_optionsViewModel.PostExposure, 0.0, 4.0);
        _optionsViewModel.PostExposure = exposure;

        var bloomThreshold = (float)Math.Clamp(_optionsViewModel.BloomThreshold, 0.0, 1.0);
        _optionsViewModel.BloomThreshold = bloomThreshold;

        var bloomIntensity = (float)Math.Clamp(_optionsViewModel.BloomIntensity, 0.0, 2.0);
        _optionsViewModel.BloomIntensity = bloomIntensity;

        var bloomRadius = Math.Clamp(_optionsViewModel.BloomRadius, 0, 16);
        _optionsViewModel.BloomRadius = bloomRadius;

        var fxaaEdge = (float)Math.Clamp(_optionsViewModel.FxaaEdgeThreshold, 0.0, 1.0);
        _optionsViewModel.FxaaEdgeThreshold = fxaaEdge;

        var fxaaEdgeMin = (float)Math.Clamp(_optionsViewModel.FxaaEdgeThresholdMin, 0.0, 1.0);
        _optionsViewModel.FxaaEdgeThresholdMin = fxaaEdgeMin;

        var fxaaBlend = (float)Math.Clamp(_optionsViewModel.FxaaSubpixelBlend, 0.0, 1.0);
        _optionsViewModel.FxaaSubpixelBlend = fxaaBlend;

        for (int i = 0; i < _viewportManager.Viewports.Count; i++)
        {
            var viewport = _viewportManager.Viewports[i];
            viewport.ShowGrid = _optionsViewModel.GridEnabled;
            viewport.Pause = _optionsViewModel.PauseEnabled;
            viewport.ShowPickDebug = _optionsViewModel.PickDebugEnabled;
            viewport.ShowStats = _optionsViewModel.ShowStats;
            viewport.ShowUvSeams = _optionsViewModel.UvShowSeams;
            viewport.ShowUvIslands = _optionsViewModel.UvShowIslands;
            viewport.RenderSettings.EnableDepthPass = _optionsViewModel.DepthEnabled;
            viewport.RenderSettings.EnableShadowPass = _optionsViewModel.ShadowEnabled;
            viewport.RenderSettings.EnableMainPass = true;
            var post = viewport.RenderSettings.PostProcess;
            post.Enabled = _optionsViewModel.PostProcessingEnabled;
            post.ToneMapping = toneMapping;
            post.Exposure = exposure;
            post.Bloom.Enabled = _optionsViewModel.BloomEnabled;
            post.Bloom.Threshold = bloomThreshold;
            post.Bloom.Intensity = bloomIntensity;
            post.Bloom.Radius = bloomRadius;
            post.Fxaa.Enabled = _optionsViewModel.FxaaEnabled;
            post.Fxaa.EdgeThreshold = fxaaEdge;
            post.Fxaa.EdgeThresholdMin = fxaaEdgeMin;
            post.Fxaa.SubpixelBlend = fxaaBlend;
        }

        _editor.Gizmo.ShowGizmo = _optionsViewModel.GizmoVisible;
        _editor.Gizmo.SnapEnabled = _optionsViewModel.GizmoSnapEnabled;
        var modeIndex = ClampIndex(_optionsViewModel.GizmoModeIndex, 0, 2, value => _optionsViewModel.GizmoModeIndex = value);
        _editor.Gizmo.Mode = modeIndex switch
        {
            1 => GizmoMode.Rotate,
            2 => GizmoMode.Scale,
            _ => GizmoMode.Translate
        };

        var axisIndex = ClampIndex(_optionsViewModel.GizmoAxisIndex, 0, 6, value => _optionsViewModel.GizmoAxisIndex = value);
        _editor.Gizmo.AxisConstraint = axisIndex switch
        {
            1 => GizmoAxisConstraint.X,
            2 => GizmoAxisConstraint.Y,
            3 => GizmoAxisConstraint.Z,
            4 => GizmoAxisConstraint.XY,
            5 => GizmoAxisConstraint.XZ,
            6 => GizmoAxisConstraint.YZ,
            _ => GizmoAxisConstraint.None
        };

        var pickIndex = ClampIndex(_optionsViewModel.PickAccelIndex, 0, 2, value => _optionsViewModel.PickAccelIndex = value);
        _renderer.PickAcceleration = pickIndex switch
        {
            1 => Renderer3D.PickAccelerationMode.Bvh,
            2 => Renderer3D.PickAccelerationMode.UniformGrid,
            _ => Renderer3D.PickAccelerationMode.Auto
        };

        _renderer.CollectStats = _optionsViewModel.ShowStats;

        var samples = Math.Clamp(_optionsViewModel.Subdivision, 4, 512);
        _renderer.ProjectedPathSamples = samples;
        _optionsViewModel.Subdivision = samples;

        var snapStep = (float)Math.Clamp(_optionsViewModel.GizmoSnap, 0.05, 5.0);
        _editor.Gizmo.SnapStep = snapStep;
        _optionsViewModel.GizmoSnap = snapStep;

        var rotateSnap = (float)Math.Clamp(_optionsViewModel.GizmoRotateSnap, 1.0, 90.0);
        _editor.Gizmo.RotateSnapDegrees = rotateSnap;
        _optionsViewModel.GizmoRotateSnap = rotateSnap;

        var scaleSnap = (float)Math.Clamp(_optionsViewModel.GizmoScaleSnap, 0.01, 2.0);
        _editor.Gizmo.ScaleSnapStep = scaleSnap;
        _optionsViewModel.GizmoScaleSnap = scaleSnap;
    }

    public void ApplySelectionOptions()
    {
        var toolIndex = ClampIndex(_optionsViewModel.SelectionToolIndex, 0, 3, value => _optionsViewModel.SelectionToolIndex = value);
        _editor.SelectionOptions.Tool = toolIndex switch
        {
            1 => SelectionTool.Box,
            2 => SelectionTool.Paint,
            3 => SelectionTool.Lasso,
            _ => SelectionTool.Click
        };

        _editor.SelectionOptions.Crossing = _optionsViewModel.SelectionCrossing;

        var paintRadius = (float)Math.Clamp(_optionsViewModel.PaintRadius, 6.0, 200.0);
        _editor.SelectionOptions.PaintRadius = paintRadius;
        _optionsViewModel.PaintRadius = paintRadius;
        _viewportManager.InvalidateAll();
    }

    public void ApplyViewportView()
    {
        var viewIndex = ClampIndex(_optionsViewModel.ViewportViewIndex, 0, 6, value => _optionsViewModel.ViewportViewIndex = value);
        _viewportManager.PrimaryViewport.SetView(viewIndex);
        _viewportManager.PrimaryViewport.Invalidate();
    }

    public void ApplyNavigationMode()
    {
        for (int i = 0; i < _viewportManager.Viewports.Count; i++)
        {
            _viewportManager.Viewports[i].SetNavigationMode(_optionsViewModel.NavigationMode);
        }
    }

    public void ApplyModelingOptions()
    {
        var options = _editor.MeshEditOptions;
        var smoothIterations = Math.Clamp(_optionsViewModel.SmoothIterations, 1, 50);
        options.SmoothIterations = smoothIterations;
        _optionsViewModel.SmoothIterations = smoothIterations;

        var smoothStrength = (float)Math.Clamp(_optionsViewModel.SmoothStrength, 0.05, 1.0);
        options.SmoothStrength = smoothStrength;
        _optionsViewModel.SmoothStrength = smoothStrength;

        var simplifyRatio = (float)Math.Clamp(_optionsViewModel.SimplifyRatio, 0.1, 1.0);
        options.SimplifyRatio = simplifyRatio;
        _optionsViewModel.SimplifyRatio = simplifyRatio;

        var uvScale = (float)Math.Clamp(_optionsViewModel.UvScale, 0.1, 8.0);
        options.UvScale = uvScale;
        _optionsViewModel.UvScale = uvScale;

        var uvUnwrapAngle = (float)Math.Clamp(_optionsViewModel.UvUnwrapAngle, 1.0, 180.0);
        options.UvUnwrapAngle = uvUnwrapAngle;
        _optionsViewModel.UvUnwrapAngle = uvUnwrapAngle;

        var unwrapIndex = ClampIndex(_optionsViewModel.UvUnwrapMethodIndex, 0, 2, value => _optionsViewModel.UvUnwrapMethodIndex = value);
        options.UvUnwrapMethod = unwrapIndex switch
        {
            1 => UvUnwrapMethod.Lscm,
            2 => UvUnwrapMethod.Abf,
            _ => UvUnwrapMethod.Planar
        };

        var uvPackPadding = (float)Math.Clamp(_optionsViewModel.UvPackPadding, 0.0, 0.25);
        options.UvPackPadding = uvPackPadding;
        _optionsViewModel.UvPackPadding = uvPackPadding;

        options.UvPackRotate = _optionsViewModel.UvPackRotate;
        options.UvPackPreserveTexelDensity = _optionsViewModel.UvPackPreserveTexelDensity;

        var texelDensity = (float)Math.Clamp(_optionsViewModel.UvPackTexelDensity, 0.01, 50.0);
        options.UvPackTexelDensity = texelDensity;
        _optionsViewModel.UvPackTexelDensity = texelDensity;

        options.UvPackUseGroups = _optionsViewModel.UvPackUseGroups;

        options.ProportionalEnabled = _optionsViewModel.ProportionalEnabled;

        var falloffIndex = ClampIndex(_optionsViewModel.ProportionalFalloffIndex, 0, 3, value => _optionsViewModel.ProportionalFalloffIndex = value);
        options.ProportionalFalloff = falloffIndex switch
        {
            0 => ProportionalFalloff.Linear,
            2 => ProportionalFalloff.Sharp,
            3 => ProportionalFalloff.Root,
            _ => ProportionalFalloff.Smooth
        };

        var proportionalRadius = (float)Math.Clamp(_optionsViewModel.ProportionalRadius, 0.1, 50.0);
        options.ProportionalRadius = proportionalRadius;
        _optionsViewModel.ProportionalRadius = proportionalRadius;

        var extrudeDistance = (float)Math.Clamp(_optionsViewModel.ExtrudeDistance, 0.01, 10.0);
        _editor.MeshEdits.ExtrudeDistance = extrudeDistance;
        _optionsViewModel.ExtrudeDistance = extrudeDistance;

        var bevelInset = (float)Math.Clamp(_optionsViewModel.BevelInset, 0.0, 5.0);
        _editor.MeshEdits.BevelInset = bevelInset;
        _optionsViewModel.BevelInset = bevelInset;

        var bevelHeight = (float)Math.Clamp(_optionsViewModel.BevelHeight, 0.0, 5.0);
        _editor.MeshEdits.BevelHeight = bevelHeight;
        _optionsViewModel.BevelHeight = bevelHeight;

        var insetDistance = (float)Math.Clamp(_optionsViewModel.InsetDistance, 0.0, 5.0);
        _editor.MeshEdits.InsetDistance = insetDistance;
        _optionsViewModel.InsetDistance = insetDistance;

        var weldTolerance = (float)Math.Clamp(_optionsViewModel.WeldTolerance, 0.0001, 1.0);
        _editor.MeshEdits.WeldTolerance = weldTolerance;
        _optionsViewModel.WeldTolerance = weldTolerance;

        var nudgeStep = (float)Math.Clamp(_optionsViewModel.NudgeStep, 0.01, 5.0);
        _editor.MeshEdits.NudgeStep = nudgeStep;
        _optionsViewModel.NudgeStep = nudgeStep;
    }

    public void ApplyEditOptions()
    {
        var mode = _editor.Mode;
        var editMode = _optionsViewModel.EditMode;
        var vertex = _optionsViewModel.VertexMode;
        var edge = _optionsViewModel.EdgeMode;
        var face = _optionsViewModel.FaceMode;

        if (!editMode)
        {
            vertex = false;
            edge = false;
            face = false;
            _optionsViewModel.VertexMode = false;
            _optionsViewModel.EdgeMode = false;
            _optionsViewModel.FaceMode = false;
            _editor.Selection.ClearSubSelection();
        }
        else if (vertex)
        {
            edge = false;
            face = false;
            _optionsViewModel.EdgeMode = false;
            _optionsViewModel.FaceMode = false;
            _editor.Selection.EdgeSelection.Clear();
            _editor.Selection.FaceSelection.Clear();
        }
        else if (edge)
        {
            vertex = false;
            face = false;
            _optionsViewModel.VertexMode = false;
            _optionsViewModel.FaceMode = false;
            _editor.Selection.VertexSelection.Clear();
            _editor.Selection.FaceSelection.Clear();
        }
        else if (face)
        {
            vertex = false;
            edge = false;
            _optionsViewModel.VertexMode = false;
            _optionsViewModel.EdgeMode = false;
            _editor.Selection.VertexSelection.Clear();
            _editor.Selection.EdgeSelection.Clear();
        }

        mode.EditMode = editMode;
        mode.VertexSelect = vertex;
        mode.EdgeSelect = edge;
        mode.FaceSelect = face;

        EditModeApplied?.Invoke();
    }

    public void SyncEditModeToggles()
    {
        var mode = _editor.Mode;
        ApplyWithGuard(() =>
        {
            _optionsViewModel.EditMode = mode.EditMode;
            _optionsViewModel.VertexMode = mode.VertexSelect;
            _optionsViewModel.EdgeMode = mode.EdgeSelect;
            _optionsViewModel.FaceMode = mode.FaceSelect;
        });
    }

    public void SetSelectionMode(bool editMode, bool vertex, bool edge, bool face)
    {
        ApplyWithGuard(() =>
        {
            _optionsViewModel.EditMode = editMode;
            _optionsViewModel.VertexMode = vertex;
            _optionsViewModel.EdgeMode = edge;
            _optionsViewModel.FaceMode = face;
            ApplyEditOptions();
        });
    }

    public void SetSelectionTool(SelectionTool tool)
    {
        ApplyWithGuard(() =>
        {
            _optionsViewModel.SelectionToolIndex = tool switch
            {
                SelectionTool.Box => 1,
                SelectionTool.Paint => 2,
                SelectionTool.Lasso => 3,
                _ => 0
            };
            ApplySelectionOptions();
        });
        _viewportManager.InvalidateAll();
    }

    public void ToggleSelectionCrossing()
    {
        ApplyWithGuard(() =>
        {
            _optionsViewModel.SelectionCrossing = !_optionsViewModel.SelectionCrossing;
            ApplySelectionOptions();
        });
        _viewportManager.InvalidateAll();
    }

    public void ApplyImportOptions()
    {
        var radius = Math.Clamp(_optionsViewModel.ImportScaleRadius, 0.1, 10.0);
        _optionsViewModel.ImportScaleRadius = radius;
    }

    public MeshProcessingOptions? BuildImportOptions()
    {
        if (!_optionsViewModel.ImportCenter && !_optionsViewModel.ImportNormals && !_optionsViewModel.ImportScaleEnabled)
        {
            return null;
        }

        return new MeshProcessingOptions
        {
            CenterToOrigin = _optionsViewModel.ImportCenter,
            RecalculateNormals = _optionsViewModel.ImportNormals,
            ScaleToRadius = _optionsViewModel.ImportScaleEnabled ? (float?)_optionsViewModel.ImportScaleRadius : null
        };
    }

    public int GetMeshSegments(int defaultSegments)
    {
        var segments = _optionsViewModel.MeshPrecision;
        if (segments <= 0)
        {
            return defaultSegments;
        }

        return Math.Clamp(segments, 4, 128);
    }

    private static int ClampIndex(int value, int min, int max, Action<int> update)
    {
        var clamped = Math.Clamp(value, min, max);
        if (clamped != value)
        {
            update(clamped);
        }

        return clamped;
    }

    private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplying)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            ApplyWithGuard(() =>
            {
                ApplyOptions();
                ApplySelectionOptions();
                ApplyModelingOptions();
                ApplyImportOptions();
                ApplyEditOptions();
                ApplyViewportView();
                ApplyNavigationMode();
            });
            _viewportManager.InvalidateAll();
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(InspectorOptionsViewModel.MeshPrecision):
                HandleMeshPrecisionChanged();
                break;
            case nameof(InspectorOptionsViewModel.DepthEnabled):
            case nameof(InspectorOptionsViewModel.LightingEnabled):
            case nameof(InspectorOptionsViewModel.ImageBasedLightingEnabled):
            case nameof(InspectorOptionsViewModel.BackfaceCullingEnabled):
            case nameof(InspectorOptionsViewModel.EnvironmentIntensity):
            case nameof(InspectorOptionsViewModel.WireframeEnabled):
            case nameof(InspectorOptionsViewModel.ShadowEnabled):
            case nameof(InspectorOptionsViewModel.SsaoEnabled):
            case nameof(InspectorOptionsViewModel.RenderScale):
            case nameof(InspectorOptionsViewModel.RenderWorkerCount):
            case nameof(InspectorOptionsViewModel.ShadowMapSize):
            case nameof(InspectorOptionsViewModel.ShadowBias):
            case nameof(InspectorOptionsViewModel.ShadowNormalBias):
            case nameof(InspectorOptionsViewModel.ShadowStrength):
            case nameof(InspectorOptionsViewModel.ShadowPcfRadius):
            case nameof(InspectorOptionsViewModel.SsaoRadius):
            case nameof(InspectorOptionsViewModel.SsaoIntensity):
            case nameof(InspectorOptionsViewModel.SsaoDepthBias):
            case nameof(InspectorOptionsViewModel.SsaoSampleCount):
            case nameof(InspectorOptionsViewModel.PostProcessingEnabled):
            case nameof(InspectorOptionsViewModel.ToneMappingIndex):
            case nameof(InspectorOptionsViewModel.PostExposure):
            case nameof(InspectorOptionsViewModel.BloomEnabled):
            case nameof(InspectorOptionsViewModel.BloomThreshold):
            case nameof(InspectorOptionsViewModel.BloomIntensity):
            case nameof(InspectorOptionsViewModel.BloomRadius):
            case nameof(InspectorOptionsViewModel.FxaaEnabled):
            case nameof(InspectorOptionsViewModel.FxaaEdgeThreshold):
            case nameof(InspectorOptionsViewModel.FxaaEdgeThresholdMin):
            case nameof(InspectorOptionsViewModel.FxaaSubpixelBlend):
            case nameof(InspectorOptionsViewModel.GridEnabled):
            case nameof(InspectorOptionsViewModel.PauseEnabled):
            case nameof(InspectorOptionsViewModel.PickDebugEnabled):
            case nameof(InspectorOptionsViewModel.ShowStats):
            case nameof(InspectorOptionsViewModel.GizmoVisible):
            case nameof(InspectorOptionsViewModel.GizmoSnapEnabled):
            case nameof(InspectorOptionsViewModel.GizmoAxisIndex):
            case nameof(InspectorOptionsViewModel.GizmoModeIndex):
            case nameof(InspectorOptionsViewModel.PickAccelIndex):
            case nameof(InspectorOptionsViewModel.Subdivision):
            case nameof(InspectorOptionsViewModel.GizmoSnap):
            case nameof(InspectorOptionsViewModel.GizmoRotateSnap):
            case nameof(InspectorOptionsViewModel.GizmoScaleSnap):
            case nameof(InspectorOptionsViewModel.UvShowSeams):
            case nameof(InspectorOptionsViewModel.UvShowIslands):
                ApplyWithGuard(ApplyOptions);
                _viewportManager.InvalidateAll();
                break;
            case nameof(InspectorOptionsViewModel.SelectionToolIndex):
            case nameof(InspectorOptionsViewModel.SelectionCrossing):
            case nameof(InspectorOptionsViewModel.PaintRadius):
                ApplyWithGuard(ApplySelectionOptions);
                _viewportManager.InvalidateAll();
                break;
            case nameof(InspectorOptionsViewModel.ViewportViewIndex):
                ApplyWithGuard(ApplyViewportView);
                break;
            case nameof(InspectorOptionsViewModel.NavigationMode):
                ApplyWithGuard(ApplyNavigationMode);
                break;
            case nameof(InspectorOptionsViewModel.EditMode):
            case nameof(InspectorOptionsViewModel.VertexMode):
            case nameof(InspectorOptionsViewModel.EdgeMode):
            case nameof(InspectorOptionsViewModel.FaceMode):
                ApplyWithGuard(ApplyEditOptions);
                break;
            case nameof(InspectorOptionsViewModel.SmoothIterations):
            case nameof(InspectorOptionsViewModel.SmoothStrength):
            case nameof(InspectorOptionsViewModel.SimplifyRatio):
            case nameof(InspectorOptionsViewModel.UvScale):
            case nameof(InspectorOptionsViewModel.UvUnwrapAngle):
            case nameof(InspectorOptionsViewModel.UvUnwrapMethodIndex):
            case nameof(InspectorOptionsViewModel.UvPackPadding):
            case nameof(InspectorOptionsViewModel.UvPackRotate):
            case nameof(InspectorOptionsViewModel.UvPackPreserveTexelDensity):
            case nameof(InspectorOptionsViewModel.UvPackTexelDensity):
            case nameof(InspectorOptionsViewModel.UvPackUseGroups):
            case nameof(InspectorOptionsViewModel.ProportionalEnabled):
            case nameof(InspectorOptionsViewModel.ProportionalFalloffIndex):
            case nameof(InspectorOptionsViewModel.ProportionalRadius):
            case nameof(InspectorOptionsViewModel.ExtrudeDistance):
            case nameof(InspectorOptionsViewModel.BevelInset):
            case nameof(InspectorOptionsViewModel.BevelHeight):
            case nameof(InspectorOptionsViewModel.InsetDistance):
            case nameof(InspectorOptionsViewModel.WeldTolerance):
            case nameof(InspectorOptionsViewModel.NudgeStep):
                ApplyWithGuard(ApplyModelingOptions);
                break;
            case nameof(InspectorOptionsViewModel.ImportCenter):
            case nameof(InspectorOptionsViewModel.ImportNormals):
            case nameof(InspectorOptionsViewModel.ImportScaleEnabled):
            case nameof(InspectorOptionsViewModel.ImportScaleRadius):
                ApplyWithGuard(ApplyImportOptions);
                break;
        }
    }

    private void HandleMeshPrecisionChanged()
    {
        var segments = GetMeshSegments(_defaultMeshSegments);
        ApplyWithGuard(() => _optionsViewModel.MeshPrecision = segments);
        _meshPrecisionChanged?.Invoke(segments);
    }

    private void ApplyWithGuard(Action action)
    {
        if (_isApplying)
        {
            return;
        }

        _isApplying = true;
        try
        {
            action();
        }
        finally
        {
            _isApplying = false;
        }
    }

    public void Dispose()
    {
        if (!_isAttached)
        {
            return;
        }

        _optionsViewModel.PropertyChanged -= OnOptionsPropertyChanged;
        _isAttached = false;
    }
}
