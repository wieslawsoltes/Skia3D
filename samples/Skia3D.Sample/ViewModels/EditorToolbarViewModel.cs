using System;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class EditorToolbarViewModel : ViewModelBase
{
    public EditorToolbarViewModel(EditorViewModel editor,
        InspectorOptionsViewModel options,
        EditorActionsViewModel actions,
        WorkspaceViewModel workspace)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public EditorViewModel Editor { get; }

    public InspectorOptionsViewModel Options { get; }

    public EditorActionsViewModel Actions { get; }

    public WorkspaceViewModel Workspace { get; }
}
