namespace Skia3D.Runtime;

public interface ISystem
{
    void Initialize(Engine engine);

    void FixedUpdate(Engine engine, Time time);

    void Update(Engine engine, Time time);

    void Render(Engine engine, in RenderContext context);

    void Shutdown(Engine engine);
}
