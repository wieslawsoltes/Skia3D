using System;
using Skia3D.Editor;
using Skia3D.Sample.Services;

namespace Skia3D.Sample.ViewModels;

public sealed class ViewportDockContext : ViewModelBase
{
    private EditorViewportService? _perspectiveViewport;
    private EditorViewportService? _topViewport;
    private EditorViewportService? _frontViewport;
    private EditorViewportService? _leftViewport;

    public ViewportDockContext(EditorViewModel editor, EditorToolbarViewModel toolbar, WorkspaceViewModel workspace, EditorActionsViewModel actions)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Toolbar = toolbar ?? throw new ArgumentNullException(nameof(toolbar));
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public EditorViewModel Editor { get; }

    public EditorToolbarViewModel Toolbar { get; }

    public WorkspaceViewModel Workspace { get; }

    public EditorActionsViewModel Actions { get; }

    public EditorViewportService? PerspectiveViewport
    {
        get => _perspectiveViewport;
        set
        {
            if (ReferenceEquals(_perspectiveViewport, value))
            {
                return;
            }

            _perspectiveViewport = value;
            RaisePropertyChanged();
        }
    }

    public EditorViewportService? TopViewport
    {
        get => _topViewport;
        set
        {
            if (ReferenceEquals(_topViewport, value))
            {
                return;
            }

            _topViewport = value;
            RaisePropertyChanged();
        }
    }

    public EditorViewportService? FrontViewport
    {
        get => _frontViewport;
        set
        {
            if (ReferenceEquals(_frontViewport, value))
            {
                return;
            }

            _frontViewport = value;
            RaisePropertyChanged();
        }
    }

    public EditorViewportService? LeftViewport
    {
        get => _leftViewport;
        set
        {
            if (ReferenceEquals(_leftViewport, value))
            {
                return;
            }

            _leftViewport = value;
            RaisePropertyChanged();
        }
    }
}

public sealed class SceneExplorerDockContext
{
    public SceneExplorerDockContext(CommandPanelViewModel commandPanel)
    {
        CommandPanel = commandPanel ?? throw new ArgumentNullException(nameof(commandPanel));
    }

    public CommandPanelViewModel CommandPanel { get; }
}

public sealed class CommandPanelDockContext
{
    public CommandPanelDockContext(CommandPanelViewModel commandPanel)
    {
        CommandPanel = commandPanel ?? throw new ArgumentNullException(nameof(commandPanel));
    }

    public CommandPanelViewModel CommandPanel { get; }
}

public sealed class TimelineDockContext
{
    public TimelineDockContext(MotionPanelViewModel motion, EditorActionsViewModel actions)
    {
        Motion = motion ?? throw new ArgumentNullException(nameof(motion));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public MotionPanelViewModel Motion { get; }

    public EditorActionsViewModel Actions { get; }
}
