namespace Skia3D.Scene;

public abstract class SceneComponent
{
    public SceneNode? Node { get; private set; }

    public bool Enabled { get; set; } = true;

    internal void Attach(SceneNode node)
    {
        Node = node;
        OnAttached(node);
    }

    internal void Detach(SceneNode node)
    {
        OnDetached(node);
        Node = null;
    }

    protected virtual void OnAttached(SceneNode node)
    {
    }

    protected virtual void OnDetached(SceneNode node)
    {
    }
}
