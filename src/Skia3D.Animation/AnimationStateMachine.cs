using System;
using System.Collections.Generic;

namespace Skia3D.Animation;

public sealed class AnimationStateMachine
{
    private readonly List<AnimationState> _states = new();
    private readonly List<AnimationTransition> _transitions = new();
    private AnimationTransition? _activeTransition;
    private float _transitionTime;
    private readonly AnimationPose _blendPose = new();

    public AnimationParameterSet Parameters { get; } = new();

    public IReadOnlyList<AnimationState> States => _states;

    public IReadOnlyList<AnimationTransition> Transitions => _transitions;

    public AnimationState? CurrentState { get; private set; }

    public AnimationState AddState(AnimationState state, bool isDefault = false)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        _states.Add(state);
        if (CurrentState == null || isDefault)
        {
            CurrentState = state;
        }

        return state;
    }

    public AnimationTransition AddTransition(AnimationState from, AnimationState to)
    {
        var transition = new AnimationTransition(from, to);
        _transitions.Add(transition);
        return transition;
    }

    public void SetCurrentState(AnimationState state)
    {
        CurrentState = state ?? throw new ArgumentNullException(nameof(state));
        _activeTransition = null;
        _transitionTime = 0f;
    }

    public void Update(float deltaSeconds)
    {
        if (CurrentState == null || deltaSeconds <= 0f)
        {
            return;
        }

        if (_activeTransition != null)
        {
            UpdateTransition(deltaSeconds);
            return;
        }

        CurrentState.Update(deltaSeconds, Parameters);
        PoseBlender.Apply(CurrentState.Pose);

        var next = FindTriggeredTransition(CurrentState);
        if (next != null)
        {
            BeginTransition(next);
        }
    }

    private void UpdateTransition(float deltaSeconds)
    {
        if (_activeTransition == null)
        {
            return;
        }

        _transitionTime += deltaSeconds;
        var duration = MathF.Max(_activeTransition.Duration, 1e-6f);
        float t = Math.Clamp(_transitionTime / duration, 0f, 1f);

        _activeTransition.From.Update(deltaSeconds, Parameters);
        _activeTransition.To.Update(deltaSeconds, Parameters);

        PoseBlender.Blend(_activeTransition.From.Pose, _activeTransition.To.Pose, t, _blendPose);
        PoseBlender.Apply(_blendPose);

        if (t >= 1f)
        {
            CurrentState = _activeTransition.To;
            _activeTransition = null;
            _transitionTime = 0f;
        }
    }

    private AnimationTransition? FindTriggeredTransition(AnimationState state)
    {
        float normalizedTime = state.GetNormalizedTime();
        for (int i = 0; i < _transitions.Count; i++)
        {
            var transition = _transitions[i];
            if (transition.From != state)
            {
                continue;
            }

            if (transition.IsTriggered(Parameters, normalizedTime, consumeTriggers: false))
            {
                return transition;
            }
        }

        return null;
    }

    private void BeginTransition(AnimationTransition transition)
    {
        if (!transition.IsTriggered(Parameters, transition.From.GetNormalizedTime(), consumeTriggers: true))
        {
            return;
        }

        transition.To.Reset();
        _activeTransition = transition;
        _transitionTime = 0f;
    }
}

