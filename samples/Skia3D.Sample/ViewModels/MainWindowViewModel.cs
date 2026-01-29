using System;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(EditorSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Editor = new EditorViewModel(session);
        var options = new InspectorOptionsViewModel();
        Actions = new EditorActionsViewModel();
        var workspaceStore = new Services.WorkspaceLayoutStore();
        Workspace = new WorkspaceViewModel(Actions, workspaceStore);
        Toolbar = new EditorToolbarViewModel(Editor, options, Actions, Workspace);
        Material = new MaterialGraphViewModel();
        Constraints = new ConstraintPanelViewModel();
        CommandPanel = new CommandPanelViewModel(Editor, options, Actions, Material, Constraints);
        StatusBar = new StatusBarViewModel();
    }

    public EditorSession Session { get; }

    public EditorViewModel Editor { get; }

    public EditorToolbarViewModel Toolbar { get; }

    public CommandPanelViewModel CommandPanel { get; }

    public EditorActionsViewModel Actions { get; }

    public WorkspaceViewModel Workspace { get; }

    public StatusBarViewModel StatusBar { get; }

    public MaterialGraphViewModel Material { get; }

    public ConstraintPanelViewModel Constraints { get; }
}
