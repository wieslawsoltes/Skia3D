using System;
using System.Collections.ObjectModel;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class ConstraintPanelViewModel : ViewModelBase
{
    private SceneNodeItem? _selectedTarget;
    private string _selectionLabel = "Constraint target: none";
    private int _constraintTypeIndex;
    private bool _maintainOffset = true;
    private double _weight = 1.0;
    private bool _canApply;
    private bool _canClear;

    public ConstraintPanelViewModel()
    {
        ApplyConstraintCommand = new DelegateCommand(() => ApplyRequested?.Invoke(), () => CanApply);
        ClearConstraintCommand = new DelegateCommand(() => ClearRequested?.Invoke(), () => CanClear);
    }

    public event Action? ApplyRequested;

    public event Action? ClearRequested;

    public event Action? TargetChanged;

    public ObservableCollection<SceneNodeItem> Targets { get; } = new();

    public SceneNodeItem? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (ReferenceEquals(_selectedTarget, value))
            {
                return;
            }

            _selectedTarget = value;
            RaisePropertyChanged();
            TargetChanged?.Invoke();
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

    public int ConstraintTypeIndex
    {
        get => _constraintTypeIndex;
        set
        {
            if (_constraintTypeIndex == value)
            {
                return;
            }

            _constraintTypeIndex = value;
            RaisePropertyChanged();
        }
    }

    public bool MaintainOffset
    {
        get => _maintainOffset;
        set
        {
            if (_maintainOffset == value)
            {
                return;
            }

            _maintainOffset = value;
            RaisePropertyChanged();
        }
    }

    public double Weight
    {
        get => _weight;
        set
        {
            if (Math.Abs(_weight - value) < 1e-6)
            {
                return;
            }

            _weight = Math.Clamp(value, 0.0, 1.0);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(WeightLabel));
        }
    }

    public string WeightLabel => $"Weight: {_weight:0.00}";

    public bool CanApply
    {
        get => _canApply;
        set
        {
            if (_canApply == value)
            {
                return;
            }

            _canApply = value;
            RaisePropertyChanged();
            ApplyConstraintCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CanClear
    {
        get => _canClear;
        set
        {
            if (_canClear == value)
            {
                return;
            }

            _canClear = value;
            RaisePropertyChanged();
            ClearConstraintCommand.RaiseCanExecuteChanged();
        }
    }

    public DelegateCommand ApplyConstraintCommand { get; }

    public DelegateCommand ClearConstraintCommand { get; }
}
