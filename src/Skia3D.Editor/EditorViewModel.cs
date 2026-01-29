using System;

namespace Skia3D.Editor;

public sealed class EditorViewModel : ViewModelBase
{
    public EditorViewModel(EditorSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Session.SelectionService.SelectionChanged += OnSelectionChanged;
        Session.MeshEdits.SelectionChanged += OnSelectionChanged;
        Session.MeshEdits.CommandStateChanged += OnCommandStateChanged;
        RefreshAll();
    }

    public EditorSession Session { get; }

    public string SelectionSummary => Session.SelectionService.BuildSelectionSummary(compact: false);

    public string SelectionSummaryCompact => Session.SelectionService.BuildSelectionSummary(compact: true);

    public string SelectionStatusText => Session.SelectionService.BuildSelectionStatusText();

    public bool CanUndo => Session.MeshEdits.CanUndo;

    public bool CanRedo => Session.MeshEdits.CanRedo;

    public void RefreshAll()
    {
        OnSelectionChanged();
        OnCommandStateChanged();
    }

    private void OnSelectionChanged()
    {
        RaisePropertyChanged(nameof(SelectionSummary));
        RaisePropertyChanged(nameof(SelectionSummaryCompact));
        RaisePropertyChanged(nameof(SelectionStatusText));
    }

    private void OnCommandStateChanged()
    {
        RaisePropertyChanged(nameof(CanUndo));
        RaisePropertyChanged(nameof(CanRedo));
    }
}
