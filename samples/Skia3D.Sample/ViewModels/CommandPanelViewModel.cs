using System;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class CommandPanelViewModel : ViewModelBase
{
    public CommandPanelViewModel(EditorViewModel editor, InspectorOptionsViewModel options, EditorActionsViewModel actions, MaterialGraphViewModel material, ConstraintPanelViewModel constraints)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Hierarchy = new HierarchyPanelViewModel();
        Motion = new MotionPanelViewModel();
        Commands = new CommandStateViewModel();
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Material = material ?? throw new ArgumentNullException(nameof(material));
        Constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
    }

    public EditorViewModel Editor { get; }

    public HierarchyPanelViewModel Hierarchy { get; }

    public MotionPanelViewModel Motion { get; }

    public CommandStateViewModel Commands { get; }

    public InspectorOptionsViewModel Options { get; }

    public EditorActionsViewModel Actions { get; }

    public MaterialGraphViewModel Material { get; }

    public ConstraintPanelViewModel Constraints { get; }
}
