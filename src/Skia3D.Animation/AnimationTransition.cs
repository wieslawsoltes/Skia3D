using System;
using System.Collections.Generic;

namespace Skia3D.Animation;

public sealed class AnimationTransition
{
    public AnimationTransition(AnimationState from, AnimationState to)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
    }

    public AnimationState From { get; }

    public AnimationState To { get; }

    public float Duration { get; set; } = 0.15f;

    public bool HasExitTime { get; set; }

    public float ExitTime { get; set; } = 0.9f;

    public List<AnimationCondition> Conditions { get; } = new();

    public bool IsTriggered(AnimationParameterSet parameters, float normalizedTime, bool consumeTriggers)
    {
        if (HasExitTime && normalizedTime < ExitTime)
        {
            return false;
        }

        if (Conditions.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < Conditions.Count; i++)
        {
            if (!Conditions[i].IsMet(parameters, consumeTriggers))
            {
                return false;
            }
        }

        return true;
    }
}

