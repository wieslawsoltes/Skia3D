using System;
using Skia3D.Sample.Models;
using Skia3D.Sample.Services;
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
        var workspaceStore = new WorkspaceLayoutStore();
        Workspace = new WorkspaceViewModel(Actions, workspaceStore);
        Material = new MaterialGraphViewModel();
        Constraints = new ConstraintPanelViewModel();
        CommandPanel = new CommandPanelViewModel(Editor, options, Actions, Material, Constraints);
        var commandCatalog = new EditorCommandCatalog(Actions, options, CommandPanel, Workspace);
        CommandPalette = new CommandPaletteViewModel(commandCatalog.Commands);
        Shortcuts = new ShortcutEditorViewModel(commandCatalog.Commands);
        Toolbar = new EditorToolbarViewModel(Editor, options, Actions, Workspace, CommandPanel, CommandPalette, Shortcuts);
        ViewportContext = new ViewportDockContext(Editor, Toolbar, Workspace, Actions);
        var sceneExplorerContext = new SceneExplorerDockContext(CommandPanel);
        var commandPanelContext = new CommandPanelDockContext(CommandPanel);
        var timelineContext = new TimelineDockContext(CommandPanel.Motion, Actions);
        DockLayout = new EditorDockLayoutViewModel(Workspace, ViewportContext, sceneExplorerContext, commandPanelContext, timelineContext);
        Workspace.AttachDockLayoutService(DockLayout);
        commandCatalog.Add(new EditorCommandDefinition("ui.commandPalette", "Command Palette", "UI", CommandPalette.ToggleCommand, defaultShortcut: "Ctrl+Shift+P"));
        commandCatalog.Add(new EditorCommandDefinition("ui.shortcutEditor", "Shortcut Editor", "UI", Shortcuts.ToggleCommand, defaultShortcut: "Ctrl+Alt+K"));
        HandleShortcutInputCommand = new DelegateCommand<ShortcutInput>(HandleShortcutInput);
        StatusBar = new StatusBarViewModel();
    }

    public EditorSession Session { get; }

    public EditorViewModel Editor { get; }

    public EditorToolbarViewModel Toolbar { get; }

    public CommandPanelViewModel CommandPanel { get; }

    public CommandPaletteViewModel CommandPalette { get; }

    public ShortcutEditorViewModel Shortcuts { get; }

    public EditorActionsViewModel Actions { get; }

    public WorkspaceViewModel Workspace { get; }

    public EditorDockLayoutViewModel DockLayout { get; }

    public StatusBarViewModel StatusBar { get; }

    public ViewportDockContext ViewportContext { get; }

    public MaterialGraphViewModel Material { get; }

    public ConstraintPanelViewModel Constraints { get; }

    public DelegateCommand<ShortcutInput> HandleShortcutInputCommand { get; }

    private void HandleShortcutInput(ShortcutInput? input)
    {
        if (input == null || input.Gesture.IsEmpty)
        {
            return;
        }

        if (CommandPalette.IsOpen)
        {
            if (IsEscapeOnly(input.Gesture))
            {
                CommandPalette.CloseCommand.Execute(null);
                return;
            }

            if (IsEnterOnly(input.Gesture))
            {
                CommandPalette.ExecuteSelectedCommand.Execute(null);
                return;
            }

            return;
        }

        if (Shortcuts.IsOpen && !Shortcuts.IsCapturing)
        {
            if (IsEscapeOnly(input.Gesture))
            {
                Shortcuts.CloseCommand.Execute(null);
            }

            return;
        }

        Shortcuts.HandleShortcutInput(input);
    }

    private static bool IsEscapeOnly(ShortcutGesture gesture)
    {
        return gesture.Key.Equals("Escape", StringComparison.OrdinalIgnoreCase)
               && !gesture.Ctrl && !gesture.Shift && !gesture.Alt && !gesture.Meta;
    }

    private static bool IsEnterOnly(ShortcutGesture gesture)
    {
        return gesture.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase)
               && !gesture.Ctrl && !gesture.Shift && !gesture.Alt && !gesture.Meta;
    }
}
