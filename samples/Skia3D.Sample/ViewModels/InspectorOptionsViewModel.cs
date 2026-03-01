using System;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public enum ViewportNavigationMode
{
    None,
    Pan,
    Orbit,
    Zoom
}

public sealed class InspectorOptionsViewModel : ViewModelBase
{
    private static readonly string[] SelectionToolLabels = { "Click", "Box", "Paint", "Lasso" };
    private static readonly string[] GizmoModeLabels = { "Move", "Rotate", "Scale" };
    private static readonly string[] GizmoAxisLabels = { "Free", "X", "Y", "Z", "XY", "XZ", "YZ" };
    private static readonly string[] ViewportViewLabels = { "Perspective", "Top", "Front", "Left", "Right", "Back", "Bottom" };

    private double _paintRadius = 24.0;
    private double _gizmoSnap = 0.5;
    private double _gizmoRotateSnap = 15.0;
    private double _gizmoScaleSnap = 0.1;
    private bool _depthEnabled;
    private bool _lightingEnabled = true;
    private bool _imageBasedLightingEnabled = true;
    private bool _backfaceCullingEnabled = true;
    private double _environmentIntensity = 0.35;
    private bool _wireframeEnabled;
    private bool _shadowEnabled;
    private bool _ssaoEnabled;
    private int _shadowMapSize = 512;
    private double _shadowBias = 0.0025;
    private double _shadowNormalBias = 0.0015;
    private double _shadowStrength = 0.7;
    private int _shadowPcfRadius = 1;
    private double _ssaoRadius = 6.0;
    private double _ssaoIntensity = 0.6;
    private double _ssaoDepthBias = 0.002;
    private int _ssaoSampleCount = 8;
    private bool _postProcessingEnabled;
    private int _toneMappingIndex = 1;
    private double _postExposure = 1.0;
    private bool _bloomEnabled;
    private double _bloomThreshold = 0.75;
    private double _bloomIntensity = 0.6;
    private int _bloomRadius = 6;
    private bool _fxaaEnabled;
    private double _fxaaEdgeThreshold = 0.125;
    private double _fxaaEdgeThresholdMin = 0.0312;
    private double _fxaaSubpixelBlend = 0.75;
    private bool _gridEnabled = true;
    private bool _pauseEnabled;
    private bool _pickDebugEnabled;
    private bool _showStats;
    private bool _gizmoVisible = true;
    private bool _gizmoSnapEnabled;
    private int _gizmoAxisIndex;
    private int _gizmoModeIndex;
    private int _selectionToolIndex;
    private bool _selectionCrossing;
    private bool _editMode;
    private bool _vertexMode;
    private bool _edgeMode;
    private bool _faceMode;
    private bool _proportionalEnabled;
    private int _proportionalFalloffIndex = 1;
    private bool _importCenter = true;
    private bool _importNormals;
    private bool _importScaleEnabled;
    private double _importScaleRadius = 1.0;
    private int _pickAccelIndex;
    private int _smoothIterations = 2;
    private double _smoothStrength = 0.6;
    private double _simplifyRatio = 0.5;
    private double _uvScale = 1.0;
    private double _uvUnwrapAngle = 45.0;
    private int _uvUnwrapMethodIndex = 1;
    private double _uvPackPadding = 0.02;
    private bool _uvPackRotate = true;
    private bool _uvPackPreserveTexelDensity = true;
    private double _uvPackTexelDensity = 1.0;
    private bool _uvPackUseGroups = true;
    private bool _uvShowSeams = true;
    private bool _uvShowIslands;
    private int _uvGroupId = 1;
    private double _proportionalRadius = 2.0;
    private double _extrudeDistance = 0.6;
    private double _bevelInset = 0.2;
    private double _bevelHeight = 0.25;
    private double _insetDistance = 0.2;
    private double _weldTolerance = 0.02;
    private double _nudgeStep = 0.2;
    private double _renderScale = 1.0;
    private int _renderWorkerCount = 8;
    private int _meshPrecision = 24;
    private int _subdivision = 64;
    private int _viewportViewIndex;
    private ViewportNavigationMode _navigationMode;

    public InspectorOptionsViewModel()
    {
        SetSelectionToolCommand = new DelegateCommand<object?>(value => SelectionToolIndex = ParseIndex(value));
        SetGizmoModeCommand = new DelegateCommand<object?>(value => GizmoModeIndex = ParseIndex(value));
        SetGizmoAxisCommand = new DelegateCommand<object?>(value => GizmoAxisIndex = ParseIndex(value));
        SetSelectionModeCommand = new DelegateCommand<object?>(value => SetSelectionMode(ParseIndex(value)));
        SetViewportViewCommand = new DelegateCommand<object?>(value => ViewportViewIndex = ParseIndex(value));
        SetViewportShadingCommand = new DelegateCommand<object?>(value => ViewportShadingIndex = ParseIndex(value));
        SetPickAccelCommand = new DelegateCommand<object?>(value => PickAccelIndex = ParseIndex(value));
        SetToneMappingCommand = new DelegateCommand<object?>(value => ToneMappingIndex = ParseIndex(value));
        ToggleSelectionCrossingCommand = new DelegateCommand(() => SelectionCrossing = !SelectionCrossing);
        ToggleGridCommand = new DelegateCommand(() => GridEnabled = !GridEnabled);
        ToggleWireframeCommand = new DelegateCommand(() => WireframeEnabled = !WireframeEnabled);
        ToggleDepthCommand = new DelegateCommand(() => DepthEnabled = !DepthEnabled);
        ToggleLightingCommand = new DelegateCommand(() => LightingEnabled = !LightingEnabled);
        TogglePauseCommand = new DelegateCommand(() => PauseEnabled = !PauseEnabled);
        ToggleStatsCommand = new DelegateCommand(() => ShowStats = !ShowStats);
        ToggleGizmoVisibilityCommand = new DelegateCommand(() => GizmoVisible = !GizmoVisible);
        ToggleGizmoSnapCommand = new DelegateCommand(() => GizmoSnapEnabled = !GizmoSnapEnabled);
    }

    public DelegateCommand<object?> SetSelectionToolCommand { get; }

    public DelegateCommand<object?> SetGizmoModeCommand { get; }

    public DelegateCommand<object?> SetGizmoAxisCommand { get; }

    public DelegateCommand<object?> SetSelectionModeCommand { get; }

    public DelegateCommand<object?> SetViewportViewCommand { get; }

    public DelegateCommand<object?> SetViewportShadingCommand { get; }

    public DelegateCommand<object?> SetPickAccelCommand { get; }

    public DelegateCommand<object?> SetToneMappingCommand { get; }

    public DelegateCommand ToggleSelectionCrossingCommand { get; }

    public DelegateCommand ToggleGridCommand { get; }

    public DelegateCommand ToggleWireframeCommand { get; }

    public DelegateCommand ToggleDepthCommand { get; }

    public DelegateCommand ToggleLightingCommand { get; }

    public DelegateCommand TogglePauseCommand { get; }

    public DelegateCommand ToggleStatsCommand { get; }

    public DelegateCommand ToggleGizmoVisibilityCommand { get; }

    public DelegateCommand ToggleGizmoSnapCommand { get; }

    public double PaintRadius
    {
        get => _paintRadius;
        set
        {
            if (Math.Abs(_paintRadius - value) < 1e-6)
            {
                return;
            }

            _paintRadius = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(PaintRadiusLabel));
        }
    }

    public string PaintRadiusLabel => $"Paint radius: {PaintRadius:0}";

    public double GizmoSnap
    {
        get => _gizmoSnap;
        set
        {
            if (Math.Abs(_gizmoSnap - value) < 1e-6)
            {
                return;
            }

            _gizmoSnap = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(GizmoSnapLabel));
            RaisePropertyChanged(nameof(ViewportHudLabel));
            RaisePropertyChanged(nameof(ViewportOverlayStatusLabel));
        }
    }

    public string GizmoSnapLabel => $"Move snap: {GizmoSnap:0.0}";

    public double GizmoRotateSnap
    {
        get => _gizmoRotateSnap;
        set
        {
            if (Math.Abs(_gizmoRotateSnap - value) < 1e-6)
            {
                return;
            }

            _gizmoRotateSnap = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(GizmoRotateSnapLabel));
        }
    }

    public string GizmoRotateSnapLabel => $"Rotate snap: {GizmoRotateSnap:0}\u00B0";

    public double GizmoScaleSnap
    {
        get => _gizmoScaleSnap;
        set
        {
            if (Math.Abs(_gizmoScaleSnap - value) < 1e-6)
            {
                return;
            }

            _gizmoScaleSnap = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(GizmoScaleSnapLabel));
        }
    }

    public string GizmoScaleSnapLabel => $"Scale snap: {GizmoScaleSnap:0.00}";

    public bool DepthEnabled
    {
        get => _depthEnabled;
        set
        {
            if (_depthEnabled == value)
            {
                return;
            }

            _depthEnabled = value;
            RaisePropertyChanged();
        }
    }

    public bool LightingEnabled
    {
        get => _lightingEnabled;
        set
        {
            if (_lightingEnabled == value)
            {
                return;
            }

            _lightingEnabled = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportShadingIndex));
            RaisePropertyChanged(nameof(ViewportShadingLabel));
            RaisePropertyChanged(nameof(ViewportTitleLabel));
        }
    }

    public bool ImageBasedLightingEnabled
    {
        get => _imageBasedLightingEnabled;
        set
        {
            if (_imageBasedLightingEnabled == value)
            {
                return;
            }

            _imageBasedLightingEnabled = value;
            RaisePropertyChanged();
        }
    }

    public bool BackfaceCullingEnabled
    {
        get => _backfaceCullingEnabled;
        set
        {
            if (_backfaceCullingEnabled == value)
            {
                return;
            }

            _backfaceCullingEnabled = value;
            RaisePropertyChanged();
        }
    }

    public double EnvironmentIntensity
    {
        get => _environmentIntensity;
        set
        {
            if (Math.Abs(_environmentIntensity - value) < 1e-6)
            {
                return;
            }

            _environmentIntensity = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(EnvironmentIntensityLabel));
        }
    }

    public string EnvironmentIntensityLabel => $"Environment intensity: {EnvironmentIntensity:0.00}";

    public bool WireframeEnabled
    {
        get => _wireframeEnabled;
        set
        {
            if (_wireframeEnabled == value)
            {
                return;
            }

            _wireframeEnabled = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(WireframeStateLabel));
            RaisePropertyChanged(nameof(ViewportShadingIndex));
            RaisePropertyChanged(nameof(ViewportShadingLabel));
            RaisePropertyChanged(nameof(ViewportTitleLabel));
        }
    }

    public string WireframeStateLabel => WireframeEnabled ? "On" : "Off";

    public bool ShadowEnabled
    {
        get => _shadowEnabled;
        set
        {
            if (_shadowEnabled == value)
            {
                return;
            }

            _shadowEnabled = value;
            RaisePropertyChanged();
        }
    }

    public bool SsaoEnabled
    {
        get => _ssaoEnabled;
        set
        {
            if (_ssaoEnabled == value)
            {
                return;
            }

            _ssaoEnabled = value;
            RaisePropertyChanged();
        }
    }

    public int ShadowMapSize
    {
        get => _shadowMapSize;
        set
        {
            if (_shadowMapSize == value)
            {
                return;
            }

            _shadowMapSize = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShadowMapSizeLabel));
        }
    }

    public string ShadowMapSizeLabel => $"Shadow map: {ShadowMapSize}";

    public double ShadowBias
    {
        get => _shadowBias;
        set
        {
            if (Math.Abs(_shadowBias - value) < 1e-6)
            {
                return;
            }

            _shadowBias = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShadowBiasLabel));
        }
    }

    public string ShadowBiasLabel => $"Shadow bias: {ShadowBias:0.0000}";

    public double ShadowNormalBias
    {
        get => _shadowNormalBias;
        set
        {
            if (Math.Abs(_shadowNormalBias - value) < 1e-6)
            {
                return;
            }

            _shadowNormalBias = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShadowNormalBiasLabel));
        }
    }

    public string ShadowNormalBiasLabel => $"Normal bias: {ShadowNormalBias:0.0000}";

    public double ShadowStrength
    {
        get => _shadowStrength;
        set
        {
            if (Math.Abs(_shadowStrength - value) < 1e-6)
            {
                return;
            }

            _shadowStrength = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShadowStrengthLabel));
        }
    }

    public string ShadowStrengthLabel => $"Shadow strength: {ShadowStrength:0.00}";

    public int ShadowPcfRadius
    {
        get => _shadowPcfRadius;
        set
        {
            if (_shadowPcfRadius == value)
            {
                return;
            }

            _shadowPcfRadius = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShadowPcfRadiusLabel));
        }
    }

    public string ShadowPcfRadiusLabel => $"PCF radius: {ShadowPcfRadius}";

    public double SsaoRadius
    {
        get => _ssaoRadius;
        set
        {
            if (Math.Abs(_ssaoRadius - value) < 1e-6)
            {
                return;
            }

            _ssaoRadius = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SsaoRadiusLabel));
        }
    }

    public string SsaoRadiusLabel => $"SSAO radius: {SsaoRadius:0.0}";

    public double SsaoIntensity
    {
        get => _ssaoIntensity;
        set
        {
            if (Math.Abs(_ssaoIntensity - value) < 1e-6)
            {
                return;
            }

            _ssaoIntensity = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SsaoIntensityLabel));
        }
    }

    public string SsaoIntensityLabel => $"SSAO intensity: {SsaoIntensity:0.00}";

    public double SsaoDepthBias
    {
        get => _ssaoDepthBias;
        set
        {
            if (Math.Abs(_ssaoDepthBias - value) < 1e-6)
            {
                return;
            }

            _ssaoDepthBias = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SsaoDepthBiasLabel));
        }
    }

    public string SsaoDepthBiasLabel => $"SSAO bias: {SsaoDepthBias:0.0000}";

    public int SsaoSampleCount
    {
        get => _ssaoSampleCount;
        set
        {
            if (_ssaoSampleCount == value)
            {
                return;
            }

            _ssaoSampleCount = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SsaoSampleCountLabel));
        }
    }

    public string SsaoSampleCountLabel => $"SSAO samples: {SsaoSampleCount}";

    public bool PostProcessingEnabled
    {
        get => _postProcessingEnabled;
        set
        {
            if (_postProcessingEnabled == value)
            {
                return;
            }

            _postProcessingEnabled = value;
            RaisePropertyChanged();
        }
    }

    public int ToneMappingIndex
    {
        get => _toneMappingIndex;
        set
        {
            if (_toneMappingIndex == value)
            {
                return;
            }

            _toneMappingIndex = value;
            RaisePropertyChanged();
        }
    }

    public double PostExposure
    {
        get => _postExposure;
        set
        {
            if (Math.Abs(_postExposure - value) < 1e-6)
            {
                return;
            }

            _postExposure = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(PostExposureLabel));
        }
    }

    public string PostExposureLabel => $"Exposure: {PostExposure:0.00}";

    public bool BloomEnabled
    {
        get => _bloomEnabled;
        set
        {
            if (_bloomEnabled == value)
            {
                return;
            }

            _bloomEnabled = value;
            RaisePropertyChanged();
        }
    }

    public double BloomThreshold
    {
        get => _bloomThreshold;
        set
        {
            if (Math.Abs(_bloomThreshold - value) < 1e-6)
            {
                return;
            }

            _bloomThreshold = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(BloomThresholdLabel));
        }
    }

    public string BloomThresholdLabel => $"Bloom threshold: {BloomThreshold:0.00}";

    public double BloomIntensity
    {
        get => _bloomIntensity;
        set
        {
            if (Math.Abs(_bloomIntensity - value) < 1e-6)
            {
                return;
            }

            _bloomIntensity = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(BloomIntensityLabel));
        }
    }

    public string BloomIntensityLabel => $"Bloom intensity: {BloomIntensity:0.00}";

    public int BloomRadius
    {
        get => _bloomRadius;
        set
        {
            if (_bloomRadius == value)
            {
                return;
            }

            _bloomRadius = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(BloomRadiusLabel));
        }
    }

    public string BloomRadiusLabel => $"Bloom radius: {BloomRadius}";

    public bool FxaaEnabled
    {
        get => _fxaaEnabled;
        set
        {
            if (_fxaaEnabled == value)
            {
                return;
            }

            _fxaaEnabled = value;
            RaisePropertyChanged();
        }
    }

    public double FxaaEdgeThreshold
    {
        get => _fxaaEdgeThreshold;
        set
        {
            if (Math.Abs(_fxaaEdgeThreshold - value) < 1e-6)
            {
                return;
            }

            _fxaaEdgeThreshold = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(FxaaEdgeThresholdLabel));
        }
    }

    public string FxaaEdgeThresholdLabel => $"FXAA edge: {FxaaEdgeThreshold:0.000}";

    public double FxaaEdgeThresholdMin
    {
        get => _fxaaEdgeThresholdMin;
        set
        {
            if (Math.Abs(_fxaaEdgeThresholdMin - value) < 1e-6)
            {
                return;
            }

            _fxaaEdgeThresholdMin = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(FxaaEdgeThresholdMinLabel));
        }
    }

    public string FxaaEdgeThresholdMinLabel => $"FXAA edge min: {FxaaEdgeThresholdMin:0.000}";

    public double FxaaSubpixelBlend
    {
        get => _fxaaSubpixelBlend;
        set
        {
            if (Math.Abs(_fxaaSubpixelBlend - value) < 1e-6)
            {
                return;
            }

            _fxaaSubpixelBlend = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(FxaaSubpixelBlendLabel));
        }
    }

    public string FxaaSubpixelBlendLabel => $"FXAA blend: {FxaaSubpixelBlend:0.00}";

    public bool GridEnabled
    {
        get => _gridEnabled;
        set
        {
            if (_gridEnabled == value)
            {
                return;
            }

            _gridEnabled = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(GridStateLabel));
            RaisePropertyChanged(nameof(ViewportOverlayStatusLabel));
        }
    }

    public string GridStateLabel => GridEnabled ? "On" : "Off";

    public bool PauseEnabled
    {
        get => _pauseEnabled;
        set
        {
            if (_pauseEnabled == value)
            {
                return;
            }

            _pauseEnabled = value;
            RaisePropertyChanged();
        }
    }

    public bool PickDebugEnabled
    {
        get => _pickDebugEnabled;
        set
        {
            if (_pickDebugEnabled == value)
            {
                return;
            }

            _pickDebugEnabled = value;
            RaisePropertyChanged();
        }
    }

    public bool ShowStats
    {
        get => _showStats;
        set
        {
            if (_showStats == value)
            {
                return;
            }

            _showStats = value;
            RaisePropertyChanged();
        }
    }

    public bool GizmoVisible
    {
        get => _gizmoVisible;
        set
        {
            if (_gizmoVisible == value)
            {
                return;
            }

            _gizmoVisible = value;
            RaisePropertyChanged();
        }
    }

    public bool GizmoSnapEnabled
    {
        get => _gizmoSnapEnabled;
        set
        {
            if (_gizmoSnapEnabled == value)
            {
                return;
            }

            _gizmoSnapEnabled = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportHudLabel));
            RaisePropertyChanged(nameof(SnapStateLabel));
            RaisePropertyChanged(nameof(ViewportOverlayStatusLabel));
        }
    }

    public string SnapStateLabel => GizmoSnapEnabled ? "On" : "Off";

    public int GizmoAxisIndex
    {
        get => _gizmoAxisIndex;
        set
        {
            if (_gizmoAxisIndex == value)
            {
                return;
            }

            _gizmoAxisIndex = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public int GizmoModeIndex
    {
        get => _gizmoModeIndex;
        set
        {
            if (_gizmoModeIndex == value)
            {
                return;
            }

            _gizmoModeIndex = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public int SelectionToolIndex
    {
        get => _selectionToolIndex;
        set
        {
            if (_selectionToolIndex == value)
            {
                return;
            }

            _selectionToolIndex = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SelectionToolLabel));
            RaisePropertyChanged(nameof(SelectionToolStatusLabel));
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public string SelectionToolLabel => BuildSelectionToolLabel();

    public string SelectionToolStatusLabel => $"Active: {SelectionToolLabel}";

    public bool SelectionCrossing
    {
        get => _selectionCrossing;
        set
        {
            if (_selectionCrossing == value)
            {
                return;
            }

            _selectionCrossing = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SelectionToolLabel));
            RaisePropertyChanged(nameof(SelectionToolStatusLabel));
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public int ViewportViewIndex
    {
        get => _viewportViewIndex;
        set
        {
            if (_viewportViewIndex == value)
            {
                return;
            }

            _viewportViewIndex = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportViewLabel));
            RaisePropertyChanged(nameof(ViewportTitleLabel));
        }
    }

    public string ViewportViewLabel => BuildViewportViewLabel();

    public int ViewportShadingIndex
    {
        get => WireframeEnabled ? 1 : LightingEnabled ? 0 : 2;
        set
        {
            switch (value)
            {
                case 1:
                    WireframeEnabled = true;
                    LightingEnabled = true;
                    break;
                case 2:
                    WireframeEnabled = false;
                    LightingEnabled = false;
                    break;
                default:
                    WireframeEnabled = false;
                    LightingEnabled = true;
                    break;
            }

            RaisePropertyChanged();
        }
    }

    public string ViewportShadingLabel => WireframeEnabled ? "Wireframe" : LightingEnabled ? "Default Shading" : "Unlit";

    public string ViewportTitleLabel => $"{ViewportViewLabel} | {ViewportShadingLabel}";

    public string ViewportOverlayStatusLabel => BuildViewportOverlayStatusLabel();

    public ViewportNavigationMode NavigationMode
    {
        get => _navigationMode;
        set
        {
            if (_navigationMode == value)
            {
                return;
            }

            _navigationMode = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsNavDefault));
            RaisePropertyChanged(nameof(IsNavPan));
            RaisePropertyChanged(nameof(IsNavOrbit));
            RaisePropertyChanged(nameof(IsNavZoom));
        }
    }

    public bool IsNavDefault
    {
        get => NavigationMode == ViewportNavigationMode.None;
        set
        {
            if (value)
            {
                NavigationMode = ViewportNavigationMode.None;
            }
        }
    }

    public bool IsNavPan
    {
        get => NavigationMode == ViewportNavigationMode.Pan;
        set
        {
            if (value)
            {
                NavigationMode = ViewportNavigationMode.Pan;
            }
            else if (NavigationMode == ViewportNavigationMode.Pan)
            {
                NavigationMode = ViewportNavigationMode.None;
            }
        }
    }

    public bool IsNavOrbit
    {
        get => NavigationMode == ViewportNavigationMode.Orbit;
        set
        {
            if (value)
            {
                NavigationMode = ViewportNavigationMode.Orbit;
            }
            else if (NavigationMode == ViewportNavigationMode.Orbit)
            {
                NavigationMode = ViewportNavigationMode.None;
            }
        }
    }

    public bool IsNavZoom
    {
        get => NavigationMode == ViewportNavigationMode.Zoom;
        set
        {
            if (value)
            {
                NavigationMode = ViewportNavigationMode.Zoom;
            }
            else if (NavigationMode == ViewportNavigationMode.Zoom)
            {
                NavigationMode = ViewportNavigationMode.None;
            }
        }
    }

    public bool EditMode
    {
        get => _editMode;
        set
        {
            if (_editMode == value)
            {
                return;
            }

            _editMode = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public bool VertexMode
    {
        get => _vertexMode;
        set
        {
            if (_vertexMode == value)
            {
                return;
            }

            _vertexMode = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public bool EdgeMode
    {
        get => _edgeMode;
        set
        {
            if (_edgeMode == value)
            {
                return;
            }

            _edgeMode = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public bool FaceMode
    {
        get => _faceMode;
        set
        {
            if (_faceMode == value)
            {
                return;
            }

            _faceMode = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ViewportHudLabel));
        }
    }

    public string ViewportHudLabel => BuildViewportHudLabel();

    private string BuildViewportHudLabel()
    {
        var tool = BuildSelectionToolLabel();

        var mode = !EditMode
            ? "Object"
            : VertexMode ? "Vertex"
            : EdgeMode ? "Edge"
            : FaceMode ? "Face"
            : "Object";

        var gizmoMode = GizmoModeIndex >= 0 && GizmoModeIndex < GizmoModeLabels.Length
            ? GizmoModeLabels[GizmoModeIndex]
            : "Move";
        var axis = GizmoAxisIndex >= 0 && GizmoAxisIndex < GizmoAxisLabels.Length
            ? GizmoAxisLabels[GizmoAxisIndex]
            : "Free";
        var snap = GizmoSnapEnabled ? $"Snap {GizmoSnap:0.##}" : "Snap Off";

        return $"Select: {tool} | Mode: {mode} | Gizmo: {gizmoMode} {axis} | {snap}";
    }

    private string BuildViewportViewLabel()
    {
        var index = ViewportViewIndex;
        return index >= 0 && index < ViewportViewLabels.Length
            ? ViewportViewLabels[index]
            : "Perspective";
    }

    private string BuildViewportOverlayStatusLabel()
    {
        var snap = GizmoSnapEnabled ? $"{GizmoSnap:0.##}" : "Off";
        return $"Grid: {GridStateLabel} | Snap: {snap}";
    }

    private string BuildSelectionToolLabel()
    {
        var toolIndex = SelectionToolIndex;
        var tool = toolIndex >= 0 && toolIndex < SelectionToolLabels.Length
            ? SelectionToolLabels[toolIndex]
            : "Click";

        if (toolIndex == 1)
        {
            tool = SelectionCrossing ? "Box (Crossing)" : "Box (Window)";
        }

        return tool;
    }

    private void SetSelectionMode(int mode)
    {
        switch (mode)
        {
            case 1:
                EditMode = true;
                VertexMode = true;
                EdgeMode = false;
                FaceMode = false;
                break;
            case 2:
                EditMode = true;
                VertexMode = false;
                EdgeMode = true;
                FaceMode = false;
                break;
            case 3:
                EditMode = true;
                VertexMode = false;
                EdgeMode = false;
                FaceMode = true;
                break;
            default:
                EditMode = true;
                VertexMode = false;
                EdgeMode = false;
                FaceMode = false;
                break;
        }
    }

    private static int ParseIndex(object? value)
    {
        return value switch
        {
            int number => number,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => 0
        };
    }

    public bool ProportionalEnabled
    {
        get => _proportionalEnabled;
        set
        {
            if (_proportionalEnabled == value)
            {
                return;
            }

            _proportionalEnabled = value;
            RaisePropertyChanged();
        }
    }

    public int ProportionalFalloffIndex
    {
        get => _proportionalFalloffIndex;
        set
        {
            if (_proportionalFalloffIndex == value)
            {
                return;
            }

            _proportionalFalloffIndex = value;
            RaisePropertyChanged();
        }
    }

    public bool ImportCenter
    {
        get => _importCenter;
        set
        {
            if (_importCenter == value)
            {
                return;
            }

            _importCenter = value;
            RaisePropertyChanged();
        }
    }

    public bool ImportNormals
    {
        get => _importNormals;
        set
        {
            if (_importNormals == value)
            {
                return;
            }

            _importNormals = value;
            RaisePropertyChanged();
        }
    }

    public bool ImportScaleEnabled
    {
        get => _importScaleEnabled;
        set
        {
            if (_importScaleEnabled == value)
            {
                return;
            }

            _importScaleEnabled = value;
            RaisePropertyChanged();
        }
    }

    public double ImportScaleRadius
    {
        get => _importScaleRadius;
        set
        {
            if (Math.Abs(_importScaleRadius - value) < 1e-6)
            {
                return;
            }

            _importScaleRadius = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ImportScaleLabel));
        }
    }

    public string ImportScaleLabel => $"Scale radius: {ImportScaleRadius:0.0}";

    public int PickAccelIndex
    {
        get => _pickAccelIndex;
        set
        {
            if (_pickAccelIndex == value)
            {
                return;
            }

            _pickAccelIndex = value;
            RaisePropertyChanged();
        }
    }

    public int SmoothIterations
    {
        get => _smoothIterations;
        set
        {
            if (_smoothIterations == value)
            {
                return;
            }

            _smoothIterations = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SmoothIterationsLabel));
        }
    }

    public string SmoothIterationsLabel => $"Smooth iterations: {SmoothIterations}";

    public double SmoothStrength
    {
        get => _smoothStrength;
        set
        {
            if (Math.Abs(_smoothStrength - value) < 1e-6)
            {
                return;
            }

            _smoothStrength = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SmoothStrengthLabel));
        }
    }

    public string SmoothStrengthLabel => $"Smooth strength: {SmoothStrength:0.00}";

    public double SimplifyRatio
    {
        get => _simplifyRatio;
        set
        {
            if (Math.Abs(_simplifyRatio - value) < 1e-6)
            {
                return;
            }

            _simplifyRatio = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SimplifyRatioLabel));
        }
    }

    public string SimplifyRatioLabel => $"Simplify ratio: {SimplifyRatio:0.00}";

    public double UvScale
    {
        get => _uvScale;
        set
        {
            if (Math.Abs(_uvScale - value) < 1e-6)
            {
                return;
            }

            _uvScale = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(UvScaleLabel));
        }
    }

    public string UvScaleLabel => $"UV scale: {UvScale:0.0}";

    public int UvUnwrapMethodIndex
    {
        get => _uvUnwrapMethodIndex;
        set
        {
            if (_uvUnwrapMethodIndex == value)
            {
                return;
            }

            _uvUnwrapMethodIndex = value;
            RaisePropertyChanged();
        }
    }

    public double UvUnwrapAngle
    {
        get => _uvUnwrapAngle;
        set
        {
            if (Math.Abs(_uvUnwrapAngle - value) < 1e-6)
            {
                return;
            }

            _uvUnwrapAngle = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(UvUnwrapAngleLabel));
        }
    }

    public string UvUnwrapAngleLabel => $"Unwrap angle: {UvUnwrapAngle:0}\u00B0";

    public double UvPackPadding
    {
        get => _uvPackPadding;
        set
        {
            if (Math.Abs(_uvPackPadding - value) < 1e-6)
            {
                return;
            }

            _uvPackPadding = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(UvPackPaddingLabel));
        }
    }

    public string UvPackPaddingLabel => $"Pack padding: {UvPackPadding * 100.0:0.#}%";

    public bool UvPackRotate
    {
        get => _uvPackRotate;
        set
        {
            if (_uvPackRotate == value)
            {
                return;
            }

            _uvPackRotate = value;
            RaisePropertyChanged();
        }
    }

    public bool UvPackPreserveTexelDensity
    {
        get => _uvPackPreserveTexelDensity;
        set
        {
            if (_uvPackPreserveTexelDensity == value)
            {
                return;
            }

            _uvPackPreserveTexelDensity = value;
            RaisePropertyChanged();
        }
    }

    public double UvPackTexelDensity
    {
        get => _uvPackTexelDensity;
        set
        {
            if (Math.Abs(_uvPackTexelDensity - value) < 1e-6)
            {
                return;
            }

            _uvPackTexelDensity = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(UvPackTexelDensityLabel));
        }
    }

    public string UvPackTexelDensityLabel => $"Texel density: {UvPackTexelDensity:0.00}";

    public bool UvPackUseGroups
    {
        get => _uvPackUseGroups;
        set
        {
            if (_uvPackUseGroups == value)
            {
                return;
            }

            _uvPackUseGroups = value;
            RaisePropertyChanged();
        }
    }

    public bool UvShowSeams
    {
        get => _uvShowSeams;
        set
        {
            if (_uvShowSeams == value)
            {
                return;
            }

            _uvShowSeams = value;
            RaisePropertyChanged();
        }
    }

    public bool UvShowIslands
    {
        get => _uvShowIslands;
        set
        {
            if (_uvShowIslands == value)
            {
                return;
            }

            _uvShowIslands = value;
            RaisePropertyChanged();
        }
    }

    public int UvGroupId
    {
        get => _uvGroupId;
        set
        {
            if (_uvGroupId == value)
            {
                return;
            }

            _uvGroupId = value;
            RaisePropertyChanged();
        }
    }

    public double ProportionalRadius
    {
        get => _proportionalRadius;
        set
        {
            if (Math.Abs(_proportionalRadius - value) < 1e-6)
            {
                return;
            }

            _proportionalRadius = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ProportionalRadiusLabel));
        }
    }

    public string ProportionalRadiusLabel => $"Radius: {ProportionalRadius:0.0}";

    public double ExtrudeDistance
    {
        get => _extrudeDistance;
        set
        {
            if (Math.Abs(_extrudeDistance - value) < 1e-6)
            {
                return;
            }

            _extrudeDistance = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ExtrudeDistanceLabel));
        }
    }

    public string ExtrudeDistanceLabel => $"Extrude: {ExtrudeDistance:0.00}";

    public double BevelInset
    {
        get => _bevelInset;
        set
        {
            if (Math.Abs(_bevelInset - value) < 1e-6)
            {
                return;
            }

            _bevelInset = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(BevelInsetLabel));
        }
    }

    public string BevelInsetLabel => $"Bevel inset: {BevelInset:0.00}";

    public double BevelHeight
    {
        get => _bevelHeight;
        set
        {
            if (Math.Abs(_bevelHeight - value) < 1e-6)
            {
                return;
            }

            _bevelHeight = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(BevelHeightLabel));
        }
    }

    public string BevelHeightLabel => $"Bevel height: {BevelHeight:0.00}";

    public double InsetDistance
    {
        get => _insetDistance;
        set
        {
            if (Math.Abs(_insetDistance - value) < 1e-6)
            {
                return;
            }

            _insetDistance = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(InsetDistanceLabel));
        }
    }

    public string InsetDistanceLabel => $"Inset: {InsetDistance:0.00}";

    public double WeldTolerance
    {
        get => _weldTolerance;
        set
        {
            if (Math.Abs(_weldTolerance - value) < 1e-6)
            {
                return;
            }

            _weldTolerance = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(WeldToleranceLabel));
        }
    }

    public string WeldToleranceLabel => $"Weld tolerance: {WeldTolerance:0.000}";

    public double NudgeStep
    {
        get => _nudgeStep;
        set
        {
            if (Math.Abs(_nudgeStep - value) < 1e-6)
            {
                return;
            }

            _nudgeStep = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(NudgeStepLabel));
        }
    }

    public string NudgeStepLabel => $"Nudge step: {NudgeStep:0.00}";

    public double RenderScale
    {
        get => _renderScale;
        set
        {
            if (Math.Abs(_renderScale - value) < 1e-6)
            {
                return;
            }

            _renderScale = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(RenderScaleLabel));
        }
    }

    public string RenderScaleLabel => $"Render scale: {RenderScale:0.00}";

    public int RenderWorkerCount
    {
        get => _renderWorkerCount;
        set
        {
            if (_renderWorkerCount == value)
            {
                return;
            }

            _renderWorkerCount = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(RenderWorkerCountLabel));
        }
    }

    public string RenderWorkerCountLabel => $"Worker threads: {RenderWorkerCount}";

    public int MeshPrecision
    {
        get => _meshPrecision;
        set
        {
            if (_meshPrecision == value)
            {
                return;
            }

            _meshPrecision = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(MeshPrecisionLabel));
        }
    }

    public string MeshPrecisionLabel => $"Mesh precision: {MeshPrecision}";

    public int Subdivision
    {
        get => _subdivision;
        set
        {
            if (_subdivision == value)
            {
                return;
            }

            _subdivision = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SubdivisionLabel));
        }
    }

    public string SubdivisionLabel => $"Path subdivision: {Subdivision}";
}
