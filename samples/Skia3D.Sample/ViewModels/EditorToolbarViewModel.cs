using System;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class EditorToolbarViewModel : ViewModelBase
{
    public EditorToolbarViewModel(EditorViewModel editor,
        InspectorOptionsViewModel options,
        EditorActionsViewModel actions,
        WorkspaceViewModel workspace,
        CommandPanelViewModel commandPanel,
        CommandPaletteViewModel commandPalette,
        ShortcutEditorViewModel shortcuts)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        CommandPanel = commandPanel ?? throw new ArgumentNullException(nameof(commandPanel));
        CommandPalette = commandPalette ?? throw new ArgumentNullException(nameof(commandPalette));
        Shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));
    }

    public EditorViewModel Editor { get; }

    public InspectorOptionsViewModel Options { get; }

    public EditorActionsViewModel Actions { get; }

    public WorkspaceViewModel Workspace { get; }

    public CommandPanelViewModel CommandPanel { get; }

    public CommandPaletteViewModel CommandPalette { get; }

    public ShortcutEditorViewModel Shortcuts { get; }

    private RibbonTab _selectedRibbonTab = RibbonTab.Modeling;

    public RibbonTab SelectedRibbonTab
    {
        get => _selectedRibbonTab;
        set
        {
            if (_selectedRibbonTab == value)
            {
                return;
            }

            _selectedRibbonTab = value;
            RaisePropertyChanged();
            RaiseRibbonTabChanged();
        }
    }

    public bool IsModelingTab
    {
        get => SelectedRibbonTab == RibbonTab.Modeling;
        set
        {
            if (value)
            {
                SelectedRibbonTab = RibbonTab.Modeling;
            }
        }
    }

    public bool IsFreeformTab
    {
        get => SelectedRibbonTab == RibbonTab.Freeform;
        set
        {
            if (value)
            {
                SelectedRibbonTab = RibbonTab.Freeform;
            }
        }
    }

    public bool IsSelectionTab
    {
        get => SelectedRibbonTab == RibbonTab.Selection;
        set
        {
            if (value)
            {
                SelectedRibbonTab = RibbonTab.Selection;
            }
        }
    }

    public bool IsObjectPaintTab
    {
        get => SelectedRibbonTab == RibbonTab.ObjectPaint;
        set
        {
            if (value)
            {
                SelectedRibbonTab = RibbonTab.ObjectPaint;
            }
        }
    }

    public bool IsPopulateTab
    {
        get => SelectedRibbonTab == RibbonTab.Populate;
        set
        {
            if (value)
            {
                SelectedRibbonTab = RibbonTab.Populate;
            }
        }
    }

    public bool ShowFileGroup => true;

    public bool ShowEditGroup => IsModelingTab || IsSelectionTab;

    public bool ShowToolsGroup => IsModelingTab || IsSelectionTab;

    public bool ShowGroupGroup => IsModelingTab || IsSelectionTab;

    public bool ShowViewsGroup => IsSelectionTab || IsFreeformTab || IsPopulateTab;

    public bool ShowCreateGroup => IsModelingTab || IsPopulateTab;

    public bool ShowModifiersGroup => IsModelingTab;

    public bool ShowAnimationGroup => IsFreeformTab;

    public bool ShowGraphGroup => IsFreeformTab;

    public bool ShowRenderingGroup => IsFreeformTab || IsObjectPaintTab;

    public bool ShowCivilGroup => IsPopulateTab;

    public bool ShowCustomizeGroup => true;

    public bool ShowScriptingGroup => IsFreeformTab;

    public bool ShowContentGroup => IsObjectPaintTab;

    public bool ShowHelpGroup => true;

    private void RaiseRibbonTabChanged()
    {
        RaisePropertyChanged(nameof(IsModelingTab));
        RaisePropertyChanged(nameof(IsFreeformTab));
        RaisePropertyChanged(nameof(IsSelectionTab));
        RaisePropertyChanged(nameof(IsObjectPaintTab));
        RaisePropertyChanged(nameof(IsPopulateTab));
        RaisePropertyChanged(nameof(ShowFileGroup));
        RaisePropertyChanged(nameof(ShowEditGroup));
        RaisePropertyChanged(nameof(ShowToolsGroup));
        RaisePropertyChanged(nameof(ShowGroupGroup));
        RaisePropertyChanged(nameof(ShowViewsGroup));
        RaisePropertyChanged(nameof(ShowCreateGroup));
        RaisePropertyChanged(nameof(ShowModifiersGroup));
        RaisePropertyChanged(nameof(ShowAnimationGroup));
        RaisePropertyChanged(nameof(ShowGraphGroup));
        RaisePropertyChanged(nameof(ShowRenderingGroup));
        RaisePropertyChanged(nameof(ShowCivilGroup));
        RaisePropertyChanged(nameof(ShowCustomizeGroup));
        RaisePropertyChanged(nameof(ShowScriptingGroup));
        RaisePropertyChanged(nameof(ShowContentGroup));
        RaisePropertyChanged(nameof(ShowHelpGroup));
    }
}

public enum RibbonTab
{
    Modeling,
    Freeform,
    Selection,
    ObjectPaint,
    Populate
}
