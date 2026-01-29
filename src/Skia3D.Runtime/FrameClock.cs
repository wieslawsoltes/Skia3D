using System.Diagnostics;

namespace Skia3D.Runtime;

public readonly record struct FrameStep(float DeltaSeconds, int FixedSteps, float Alpha);

public sealed class FrameClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTicks;
    private double _accumulator;

    public FrameClock()
    {
        _lastTicks = _stopwatch.ElapsedTicks;
    }

    public float FixedDeltaSeconds { get; set; } = 1f / 60f;

    public float MaxDeltaSeconds { get; set; } = 0.1f;

    public int MaxFixedSteps { get; set; } = 5;

    public FrameStep Step()
    {
        return Step(GetUnscaledDeltaSeconds());
    }

    public FrameStep Step(float unscaledDeltaSeconds)
    {
        if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds))
        {
            unscaledDeltaSeconds = 0f;
        }

        if (unscaledDeltaSeconds < 0f)
        {
            unscaledDeltaSeconds = 0f;
        }

        if (MaxDeltaSeconds > 0f && unscaledDeltaSeconds > MaxDeltaSeconds)
        {
            unscaledDeltaSeconds = MaxDeltaSeconds;
        }

        var fixedDelta = Math.Max(1e-6f, FixedDeltaSeconds);
        _accumulator += unscaledDeltaSeconds;

        int fixedSteps = MaxFixedSteps > 0
            ? Math.Min((int)(_accumulator / fixedDelta), MaxFixedSteps)
            : (int)(_accumulator / fixedDelta);

        _accumulator -= fixedSteps * fixedDelta;
        if (_accumulator < 0)
        {
            _accumulator = 0;
        }

        float alpha = fixedDelta > 0f ? (float)(_accumulator / fixedDelta) : 0f;
        alpha = Math.Clamp(alpha, 0f, 1f);
        return new FrameStep(unscaledDeltaSeconds, fixedSteps, alpha);
    }

    public void Reset()
    {
        _stopwatch.Restart();
        _lastTicks = _stopwatch.ElapsedTicks;
        _accumulator = 0;
    }

    private float GetUnscaledDeltaSeconds()
    {
        var ticks = _stopwatch.ElapsedTicks;
        var deltaTicks = ticks - _lastTicks;
        _lastTicks = ticks;

        if (deltaTicks <= 0)
        {
            return 0f;
        }

        return (float)((double)deltaTicks / Stopwatch.Frequency);
    }
}
