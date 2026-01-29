namespace Skia3D.Runtime;

public sealed class Time
{
    public float DeltaSeconds { get; private set; }

    public float UnscaledDeltaSeconds { get; private set; }

    public float TotalSeconds { get; private set; }

    public float UnscaledTotalSeconds { get; private set; }

    public float FixedDeltaSeconds { get; internal set; } = 1f / 60f;

    public float FixedTotalSeconds { get; private set; }

    public int FixedStepCount { get; internal set; }

    public int FrameCount { get; internal set; }

    public int FixedFrameCount { get; private set; }

    public float TimeScale { get; set; } = 1f;

    public float Alpha { get; internal set; }

    internal void AdvanceFrame(float unscaledDeltaSeconds, float fixedDeltaSeconds, int fixedSteps, float alpha)
    {
        if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds))
        {
            unscaledDeltaSeconds = 0f;
        }

        if (unscaledDeltaSeconds < 0f)
        {
            unscaledDeltaSeconds = 0f;
        }

        var timeScale = float.IsNaN(TimeScale) || float.IsInfinity(TimeScale) ? 1f : TimeScale;
        if (timeScale < 0f)
        {
            timeScale = 0f;
        }

        UnscaledDeltaSeconds = unscaledDeltaSeconds;
        DeltaSeconds = unscaledDeltaSeconds * timeScale;
        UnscaledTotalSeconds += unscaledDeltaSeconds;
        TotalSeconds += DeltaSeconds;

        FixedDeltaSeconds = Math.Max(1e-6f, fixedDeltaSeconds);
        FixedStepCount = fixedSteps;
        Alpha = alpha;
        FrameCount++;
    }

    internal void AdvanceFixed()
    {
        FixedTotalSeconds += FixedDeltaSeconds;
        FixedFrameCount++;
    }

    public void Reset()
    {
        DeltaSeconds = 0f;
        UnscaledDeltaSeconds = 0f;
        TotalSeconds = 0f;
        UnscaledTotalSeconds = 0f;
        FixedTotalSeconds = 0f;
        FixedStepCount = 0;
        FrameCount = 0;
        FixedFrameCount = 0;
        Alpha = 0f;
    }
}
