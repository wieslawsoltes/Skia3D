using System;
using System.ComponentModel;
using Skia3D.Animation;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Services;

public sealed class MotionPanelService : IDisposable
{
    private readonly EditorViewportService _viewportService;
    private readonly MotionPanelViewModel _viewModel;

    public MotionPanelService(EditorViewportService viewportService, MotionPanelViewModel viewModel)
    {
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewportService.AnimationTimelineChanged += OnAnimationTimelineChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Refresh();
    }

    public bool IsUpdating { get; private set; }

    public void Refresh()
    {
        UpdateState(includeTimeline: true);
    }

    public void RefreshTimeline()
    {
        UpdateTimeline();
    }

    public void SetPlaying(bool isPlaying)
    {
        ApplyToAnimationPlayers(player => player.IsPlaying = isPlaying);
        Refresh();
        _viewportService.Invalidate();
    }

    public void SetLoop(bool loop)
    {
        ApplyToAnimationPlayers(player => player.Loop = loop);
        Refresh();
    }

    public void SetSpeed(float speed)
    {
        ApplyToAnimationPlayers(player => player.Speed = speed);
        Refresh();
    }

    public void Reset()
    {
        ApplyToAnimationPlayers(player => player.Reset());
        Refresh();
        _viewportService.Invalidate();
    }

    public void Seek(float time)
    {
        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            mixer.Seek(time);
        }
        else
        {
            _viewportService.AnimationPlayer?.Seek(time);
        }

        UpdateTimeline();
        _viewportService.Invalidate();
    }

    private void UpdateState(bool includeTimeline)
    {
        var count = GetAnimationPlayerCount();
        var hasAnimation = count > 0;
        var primary = GetPrimaryAnimationPlayer();
        var isPlaying = hasAnimation && IsAnyAnimationPlaying();
        var loop = primary?.Loop ?? true;
        var speed = primary?.Speed ?? 1f;
        var status = GetAnimationStatusText(count);

        IsUpdating = true;
        try
        {
            _viewModel.CanPlay = hasAnimation;
            _viewModel.IsPlaying = hasAnimation && isPlaying;
            _viewModel.PlayLabel = isPlaying ? "Pause" : "Play";
            _viewModel.CanLoop = hasAnimation;
            _viewModel.Loop = hasAnimation && loop;
            _viewModel.CanAdjustSpeed = hasAnimation;
            _viewModel.Speed = hasAnimation ? speed : 1.0;
            _viewModel.SpeedLabel = hasAnimation ? $"Speed: {speed:0.00}x" : "Speed: --";
            _viewModel.StatusLabel = status;
            _viewModel.CanReset = hasAnimation;

            if (includeTimeline)
            {
                UpdateTimelineInternal();
            }
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private void UpdateTimeline()
    {
        IsUpdating = true;
        try
        {
            UpdateTimelineInternal();
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private void UpdateTimelineInternal()
    {
        var count = GetAnimationPlayerCount();
        var duration = GetAnimationDuration();
        var hasAnimation = count > 0 && duration > 0f;
        var time = GetAnimationTime();

        _viewModel.CanScrub = hasAnimation;
        _viewModel.TimeMax = hasAnimation ? duration : 1.0;
        _viewModel.Time = hasAnimation ? Math.Clamp(time, 0f, duration) : 0.0;
        _viewModel.TimeLabel = hasAnimation
            ? $"Time: {time:0.00} / {duration:0.00}s"
            : "Time: --";
    }

    private void OnAnimationTimelineChanged(object? sender, EventArgs e)
    {
        UpdateTimeline();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsUpdating || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MotionPanelViewModel.IsPlaying):
                SetPlaying(_viewModel.IsPlaying);
                break;
            case nameof(MotionPanelViewModel.Loop):
                SetLoop(_viewModel.Loop);
                break;
            case nameof(MotionPanelViewModel.Speed):
                SetSpeed((float)_viewModel.Speed);
                break;
            case nameof(MotionPanelViewModel.Time):
                Seek((float)_viewModel.Time);
                break;
        }
    }

    private int GetAnimationPlayerCount()
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            return 1;
        }

        return _viewportService.AnimationMixer?.Layers.Count ?? 0;
    }

    private float GetAnimationDuration()
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            return player.Clip.Clip.Duration;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            float duration = 0f;
            foreach (var layer in mixer.Layers)
            {
                duration = MathF.Max(duration, layer.Player.Clip.Clip.Duration);
            }

            return duration;
        }

        return 0f;
    }

    private float GetAnimationTime()
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            return player.Time;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer is { Layers.Count: > 0 })
        {
            return mixer.Layers[0].Player.Time;
        }

        return 0f;
    }

    private int ApplyToAnimationPlayers(Action<AnimationPlayer> action)
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            action(player);
            return 1;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            int count = 0;
            foreach (var layer in mixer.Layers)
            {
                action(layer.Player);
                count++;
            }

            return count;
        }

        return 0;
    }

    private AnimationPlayer? GetPrimaryAnimationPlayer()
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            return player;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer is { Layers.Count: > 0 })
        {
            return mixer.Layers[0].Player;
        }

        return null;
    }

    private bool IsAnyAnimationPlaying()
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            return player.IsPlaying;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            foreach (var layer in mixer.Layers)
            {
                if (layer.Player.IsPlaying)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string GetAnimationStatusText(int count)
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            return $"Clip: {player.Clip.Clip.Name}";
        }

        if (_viewportService.AnimationMixer != null)
        {
            return count > 0 ? $"Mixer: {count} layers" : "Mixer: no layers";
        }

        return "No animation loaded";
    }

    public void Dispose()
    {
        _viewportService.AnimationTimelineChanged -= OnAnimationTimelineChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
