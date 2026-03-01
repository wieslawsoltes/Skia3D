using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Skia3D.Animation;
using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public enum AnimationKeyframeChannel
{
    Translation,
    Rotation,
    Scale
}

public readonly record struct AnimationCurvePoint(float Time, float Value);

public sealed class AnimationCurveSeries
{
    public AnimationCurveSeries(string name, uint color, IReadOnlyList<AnimationCurvePoint> points)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Color = color;
        Points = points ?? throw new ArgumentNullException(nameof(points));
    }

    public string Name { get; }

    public uint Color { get; }

    public IReadOnlyList<AnimationCurvePoint> Points { get; }
}

public sealed class AnimationClipItem : ViewModelBase
{
    private string _label = string.Empty;
    private double _duration;

    public AnimationClipItem(string name, AnimationClip clip, bool isMixer, bool isEditable)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        IsMixer = isMixer;
        IsEditable = isEditable;
        Refresh();
    }

    public string Name { get; }

    public AnimationClip Clip { get; }

    public bool IsMixer { get; }

    public bool IsEditable { get; }

    public double Duration
    {
        get => _duration;
        private set
        {
            if (Math.Abs(_duration - value) < 1e-6)
            {
                return;
            }

            _duration = value;
            RaisePropertyChanged();
        }
    }

    public string Label
    {
        get => _label;
        private set
        {
            if (_label == value)
            {
                return;
            }

            _label = value;
            RaisePropertyChanged();
        }
    }

    public void Refresh()
    {
        Clip.RecalculateDuration();
        Duration = Clip.Duration;
        var suffix = IsMixer ? " [Mixer]" : string.Empty;
        Label = $"{Name} ({Duration:0.##}s){suffix}";
    }
}

public sealed class AnimationTrackItem : ViewModelBase
{
    private string _label = string.Empty;

    public AnimationTrackItem(TransformTrack track)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
        Refresh();
    }

    public TransformTrack Track { get; }

    public string Label
    {
        get => _label;
        private set
        {
            if (_label == value)
            {
                return;
            }

            _label = value;
            RaisePropertyChanged();
        }
    }

    public void Refresh()
    {
        Label = $"{Track.TargetName} (T:{Track.TranslationKeys.Count} R:{Track.RotationKeys.Count} S:{Track.ScaleKeys.Count})";
    }
}

public sealed class AnimationKeyframeItem
{
    public AnimationKeyframeItem(TransformTrack track, AnimationKeyframeChannel channel, int index, float time, string label)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
        Channel = channel;
        Index = index;
        Time = time;
        Label = label ?? string.Empty;
    }

    public TransformTrack Track { get; }

    public AnimationKeyframeChannel Channel { get; }

    public int Index { get; }

    public float Time { get; }

    public string Label { get; }
}

public sealed class AnimationEditorViewModel : ViewModelBase
{
    private AnimationClipItem? _selectedClip;
    private AnimationTrackItem? _selectedTrack;
    private AnimationKeyframeItem? _selectedKeyframe;
    private AnimationKeyframeChannel _selectedChannel;
    private string _clipSummary = "Clip: none";
    private string _trackSummary = "Track: none";
    private string _keySummary = "Key: none";
    private double _selectedClipDuration;
    private double _selectedKeyTime = double.NaN;
    private string _keyTimeText = "0";
    private string _keyValueXText = "0";
    private string _keyValueYText = "0";
    private string _keyValueZText = "0";

    public AnimationEditorViewModel()
    {
        Clips = new ObservableCollection<AnimationClipItem>();
        Tracks = new ObservableCollection<AnimationTrackItem>();
        Keyframes = new ObservableCollection<AnimationKeyframeItem>();
        CurveSeries = new ObservableCollection<AnimationCurveSeries>();
        Channels = new ObservableCollection<AnimationKeyframeChannel>
        {
            AnimationKeyframeChannel.Translation,
            AnimationKeyframeChannel.Rotation,
            AnimationKeyframeChannel.Scale
        };

        _selectedChannel = AnimationKeyframeChannel.Translation;

        var canEditClip = this.WhenAnyValue(vm => vm.SelectedClip)
            .Select(selected => selected != null && selected.IsEditable);
        var hasSelectedTrack = this.WhenAnyValue(vm => vm.SelectedTrack)
            .Select(track => track != null);
        var hasSelectedKey = this.WhenAnyValue(vm => vm.SelectedKeyframe)
            .Select(key => key != null);

        CreateClipCommand = ReactiveCommand.Create(() => { });
        DuplicateClipCommand = ReactiveCommand.Create(() => { }, canEditClip);
        DeleteClipCommand = ReactiveCommand.Create(() => { }, canEditClip);
        AddTrackCommand = ReactiveCommand.Create(() => { }, canEditClip);
        RemoveTrackCommand = ReactiveCommand.Create(() => { }, hasSelectedTrack);
        DeleteKeyCommand = ReactiveCommand.Create(() => { }, hasSelectedKey);
        ApplyKeyEditsCommand = ReactiveCommand.Create(() => { }, hasSelectedKey);
        JumpToKeyCommand = ReactiveCommand.Create(() => { }, hasSelectedKey);
        PrevKeyCommand = ReactiveCommand.Create(() => { }, hasSelectedTrack);
        NextKeyCommand = ReactiveCommand.Create(() => { }, hasSelectedTrack);
        RefreshCommand = ReactiveCommand.Create(() => { });
    }

    public ObservableCollection<AnimationClipItem> Clips { get; }

    public ObservableCollection<AnimationTrackItem> Tracks { get; }

    public ObservableCollection<AnimationKeyframeChannel> Channels { get; }

    public ObservableCollection<AnimationKeyframeItem> Keyframes { get; }

    public ObservableCollection<AnimationCurveSeries> CurveSeries { get; }

    public AnimationClipItem? SelectedClip
    {
        get => _selectedClip;
        set
        {
            if (ReferenceEquals(_selectedClip, value))
            {
                return;
            }

            _selectedClip = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(CanEditClip));
        }
    }

    public AnimationTrackItem? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (ReferenceEquals(_selectedTrack, value))
            {
                return;
            }

            _selectedTrack = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasSelectedTrack));
            RaisePropertyChanged(nameof(CanEditTrack));
        }
    }

    public AnimationKeyframeChannel SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            if (_selectedChannel == value)
            {
                return;
            }

            _selectedChannel = value;
            RaisePropertyChanged();
        }
    }

    public AnimationKeyframeItem? SelectedKeyframe
    {
        get => _selectedKeyframe;
        set
        {
            if (ReferenceEquals(_selectedKeyframe, value))
            {
                return;
            }

            _selectedKeyframe = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(CanEditKey));
        }
    }

    public string ClipSummary
    {
        get => _clipSummary;
        set
        {
            if (_clipSummary == value)
            {
                return;
            }

            _clipSummary = value;
            RaisePropertyChanged();
        }
    }

    public string TrackSummary
    {
        get => _trackSummary;
        set
        {
            if (_trackSummary == value)
            {
                return;
            }

            _trackSummary = value;
            RaisePropertyChanged();
        }
    }

    public string KeySummary
    {
        get => _keySummary;
        set
        {
            if (_keySummary == value)
            {
                return;
            }

            _keySummary = value;
            RaisePropertyChanged();
        }
    }

    public double SelectedClipDuration
    {
        get => _selectedClipDuration;
        set
        {
            if (Math.Abs(_selectedClipDuration - value) < 1e-6)
            {
                return;
            }

            _selectedClipDuration = Math.Max(0.0, value);
            RaisePropertyChanged();
        }
    }

    public double SelectedKeyTime
    {
        get => _selectedKeyTime;
        set
        {
            if (double.IsNaN(_selectedKeyTime) && double.IsNaN(value))
            {
                return;
            }

            if (Math.Abs(_selectedKeyTime - value) < 1e-6)
            {
                return;
            }

            _selectedKeyTime = value;
            RaisePropertyChanged();
        }
    }

    public string KeyTimeText
    {
        get => _keyTimeText;
        set
        {
            if (_keyTimeText == value)
            {
                return;
            }

            _keyTimeText = value;
            RaisePropertyChanged();
        }
    }

    public string KeyValueXText
    {
        get => _keyValueXText;
        set
        {
            if (_keyValueXText == value)
            {
                return;
            }

            _keyValueXText = value;
            RaisePropertyChanged();
        }
    }

    public string KeyValueYText
    {
        get => _keyValueYText;
        set
        {
            if (_keyValueYText == value)
            {
                return;
            }

            _keyValueYText = value;
            RaisePropertyChanged();
        }
    }

    public string KeyValueZText
    {
        get => _keyValueZText;
        set
        {
            if (_keyValueZText == value)
            {
                return;
            }

            _keyValueZText = value;
            RaisePropertyChanged();
        }
    }

    public bool CanEditClip => SelectedClip?.IsEditable == true;

    public bool HasSelectedTrack => SelectedTrack != null;

    public bool CanEditTrack => SelectedTrack != null;

    public bool CanEditKey => SelectedKeyframe != null;

    public ReactiveCommand<Unit, Unit> CreateClipCommand { get; }

    public ReactiveCommand<Unit, Unit> DuplicateClipCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteClipCommand { get; }

    public ReactiveCommand<Unit, Unit> AddTrackCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveTrackCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteKeyCommand { get; }

    public ReactiveCommand<Unit, Unit> ApplyKeyEditsCommand { get; }

    public ReactiveCommand<Unit, Unit> JumpToKeyCommand { get; }

    public ReactiveCommand<Unit, Unit> PrevKeyCommand { get; }

    public ReactiveCommand<Unit, Unit> NextKeyCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
}
