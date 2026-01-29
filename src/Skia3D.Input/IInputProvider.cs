namespace Skia3D.Input;

public interface IInputProvider
{
    InputState State { get; }

    event EventHandler<InputEvent>? Input;
}
