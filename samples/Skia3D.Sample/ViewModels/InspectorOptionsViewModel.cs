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
    private bool _wireframeEnabled;
    private bool _shadowEnabled;
    private bool _ssaoEnabled;
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
    private int _meshPrecision = 24;
    private int _subdivision = 64;
    private int _viewportViewIndex;
    private ViewportNavigationMode _navigationMode;

    public InspectorOptionsViewModel()
    {
        SetSelectionToolCommand = new DelegateCommand<object?>(value => SelectionToolIndex = ParseIndex(value));
        SetGizmoModeCommand = new DelegateCommand<object?>(value => GizmoModeIndex = ParseIndex(value));
        SetSelectionModeCommand = new DelegateCommand<object?>(value => SetSelectionMode(ParseIndex(value)));
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

    public DelegateCommand<object?> SetSelectionModeCommand { get; }

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
