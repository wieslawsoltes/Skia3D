using System;

namespace Skia3D.Animation;

public enum AnimationParameterType
{
    Float,
    Int,
    Bool,
    Trigger
}

public enum AnimationComparison
{
    Equals,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual
}

public sealed class AnimationCondition
{
    private AnimationCondition(string parameter, AnimationParameterType parameterType, AnimationComparison comparison)
    {
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        ParameterType = parameterType;
        Comparison = comparison;
    }

    public string Parameter { get; }

    public AnimationParameterType ParameterType { get; }

    public AnimationComparison Comparison { get; }

    public float FloatValue { get; private set; }

    public int IntValue { get; private set; }

    public bool BoolValue { get; private set; }

    public static AnimationCondition Float(string parameter, AnimationComparison comparison, float value)
    {
        return new AnimationCondition(parameter, AnimationParameterType.Float, comparison)
        {
            FloatValue = value
        };
    }

    public static AnimationCondition Int(string parameter, AnimationComparison comparison, int value)
    {
        return new AnimationCondition(parameter, AnimationParameterType.Int, comparison)
        {
            IntValue = value
        };
    }

    public static AnimationCondition Bool(string parameter, bool value)
    {
        return new AnimationCondition(parameter, AnimationParameterType.Bool, AnimationComparison.Equals)
        {
            BoolValue = value
        };
    }

    public static AnimationCondition Trigger(string parameter)
    {
        return new AnimationCondition(parameter, AnimationParameterType.Trigger, AnimationComparison.Equals)
        {
            BoolValue = true
        };
    }

    public bool IsMet(AnimationParameterSet parameters, bool consumeTrigger)
    {
        if (parameters is null)
        {
            return false;
        }

        return ParameterType switch
        {
            AnimationParameterType.Float => Compare(parameters.GetFloat(Parameter), FloatValue),
            AnimationParameterType.Int => Compare(parameters.GetInt(Parameter), IntValue),
            AnimationParameterType.Bool => parameters.GetBool(Parameter) == BoolValue,
            AnimationParameterType.Trigger => consumeTrigger ? parameters.ConsumeTrigger(Parameter) : parameters.IsTriggerSet(Parameter),
            _ => false
        };
    }

    private bool Compare(float value, float target)
    {
        return Comparison switch
        {
            AnimationComparison.Equals => MathF.Abs(value - target) <= 1e-6f,
            AnimationComparison.NotEqual => MathF.Abs(value - target) > 1e-6f,
            AnimationComparison.Greater => value > target,
            AnimationComparison.GreaterOrEqual => value >= target,
            AnimationComparison.Less => value < target,
            AnimationComparison.LessOrEqual => value <= target,
            _ => false
        };
    }

    private bool Compare(int value, int target)
    {
        return Comparison switch
        {
            AnimationComparison.Equals => value == target,
            AnimationComparison.NotEqual => value != target,
            AnimationComparison.Greater => value > target,
            AnimationComparison.GreaterOrEqual => value >= target,
            AnimationComparison.Less => value < target,
            AnimationComparison.LessOrEqual => value <= target,
            _ => false
        };
    }
}

