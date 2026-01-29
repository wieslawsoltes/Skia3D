namespace Skia3D.Runtime;

public sealed class Engine
{
    private readonly List<ISystem> _systems = new();

    public Engine(FrameClock? clock = null)
    {
        Clock = clock ?? new FrameClock();
        Time = new Time();
    }

    public FrameClock Clock { get; }

    public Time Time { get; }

    public bool IsInitialized { get; private set; }

    public IReadOnlyList<ISystem> Systems => _systems;

    public void AddSystem(ISystem system)
    {
        if (system is null)
        {
            throw new ArgumentNullException(nameof(system));
        }

        if (_systems.Contains(system))
        {
            return;
        }

        _systems.Add(system);
        if (IsInitialized)
        {
            system.Initialize(this);
        }
    }

    public bool RemoveSystem(ISystem system, bool shutdown = true)
    {
        if (system is null)
        {
            return false;
        }

        if (!_systems.Remove(system))
        {
            return false;
        }

        if (shutdown && IsInitialized)
        {
            system.Shutdown(this);
        }

        return true;
    }

    public void Initialize()
    {
        if (IsInitialized)
        {
            return;
        }

        Clock.Reset();
        Time.Reset();
        for (int i = 0; i < _systems.Count; i++)
        {
            _systems[i].Initialize(this);
        }

        IsInitialized = true;
    }

    public void Shutdown()
    {
        if (!IsInitialized)
        {
            return;
        }

        for (int i = _systems.Count - 1; i >= 0; i--)
        {
            _systems[i].Shutdown(this);
        }

        IsInitialized = false;
    }

    public void Tick()
    {
        EnsureInitialized();
        TickInternal(Clock.Step());
    }

    public void Tick(float unscaledDeltaSeconds)
    {
        EnsureInitialized();
        TickInternal(Clock.Step(unscaledDeltaSeconds));
    }

    private void TickInternal(FrameStep step)
    {
        Time.AdvanceFrame(step.DeltaSeconds, Clock.FixedDeltaSeconds, step.FixedSteps, step.Alpha);

        for (int i = 0; i < step.FixedSteps; i++)
        {
            Time.AdvanceFixed();
            for (int s = 0; s < _systems.Count; s++)
            {
                _systems[s].FixedUpdate(this, Time);
            }
        }

        for (int i = 0; i < _systems.Count; i++)
        {
            _systems[i].Update(this, Time);
        }
    }

    public void Render(in RenderContext context)
    {
        EnsureInitialized();

        for (int i = 0; i < _systems.Count; i++)
        {
            _systems[i].Render(this, context);
        }
    }

    public void TickAndRender(in RenderContext context)
    {
        Tick();
        Render(context);
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("Engine must be initialized before use.");
        }
    }
}
