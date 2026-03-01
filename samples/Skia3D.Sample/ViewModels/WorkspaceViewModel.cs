using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Skia3D.Editor;
using Skia3D.Sample.Services;

namespace Skia3D.Sample.ViewModels;

public sealed class WorkspaceViewModel : ViewModelBase
{
    private readonly EditorActionsViewModel _actions;
    private readonly WorkspaceLayoutStore _store;
    private IDockLayoutService? _dockLayoutService;
    private readonly Dictionary<string, WorkspaceLayout> _layouts;
    private readonly ObservableCollection<string> _workspaces;
    private string _selectedWorkspace = string.Empty;
    private string _newWorkspaceName = string.Empty;
    private bool _isManagerOpen;
    private bool _showSceneExplorer = true;
    private bool _showRibbon = true;
    private bool _showTimeline = true;
    private bool _showStatusBar = true;
    private bool _showViewportToolbar = true;

    public WorkspaceViewModel(EditorActionsViewModel actions, WorkspaceLayoutStore store)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _actions.PropertyChanged += OnActionsPropertyChanged;

        _layouts = new Dictionary<string, WorkspaceLayout>(StringComparer.OrdinalIgnoreCase);
        _workspaces = new ObservableCollection<string>();

        SaveCommand = new DelegateCommand(Save, CanSave);
        SaveAsCommand = new DelegateCommand(SaveAs, CanSaveAs);
        ToggleManagerCommand = new DelegateCommand(ToggleManager);
        CloseManagerCommand = new DelegateCommand(() => IsManagerOpen = false);
        ResetLayoutCommand = new DelegateCommand(ResetLayout);

        LoadLayouts();
    }

    public ObservableCollection<string> Workspaces => _workspaces;

    public string SelectedWorkspace
    {
        get => _selectedWorkspace;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _selectedWorkspace == value)
            {
                return;
            }

            _selectedWorkspace = value;
            RaisePropertyChanged();
            ApplyWorkspace(_selectedWorkspace);
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewWorkspaceName
    {
        get => _newWorkspaceName;
        set
        {
            if (_newWorkspaceName == value)
            {
                return;
            }

            _newWorkspaceName = value;
            RaisePropertyChanged();
            SaveAsCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsManagerOpen
    {
        get => _isManagerOpen;
        set
        {
            if (_isManagerOpen == value)
            {
                return;
            }

            _isManagerOpen = value;
            RaisePropertyChanged();
        }
    }

    public bool ShowCommandPanel
    {
        get => _actions.IsCommandPanelOpen;
        set
        {
            if (_actions.IsCommandPanelOpen == value)
            {
                return;
            }

            _actions.IsCommandPanelOpen = value;
            RaisePropertyChanged();
        }
    }

    public bool ShowSceneExplorer
    {
        get => _showSceneExplorer;
        set
        {
            if (_showSceneExplorer == value)
            {
                return;
            }

            _showSceneExplorer = value;
            RaisePropertyChanged();
        }
    }

    public bool ShowRibbon
    {
        get => _showRibbon;
        set
        {
            if (_showRibbon == value)
            {
                return;
            }

            _showRibbon = value;
            RaisePropertyChanged();
        }
    }

    public bool ShowTimeline
    {
        get => _showTimeline;
        set
        {
            if (_showTimeline == value)
            {
                return;
            }

            _showTimeline = value;
            RaisePropertyChanged();
        }
    }

    public bool ShowStatusBar
    {
        get => _showStatusBar;
        set
        {
            if (_showStatusBar == value)
            {
                return;
            }

            _showStatusBar = value;
            RaisePropertyChanged();
        }
    }

    public bool ShowViewportToolbar
    {
        get => _showViewportToolbar;
        set
        {
            if (_showViewportToolbar == value)
            {
                return;
            }

            _showViewportToolbar = value;
            RaisePropertyChanged();
        }
    }

    public DelegateCommand SaveCommand { get; }

    public DelegateCommand SaveAsCommand { get; }

    public DelegateCommand ToggleManagerCommand { get; }

    public DelegateCommand CloseManagerCommand { get; }

    public DelegateCommand ResetLayoutCommand { get; }

    public void AttachDockLayoutService(IDockLayoutService dockLayoutService)
    {
        _dockLayoutService = dockLayoutService;
        if (!string.IsNullOrWhiteSpace(_selectedWorkspace) && _layouts.TryGetValue(_selectedWorkspace, out var layout))
        {
            _dockLayoutService.RestoreLayout(layout.DockLayout);
        }
    }

    private void ToggleManager()
    {
        IsManagerOpen = !IsManagerOpen;
    }

    private void ResetLayout()
    {
        if (string.IsNullOrWhiteSpace(_selectedWorkspace))
        {
            return;
        }

        var layout = BuildDefaultLayout(_selectedWorkspace);
        _layouts[_selectedWorkspace] = layout;
        ApplyLayout(layout);
        _dockLayoutService?.ResetLayout();
        PersistLayouts();
    }

    private void ApplyWorkspace(string workspace)
    {
        if (!_layouts.TryGetValue(workspace, out var layout))
        {
            layout = BuildDefaultLayout(workspace);
            _layouts[workspace] = layout;
        }

        ApplyLayout(layout);
    }

    private void ApplyLayout(WorkspaceLayout layout)
    {
        ShowRibbon = layout.ShowRibbon;
        ShowSceneExplorer = layout.ShowSceneExplorer;
        ShowCommandPanel = layout.ShowCommandPanel;
        ShowViewportToolbar = layout.ShowViewportToolbar;
        ShowTimeline = layout.ShowTimeline;
        ShowStatusBar = layout.ShowStatusBar;
        _dockLayoutService?.RestoreLayout(layout.DockLayout);
    }

    private WorkspaceLayout BuildSnapshot(string name)
    {
        return new WorkspaceLayout
        {
            Name = name,
            ShowRibbon = ShowRibbon,
            ShowSceneExplorer = ShowSceneExplorer,
            ShowCommandPanel = ShowCommandPanel,
            ShowViewportToolbar = ShowViewportToolbar,
            ShowTimeline = ShowTimeline,
            ShowStatusBar = ShowStatusBar,
            DockLayout = _dockLayoutService?.CaptureLayout()
        };
    }

    private WorkspaceLayout BuildDefaultLayout(string workspace)
    {
        return workspace switch
        {
            "UV Editing" => new WorkspaceLayout
            {
                Name = workspace,
                ShowRibbon = true,
                ShowSceneExplorer = true,
                ShowCommandPanel = true,
                ShowViewportToolbar = true,
                ShowTimeline = false,
                ShowStatusBar = true
            },
            "Presentation" => new WorkspaceLayout
            {
                Name = workspace,
                ShowRibbon = false,
                ShowSceneExplorer = false,
                ShowCommandPanel = false,
                ShowViewportToolbar = false,
                ShowTimeline = false,
                ShowStatusBar = true
            },
            _ => new WorkspaceLayout
            {
                Name = workspace,
                ShowRibbon = true,
                ShowSceneExplorer = true,
                ShowCommandPanel = true,
                ShowViewportToolbar = true,
                ShowTimeline = true,
                ShowStatusBar = true
            }
        };
    }

    private void LoadLayouts()
    {
        var data = _store.Load();
        if (data.Workspaces.Count == 0)
        {
            var defaults = new[]
            {
                "Default",
                "Modeling",
                "UV Editing",
                "Animation",
                "Presentation"
            };

            foreach (var name in defaults)
            {
                var layout = BuildDefaultLayout(name);
                _layouts[name] = layout;
                _workspaces.Add(name);
            }
        }
        else
        {
            foreach (var layout in data.Workspaces)
            {
                if (string.IsNullOrWhiteSpace(layout.Name) || _layouts.ContainsKey(layout.Name))
                {
                    continue;
                }

                _layouts[layout.Name] = layout;
                _workspaces.Add(layout.Name);
            }
        }

        if (_workspaces.Count == 0)
        {
            var fallback = "Default";
            _layouts[fallback] = BuildDefaultLayout(fallback);
            _workspaces.Add(fallback);
        }

        var selected = data.SelectedWorkspace;
        if (string.IsNullOrWhiteSpace(selected) || !_layouts.ContainsKey(selected))
        {
            selected = _workspaces[0];
        }

        _selectedWorkspace = selected;
        RaisePropertyChanged(nameof(SelectedWorkspace));
        ApplyWorkspace(_selectedWorkspace);
        SaveCommand.RaiseCanExecuteChanged();
        SaveAsCommand.RaiseCanExecuteChanged();
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_selectedWorkspace))
        {
            return;
        }

        _layouts[_selectedWorkspace] = BuildSnapshot(_selectedWorkspace);
        PersistLayouts();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(_selectedWorkspace);

    private void SaveAs()
    {
        var name = (_newWorkspaceName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!_layouts.ContainsKey(name))
        {
            _workspaces.Add(name);
        }

        _layouts[name] = BuildSnapshot(name);
        SelectedWorkspace = name;
        NewWorkspaceName = string.Empty;
        PersistLayouts();
    }

    private bool CanSaveAs() => !string.IsNullOrWhiteSpace(_newWorkspaceName);

    private void PersistLayouts()
    {
        var ordered = new List<WorkspaceLayout>(_workspaces.Count);
        foreach (var name in _workspaces)
        {
            if (_layouts.TryGetValue(name, out var layout))
            {
                ordered.Add(layout);
            }
        }

        _store.Save(new WorkspaceStoreData
        {
            SelectedWorkspace = _selectedWorkspace,
            Workspaces = ordered
        });
    }

    private void OnActionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorActionsViewModel.IsCommandPanelOpen))
        {
            RaisePropertyChanged(nameof(ShowCommandPanel));
        }
    }
}
