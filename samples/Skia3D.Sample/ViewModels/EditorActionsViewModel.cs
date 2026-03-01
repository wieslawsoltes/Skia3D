using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class EditorActionsViewModel : ViewModelBase
{
    private EditorActionHandlers _handlers = new();
    private bool _isCommandPanelOpen = true;
    private bool _isShortcutsOpen;
    private bool _isQuadMenuOpen;

    public EditorActionsViewModel()
    {
        ToggleCommandPanelCommand = new DelegateCommand(ToggleCommandPanel);
        ToggleShortcutsCommand = new DelegateCommand(ToggleShortcuts);
        CloseShortcutsCommand = new DelegateCommand(() => IsShortcutsOpen = false);
        CloseQuadMenuCommand = new DelegateCommand(() => IsQuadMenuOpen = false);
        OpenSceneCommand = new AsyncCommand(() => _handlers.OpenSceneAsync?.Invoke() ?? Task.CompletedTask);
        LoadObjCommand = new AsyncCommand(() => _handlers.LoadObjAsync?.Invoke() ?? Task.CompletedTask);
        SaveSceneCommand = new AsyncCommand(() => _handlers.SaveSceneAsync?.Invoke() ?? Task.CompletedTask);
        SaveSceneAsCommand = new AsyncCommand(() => _handlers.SaveSceneAsAsync?.Invoke() ?? Task.CompletedTask);
        ZoomExtentsCommand = new DelegateCommand(() => _handlers.ZoomExtents?.Invoke());
        IsolateSelectionCommand = new DelegateCommand(() => _handlers.IsolateSelection?.Invoke());
        UnhideAllCommand = new DelegateCommand(() => _handlers.UnhideAll?.Invoke());
        RenameSelectionCommand = new AsyncCommand(() => _handlers.RenameSelectionAsync?.Invoke() ?? Task.CompletedTask);
        SelectAllCommand = new DelegateCommand(() => _handlers.SelectAll?.Invoke());
        InvertSelectionCommand = new DelegateCommand(() => _handlers.InvertSelection?.Invoke());
        ClearSceneCommand = new DelegateCommand(() => _handlers.ClearScene?.Invoke());
        GroupSelectionCommand = new DelegateCommand(() => _handlers.GroupSelection?.Invoke());
        UngroupSelectionCommand = new DelegateCommand(() => _handlers.UngroupSelection?.Invoke());
        CreateCubeCommand = new DelegateCommand(() => _handlers.CreateCube?.Invoke());
        CreateSphereCommand = new DelegateCommand(() => _handlers.CreateSphere?.Invoke());
        CreateCylinderCommand = new DelegateCommand(() => _handlers.CreateCylinder?.Invoke());
        CreatePyramidCommand = new DelegateCommand(() => _handlers.CreatePyramid?.Invoke());
        CreatePlaneCommand = new DelegateCommand(() => _handlers.CreatePlane?.Invoke());
        CreateGridCommand = new DelegateCommand(() => _handlers.CreateGrid?.Invoke());
        CreateDirectionalLightCommand = new DelegateCommand(() => _handlers.CreateDirectionalLight?.Invoke());
        CreatePointLightCommand = new DelegateCommand(() => _handlers.CreatePointLight?.Invoke());
        CreateSpotLightCommand = new DelegateCommand(() => _handlers.CreateSpotLight?.Invoke());
        CreateCameraCommand = new DelegateCommand(() => _handlers.CreateCamera?.Invoke());
        CreateNavGridCommand = new DelegateCommand(() => _handlers.CreateNavGrid?.Invoke());
        CreateFlowCommand = new DelegateCommand(() => _handlers.CreateFlow?.Invoke());
        CreateIdleAreaCommand = new DelegateCommand(() => _handlers.CreateIdleArea?.Invoke());
        ClearNavigationCommand = new DelegateCommand(() => _handlers.ClearNavigation?.Invoke());
        OpenMaterialGraphCommand = new DelegateCommand(() => _handlers.OpenMaterialGraph?.Invoke());
        OpenScriptConsoleCommand = new DelegateCommand(() => _handlers.OpenScriptConsole?.Invoke());
        AssignTextureCommand = new AsyncCommand(() => _handlers.AssignTextureAsync?.Invoke() ?? Task.CompletedTask);
        AssignCheckerTextureCommand = new DelegateCommand(() => _handlers.AssignCheckerTexture?.Invoke());
        ClearTexturesCommand = new DelegateCommand(() => _handlers.ClearTextures?.Invoke());
        UndoCommand = new DelegateCommand(() => _handlers.Undo?.Invoke());
        RedoCommand = new DelegateCommand(() => _handlers.Redo?.Invoke());
        ClearSelectionCommand = new DelegateCommand(() => _handlers.ClearSelection?.Invoke());
        DeleteSelectionCommand = new DelegateCommand(() => _handlers.DeleteSelection?.Invoke());
        DuplicateSelectionCommand = new DelegateCommand(() => _handlers.DuplicateSelection?.Invoke());
        DetachFacesCommand = new DelegateCommand(() => _handlers.DetachFaces?.Invoke());
        ConvertSelectionToVerticesCommand = new DelegateCommand(() => _handlers.ConvertSelectionToVertices?.Invoke());
        ConvertSelectionToEdgesCommand = new DelegateCommand(() => _handlers.ConvertSelectionToEdges?.Invoke());
        ConvertSelectionToFacesCommand = new DelegateCommand(() => _handlers.ConvertSelectionToFaces?.Invoke());
        TransformSelectionCommand = new AsyncCommand(() => _handlers.TransformSelectionAsync?.Invoke() ?? Task.CompletedTask);
        SelectEdgeLoopCommand = new DelegateCommand(() => _handlers.SelectEdgeLoop?.Invoke());
        SelectEdgeRingCommand = new DelegateCommand(() => _handlers.SelectEdgeRing?.Invoke());
        GrowSelectionCommand = new DelegateCommand(() => _handlers.GrowSelection?.Invoke());
        ShrinkSelectionCommand = new DelegateCommand(() => _handlers.ShrinkSelection?.Invoke());
        ExtrudeFacesCommand = new DelegateCommand(() => _handlers.ExtrudeFaces?.Invoke());
        BevelFacesCommand = new DelegateCommand(() => _handlers.BevelFaces?.Invoke());
        InsetFacesCommand = new DelegateCommand(() => _handlers.InsetFaces?.Invoke());
        LoopCutEdgeLoopCommand = new DelegateCommand(() => _handlers.LoopCutEdgeLoop?.Invoke());
        SplitEdgeCommand = new DelegateCommand(() => _handlers.SplitEdge?.Invoke());
        BridgeEdgesCommand = new DelegateCommand(() => _handlers.BridgeEdges?.Invoke());
        BridgeEdgeLoopsCommand = new DelegateCommand(() => _handlers.BridgeEdgeLoops?.Invoke());
        DissolveEdgeCommand = new DelegateCommand(() => _handlers.DissolveEdge?.Invoke());
        CollapseEdgeCommand = new DelegateCommand(() => _handlers.CollapseEdge?.Invoke());
        MergeVerticesCommand = new DelegateCommand(() => _handlers.MergeVertices?.Invoke());
        DissolveFacesCommand = new DelegateCommand(() => _handlers.DissolveFaces?.Invoke());
        WeldVerticesCommand = new DelegateCommand(() => _handlers.WeldVertices?.Invoke());
        CleanupMeshCommand = new DelegateCommand(() => _handlers.CleanupMesh?.Invoke());
        SmoothMeshCommand = new DelegateCommand(() => _handlers.SmoothMesh?.Invoke());
        SimplifyMeshCommand = new DelegateCommand(() => _handlers.SimplifyMesh?.Invoke());
        NudgePosXCommand = new DelegateCommand(() => _handlers.NudgePosX?.Invoke());
        NudgeNegXCommand = new DelegateCommand(() => _handlers.NudgeNegX?.Invoke());
        PlanarUvCommand = new DelegateCommand(() => _handlers.PlanarUv?.Invoke());
        BoxUvCommand = new DelegateCommand(() => _handlers.BoxUv?.Invoke());
        NormalizeUvCommand = new DelegateCommand(() => _handlers.NormalizeUv?.Invoke());
        FlipUCommand = new DelegateCommand(() => _handlers.FlipU?.Invoke());
        FlipVCommand = new DelegateCommand(() => _handlers.FlipV?.Invoke());
        UnwrapUvCommand = new DelegateCommand(() => _handlers.UnwrapUv?.Invoke());
        PackUvCommand = new DelegateCommand(() => _handlers.PackUv?.Invoke());
        MarkUvSeamsCommand = new DelegateCommand(() => _handlers.MarkUvSeams?.Invoke());
        ClearUvSeamsCommand = new DelegateCommand(() => _handlers.ClearUvSeams?.Invoke());
        ClearAllUvSeamsCommand = new DelegateCommand(() => _handlers.ClearAllUvSeams?.Invoke());
        SelectUvIslandCommand = new DelegateCommand(() => _handlers.SelectUvIsland?.Invoke());
        AssignUvGroupCommand = new DelegateCommand<int>(groupId => _handlers.AssignUvGroup?.Invoke(groupId));
        ClearUvGroupCommand = new DelegateCommand(() => _handlers.ClearUvGroup?.Invoke());
        CenterPivotCommand = new DelegateCommand(() => _handlers.CenterPivot?.Invoke());
        ResetTransformCommand = new DelegateCommand(() => _handlers.ResetTransform?.Invoke());
        AnimationResetCommand = new DelegateCommand(() => _handlers.AnimationReset?.Invoke());
        ClearAnimationKeysCommand = new DelegateCommand(() => _handlers.ClearAnimationKeys?.Invoke());
        ShowAboutCommand = new DelegateCommand(() => _handlers.ShowAbout?.Invoke());
    }

    public void Bind(EditorActionHandlers handlers)
    {
        _handlers = handlers ?? new EditorActionHandlers();
    }

    public ICommand ToggleCommandPanelCommand { get; }

    public ICommand ToggleShortcutsCommand { get; }

    public ICommand CloseShortcutsCommand { get; }

    public ICommand CloseQuadMenuCommand { get; }

    public ICommand OpenSceneCommand { get; }

    public ICommand LoadObjCommand { get; }

    public ICommand SaveSceneCommand { get; }

    public ICommand SaveSceneAsCommand { get; }

    public ICommand ZoomExtentsCommand { get; }

    public ICommand IsolateSelectionCommand { get; }

    public ICommand UnhideAllCommand { get; }

    public ICommand RenameSelectionCommand { get; }

    public ICommand SelectAllCommand { get; }

    public ICommand InvertSelectionCommand { get; }

    public ICommand ClearSceneCommand { get; }

    public ICommand GroupSelectionCommand { get; }

    public ICommand UngroupSelectionCommand { get; }

    public ICommand CreateCubeCommand { get; }

    public ICommand CreateSphereCommand { get; }

    public ICommand CreateCylinderCommand { get; }

    public ICommand CreatePyramidCommand { get; }

    public ICommand CreatePlaneCommand { get; }

    public ICommand CreateGridCommand { get; }

    public ICommand CreateDirectionalLightCommand { get; }

    public ICommand CreatePointLightCommand { get; }

    public ICommand CreateSpotLightCommand { get; }

    public ICommand CreateCameraCommand { get; }

    public ICommand CreateNavGridCommand { get; }

    public ICommand CreateFlowCommand { get; }

    public ICommand CreateIdleAreaCommand { get; }

    public ICommand ClearNavigationCommand { get; }

    public ICommand OpenMaterialGraphCommand { get; }

    public ICommand OpenScriptConsoleCommand { get; }

    public ICommand AssignTextureCommand { get; }

    public ICommand AssignCheckerTextureCommand { get; }

    public ICommand ClearTexturesCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public ICommand DeleteSelectionCommand { get; }

    public ICommand DuplicateSelectionCommand { get; }

    public ICommand DetachFacesCommand { get; }

    public ICommand ConvertSelectionToVerticesCommand { get; }

    public ICommand ConvertSelectionToEdgesCommand { get; }

    public ICommand ConvertSelectionToFacesCommand { get; }

    public ICommand TransformSelectionCommand { get; }

    public ICommand SelectEdgeLoopCommand { get; }

    public ICommand SelectEdgeRingCommand { get; }

    public ICommand GrowSelectionCommand { get; }

    public ICommand ShrinkSelectionCommand { get; }

    public ICommand ExtrudeFacesCommand { get; }

    public ICommand BevelFacesCommand { get; }

    public ICommand InsetFacesCommand { get; }

    public ICommand LoopCutEdgeLoopCommand { get; }

    public ICommand SplitEdgeCommand { get; }

    public ICommand BridgeEdgesCommand { get; }

    public ICommand BridgeEdgeLoopsCommand { get; }

    public ICommand DissolveEdgeCommand { get; }

    public ICommand CollapseEdgeCommand { get; }

    public ICommand MergeVerticesCommand { get; }

    public ICommand DissolveFacesCommand { get; }

    public ICommand WeldVerticesCommand { get; }

    public ICommand CleanupMeshCommand { get; }

    public ICommand SmoothMeshCommand { get; }

    public ICommand SimplifyMeshCommand { get; }

    public ICommand NudgePosXCommand { get; }

    public ICommand NudgeNegXCommand { get; }

    public ICommand PlanarUvCommand { get; }

    public ICommand BoxUvCommand { get; }

    public ICommand NormalizeUvCommand { get; }

    public ICommand FlipUCommand { get; }

    public ICommand FlipVCommand { get; }

    public ICommand UnwrapUvCommand { get; }

    public ICommand PackUvCommand { get; }

    public ICommand MarkUvSeamsCommand { get; }

    public ICommand ClearUvSeamsCommand { get; }

    public ICommand ClearAllUvSeamsCommand { get; }

    public ICommand SelectUvIslandCommand { get; }

    public ICommand AssignUvGroupCommand { get; }

    public ICommand ClearUvGroupCommand { get; }

    public ICommand CenterPivotCommand { get; }

    public ICommand ResetTransformCommand { get; }

    public ICommand AnimationResetCommand { get; }

    public ICommand ClearAnimationKeysCommand { get; }

    public ICommand ShowAboutCommand { get; }

    public bool IsCommandPanelOpen
    {
        get => _isCommandPanelOpen;
        set
        {
            if (_isCommandPanelOpen == value)
            {
                return;
            }

            _isCommandPanelOpen = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(CommandPanelLabel));
        }
    }

    public bool IsShortcutsOpen
    {
        get => _isShortcutsOpen;
        set
        {
            if (_isShortcutsOpen == value)
            {
                return;
            }

            _isShortcutsOpen = value;
            RaisePropertyChanged();
        }
    }

    public bool IsQuadMenuOpen
    {
        get => _isQuadMenuOpen;
        set
        {
            if (_isQuadMenuOpen == value)
            {
                return;
            }

            _isQuadMenuOpen = value;
            RaisePropertyChanged();
        }
    }

    public string CommandPanelLabel => IsCommandPanelOpen ? "Hide Command Panel" : "Show Command Panel";

    private void ToggleCommandPanel()
    {
        IsCommandPanelOpen = !IsCommandPanelOpen;
    }

    private void ToggleShortcuts()
    {
        IsShortcutsOpen = !IsShortcutsOpen;
    }
}

public sealed class EditorActionHandlers
{
    public Func<Task>? OpenSceneAsync { get; init; }
    public Func<Task>? LoadObjAsync { get; init; }
    public Func<Task>? SaveSceneAsync { get; init; }
    public Func<Task>? SaveSceneAsAsync { get; init; }
    public Action? ZoomExtents { get; init; }
    public Action? IsolateSelection { get; init; }
    public Action? UnhideAll { get; init; }
    public Func<Task>? RenameSelectionAsync { get; init; }
    public Action? SelectAll { get; init; }
    public Action? InvertSelection { get; init; }
    public Action? ClearScene { get; init; }
    public Action? GroupSelection { get; init; }
    public Action? UngroupSelection { get; init; }
    public Action? CreateCube { get; init; }
    public Action? CreateSphere { get; init; }
    public Action? CreateCylinder { get; init; }
    public Action? CreatePyramid { get; init; }
    public Action? CreatePlane { get; init; }
    public Action? CreateGrid { get; init; }
    public Action? CreateDirectionalLight { get; init; }
    public Action? CreatePointLight { get; init; }
    public Action? CreateSpotLight { get; init; }
    public Action? CreateCamera { get; init; }
    public Action? CreateNavGrid { get; init; }
    public Action? CreateFlow { get; init; }
    public Action? CreateIdleArea { get; init; }
    public Action? ClearNavigation { get; init; }
    public Action? OpenMaterialGraph { get; init; }
    public Action? OpenScriptConsole { get; init; }
    public Func<Task>? AssignTextureAsync { get; init; }
    public Action? AssignCheckerTexture { get; init; }
    public Action? ClearTextures { get; init; }
    public Action? Undo { get; init; }
    public Action? Redo { get; init; }
    public Action? ClearSelection { get; init; }
    public Action? DeleteSelection { get; init; }
    public Action? DuplicateSelection { get; init; }
    public Action? DetachFaces { get; init; }
    public Action? ConvertSelectionToVertices { get; init; }
    public Action? ConvertSelectionToEdges { get; init; }
    public Action? ConvertSelectionToFaces { get; init; }
    public Func<Task>? TransformSelectionAsync { get; init; }
    public Action? SelectEdgeLoop { get; init; }
    public Action? SelectEdgeRing { get; init; }
    public Action? GrowSelection { get; init; }
    public Action? ShrinkSelection { get; init; }
    public Action? ExtrudeFaces { get; init; }
    public Action? BevelFaces { get; init; }
    public Action? InsetFaces { get; init; }
    public Action? LoopCutEdgeLoop { get; init; }
    public Action? SplitEdge { get; init; }
    public Action? BridgeEdges { get; init; }
    public Action? BridgeEdgeLoops { get; init; }
    public Action? DissolveEdge { get; init; }
    public Action? CollapseEdge { get; init; }
    public Action? MergeVertices { get; init; }
    public Action? DissolveFaces { get; init; }
    public Action? WeldVertices { get; init; }
    public Action? CleanupMesh { get; init; }
    public Action? SmoothMesh { get; init; }
    public Action? SimplifyMesh { get; init; }
    public Action? NudgePosX { get; init; }
    public Action? NudgeNegX { get; init; }
    public Action? PlanarUv { get; init; }
    public Action? BoxUv { get; init; }
    public Action? NormalizeUv { get; init; }
    public Action? FlipU { get; init; }
    public Action? FlipV { get; init; }
    public Action? UnwrapUv { get; init; }
    public Action? PackUv { get; init; }
    public Action? MarkUvSeams { get; init; }
    public Action? ClearUvSeams { get; init; }
    public Action? ClearAllUvSeams { get; init; }
    public Action? SelectUvIsland { get; init; }
    public Action<int>? AssignUvGroup { get; init; }
    public Action? ClearUvGroup { get; init; }
    public Action? CenterPivot { get; init; }
    public Action? ResetTransform { get; init; }
    public Action? AnimationReset { get; init; }
    public Action? ClearAnimationKeys { get; init; }
    public Action? ShowAbout { get; init; }
}
