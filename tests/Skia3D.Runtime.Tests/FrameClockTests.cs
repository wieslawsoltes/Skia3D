using Skia3D.Runtime;
using Xunit;

namespace Skia3D.Runtime.Tests;

public sealed class FrameClockTests
{
    [Fact]
    public void FrameClock_Step_ComputesFixedStepsAndAlpha()
    {
        var clock = new FrameClock
        {
            FixedDeltaSeconds = 0.02f,
            MaxFixedSteps = 10,
            MaxDeltaSeconds = 1f
        };

        var step = clock.Step(0.05f);

        Assert.Equal(0.05f, step.DeltaSeconds, 3);
        Assert.Equal(2, step.FixedSteps);
        Assert.InRange(step.Alpha, 0.49f, 0.51f);
    }

    [Fact]
    public void FrameClock_Step_RespectsMaxFixedSteps()
    {
        var clock = new FrameClock
        {
            FixedDeltaSeconds = 0.02f,
            MaxFixedSteps = 5,
            MaxDeltaSeconds = 1f
        };

        var step = clock.Step(1f);

        Assert.Equal(5, step.FixedSteps);
        Assert.InRange(step.Alpha, 0f, 1f);
    }

    [Fact]
    public void Engine_Tick_AdvancesTimeAndFixedSteps()
    {
        var clock = new FrameClock
        {
            FixedDeltaSeconds = 0.02f,
            MaxFixedSteps = 10,
            MaxDeltaSeconds = 1f
        };

        var engine = new Engine(clock);
        var tracker = new TimeCaptureSystem();
        engine.AddSystem(tracker);
        engine.Initialize();

        engine.Tick(0.05f);

        Assert.Equal(0.05f, engine.Time.UnscaledDeltaSeconds, 3);
        Assert.Equal(0.05f, engine.Time.UnscaledTotalSeconds, 3);
        Assert.Equal(2, engine.Time.FixedStepCount);
        Assert.Equal(2, tracker.FixedUpdates);
        Assert.Equal(1, tracker.Updates);
    }

    private sealed class TimeCaptureSystem : SystemBase
    {
        public int FixedUpdates { get; private set; }

        public int Updates { get; private set; }

        public override void FixedUpdate(Engine engine, Time time)
        {
            FixedUpdates++;
        }

        public override void Update(Engine engine, Time time)
        {
            Updates++;
        }
    }
}
