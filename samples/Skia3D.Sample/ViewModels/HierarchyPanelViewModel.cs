using System;
using System.Collections.ObjectModel;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class HierarchyPanelViewModel : ViewModelBase
{
    private SceneNodeItem? _selectedItem;
    private string _selectionLabel = "Selection: none";
    private string _positionLabel = "Position: --";
    private string _rotationLabel = "Rotation: --";
    private string _scaleLabel = "Scale: --";
    private bool _canCenterPivot;
    private bool _canResetTransform;
    private bool _canRename;
    private bool _canIsolate;
    private bool _canUnhide;
    private bool _suppressSelectionEvent;

    public event Action<SceneNodeItem?>? SelectionChanged;

    public ObservableCollection<SceneNodeItem> Items { get; } = new();

    public SceneNodeItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value))
            {
                return;
            }

            _selectedItem = value;
            RaisePropertyChanged();

            if (!_suppressSelectionEvent)
            {
                SelectionChanged?.Invoke(_selectedItem);
            }
        }
    }

    public string SelectionLabel
    {
        get => _selectionLabel;
        set
        {
            if (_selectionLabel == value)
            {
                return;
            }

            _selectionLabel = value;
            RaisePropertyChanged();
        }
    }

    public string PositionLabel
    {
        get => _positionLabel;
        set
        {
            if (_positionLabel == value)
            {
                return;
            }

            _positionLabel = value;
            RaisePropertyChanged();
        }
    }

    public string RotationLabel
    {
        get => _rotationLabel;
        set
        {
            if (_rotationLabel == value)
            {
                return;
            }

            _rotationLabel = value;
            RaisePropertyChanged();
        }
    }

    public string ScaleLabel
    {
        get => _scaleLabel;
        set
        {
            if (_scaleLabel == value)
            {
                return;
            }

            _scaleLabel = value;
            RaisePropertyChanged();
        }
    }

    public bool CanCenterPivot
    {
        get => _canCenterPivot;
        set
        {
            if (_canCenterPivot == value)
            {
                return;
            }

            _canCenterPivot = value;
            RaisePropertyChanged();
        }
    }

    public bool CanResetTransform
    {
        get => _canResetTransform;
        set
        {
            if (_canResetTransform == value)
            {
                return;
            }

            _canResetTransform = value;
            RaisePropertyChanged();
        }
    }

    public bool CanRename
    {
        get => _canRename;
        set
        {
            if (_canRename == value)
            {
                return;
            }

            _canRename = value;
            RaisePropertyChanged();
        }
    }

    public bool CanIsolate
    {
        get => _canIsolate;
        set
        {
            if (_canIsolate == value)
            {
                return;
            }

            _canIsolate = value;
            RaisePropertyChanged();
        }
    }

    public bool CanUnhide
    {
        get => _canUnhide;
        set
        {
            if (_canUnhide == value)
            {
                return;
            }

            _canUnhide = value;
            RaisePropertyChanged();
        }
    }

    public void SetSelectedItem(SceneNodeItem? item)
    {
        if (ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        _suppressSelectionEvent = true;
        _selectedItem = item;
        RaisePropertyChanged(nameof(SelectedItem));
        _suppressSelectionEvent = false;
    }
}
