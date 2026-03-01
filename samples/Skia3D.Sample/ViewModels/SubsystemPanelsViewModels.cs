using System;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class VfxPanelViewModel : ViewModelBase
{
    private bool _hasSelection;
    private string _selectionLabel = "Selection: none";
    private bool _hasEmitter;
    private bool _emitterEnabled = true;
    private bool _isEmitting = true;
    private bool _worldSpace;
    private double _maxParticles = 256;
    private double _emissionRate = 20;
    private double _lifetime = 2;
    private double _startSize = 0.2;
    private double _endSize = 0.05;
    private double _baseVelX;
    private double _baseVelY = 1;
    private double _baseVelZ;
    private double _velRandX = 0.5;
    private double _velRandY = 0.5;
    private double _velRandZ = 0.5;
    private double _startColorR = 1;
    private double _startColorG = 0.78;
    private double _startColorB = 0.47;
    private double _startColorA = 0.78;
    private double _endColorR = 1;
    private double _endColorG = 0.31;
    private double _endColorB = 0.08;
    private double _endColorA;

    public VfxPanelViewModel()
    {
        AddEmitterCommand = new DelegateCommand(() => AddEmitterRequested?.Invoke(), () => HasSelection && !HasEmitter);
        RemoveEmitterCommand = new DelegateCommand(() => RemoveEmitterRequested?.Invoke(), () => HasEmitter);
    }

    public event Action? AddEmitterRequested;

    public event Action? RemoveEmitterRequested;

    public bool HasSelection
    {
        get => _hasSelection;
        set
        {
            if (_hasSelection == value)
            {
                return;
            }

            _hasSelection = value;
            RaisePropertyChanged();
            AddEmitterCommand.RaiseCanExecuteChanged();
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

    public bool HasEmitter
    {
        get => _hasEmitter;
        set
        {
            if (_hasEmitter == value)
            {
                return;
            }

            _hasEmitter = value;
            RaisePropertyChanged();
            AddEmitterCommand.RaiseCanExecuteChanged();
            RemoveEmitterCommand.RaiseCanExecuteChanged();
        }
    }

    public bool EmitterEnabled
    {
        get => _emitterEnabled;
        set => SetField(ref _emitterEnabled, value);
    }

    public bool IsEmitting
    {
        get => _isEmitting;
        set => SetField(ref _isEmitting, value);
    }

    public bool WorldSpace
    {
        get => _worldSpace;
        set => SetField(ref _worldSpace, value);
    }

    public double MaxParticles
    {
        get => _maxParticles;
        set => SetField(ref _maxParticles, Math.Clamp(value, 1, 8192));
    }

    public double EmissionRate
    {
        get => _emissionRate;
        set => SetField(ref _emissionRate, Math.Clamp(value, 0, 4096));
    }

    public double Lifetime
    {
        get => _lifetime;
        set => SetField(ref _lifetime, Math.Clamp(value, 0.05, 60));
    }

    public double StartSize
    {
        get => _startSize;
        set => SetField(ref _startSize, Math.Clamp(value, 0.001, 50));
    }

    public double EndSize
    {
        get => _endSize;
        set => SetField(ref _endSize, Math.Clamp(value, 0.001, 50));
    }

    public double BaseVelX
    {
        get => _baseVelX;
        set => SetField(ref _baseVelX, value);
    }

    public double BaseVelY
    {
        get => _baseVelY;
        set => SetField(ref _baseVelY, value);
    }

    public double BaseVelZ
    {
        get => _baseVelZ;
        set => SetField(ref _baseVelZ, value);
    }

    public double VelRandX
    {
        get => _velRandX;
        set => SetField(ref _velRandX, Math.Max(0, value));
    }

    public double VelRandY
    {
        get => _velRandY;
        set => SetField(ref _velRandY, Math.Max(0, value));
    }

    public double VelRandZ
    {
        get => _velRandZ;
        set => SetField(ref _velRandZ, Math.Max(0, value));
    }

    public double StartColorR
    {
        get => _startColorR;
        set => SetField(ref _startColorR, ClampUnit(value));
    }

    public double StartColorG
    {
        get => _startColorG;
        set => SetField(ref _startColorG, ClampUnit(value));
    }

    public double StartColorB
    {
        get => _startColorB;
        set => SetField(ref _startColorB, ClampUnit(value));
    }

    public double StartColorA
    {
        get => _startColorA;
        set => SetField(ref _startColorA, ClampUnit(value));
    }

    public double EndColorR
    {
        get => _endColorR;
        set => SetField(ref _endColorR, ClampUnit(value));
    }

    public double EndColorG
    {
        get => _endColorG;
        set => SetField(ref _endColorG, ClampUnit(value));
    }

    public double EndColorB
    {
        get => _endColorB;
        set => SetField(ref _endColorB, ClampUnit(value));
    }

    public double EndColorA
    {
        get => _endColorA;
        set => SetField(ref _endColorA, ClampUnit(value));
    }

    public DelegateCommand AddEmitterCommand { get; }

    public DelegateCommand RemoveEmitterCommand { get; }

    private void SetField<T>(ref T field, T value, string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);
    }

    private static double ClampUnit(double value) => Math.Clamp(value, 0, 1);
}

public sealed class PhysicsPanelViewModel : ViewModelBase
{
    private bool _hasSelection;
    private string _selectionLabel = "Selection: none";
    private bool _hasRigidBody;
    private bool _bodyEnabled = true;
    private int _bodyTypeIndex = 1;
    private double _mass = 1;
    private bool _useGravity = true;
    private double _restitution = 0.1;
    private double _friction = 0.6;
    private double _linearDamping = 0.01;
    private double _angularDamping = 0.01;
    private bool _isAwake = true;
    private bool _hasCollider;
    private bool _colliderEnabled = true;
    private bool _isTrigger;
    private int _colliderShapeIndex;
    private double _colliderRadius = 0.5;
    private double _colliderSizeX = 1;
    private double _colliderSizeY = 1;
    private double _colliderSizeZ = 1;
    private double _colliderOffsetX;
    private double _colliderOffsetY;
    private double _colliderOffsetZ;

    public PhysicsPanelViewModel()
    {
        AddRigidBodyCommand = new DelegateCommand(() => AddRigidBodyRequested?.Invoke(), () => HasSelection && !HasRigidBody);
        RemoveRigidBodyCommand = new DelegateCommand(() => RemoveRigidBodyRequested?.Invoke(), () => HasRigidBody);
        AddColliderCommand = new DelegateCommand(() => AddColliderRequested?.Invoke(), () => HasSelection && !HasCollider);
        RemoveColliderCommand = new DelegateCommand(() => RemoveColliderRequested?.Invoke(), () => HasCollider);
    }

    public event Action? AddRigidBodyRequested;

    public event Action? RemoveRigidBodyRequested;

    public event Action? AddColliderRequested;

    public event Action? RemoveColliderRequested;

    public bool HasSelection
    {
        get => _hasSelection;
        set
        {
            if (_hasSelection == value)
            {
                return;
            }

            _hasSelection = value;
            RaisePropertyChanged();
            AddRigidBodyCommand.RaiseCanExecuteChanged();
            AddColliderCommand.RaiseCanExecuteChanged();
        }
    }

    public string SelectionLabel
    {
        get => _selectionLabel;
        set => SetField(ref _selectionLabel, value);
    }

    public bool HasRigidBody
    {
        get => _hasRigidBody;
        set
        {
            if (_hasRigidBody == value)
            {
                return;
            }

            _hasRigidBody = value;
            RaisePropertyChanged();
            AddRigidBodyCommand.RaiseCanExecuteChanged();
            RemoveRigidBodyCommand.RaiseCanExecuteChanged();
        }
    }

    public bool BodyEnabled
    {
        get => _bodyEnabled;
        set => SetField(ref _bodyEnabled, value);
    }

    public int BodyTypeIndex
    {
        get => _bodyTypeIndex;
        set => SetField(ref _bodyTypeIndex, Math.Clamp(value, 0, 2));
    }

    public double Mass
    {
        get => _mass;
        set => SetField(ref _mass, Math.Clamp(value, 0.01, 500));
    }

    public bool UseGravity
    {
        get => _useGravity;
        set => SetField(ref _useGravity, value);
    }

    public double Restitution
    {
        get => _restitution;
        set => SetField(ref _restitution, Math.Clamp(value, 0, 1));
    }

    public double Friction
    {
        get => _friction;
        set => SetField(ref _friction, Math.Clamp(value, 0, 1));
    }

    public double LinearDamping
    {
        get => _linearDamping;
        set => SetField(ref _linearDamping, Math.Clamp(value, 0, 1));
    }

    public double AngularDamping
    {
        get => _angularDamping;
        set => SetField(ref _angularDamping, Math.Clamp(value, 0, 1));
    }

    public bool IsAwake
    {
        get => _isAwake;
        set => SetField(ref _isAwake, value);
    }

    public bool HasCollider
    {
        get => _hasCollider;
        set
        {
            if (_hasCollider == value)
            {
                return;
            }

            _hasCollider = value;
            RaisePropertyChanged();
            AddColliderCommand.RaiseCanExecuteChanged();
            RemoveColliderCommand.RaiseCanExecuteChanged();
        }
    }

    public bool ColliderEnabled
    {
        get => _colliderEnabled;
        set => SetField(ref _colliderEnabled, value);
    }

    public bool IsTrigger
    {
        get => _isTrigger;
        set => SetField(ref _isTrigger, value);
    }

    public int ColliderShapeIndex
    {
        get => _colliderShapeIndex;
        set
        {
            if (_colliderShapeIndex == value)
            {
                return;
            }

            _colliderShapeIndex = Math.Clamp(value, 0, 1);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSphereCollider));
            RaisePropertyChanged(nameof(IsBoxCollider));
        }
    }

    public bool IsSphereCollider => ColliderShapeIndex == 0;

    public bool IsBoxCollider => ColliderShapeIndex == 1;

    public double ColliderRadius
    {
        get => _colliderRadius;
        set => SetField(ref _colliderRadius, Math.Clamp(value, 0.01, 100));
    }

    public double ColliderSizeX
    {
        get => _colliderSizeX;
        set => SetField(ref _colliderSizeX, Math.Clamp(value, 0.01, 100));
    }

    public double ColliderSizeY
    {
        get => _colliderSizeY;
        set => SetField(ref _colliderSizeY, Math.Clamp(value, 0.01, 100));
    }

    public double ColliderSizeZ
    {
        get => _colliderSizeZ;
        set => SetField(ref _colliderSizeZ, Math.Clamp(value, 0.01, 100));
    }

    public double ColliderOffsetX
    {
        get => _colliderOffsetX;
        set => SetField(ref _colliderOffsetX, value);
    }

    public double ColliderOffsetY
    {
        get => _colliderOffsetY;
        set => SetField(ref _colliderOffsetY, value);
    }

    public double ColliderOffsetZ
    {
        get => _colliderOffsetZ;
        set => SetField(ref _colliderOffsetZ, value);
    }

    public DelegateCommand AddRigidBodyCommand { get; }

    public DelegateCommand RemoveRigidBodyCommand { get; }

    public DelegateCommand AddColliderCommand { get; }

    public DelegateCommand RemoveColliderCommand { get; }

    private void SetField<T>(ref T field, T value, string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);
    }
}

public sealed class NavigationPanelViewModel : ViewModelBase
{
    private bool _hasSelection;
    private string _selectionLabel = "Selection: none";
    private bool _hasNavGrid;
    private double _gridWidth = 16;
    private double _gridHeight = 16;
    private double _cellSize = 1;
    private double _originX;
    private double _originY;
    private double _originZ;
    private bool _hasNavAgent;
    private double _agentSpeed = 2;
    private double _agentStoppingDistance = 0.1;
    private string _destinationX = "0";
    private string _destinationY = "0";
    private string _destinationZ = "0";
    private string _pathLabel = "Path: none";

    public NavigationPanelViewModel()
    {
        AddGridCommand = new DelegateCommand(() => AddGridRequested?.Invoke(), () => HasSelection && !HasNavGrid);
        RemoveGridCommand = new DelegateCommand(() => RemoveGridRequested?.Invoke(), () => HasNavGrid);
        ApplyGridCommand = new DelegateCommand(() => ApplyGridRequested?.Invoke(), () => HasNavGrid);
        BakeGridCommand = new DelegateCommand(() => BakeGridRequested?.Invoke(), () => HasNavGrid);
        AddAgentCommand = new DelegateCommand(() => AddAgentRequested?.Invoke(), () => HasSelection && !HasNavAgent);
        RemoveAgentCommand = new DelegateCommand(() => RemoveAgentRequested?.Invoke(), () => HasNavAgent);
        ApplyDestinationCommand = new DelegateCommand(() => ApplyDestinationRequested?.Invoke(), () => HasNavAgent);
        ClearDestinationCommand = new DelegateCommand(() => ClearDestinationRequested?.Invoke(), () => HasNavAgent);
    }

    public event Action? AddGridRequested;

    public event Action? RemoveGridRequested;

    public event Action? ApplyGridRequested;

    public event Action? BakeGridRequested;

    public event Action? AddAgentRequested;

    public event Action? RemoveAgentRequested;

    public event Action? ApplyDestinationRequested;

    public event Action? ClearDestinationRequested;

    public bool HasSelection
    {
        get => _hasSelection;
        set
        {
            if (_hasSelection == value)
            {
                return;
            }

            _hasSelection = value;
            RaisePropertyChanged();
            AddGridCommand.RaiseCanExecuteChanged();
            AddAgentCommand.RaiseCanExecuteChanged();
        }
    }

    public string SelectionLabel
    {
        get => _selectionLabel;
        set => SetField(ref _selectionLabel, value);
    }

    public bool HasNavGrid
    {
        get => _hasNavGrid;
        set
        {
            if (_hasNavGrid == value)
            {
                return;
            }

            _hasNavGrid = value;
            RaisePropertyChanged();
            AddGridCommand.RaiseCanExecuteChanged();
            RemoveGridCommand.RaiseCanExecuteChanged();
            ApplyGridCommand.RaiseCanExecuteChanged();
            BakeGridCommand.RaiseCanExecuteChanged();
        }
    }

    public double GridWidth
    {
        get => _gridWidth;
        set => SetField(ref _gridWidth, Math.Clamp(value, 1, 512));
    }

    public double GridHeight
    {
        get => _gridHeight;
        set => SetField(ref _gridHeight, Math.Clamp(value, 1, 512));
    }

    public double CellSize
    {
        get => _cellSize;
        set => SetField(ref _cellSize, Math.Clamp(value, 0.01, 100));
    }

    public double OriginX
    {
        get => _originX;
        set => SetField(ref _originX, value);
    }

    public double OriginY
    {
        get => _originY;
        set => SetField(ref _originY, value);
    }

    public double OriginZ
    {
        get => _originZ;
        set => SetField(ref _originZ, value);
    }

    public bool HasNavAgent
    {
        get => _hasNavAgent;
        set
        {
            if (_hasNavAgent == value)
            {
                return;
            }

            _hasNavAgent = value;
            RaisePropertyChanged();
            AddAgentCommand.RaiseCanExecuteChanged();
            RemoveAgentCommand.RaiseCanExecuteChanged();
            ApplyDestinationCommand.RaiseCanExecuteChanged();
            ClearDestinationCommand.RaiseCanExecuteChanged();
        }
    }

    public double AgentSpeed
    {
        get => _agentSpeed;
        set => SetField(ref _agentSpeed, Math.Clamp(value, 0, 100));
    }

    public double AgentStoppingDistance
    {
        get => _agentStoppingDistance;
        set => SetField(ref _agentStoppingDistance, Math.Clamp(value, 0, 20));
    }

    public string DestinationX
    {
        get => _destinationX;
        set => SetField(ref _destinationX, value);
    }

    public string DestinationY
    {
        get => _destinationY;
        set => SetField(ref _destinationY, value);
    }

    public string DestinationZ
    {
        get => _destinationZ;
        set => SetField(ref _destinationZ, value);
    }

    public string PathLabel
    {
        get => _pathLabel;
        set => SetField(ref _pathLabel, value);
    }

    public DelegateCommand AddGridCommand { get; }

    public DelegateCommand RemoveGridCommand { get; }

    public DelegateCommand ApplyGridCommand { get; }

    public DelegateCommand BakeGridCommand { get; }

    public DelegateCommand AddAgentCommand { get; }

    public DelegateCommand RemoveAgentCommand { get; }

    public DelegateCommand ApplyDestinationCommand { get; }

    public DelegateCommand ClearDestinationCommand { get; }

    private void SetField<T>(ref T field, T value, string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);
    }
}

public sealed class AudioPanelViewModel : ViewModelBase
{
    private bool _hasSelection;
    private string _selectionLabel = "Selection: none";
    private bool _hasListener;
    private bool _listenerEnabled = true;
    private double _listenerVolume = 1;
    private bool _hasSource;
    private bool _sourceEnabled = true;
    private bool _sourceLoop;
    private string _clipLabel = "Clip: none";
    private string _playbackLabel = "Playback: stopped";
    private double _clipDuration = 1;
    private double _sourceVolume = 1;
    private double _sourcePitch = 1;
    private double _sourceSpatialBlend = 1;
    private double _sourceMinDistance = 1;
    private double _sourceMaxDistance = 30;

    public AudioPanelViewModel()
    {
        AddListenerCommand = new DelegateCommand(() => AddListenerRequested?.Invoke(), () => HasSelection && !HasListener);
        RemoveListenerCommand = new DelegateCommand(() => RemoveListenerRequested?.Invoke(), () => HasListener);
        AddSourceCommand = new DelegateCommand(() => AddSourceRequested?.Invoke(), () => HasSelection && !HasSource);
        RemoveSourceCommand = new DelegateCommand(() => RemoveSourceRequested?.Invoke(), () => HasSource);
        PlayCommand = new DelegateCommand(() => PlayRequested?.Invoke(), () => HasSource);
        StopCommand = new DelegateCommand(() => StopRequested?.Invoke(), () => HasSource);
        CreateClipCommand = new DelegateCommand(() => CreateClipRequested?.Invoke(), () => HasSource);
        ClearClipCommand = new DelegateCommand(() => ClearClipRequested?.Invoke(), () => HasSource);
    }

    public event Action? AddListenerRequested;

    public event Action? RemoveListenerRequested;

    public event Action? AddSourceRequested;

    public event Action? RemoveSourceRequested;

    public event Action? PlayRequested;

    public event Action? StopRequested;

    public event Action? CreateClipRequested;

    public event Action? ClearClipRequested;

    public bool HasSelection
    {
        get => _hasSelection;
        set
        {
            if (_hasSelection == value)
            {
                return;
            }

            _hasSelection = value;
            RaisePropertyChanged();
            AddListenerCommand.RaiseCanExecuteChanged();
            AddSourceCommand.RaiseCanExecuteChanged();
        }
    }

    public string SelectionLabel
    {
        get => _selectionLabel;
        set => SetField(ref _selectionLabel, value);
    }

    public bool HasListener
    {
        get => _hasListener;
        set
        {
            if (_hasListener == value)
            {
                return;
            }

            _hasListener = value;
            RaisePropertyChanged();
            AddListenerCommand.RaiseCanExecuteChanged();
            RemoveListenerCommand.RaiseCanExecuteChanged();
        }
    }

    public bool ListenerEnabled
    {
        get => _listenerEnabled;
        set => SetField(ref _listenerEnabled, value);
    }

    public double ListenerVolume
    {
        get => _listenerVolume;
        set => SetField(ref _listenerVolume, Math.Clamp(value, 0, 2));
    }

    public bool HasSource
    {
        get => _hasSource;
        set
        {
            if (_hasSource == value)
            {
                return;
            }

            _hasSource = value;
            RaisePropertyChanged();
            AddSourceCommand.RaiseCanExecuteChanged();
            RemoveSourceCommand.RaiseCanExecuteChanged();
            PlayCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            CreateClipCommand.RaiseCanExecuteChanged();
            ClearClipCommand.RaiseCanExecuteChanged();
        }
    }

    public bool SourceEnabled
    {
        get => _sourceEnabled;
        set => SetField(ref _sourceEnabled, value);
    }

    public bool SourceLoop
    {
        get => _sourceLoop;
        set => SetField(ref _sourceLoop, value);
    }

    public string ClipLabel
    {
        get => _clipLabel;
        set => SetField(ref _clipLabel, value);
    }

    public string PlaybackLabel
    {
        get => _playbackLabel;
        set => SetField(ref _playbackLabel, value);
    }

    public double ClipDuration
    {
        get => _clipDuration;
        set => SetField(ref _clipDuration, Math.Clamp(value, 0.01, 120));
    }

    public double SourceVolume
    {
        get => _sourceVolume;
        set => SetField(ref _sourceVolume, Math.Clamp(value, 0, 2));
    }

    public double SourcePitch
    {
        get => _sourcePitch;
        set => SetField(ref _sourcePitch, Math.Clamp(value, 0.1, 3));
    }

    public double SourceSpatialBlend
    {
        get => _sourceSpatialBlend;
        set => SetField(ref _sourceSpatialBlend, Math.Clamp(value, 0, 1));
    }

    public double SourceMinDistance
    {
        get => _sourceMinDistance;
        set => SetField(ref _sourceMinDistance, Math.Clamp(value, 0.01, 1000));
    }

    public double SourceMaxDistance
    {
        get => _sourceMaxDistance;
        set => SetField(ref _sourceMaxDistance, Math.Clamp(value, 0.01, 1000));
    }

    public DelegateCommand AddListenerCommand { get; }

    public DelegateCommand RemoveListenerCommand { get; }

    public DelegateCommand AddSourceCommand { get; }

    public DelegateCommand RemoveSourceCommand { get; }

    public DelegateCommand PlayCommand { get; }

    public DelegateCommand StopCommand { get; }

    public DelegateCommand CreateClipCommand { get; }

    public DelegateCommand ClearClipCommand { get; }

    private void SetField<T>(ref T field, T value, string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);
    }
}
