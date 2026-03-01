using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Skia3D.Editor;
using Skia3D.Sample.Models;

namespace Skia3D.Sample.ViewModels;

public sealed class CommandPaletteViewModel : ViewModelBase
{
    private readonly ObservableCollection<EditorCommandDefinition> _allCommands;
    private readonly ObservableCollection<EditorCommandDefinition> _commandsView = new();
    private EditorCommandDefinition? _selectedCommand;
    private string _searchText = string.Empty;
    private bool _isOpen;

    public CommandPaletteViewModel(IEnumerable<EditorCommandDefinition> commands)
    {
        if (commands is null)
        {
            throw new ArgumentNullException(nameof(commands));
        }

        _allCommands = new ObservableCollection<EditorCommandDefinition>(commands);
        Columns = new ObservableCollection<object>();
        SortingModel = null;
        FilteringModel = null;
        SearchModel = null;

        ToggleCommand = new DelegateCommand(() => IsOpen = !IsOpen);
        CloseCommand = new DelegateCommand(() => IsOpen = false);
        ExecuteSelectedCommand = new DelegateCommand(ExecuteSelected, CanExecuteSelected);

        RebuildView();
        if (_commandsView.Count > 0)
        {
            SelectedCommand = _commandsView[0];
        }
    }

    public ObservableCollection<EditorCommandDefinition> CommandsView => _commandsView;

    public ObservableCollection<object> Columns { get; }

    public object? SortingModel { get; }

    public object? FilteringModel { get; }

    public object? SearchModel { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            RaisePropertyChanged();
            RebuildView();
        }
    }

    public EditorCommandDefinition? SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            if (ReferenceEquals(_selectedCommand, value))
            {
                return;
            }

            _selectedCommand = value;
            RaisePropertyChanged();
            ExecuteSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value)
            {
                return;
            }

            _isOpen = value;
            RaisePropertyChanged();

            if (_isOpen)
            {
                RebuildView();
                if (SelectedCommand == null && _commandsView.Count > 0)
                {
                    SelectedCommand = _commandsView[0];
                }
            }
        }
    }

    public DelegateCommand ToggleCommand { get; }

    public DelegateCommand CloseCommand { get; }

    public DelegateCommand ExecuteSelectedCommand { get; }

    private void RebuildView()
    {
        var query = (_searchText ?? string.Empty).Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allCommands
            : new ObservableCollection<EditorCommandDefinition>(_allCommands.Where(command =>
                command.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || command.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || command.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || command.Shortcut.Contains(query, StringComparison.OrdinalIgnoreCase)));

        _commandsView.Clear();
        for (int i = 0; i < filtered.Count; i++)
        {
            _commandsView.Add(filtered[i]);
        }

        if (_selectedCommand != null && !_commandsView.Contains(_selectedCommand))
        {
            SelectedCommand = _commandsView.Count > 0 ? _commandsView[0] : null;
        }

        RaisePropertyChanged(nameof(CommandsView));
        ExecuteSelectedCommand.RaiseCanExecuteChanged();
    }

    private void ExecuteSelected()
    {
        var selected = _selectedCommand;
        if (selected == null)
        {
            return;
        }

        if (selected.Command.CanExecute(null))
        {
            selected.Command.Execute(null);
            IsOpen = false;
        }
    }

    private bool CanExecuteSelected()
    {
        return _selectedCommand != null && _selectedCommand.Command.CanExecute(null);
    }
}

public sealed class ShortcutBindingItem : ViewModelBase
{
    private ShortcutGesture _gesture;

    public ShortcutBindingItem(EditorCommandDefinition command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        _gesture = ShortcutGesture.Parse(command.Shortcut);
    }

    public EditorCommandDefinition Command { get; }

    public string Id => Command.Id;

    public string Name => Command.Name;

    public string Category => Command.Category;

    public ShortcutGesture Gesture
    {
        get => _gesture;
        set
        {
            if (_gesture.Equals(value))
            {
                return;
            }

            _gesture = value;
            Command.Shortcut = value.ToString();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShortcutText));
        }
    }

    public string ShortcutText => Gesture.ToString();

    public void Reset()
    {
        Gesture = ShortcutGesture.Parse(Command.DefaultShortcut);
    }

    public void Clear()
    {
        Gesture = ShortcutGesture.Empty;
    }
}

public sealed class ShortcutEditorViewModel : ViewModelBase
{
    private readonly ObservableCollection<ShortcutBindingItem> _allBindings;
    private readonly ObservableCollection<ShortcutBindingItem> _bindingsView = new();
    private ShortcutBindingItem? _selectedBinding;
    private string _searchText = string.Empty;
    private bool _isOpen;
    private bool _isCapturing;
    private string _captureStatus = "Select a command to edit shortcut.";

    public ShortcutEditorViewModel(IEnumerable<EditorCommandDefinition> commands)
    {
        if (commands is null)
        {
            throw new ArgumentNullException(nameof(commands));
        }

        _allBindings = new ObservableCollection<ShortcutBindingItem>(commands.Select(command => new ShortcutBindingItem(command)));

        Columns = new ObservableCollection<object>();
        SortingModel = null;
        FilteringModel = null;
        SearchModel = null;

        ToggleCommand = new DelegateCommand(() => IsOpen = !IsOpen);
        CloseCommand = new DelegateCommand(Close);
        BeginCaptureCommand = new DelegateCommand(BeginCapture, () => SelectedBinding != null);
        ClearShortcutCommand = new DelegateCommand(ClearShortcut, () => SelectedBinding != null);
        ResetDefaultsCommand = new DelegateCommand(ResetDefaults, () => _allBindings.Count > 0);

        RebuildView();
        if (_bindingsView.Count > 0)
        {
            SelectedBinding = _bindingsView[0];
        }
    }

    public ObservableCollection<ShortcutBindingItem> BindingsView => _bindingsView;

    public ObservableCollection<object> Columns { get; }

    public object? SortingModel { get; }

    public object? FilteringModel { get; }

    public object? SearchModel { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            RaisePropertyChanged();
            RebuildView();
        }
    }

    public ShortcutBindingItem? SelectedBinding
    {
        get => _selectedBinding;
        set
        {
            if (ReferenceEquals(_selectedBinding, value))
            {
                return;
            }

            _selectedBinding = value;
            RaisePropertyChanged();
            BeginCaptureCommand.RaiseCanExecuteChanged();
            ClearShortcutCommand.RaiseCanExecuteChanged();
            UpdateCaptureStatus();
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value)
            {
                return;
            }

            _isOpen = value;
            RaisePropertyChanged();
            if (!_isOpen)
            {
                IsCapturing = false;
            }
        }
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (_isCapturing == value)
            {
                return;
            }

            _isCapturing = value;
            RaisePropertyChanged();
            UpdateCaptureStatus();
        }
    }

    public string CaptureStatus
    {
        get => _captureStatus;
        private set
        {
            if (_captureStatus == value)
            {
                return;
            }

            _captureStatus = value;
            RaisePropertyChanged();
        }
    }

    public DelegateCommand ToggleCommand { get; }

    public DelegateCommand CloseCommand { get; }

    public DelegateCommand BeginCaptureCommand { get; }

    public DelegateCommand ClearShortcutCommand { get; }

    public DelegateCommand ResetDefaultsCommand { get; }

    public void HandleShortcutInput(ShortcutInput? input)
    {
        if (input == null || input.Gesture.IsEmpty)
        {
            return;
        }

        if (IsCapturing)
        {
            var target = SelectedBinding;
            if (target == null)
            {
                IsCapturing = false;
                return;
            }

            ClearDuplicateBinding(input.Gesture, except: target);
            target.Gesture = input.Gesture;
            IsCapturing = false;
            return;
        }

        if (!IsOpen)
        {
            TryExecute(input.Gesture);
            return;
        }

        TryExecute(input.Gesture);
    }

    private void TryExecute(ShortcutGesture gesture)
    {
        for (int i = 0; i < _allBindings.Count; i++)
        {
            var binding = _allBindings[i];
            if (!binding.Gesture.Equals(gesture))
            {
                continue;
            }

            if (!binding.Command.Command.CanExecute(null))
            {
                return;
            }

            binding.Command.Command.Execute(null);
            return;
        }
    }

    private void BeginCapture()
    {
        if (SelectedBinding == null)
        {
            return;
        }

        IsCapturing = true;
    }

    private void ClearShortcut()
    {
        SelectedBinding?.Clear();
        UpdateCaptureStatus();
    }

    private void ResetDefaults()
    {
        for (int i = 0; i < _allBindings.Count; i++)
        {
            _allBindings[i].Reset();
        }

        UpdateCaptureStatus();
    }

    private void Close()
    {
        IsOpen = false;
        IsCapturing = false;
    }

    private void RebuildView()
    {
        var query = (_searchText ?? string.Empty).Trim();

        IEnumerable<ShortcutBindingItem> filtered = _allBindings;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(binding =>
                binding.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || binding.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || binding.ShortcutText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || binding.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _bindingsView.Clear();
        foreach (var item in filtered)
        {
            _bindingsView.Add(item);
        }

        if (_selectedBinding != null && !_bindingsView.Contains(_selectedBinding))
        {
            SelectedBinding = _bindingsView.Count > 0 ? _bindingsView[0] : null;
        }

        RaisePropertyChanged(nameof(BindingsView));
    }

    private void ClearDuplicateBinding(ShortcutGesture gesture, ShortcutBindingItem except)
    {
        for (int i = 0; i < _allBindings.Count; i++)
        {
            var binding = _allBindings[i];
            if (ReferenceEquals(binding, except))
            {
                continue;
            }

            if (binding.Gesture.Equals(gesture))
            {
                binding.Clear();
            }
        }
    }

    private void UpdateCaptureStatus()
    {
        if (IsCapturing)
        {
            CaptureStatus = SelectedBinding == null
                ? "Press a key combination to capture shortcut."
                : $"Press a key combination for {SelectedBinding.Name}.";
            return;
        }

        if (SelectedBinding == null)
        {
            CaptureStatus = "Select a command to edit shortcut.";
            return;
        }

        var shortcut = SelectedBinding.ShortcutText;
        CaptureStatus = string.IsNullOrWhiteSpace(shortcut)
            ? $"{SelectedBinding.Name}: no shortcut assigned"
            : $"{SelectedBinding.Name}: {shortcut}";
    }
}
