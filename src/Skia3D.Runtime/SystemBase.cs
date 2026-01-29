namespace Skia3D.Runtime;

public abstract class SystemBase : ISystem
{
    public virtual void Initialize(Engine engine)
    {
    }

    public virtual void FixedUpdate(Engine engine, Time time)
    {
    }

    public virtual void Update(Engine engine, Time time)
    {
    }

    public virtual void Render(Engine engine, in RenderContext context)
    {
    }

    public virtual void Shutdown(Engine engine)
    {
    }
}
