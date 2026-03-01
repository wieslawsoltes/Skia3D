using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Skia3D.Animation;
using Skia3D.Scene;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Services;

public sealed class MotionPanelService : IDisposable
{
    private readonly EditorViewportService _viewportService;
    private readonly MotionPanelViewModel _viewModel;
    private readonly AnimationEditorViewModel _editorViewModel;
    private readonly Dictionary<SceneNode, TransformTrack> _tracks = new();
    private readonly CompositeDisposable _subscriptions = new();
    private AnimationClip? _editClip;
    private AnimationPlayer? _editPlayer;
    private bool _clipDirty;
    private bool _autoKeyHooked;

    public MotionPanelService(EditorViewportService viewportService, MotionPanelViewModel viewModel)
    {
        _viewportService = viewportService ?? throw new ArgumentNullException(nameof(viewportService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _editorViewModel = _viewModel.Editor;
        _viewportService.AnimationTimelineChanged += OnAnimationTimelineChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _editorViewModel.PropertyChanged += OnEditorPropertyChanged;
        _subscriptions.Add(_viewModel.SetKeyCommand.Subscribe(_ => OnSetKeyRequested()));
        _subscriptions.Add(_editorViewModel.CreateClipCommand.Subscribe(_ => CreateClip()));
        _subscriptions.Add(_editorViewModel.DuplicateClipCommand.Subscribe(_ => DuplicateClip()));
        _subscriptions.Add(_editorViewModel.DeleteClipCommand.Subscribe(_ => DeleteClip()));
        _subscriptions.Add(_editorViewModel.AddTrackCommand.Subscribe(_ => AddTrackFromSelection()));
        _subscriptions.Add(_editorViewModel.RemoveTrackCommand.Subscribe(_ => RemoveSelectedTrack()));
        _subscriptions.Add(_editorViewModel.DeleteKeyCommand.Subscribe(_ => DeleteSelectedKeyframe()));
        _subscriptions.Add(_editorViewModel.ApplyKeyEditsCommand.Subscribe(_ => ApplyKeyEdits()));
        _subscriptions.Add(_editorViewModel.JumpToKeyCommand.Subscribe(_ => JumpToSelectedKeyframe()));
        _subscriptions.Add(_editorViewModel.PrevKeyCommand.Subscribe(_ => JumpToAdjacentKeyframe(previous: true)));
        _subscriptions.Add(_editorViewModel.NextKeyCommand.Subscribe(_ => JumpToAdjacentKeyframe(previous: false)));
        _subscriptions.Add(_editorViewModel.RefreshCommand.Subscribe(_ => Refresh()));
        Refresh();
    }

    public bool IsUpdating { get; private set; }

    public void Refresh()
    {
        UpdateState(includeTimeline: true);
        UpdateEditorState();
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
        var canSetKey = _viewportService.SelectedNode != null || _editorViewModel.SelectedTrack != null;

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
            _viewModel.CanSetKey = canSetKey;

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

    private void UpdateEditorState()
    {
        IsUpdating = true;
        try
        {
            var previousClip = _editorViewModel.SelectedClip?.Clip;
            var previousTrack = _editorViewModel.SelectedTrack?.Track;
            var previousKey = _editorViewModel.SelectedKeyframe;

            var clips = BuildClipItems();
            ReplaceCollection(_editorViewModel.Clips, clips);

            var selectedClip = FindClipItem(clips, previousClip);
            if (selectedClip == null && clips.Count > 0)
            {
                selectedClip = clips[0];
            }

            if (selectedClip != null)
            {
                selectedClip.Refresh();
            }

            _editorViewModel.SelectedClip = selectedClip;
            if (selectedClip?.Clip != null && !ReferenceEquals(_editClip, selectedClip.Clip))
            {
                SetEditClip(selectedClip.Clip);
            }
            UpdateSelectedClipDetails(selectedClip);
            UpdateTracksForSelectedClip(previousTrack, previousKey);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private void UpdateSelectedClipDetails(AnimationClipItem? selectedClip)
    {
        if (selectedClip?.Clip == null)
        {
            _editorViewModel.SelectedClipDuration = 0.0;
            return;
        }

        selectedClip.Refresh();
        _editorViewModel.SelectedClipDuration = selectedClip.Duration;
        _editorViewModel.ClipSummary = selectedClip.Label;
    }

    private void UpdateTracksForSelectedClip(TransformTrack? previousTrack, AnimationKeyframeItem? previousKey)
    {
        var clip = _editorViewModel.SelectedClip?.Clip;
        var trackItems = clip == null ? new List<AnimationTrackItem>() : BuildTrackItems(clip);
        ReplaceCollection(_editorViewModel.Tracks, trackItems);

        var selectedTrack = FindTrackItem(trackItems, previousTrack);
        if (selectedTrack == null && trackItems.Count > 0)
        {
            selectedTrack = trackItems[0];
        }

        if (selectedTrack != null)
        {
            selectedTrack.Refresh();
            _editorViewModel.TrackSummary = selectedTrack.Label;
        }

        _editorViewModel.SelectedTrack = selectedTrack;
        UpdateKeyframes(selectedTrack, previousKey);
    }

    private void UpdateKeyframes(AnimationTrackItem? selectedTrack, AnimationKeyframeItem? previousKey)
    {
        var channel = _editorViewModel.SelectedChannel;
        var keyItems = BuildKeyframeItems(selectedTrack?.Track, channel);
        ReplaceCollection(_editorViewModel.Keyframes, keyItems);

        AnimationKeyframeItem? selectedKey = null;
        if (previousKey != null && selectedTrack != null && ReferenceEquals(previousKey.Track, selectedTrack.Track) && previousKey.Channel == channel)
        {
            selectedKey = FindKeyItemByTime(keyItems, previousKey.Time);
        }

        if (selectedKey == null && keyItems.Count > 0)
        {
            selectedKey = keyItems[0];
        }

        _editorViewModel.SelectedKeyframe = selectedKey;
        UpdateKeyEditorFields(selectedKey, channel);
        UpdateCurveSeries(selectedTrack?.Track, channel);
    }

    private void UpdateKeyframesForSelection()
    {
        IsUpdating = true;
        try
        {
            UpdateKeyframes(_editorViewModel.SelectedTrack, _editorViewModel.SelectedKeyframe);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private void UpdateKeyEditorFields(AnimationKeyframeItem? keyframe, AnimationKeyframeChannel channel)
    {
        if (keyframe == null)
        {
            _editorViewModel.KeyTimeText = "0";
            _editorViewModel.KeyValueXText = "0";
            _editorViewModel.KeyValueYText = "0";
            _editorViewModel.KeyValueZText = "0";
            _editorViewModel.SelectedKeyTime = double.NaN;
            return;
        }

        var time = keyframe.Time;
        _editorViewModel.KeyTimeText = FormatFloat(time);
        _editorViewModel.SelectedKeyTime = time;

        var track = keyframe.Track;
        switch (channel)
        {
            case AnimationKeyframeChannel.Translation:
                if (TryGetVectorKey(track.TranslationKeys, keyframe.Index, out var translation))
                {
                    _editorViewModel.KeyValueXText = FormatFloat(translation.X);
                    _editorViewModel.KeyValueYText = FormatFloat(translation.Y);
                    _editorViewModel.KeyValueZText = FormatFloat(translation.Z);
                }
                else
                {
                    _editorViewModel.KeyValueXText = "0";
                    _editorViewModel.KeyValueYText = "0";
                    _editorViewModel.KeyValueZText = "0";
                }
                break;
            case AnimationKeyframeChannel.Rotation:
                if (TryGetQuaternionKey(track.RotationKeys, keyframe.Index, out var rotation))
                {
                    var euler = QuaternionToEulerDegrees(rotation);
                    _editorViewModel.KeyValueXText = FormatFloat(euler.X);
                    _editorViewModel.KeyValueYText = FormatFloat(euler.Y);
                    _editorViewModel.KeyValueZText = FormatFloat(euler.Z);
                }
                else
                {
                    _editorViewModel.KeyValueXText = "0";
                    _editorViewModel.KeyValueYText = "0";
                    _editorViewModel.KeyValueZText = "0";
                }
                break;
            case AnimationKeyframeChannel.Scale:
                if (TryGetVectorKey(track.ScaleKeys, keyframe.Index, out var scale))
                {
                    _editorViewModel.KeyValueXText = FormatFloat(scale.X);
                    _editorViewModel.KeyValueYText = FormatFloat(scale.Y);
                    _editorViewModel.KeyValueZText = FormatFloat(scale.Z);
                }
                else
                {
                    _editorViewModel.KeyValueXText = "0";
                    _editorViewModel.KeyValueYText = "0";
                    _editorViewModel.KeyValueZText = "0";
                }
                break;
        }
    }

    private void UpdateCurveSeries(TransformTrack? track, AnimationKeyframeChannel channel)
    {
        var curves = BuildCurveSeries(track, channel);
        ReplaceCollection(_editorViewModel.CurveSeries, curves);
    }

    private void ApplySelectedClip()
    {
        var clipItem = _editorViewModel.SelectedClip;
        SetEditClip(clipItem?.Clip);
        UpdateSelectedClipDetails(clipItem);
        UpdateTracksForSelectedClip(null, null);
    }

    private AnimationClip? EnsureEditableClip()
    {
        var selectedClip = _editorViewModel.SelectedClip?.Clip;
        if (selectedClip != null)
        {
            if (!ReferenceEquals(_editClip, selectedClip))
            {
                SetEditClip(selectedClip);
            }

            return selectedClip;
        }

        if (_editClip == null)
        {
            var clip = new AnimationClip("Editor Keys");
            SetEditClip(clip);
        }

        return _editClip;
    }

    private void SetEditClip(AnimationClip? clip)
    {
        _editClip = clip;
        _clipDirty = false;
        _tracks.Clear();
        _editPlayer = null;

        if (clip == null)
        {
            return;
        }

        var nodeMap = BuildNodeMap(_viewportService.SceneGraph);
        for (int i = 0; i < clip.Tracks.Count; i++)
        {
            var track = clip.Tracks[i];
            if (nodeMap.TryGetValue(track.TargetName, out var node))
            {
                _tracks[node] = track;
            }
        }

        var player = _viewportService.AnimationPlayer;
        if (player != null && ReferenceEquals(player.Clip.Clip, clip))
        {
            _editPlayer = player;
            return;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            foreach (var layer in mixer.Layers)
            {
                if (ReferenceEquals(layer.Player.Clip.Clip, clip))
                {
                    _editPlayer = layer.Player;
                    break;
                }
            }
        }
    }

    private void EnsureClipPlayback(AnimationClip clip, float time)
    {
        if (IsClipInPlayers(clip))
        {
            return;
        }

        _editClip = clip;
        RebindEditPlayer(time);
        _viewportService.AnimationMixer = null;
        _viewportService.SpinNode = null;
        _viewportService.AnimationPlayer = _editPlayer;
    }

    private bool IsClipInPlayers(AnimationClip clip)
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null && ReferenceEquals(player.Clip.Clip, clip))
        {
            return true;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            foreach (var layer in mixer.Layers)
            {
                if (ReferenceEquals(layer.Player.Clip.Clip, clip))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void RebindClipInPlayers(AnimationClip clip, float time)
    {
        var player = _viewportService.AnimationPlayer;
        if (player != null && ReferenceEquals(player.Clip.Clip, clip))
        {
            var replacement = CreatePlayerForClip(clip, player);
            _viewportService.AnimationPlayer = replacement;
            if (ReferenceEquals(_editPlayer, player))
            {
                _editPlayer = replacement;
            }
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            var layers = new List<AnimationLayer>(mixer.Layers.Count);
            foreach (var layer in mixer.Layers)
            {
                layers.Add(layer);
            }

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!ReferenceEquals(layer.Player.Clip.Clip, clip))
                {
                    continue;
                }

                var oldPlayer = layer.Player;
                var weight = layer.Weight;
                mixer.RemoveLayer(layer);
                var replacement = CreatePlayerForClip(clip, oldPlayer);
                mixer.AddLayer(replacement, weight);
                if (ReferenceEquals(_editPlayer, oldPlayer))
                {
                    _editPlayer = replacement;
                }
            }
        }

        if (_editPlayer == null && ReferenceEquals(_editClip, clip))
        {
            RebindEditPlayer(time);
        }
    }

    private AnimationPlayer CreatePlayerForClip(AnimationClip clip, AnimationPlayer source)
    {
        var bound = clip.Bind(_viewportService.SceneGraph);
        var player = new AnimationPlayer(bound)
        {
            Loop = source.Loop,
            Speed = source.Speed,
            IsPlaying = source.IsPlaying
        };
        player.Seek(source.Time);
        return player;
    }

    private void CreateClip()
    {
        var clip = new AnimationClip(GenerateClipName("Clip"));
        SetEditClip(clip);
        _clipDirty = true;
        RebindEditPlayer(0f);
        _viewportService.AnimationMixer = null;
        _viewportService.SpinNode = null;
        _viewportService.AnimationPlayer = _editPlayer;
        Refresh();
        SelectClipItem(clip);
    }

    private void DuplicateClip()
    {
        var source = _editorViewModel.SelectedClip?.Clip;
        if (source == null)
        {
            return;
        }

        var name = GenerateClipName($"{source.Name} Copy");
        var clone = CloneClip(source, name);
        SetEditClip(clone);
        RebindEditPlayer(GetKeyTime());
        _viewportService.AnimationMixer = null;
        _viewportService.SpinNode = null;
        _viewportService.AnimationPlayer = _editPlayer;
        Refresh();
        SelectClipItem(clone);
    }

    private void DeleteClip()
    {
        var clip = _editorViewModel.SelectedClip?.Clip;
        if (clip == null)
        {
            return;
        }

        RemoveClipInternal(clip);
        Refresh();
    }

    private void AddTrackFromSelection()
    {
        var clip = EnsureEditableClip();
        if (clip == null)
        {
            return;
        }

        var node = _viewportService.SelectedNode;
        if (node == null)
        {
            return;
        }

        var existing = FindTrackByName(clip, node.Name);
        if (existing == null)
        {
            existing = new TransformTrack(node.Name);
            clip.Tracks.Add(existing);
            _clipDirty = true;
            clip.RecalculateDuration();
            RebindClipInPlayers(clip, GetKeyTime());
        }

        _tracks[node] = existing;

        Refresh();
        SelectTrackItem(existing);
    }

    private void RemoveSelectedTrack()
    {
        var clip = _editorViewModel.SelectedClip?.Clip;
        var trackItem = _editorViewModel.SelectedTrack;
        if (clip == null || trackItem == null)
        {
            return;
        }

        clip.Tracks.Remove(trackItem.Track);
        RemoveTrackMappings(trackItem.Track);
        clip.RecalculateDuration();
        RebindClipInPlayers(clip, GetKeyTime());
        Refresh();
    }

    private void DeleteSelectedKeyframe()
    {
        var keyframe = _editorViewModel.SelectedKeyframe;
        if (keyframe == null)
        {
            return;
        }

        var track = keyframe.Track;
        var removed = RemoveKey(track, keyframe.Channel, keyframe.Index);
        if (!removed)
        {
            return;
        }

        _editorViewModel.SelectedClip?.Clip?.RecalculateDuration();
        Refresh();
    }

    private void ApplyKeyEdits()
    {
        var keyframe = _editorViewModel.SelectedKeyframe;
        if (keyframe == null)
        {
            return;
        }

        if (!TryParseFloat(_editorViewModel.KeyTimeText, out var time))
        {
            time = keyframe.Time;
        }

        time = MathF.Max(0f, time);

        var track = keyframe.Track;
        switch (keyframe.Channel)
        {
            case AnimationKeyframeChannel.Translation:
                ApplyVectorKeyEdits(track.TranslationKeys, keyframe.Index, time);
                break;
            case AnimationKeyframeChannel.Rotation:
                ApplyRotationKeyEdits(track.RotationKeys, keyframe.Index, time);
                break;
            case AnimationKeyframeChannel.Scale:
                ApplyVectorKeyEdits(track.ScaleKeys, keyframe.Index, time);
                break;
        }

        _editorViewModel.SelectedClip?.Clip?.RecalculateDuration();
        Refresh();
        SelectKeyframe(track, keyframe.Channel, time);
    }

    private void JumpToSelectedKeyframe()
    {
        var keyframe = _editorViewModel.SelectedKeyframe;
        if (keyframe == null)
        {
            return;
        }

        Seek(keyframe.Time);
    }

    private void JumpToAdjacentKeyframe(bool previous)
    {
        var trackItem = _editorViewModel.SelectedTrack;
        if (trackItem == null)
        {
            return;
        }

        var channel = _editorViewModel.SelectedChannel;
        var keyCount = GetKeyCount(trackItem.Track, channel);
        if (keyCount == 0)
        {
            return;
        }

        var currentTime = _editorViewModel.SelectedKeyframe?.Time ?? GetKeyTime();
        float? target = null;
        if (previous)
        {
            for (int i = keyCount - 1; i >= 0; i--)
            {
                var keyTime = GetKeyTime(trackItem.Track, channel, i);
                if (keyTime < currentTime - 1e-4f)
                {
                    target = keyTime;
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < keyCount; i++)
            {
                var keyTime = GetKeyTime(trackItem.Track, channel, i);
                if (keyTime > currentTime + 1e-4f)
                {
                    target = keyTime;
                    break;
                }
            }
        }

        if (target.HasValue)
        {
            Seek(target.Value);
            SelectKeyframe(trackItem.Track, channel, target.Value);
        }
    }

    private void ApplyVectorKeyEdits(List<Keyframe<Vector3>> keys, int index, float time)
    {
        var existing = GetVectorKeyValue(keys, index, Vector3.Zero);

        if (!TryParseFloat(_editorViewModel.KeyValueXText, out var x))
        {
            x = existing.X;
        }

        if (!TryParseFloat(_editorViewModel.KeyValueYText, out var y))
        {
            y = existing.Y;
        }

        if (!TryParseFloat(_editorViewModel.KeyValueZText, out var z))
        {
            z = existing.Z;
        }

        UpdateKey(keys, index, time, new Vector3(x, y, z));
    }

    private void ApplyRotationKeyEdits(List<Keyframe<Quaternion>> keys, int index, float time)
    {
        var existing = GetQuaternionKeyValue(keys, index, Quaternion.Identity);
        var euler = QuaternionToEulerDegrees(existing);

        if (!TryParseFloat(_editorViewModel.KeyValueXText, out var pitch))
        {
            pitch = euler.X;
        }

        if (!TryParseFloat(_editorViewModel.KeyValueYText, out var yaw))
        {
            yaw = euler.Y;
        }

        if (!TryParseFloat(_editorViewModel.KeyValueZText, out var roll))
        {
            roll = euler.Z;
        }

        var rotation = EulerDegreesToQuaternion(new Vector3(pitch, yaw, roll));
        UpdateKey(keys, index, time, rotation);
    }

    private void SelectClipItem(AnimationClip clip)
    {
        for (int i = 0; i < _editorViewModel.Clips.Count; i++)
        {
            var item = _editorViewModel.Clips[i];
            if (ReferenceEquals(item.Clip, clip))
            {
                _editorViewModel.SelectedClip = item;
                break;
            }
        }
    }

    private void SelectTrackItem(TransformTrack track)
    {
        for (int i = 0; i < _editorViewModel.Tracks.Count; i++)
        {
            var item = _editorViewModel.Tracks[i];
            if (ReferenceEquals(item.Track, track))
            {
                _editorViewModel.SelectedTrack = item;
                break;
            }
        }
    }

    private void SelectKeyframe(TransformTrack track, AnimationKeyframeChannel channel, float time)
    {
        const float tolerance = 1e-3f;
        for (int i = 0; i < _editorViewModel.Keyframes.Count; i++)
        {
            var item = _editorViewModel.Keyframes[i];
            if (ReferenceEquals(item.Track, track) && item.Channel == channel && MathF.Abs(item.Time - time) <= tolerance)
            {
                _editorViewModel.SelectedKeyframe = item;
                break;
            }
        }
    }

    private string GenerateClipName(string baseName)
    {
        var name = baseName;
        var index = 1;
        while (ClipNameExists(name))
        {
            name = $"{baseName} {index}";
            index++;
        }

        return name;
    }

    private bool ClipNameExists(string name)
    {
        for (int i = 0; i < _editorViewModel.Clips.Count; i++)
        {
            if (string.Equals(_editorViewModel.Clips[i].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static AnimationClip CloneClip(AnimationClip source, string name)
    {
        var clone = new AnimationClip(name);
        for (int i = 0; i < source.Tracks.Count; i++)
        {
            var track = source.Tracks[i];
            var copy = new TransformTrack(track.TargetName);
            copy.TranslationKeys.AddRange(track.TranslationKeys);
            copy.RotationKeys.AddRange(track.RotationKeys);
            copy.ScaleKeys.AddRange(track.ScaleKeys);
            clone.Tracks.Add(copy);
        }

        clone.RecalculateDuration();
        return clone;
    }

    private void RemoveClipInternal(AnimationClip clip)
    {
        if (ReferenceEquals(_editClip, clip))
        {
            _tracks.Clear();
            _editClip = null;
            _editPlayer = null;
        }

        var player = _viewportService.AnimationPlayer;
        if (player != null && ReferenceEquals(player.Clip.Clip, clip))
        {
            _viewportService.AnimationPlayer = null;
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            var layers = new List<AnimationLayer>(mixer.Layers.Count);
            foreach (var layer in mixer.Layers)
            {
                layers.Add(layer);
            }

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (ReferenceEquals(layer.Player.Clip.Clip, clip))
                {
                    mixer.RemoveLayer(layer);
                }
            }

            if (mixer.Layers.Count == 0)
            {
                _viewportService.AnimationMixer = null;
            }
        }
    }

    private static TransformTrack? FindTrackByName(AnimationClip clip, string targetName)
    {
        for (int i = 0; i < clip.Tracks.Count; i++)
        {
            var track = clip.Tracks[i];
            if (string.Equals(track.TargetName, targetName, StringComparison.Ordinal))
            {
                return track;
            }
        }

        return null;
    }

    private void RemoveTrackMappings(TransformTrack track)
    {
        var toRemove = new List<SceneNode>();
        foreach (var pair in _tracks)
        {
            if (ReferenceEquals(pair.Value, track))
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _tracks.Remove(toRemove[i]);
        }
    }

    private SceneNode? FindNodeByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var stack = new Stack<SceneNode>();
        var roots = _viewportService.SceneGraph.Roots;
        for (int i = 0; i < roots.Count; i++)
        {
            stack.Push(roots[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (string.Equals(node.Name, name, StringComparison.Ordinal))
            {
                return node;
            }

            var children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                stack.Push(children[i]);
            }
        }

        return null;
    }

    private static Dictionary<string, SceneNode> BuildNodeMap(Skia3D.Scene.Scene scene)
    {
        var map = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
        var stack = new Stack<SceneNode>();
        for (int i = 0; i < scene.Roots.Count; i++)
        {
            stack.Push(scene.Roots[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!map.ContainsKey(node.Name))
            {
                map.Add(node.Name, node);
            }

            var children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                stack.Push(children[i]);
            }
        }

        return map;
    }

    private List<AnimationClipItem> BuildClipItems()
    {
        var items = new List<AnimationClipItem>();
        var seen = new HashSet<AnimationClip>();

        if (_editClip != null)
        {
            AddClipItem(items, seen, _editClip, isMixer: false);
        }

        var player = _viewportService.AnimationPlayer;
        if (player != null)
        {
            AddClipItem(items, seen, player.Clip.Clip, isMixer: false);
        }

        var mixer = _viewportService.AnimationMixer;
        if (mixer != null)
        {
            foreach (var layer in mixer.Layers)
            {
                AddClipItem(items, seen, layer.Player.Clip.Clip, isMixer: true);
            }
        }

        return items;
    }

    private static void AddClipItem(List<AnimationClipItem> items, HashSet<AnimationClip> seen, AnimationClip clip, bool isMixer)
    {
        if (!seen.Add(clip))
        {
            return;
        }

        items.Add(new AnimationClipItem(clip.Name, clip, isMixer, isEditable: true));
    }

    private static AnimationClipItem? FindClipItem(List<AnimationClipItem> items, AnimationClip? clip)
    {
        if (clip == null)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i].Clip, clip))
            {
                return items[i];
            }
        }

        return null;
    }

    private static List<AnimationTrackItem> BuildTrackItems(AnimationClip clip)
    {
        var tracks = new List<AnimationTrackItem>(clip.Tracks.Count);
        for (int i = 0; i < clip.Tracks.Count; i++)
        {
            tracks.Add(new AnimationTrackItem(clip.Tracks[i]));
        }

        return tracks;
    }

    private static AnimationTrackItem? FindTrackItem(List<AnimationTrackItem> items, TransformTrack? track)
    {
        if (track == null)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i].Track, track))
            {
                return items[i];
            }
        }

        return null;
    }

    private static List<AnimationKeyframeItem> BuildKeyframeItems(TransformTrack? track, AnimationKeyframeChannel channel)
    {
        var items = new List<AnimationKeyframeItem>();
        if (track == null)
        {
            return items;
        }

        switch (channel)
        {
            case AnimationKeyframeChannel.Translation:
                items.Capacity = track.TranslationKeys.Count;
                for (int i = 0; i < track.TranslationKeys.Count; i++)
                {
                    var key = track.TranslationKeys[i];
                    items.Add(new AnimationKeyframeItem(track, channel, i, key.Time, FormatVectorLabel(key.Value)));
                }
                break;
            case AnimationKeyframeChannel.Rotation:
                items.Capacity = track.RotationKeys.Count;
                for (int i = 0; i < track.RotationKeys.Count; i++)
                {
                    var key = track.RotationKeys[i];
                    items.Add(new AnimationKeyframeItem(track, channel, i, key.Time, FormatRotationLabel(key.Value)));
                }
                break;
            case AnimationKeyframeChannel.Scale:
                items.Capacity = track.ScaleKeys.Count;
                for (int i = 0; i < track.ScaleKeys.Count; i++)
                {
                    var key = track.ScaleKeys[i];
                    items.Add(new AnimationKeyframeItem(track, channel, i, key.Time, FormatVectorLabel(key.Value)));
                }
                break;
        }

        return items;
    }

    private static AnimationKeyframeItem? FindKeyItemByTime(List<AnimationKeyframeItem> items, float time)
    {
        const float tolerance = 1e-3f;
        for (int i = 0; i < items.Count; i++)
        {
            if (MathF.Abs(items[i].Time - time) <= tolerance)
            {
                return items[i];
            }
        }

        return null;
    }

    private static List<AnimationCurveSeries> BuildCurveSeries(TransformTrack? track, AnimationKeyframeChannel channel)
    {
        var curves = new List<AnimationCurveSeries>();
        if (track == null)
        {
            return curves;
        }

        const uint curveX = 0xFFE07070;
        const uint curveY = 0xFF6ED0E0;
        const uint curveZ = 0xFF74D074;

        switch (channel)
        {
            case AnimationKeyframeChannel.Translation:
                AddVectorSeries(curves, track.TranslationKeys, curveX, curveY, curveZ);
                break;
            case AnimationKeyframeChannel.Rotation:
                AddRotationSeries(curves, track.RotationKeys, curveX, curveY, curveZ);
                break;
            case AnimationKeyframeChannel.Scale:
                AddVectorSeries(curves, track.ScaleKeys, curveX, curveY, curveZ);
                break;
        }

        return curves;
    }

    private static void AddVectorSeries(List<AnimationCurveSeries> curves, List<Keyframe<Vector3>> keys, uint xColor, uint yColor, uint zColor)
    {
        if (keys.Count == 0)
        {
            return;
        }

        var xPoints = new List<AnimationCurvePoint>(keys.Count);
        var yPoints = new List<AnimationCurvePoint>(keys.Count);
        var zPoints = new List<AnimationCurvePoint>(keys.Count);

        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            xPoints.Add(new AnimationCurvePoint(key.Time, key.Value.X));
            yPoints.Add(new AnimationCurvePoint(key.Time, key.Value.Y));
            zPoints.Add(new AnimationCurvePoint(key.Time, key.Value.Z));
        }

        curves.Add(new AnimationCurveSeries("X", xColor, xPoints));
        curves.Add(new AnimationCurveSeries("Y", yColor, yPoints));
        curves.Add(new AnimationCurveSeries("Z", zColor, zPoints));
    }

    private static void AddRotationSeries(List<AnimationCurveSeries> curves, List<Keyframe<Quaternion>> keys, uint xColor, uint yColor, uint zColor)
    {
        if (keys.Count == 0)
        {
            return;
        }

        var xPoints = new List<AnimationCurvePoint>(keys.Count);
        var yPoints = new List<AnimationCurvePoint>(keys.Count);
        var zPoints = new List<AnimationCurvePoint>(keys.Count);

        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var euler = QuaternionToEulerDegrees(key.Value);
            xPoints.Add(new AnimationCurvePoint(key.Time, euler.X));
            yPoints.Add(new AnimationCurvePoint(key.Time, euler.Y));
            zPoints.Add(new AnimationCurvePoint(key.Time, euler.Z));
        }

        curves.Add(new AnimationCurveSeries("Pitch", xColor, xPoints));
        curves.Add(new AnimationCurveSeries("Yaw", yColor, yPoints));
        curves.Add(new AnimationCurveSeries("Roll", zColor, zPoints));
    }

    private static void ReplaceCollection<T>(ICollection<T> collection, IReadOnlyList<T> items)
    {
        collection.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            collection.Add(items[i]);
        }
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatVectorLabel(Vector3 value)
    {
        return $"X:{FormatFloat(value.X)} Y:{FormatFloat(value.Y)} Z:{FormatFloat(value.Z)}";
    }

    private static string FormatRotationLabel(Quaternion value)
    {
        var euler = QuaternionToEulerDegrees(value);
        return $"P:{FormatFloat(euler.X)} Y:{FormatFloat(euler.Y)} R:{FormatFloat(euler.Z)}";
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static Vector3 SampleVector(List<Keyframe<Vector3>> keys, float time, Vector3 fallback)
    {
        if (keys.Count == 0)
        {
            return fallback;
        }

        if (keys.Count == 1)
        {
            return keys[0].Value;
        }

        if (time <= keys[0].Time)
        {
            return keys[0].Value;
        }

        if (time >= keys[^1].Time)
        {
            return keys[^1].Value;
        }

        for (int i = 0; i + 1 < keys.Count; i++)
        {
            var a = keys[i];
            var b = keys[i + 1];
            if (time >= a.Time && time <= b.Time)
            {
                var span = b.Time - a.Time;
                var t = span <= 1e-6f ? 0f : (time - a.Time) / span;
                return Vector3.Lerp(a.Value, b.Value, t);
            }
        }

        return keys[^1].Value;
    }

    private static Quaternion SampleQuaternion(List<Keyframe<Quaternion>> keys, float time, Quaternion fallback)
    {
        if (keys.Count == 0)
        {
            return fallback;
        }

        if (keys.Count == 1)
        {
            return Quaternion.Normalize(keys[0].Value);
        }

        if (time <= keys[0].Time)
        {
            return Quaternion.Normalize(keys[0].Value);
        }

        if (time >= keys[^1].Time)
        {
            return Quaternion.Normalize(keys[^1].Value);
        }

        for (int i = 0; i + 1 < keys.Count; i++)
        {
            var a = keys[i];
            var b = keys[i + 1];
            if (time >= a.Time && time <= b.Time)
            {
                var span = b.Time - a.Time;
                var t = span <= 1e-6f ? 0f : (time - a.Time) / span;
                return Quaternion.Slerp(a.Value, b.Value, t);
            }
        }

        return Quaternion.Normalize(keys[^1].Value);
    }

    private static Vector3 QuaternionToEulerDegrees(Quaternion value)
    {
        var q = Quaternion.Normalize(value);

        var sinr = 2f * (q.W * q.X + q.Y * q.Z);
        var cosr = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        var roll = MathF.Atan2(sinr, cosr);

        var sinp = 2f * (q.W * q.Y - q.Z * q.X);
        var pitch = MathF.Abs(sinp) >= 1f ? MathF.CopySign(MathF.PI / 2f, sinp) : MathF.Asin(sinp);

        var siny = 2f * (q.W * q.Z + q.X * q.Y);
        var cosy = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        var yaw = MathF.Atan2(siny, cosy);

        return new Vector3(pitch * (180f / MathF.PI), yaw * (180f / MathF.PI), roll * (180f / MathF.PI));
    }

    private static Quaternion EulerDegreesToQuaternion(Vector3 euler)
    {
        var pitch = euler.X * (MathF.PI / 180f);
        var yaw = euler.Y * (MathF.PI / 180f);
        var roll = euler.Z * (MathF.PI / 180f);
        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
    }

    private static bool TryGetVectorKey(List<Keyframe<Vector3>> keys, int index, out Vector3 value)
    {
        if (index >= 0 && index < keys.Count)
        {
            value = keys[index].Value;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetQuaternionKey(List<Keyframe<Quaternion>> keys, int index, out Quaternion value)
    {
        if (index >= 0 && index < keys.Count)
        {
            value = keys[index].Value;
            return true;
        }

        value = default;
        return false;
    }

    private static Vector3 GetVectorKeyValue(List<Keyframe<Vector3>> keys, int index, Vector3 fallback)
    {
        return TryGetVectorKey(keys, index, out var value) ? value : fallback;
    }

    private static Quaternion GetQuaternionKeyValue(List<Keyframe<Quaternion>> keys, int index, Quaternion fallback)
    {
        return TryGetQuaternionKey(keys, index, out var value) ? value : fallback;
    }

    private static void UpdateKey<T>(List<Keyframe<T>> keys, int index, float time, T value)
    {
        if (index >= 0 && index < keys.Count)
        {
            keys.RemoveAt(index);
        }

        UpsertKey(keys, time, value);
    }

    private static bool RemoveKey(TransformTrack track, AnimationKeyframeChannel channel, int index)
    {
        switch (channel)
        {
            case AnimationKeyframeChannel.Translation:
                return RemoveKeyAt(track.TranslationKeys, index);
            case AnimationKeyframeChannel.Rotation:
                return RemoveKeyAt(track.RotationKeys, index);
            case AnimationKeyframeChannel.Scale:
                return RemoveKeyAt(track.ScaleKeys, index);
            default:
                return false;
        }
    }

    private static bool RemoveKeyAt<T>(List<Keyframe<T>> keys, int index)
    {
        if (index < 0 || index >= keys.Count)
        {
            return false;
        }

        keys.RemoveAt(index);
        return true;
    }

    private static int GetKeyCount(TransformTrack track, AnimationKeyframeChannel channel)
    {
        return channel switch
        {
            AnimationKeyframeChannel.Translation => track.TranslationKeys.Count,
            AnimationKeyframeChannel.Rotation => track.RotationKeys.Count,
            AnimationKeyframeChannel.Scale => track.ScaleKeys.Count,
            _ => 0
        };
    }

    private static float GetKeyTime(TransformTrack track, AnimationKeyframeChannel channel, int index)
    {
        return channel switch
        {
            AnimationKeyframeChannel.Translation => track.TranslationKeys[index].Time,
            AnimationKeyframeChannel.Rotation => track.RotationKeys[index].Time,
            AnimationKeyframeChannel.Scale => track.ScaleKeys[index].Time,
            _ => 0f
        };
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
            case nameof(MotionPanelViewModel.AutoKeyEnabled):
                UpdateAutoKeySubscription();
                break;
        }
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsUpdating || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AnimationEditorViewModel.SelectedClip):
                ApplySelectedClip();
                UpdateState(includeTimeline: false);
                break;
            case nameof(AnimationEditorViewModel.SelectedTrack):
            case nameof(AnimationEditorViewModel.SelectedChannel):
                UpdateKeyframesForSelection();
                UpdateState(includeTimeline: false);
                break;
            case nameof(AnimationEditorViewModel.SelectedKeyframe):
                UpdateKeyEditorFields(_editorViewModel.SelectedKeyframe, _editorViewModel.SelectedChannel);
                break;
        }
    }

    private void OnSetKeyRequested()
    {
        SetKeyForSelection();
    }

    private void UpdateAutoKeySubscription()
    {
        if (_viewModel.AutoKeyEnabled)
        {
            if (_autoKeyHooked)
            {
                return;
            }

            _viewportService.TransformCommitted += OnTransformCommitted;
            _autoKeyHooked = true;
            return;
        }

        if (!_autoKeyHooked)
        {
            return;
        }

        _viewportService.TransformCommitted -= OnTransformCommitted;
        _autoKeyHooked = false;
    }

    private void OnTransformCommitted(SceneNode? node)
    {
        if (node == null || !_viewModel.AutoKeyEnabled)
        {
            return;
        }

        SetKeyForSelection();
    }

    private void SetKeyForSelection()
    {
        var clip = EnsureEditableClip();
        if (clip == null)
        {
            return;
        }

        var createdTrack = false;
        TransformTrack? track = null;
        SceneNode? node = null;

        var selectedTrack = _editorViewModel.SelectedTrack;
        if (selectedTrack != null)
        {
            track = selectedTrack.Track;
            node = FindNodeByName(track.TargetName);
        }

        if (track == null)
        {
            node = _viewportService.SelectedNode;
            if (node == null)
            {
                return;
            }

            if (!_tracks.TryGetValue(node, out track))
            {
                track = new TransformTrack(node.Name);
                _tracks[node] = track;
                clip.Tracks.Add(track);
                _clipDirty = true;
                createdTrack = true;
            }
        }

        var time = GetKeyTime();
        if (node != null)
        {
            UpsertKey(track.TranslationKeys, time, node.Transform.LocalPosition);
            UpsertKey(track.RotationKeys, time, node.Transform.LocalRotation);
            UpsertKey(track.ScaleKeys, time, node.Transform.LocalScale);
        }
        else
        {
            var translation = SampleVector(track.TranslationKeys, time, Vector3.Zero);
            var rotation = SampleQuaternion(track.RotationKeys, time, Quaternion.Identity);
            var scale = SampleVector(track.ScaleKeys, time, Vector3.One);
            UpsertKey(track.TranslationKeys, time, translation);
            UpsertKey(track.RotationKeys, time, rotation);
            UpsertKey(track.ScaleKeys, time, scale);
        }

        clip.RecalculateDuration();
        if (_clipDirty || createdTrack)
        {
            RebindClipInPlayers(clip, time);
        }
        else if (_editPlayer != null && ReferenceEquals(_editPlayer.Clip.Clip, clip))
        {
            _editPlayer.Seek(time);
        }
        else if (_viewportService.AnimationPlayer != null && ReferenceEquals(_viewportService.AnimationPlayer.Clip.Clip, clip))
        {
            _viewportService.AnimationPlayer.Seek(time);
        }

        EnsureClipPlayback(clip, time);
        Refresh();
        _viewportService.Invalidate();
    }

    private float GetKeyTime()
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

        return (float)_viewModel.Time;
    }

    private void RebindEditPlayer(float time)
    {
        if (_editClip == null)
        {
            return;
        }

        var previous = _editPlayer;
        var loop = previous?.Loop ?? true;
        var speed = previous?.Speed ?? 1f;
        var isPlaying = previous?.IsPlaying ?? false;
        var bound = _editClip.Bind(_viewportService.SceneGraph);

        var player = new AnimationPlayer(bound)
        {
            Loop = loop,
            Speed = speed,
            IsPlaying = isPlaying
        };
        player.Seek(time);
        _editPlayer = player;
        _clipDirty = false;
        if (ReferenceEquals(_viewportService.AnimationPlayer, previous) || _viewportService.AnimationPlayer == null)
        {
            _viewportService.AnimationPlayer = player;
        }
    }

    private static void UpsertKey<T>(List<Keyframe<T>> keys, float time, T value)
    {
        const float tolerance = 1e-3f;
        for (int i = 0; i < keys.Count; i++)
        {
            var existing = keys[i];
            if (MathF.Abs(existing.Time - time) <= tolerance)
            {
                keys[i] = new Keyframe<T>(time, value);
                return;
            }

            if (existing.Time > time)
            {
                keys.Insert(i, new Keyframe<T>(time, value));
                return;
            }
        }

        keys.Add(new Keyframe<T>(time, value));
    }

    public void ClearKeys()
    {
        var clip = _editorViewModel.SelectedClip?.Clip ?? _editClip;
        if (clip == null)
        {
            _tracks.Clear();
            _editClip = null;
            _clipDirty = false;

            if (_viewportService.AnimationPlayer == _editPlayer)
            {
                _viewportService.AnimationPlayer = null;
            }

            _editPlayer = null;
            Refresh();
            return;
        }

        clip.Tracks.Clear();
        clip.RecalculateDuration();
        if (ReferenceEquals(clip, _editClip))
        {
            _tracks.Clear();
            _clipDirty = false;
        }

        RebindClipInPlayers(clip, 0f);
        Refresh();
    }

    public void RemoveKeysForNode(SceneNode node)
    {
        if (_editClip == null)
        {
            return;
        }

        if (!_tracks.Remove(node, out var track))
        {
            return;
        }

        _editClip.Tracks.Remove(track);
        _clipDirty = true;

        if (_editClip.Tracks.Count == 0)
        {
            ClearKeys();
            return;
        }

        RebindClipInPlayers(_editClip, GetKeyTime());
        Refresh();
    }

    public void RenameNode(SceneNode node, string previousName)
    {
        if (_editClip == null)
        {
            return;
        }

        if (!_tracks.TryGetValue(node, out var track))
        {
            return;
        }

        if (string.Equals(previousName, node.Name, StringComparison.Ordinal))
        {
            return;
        }

        var replacement = new TransformTrack(node.Name);
        replacement.TranslationKeys.AddRange(track.TranslationKeys);
        replacement.RotationKeys.AddRange(track.RotationKeys);
        replacement.ScaleKeys.AddRange(track.ScaleKeys);

        var index = _editClip.Tracks.IndexOf(track);
        if (index >= 0)
        {
            _editClip.Tracks[index] = replacement;
        }

        _tracks[node] = replacement;
        _clipDirty = true;
        RebindClipInPlayers(_editClip, GetKeyTime());
        Refresh();
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
        _editorViewModel.PropertyChanged -= OnEditorPropertyChanged;
        _subscriptions.Dispose();
        if (_autoKeyHooked)
        {
            _viewportService.TransformCommitted -= OnTransformCommitted;
            _autoKeyHooked = false;
        }
    }
}
