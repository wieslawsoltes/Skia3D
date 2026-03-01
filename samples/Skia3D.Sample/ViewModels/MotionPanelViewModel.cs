using System;
using System.Reactive;
using ReactiveUI;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class MotionPanelViewModel : ViewModelBase
{
    private bool _canPlay;
    private bool _isPlaying;
    private string _playLabel = "Play";
    private bool _canLoop;
    private bool _loop;
    private bool _canReset;
    private bool _canAdjustSpeed;
    private double _speed = 1.0;
    private string _speedLabel = "Speed: --";
    private string _statusLabel = "No animation loaded";
    private bool _canScrub;
    private double _time;
    private double _timeMax = 1.0;
    private string _timeLabel = "Time: --";
    private bool _autoKeyEnabled;
    private bool _canSetKey;

    public MotionPanelViewModel()
    {
        Editor = new AnimationEditorViewModel();
        var canSetKey = this.WhenAnyValue(vm => vm.CanSetKey);
        SetKeyCommand = ReactiveCommand.Create(() => { }, canSetKey);
    }

    public AnimationEditorViewModel Editor { get; }

    public bool CanPlay
    {
        get => _canPlay;
        set
        {
            if (_canPlay == value)
            {
                return;
            }

            _canPlay = value;
            RaisePropertyChanged();
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value)
            {
                return;
            }

            _isPlaying = value;
            RaisePropertyChanged();
        }
    }

    public string PlayLabel
    {
        get => _playLabel;
        set
        {
            if (_playLabel == value)
            {
                return;
            }

            _playLabel = value;
            RaisePropertyChanged();
        }
    }

    public bool CanLoop
    {
        get => _canLoop;
        set
        {
            if (_canLoop == value)
            {
                return;
            }

            _canLoop = value;
            RaisePropertyChanged();
        }
    }

    public bool Loop
    {
        get => _loop;
        set
        {
            if (_loop == value)
            {
                return;
            }

            _loop = value;
            RaisePropertyChanged();
        }
    }

    public bool CanReset
    {
        get => _canReset;
        set
        {
            if (_canReset == value)
            {
                return;
            }

            _canReset = value;
            RaisePropertyChanged();
        }
    }

    public bool CanAdjustSpeed
    {
        get => _canAdjustSpeed;
        set
        {
            if (_canAdjustSpeed == value)
            {
                return;
            }

            _canAdjustSpeed = value;
            RaisePropertyChanged();
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            if (System.Math.Abs(_speed - value) < 1e-6)
            {
                return;
            }

            _speed = value;
            RaisePropertyChanged();
        }
    }

    public string SpeedLabel
    {
        get => _speedLabel;
        set
        {
            if (_speedLabel == value)
            {
                return;
            }

            _speedLabel = value;
            RaisePropertyChanged();
        }
    }

    public string StatusLabel
    {
        get => _statusLabel;
        set
        {
            if (_statusLabel == value)
            {
                return;
            }

            _statusLabel = value;
            RaisePropertyChanged();
        }
    }

    public bool CanScrub
    {
        get => _canScrub;
        set
        {
            if (_canScrub == value)
            {
                return;
            }

            _canScrub = value;
            RaisePropertyChanged();
        }
    }

    public double Time
    {
        get => _time;
        set
        {
            if (System.Math.Abs(_time - value) < 1e-6)
            {
                return;
            }

            _time = value;
            RaisePropertyChanged();
        }
    }

    public double TimeMax
    {
        get => _timeMax;
        set
        {
            if (System.Math.Abs(_timeMax - value) < 1e-6)
            {
                return;
            }

            _timeMax = value;
            RaisePropertyChanged();
        }
    }

    public string TimeLabel
    {
        get => _timeLabel;
        set
        {
            if (_timeLabel == value)
            {
                return;
            }

            _timeLabel = value;
            RaisePropertyChanged();
        }
    }

    public bool AutoKeyEnabled
    {
        get => _autoKeyEnabled;
        set
        {
            if (_autoKeyEnabled == value)
            {
                return;
            }

            _autoKeyEnabled = value;
            RaisePropertyChanged();
        }
    }

    public bool CanSetKey
    {
        get => _canSetKey;
        set
        {
            if (_canSetKey == value)
            {
                return;
            }

            _canSetKey = value;
            RaisePropertyChanged();
        }
    }

    public ReactiveCommand<Unit, Unit> SetKeyCommand { get; }
}
