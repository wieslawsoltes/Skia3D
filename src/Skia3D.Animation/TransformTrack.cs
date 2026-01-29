using System;
using System.Collections.Generic;
using System.Numerics;

namespace Skia3D.Animation;

public sealed class TransformTrack
{
    public TransformTrack(string targetName)
    {
        TargetName = targetName ?? throw new ArgumentNullException(nameof(targetName));
    }

    public string TargetName { get; }

    public List<Keyframe<Vector3>> TranslationKeys { get; } = new();

    public List<Keyframe<Quaternion>> RotationKeys { get; } = new();

    public List<Keyframe<Vector3>> ScaleKeys { get; } = new();

    public float MaxTime
    {
        get
        {
            float max = 0f;
            if (TranslationKeys.Count > 0)
            {
                max = MathF.Max(max, TranslationKeys[^1].Time);
            }
            if (RotationKeys.Count > 0)
            {
                max = MathF.Max(max, RotationKeys[^1].Time);
            }
            if (ScaleKeys.Count > 0)
            {
                max = MathF.Max(max, ScaleKeys[^1].Time);
            }

            return max;
        }
    }

    public bool TryEvaluateTranslation(float time, out Vector3 value)
    {
        if (TranslationKeys.Count == 0)
        {
            value = default;
            return false;
        }

        value = SampleVector(TranslationKeys, time);
        return true;
    }

    public bool TryEvaluateRotation(float time, out Quaternion value)
    {
        if (RotationKeys.Count == 0)
        {
            value = default;
            return false;
        }

        value = SampleQuaternion(RotationKeys, time);
        return true;
    }

    public bool TryEvaluateScale(float time, out Vector3 value)
    {
        if (ScaleKeys.Count == 0)
        {
            value = default;
            return false;
        }

        value = SampleVector(ScaleKeys, time);
        return true;
    }

    private static Vector3 SampleVector(IReadOnlyList<Keyframe<Vector3>> keys, float time)
    {
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
                float span = b.Time - a.Time;
                float t = span <= 1e-6f ? 0f : (time - a.Time) / span;
                return Vector3.Lerp(a.Value, b.Value, t);
            }
        }

        return keys[^1].Value;
    }

    private static Quaternion SampleQuaternion(IReadOnlyList<Keyframe<Quaternion>> keys, float time)
    {
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
                float span = b.Time - a.Time;
                float t = span <= 1e-6f ? 0f : (time - a.Time) / span;
                return Quaternion.Slerp(a.Value, b.Value, t);
            }
        }

        return Quaternion.Normalize(keys[^1].Value);
    }
}
