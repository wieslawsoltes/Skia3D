namespace Skia3D.Input;

public readonly record struct InputEvent(InputEventType Type, InputState State, InputPointerButton Button, float WheelDelta);
