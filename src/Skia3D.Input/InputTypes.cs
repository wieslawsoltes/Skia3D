using System;

namespace Skia3D.Input;

[Flags]
public enum InputModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Command = 1 << 3
}

[Flags]
public enum InputButtons
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Middle = 1 << 2,
    XButton1 = 1 << 3,
    XButton2 = 1 << 4
}

public enum InputPointerButton
{
    None,
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

public enum InputEventType
{
    Pressed,
    Released,
    Moved,
    Wheel
}

public enum InputActionTrigger
{
    Pressed,
    Released,
    Held,
    Wheel
}

public readonly record struct InputBinding(InputButtons Buttons, InputModifiers Modifiers, InputActionTrigger Trigger)
{
    public bool Matches(InputEvent inputEvent)
    {
        if ((inputEvent.State.Buttons & Buttons) != Buttons)
        {
            return false;
        }

        if ((inputEvent.State.Modifiers & Modifiers) != Modifiers)
        {
            return false;
        }

        return Trigger switch
        {
            InputActionTrigger.Pressed => inputEvent.Type == InputEventType.Pressed,
            InputActionTrigger.Released => inputEvent.Type == InputEventType.Released,
            InputActionTrigger.Wheel => inputEvent.Type == InputEventType.Wheel,
            _ => inputEvent.Type == InputEventType.Moved || inputEvent.Type == InputEventType.Pressed
        };
    }
}
