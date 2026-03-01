using System;
using Dock.Model.Core;
using Dock.Model.Controls;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Skia3D.Editor;
using Skia3D.Sample.Services;

namespace Skia3D.Sample.ViewModels;

public sealed class EditorDockLayoutViewModel : ViewModelBase, IDockLayoutService, IDisposable
{
    private IRootDock _layout;

    public EditorDockLayoutViewModel(
        WorkspaceViewModel workspace,
        ViewportDockContext viewport,
        SceneExplorerDockContext sceneExplorer,
        CommandPanelDockContext commandPanel,
        TimelineDockContext timeline)
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        Viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        SceneExplorer = sceneExplorer ?? throw new ArgumentNullException(nameof(sceneExplorer));
        CommandPanel = commandPanel ?? throw new ArgumentNullException(nameof(commandPanel));
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));

        Factory = new Factory();
        _layout = CreateDefaultLayout();
        Factory.InitLayout(_layout);
    }

    public WorkspaceViewModel Workspace { get; }

    public ViewportDockContext Viewport { get; }

    public SceneExplorerDockContext SceneExplorer { get; }

    public CommandPanelDockContext CommandPanel { get; }

    public TimelineDockContext Timeline { get; }

    public IFactory Factory { get; }

    public IRootDock Layout
    {
        get => _layout;
        private set
        {
            if (ReferenceEquals(_layout, value))
            {
                return;
            }

            _layout = value;
            RaisePropertyChanged();
        }
    }

    public string? CaptureLayout()
    {
        return null;
    }

    public void RestoreLayout(string? layout)
    {
        ResetLayout();
    }

    public void ResetLayout()
    {
        var next = CreateDefaultLayout();
        Factory.InitLayout(next);
        Layout = next;
    }

    private IRootDock CreateDefaultLayout()
    {
        var viewportDocument = Factory.CreateDocument();
        viewportDocument.Id = "Document.Viewport";
        viewportDocument.Title = "Viewport";
        viewportDocument.CanClose = false;
        viewportDocument.CanFloat = false;
        viewportDocument.Context = Viewport;

        var documents = Factory.CreateDocumentDock();
        documents.Id = "Dock.Documents";
        documents.Title = "Documents";
        documents.Proportion = 0.72;
        documents.CanClose = false;
        documents.VisibleDockables = Factory.CreateList<IDockable>(viewportDocument);
        documents.ActiveDockable = viewportDocument;
        documents.DefaultDockable = viewportDocument;

        var sceneTool = Factory.CreateTool();
        sceneTool.Id = "Tool.SceneExplorer";
        sceneTool.Title = "Scene";
        sceneTool.CanClose = false;
        sceneTool.Context = SceneExplorer;

        var commandTool = Factory.CreateTool();
        commandTool.Id = "Tool.CommandPanel";
        commandTool.Title = "Inspector";
        commandTool.CanClose = false;
        commandTool.Context = CommandPanel;

        var timelineTool = Factory.CreateTool();
        timelineTool.Id = "Tool.Timeline";
        timelineTool.Title = "Timeline";
        timelineTool.CanClose = false;
        timelineTool.Context = Timeline;

        var tools = Factory.CreateToolDock();
        tools.Id = "Dock.Tools";
        tools.Title = "Tools";
        tools.Proportion = 0.28;
        tools.CanClose = false;
        tools.VisibleDockables = Factory.CreateList<IDockable>(sceneTool, commandTool, timelineTool);
        tools.ActiveDockable = sceneTool;
        tools.DefaultDockable = sceneTool;

        var main = Factory.CreateProportionalDock();
        main.Id = "Dock.Main";
        main.Title = "Main";
        main.CanClose = false;
        main.VisibleDockables = Factory.CreateList<IDockable>(documents, Factory.CreateProportionalDockSplitter(), tools);
        main.ActiveDockable = documents;
        main.DefaultDockable = documents;

        var root = Factory.CreateRootDock();
        root.Id = "Root";
        root.Title = "Root";
        root.VisibleDockables = Factory.CreateList<IDockable>(main);
        root.ActiveDockable = main;
        root.DefaultDockable = main;
        return root;
    }

    public void Dispose()
    {
    }
}
