using System.Numerics;

namespace Skia3D.Input;

public sealed class InputState
{
    public Vector2 Position { get; set; }

    public Vector2 Delta { get; set; }

    public InputButtons Buttons { get; set; }

    public InputModifiers Modifiers { get; set; }

    public float WheelDelta { get; set; }

    public bool IsButtonDown(InputPointerButton button)
    {
        return button switch
        {
            InputPointerButton.Left => (Buttons & InputButtons.Left) != 0,
            InputPointerButton.Right => (Buttons & InputButtons.Right) != 0,
            InputPointerButton.Middle => (Buttons & InputButtons.Middle) != 0,
            InputPointerButton.XButton1 => (Buttons & InputButtons.XButton1) != 0,
            InputPointerButton.XButton2 => (Buttons & InputButtons.XButton2) != 0,
            _ => false
        };
    }
}
