using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Skia3D.Input;

namespace Skia3D.Sample.Services;

public sealed class AvaloniaInputProvider : IInputProvider
{
    private Control? _control;
    private bool _hasLastPosition;
    private Vector2 _lastPosition;
    private InputButtons _buttons;

    public InputState State { get; } = new();

    public event EventHandler<InputEvent>? Input;

    public void Attach(Control control)
    {
        if (control is null)
        {
            throw new ArgumentNullException(nameof(control));
        }

        Detach();
        _control = control;
        _hasLastPosition = false;
        _buttons = InputButtons.None;

        control.PointerPressed += OnPointerPressed;
        control.PointerReleased += OnPointerReleased;
        control.PointerMoved += OnPointerMoved;
        control.PointerWheelChanged += OnPointerWheelChanged;
    }

    public void Detach()
    {
        if (_control == null)
        {
            return;
        }

        _control.PointerPressed -= OnPointerPressed;
        _control.PointerReleased -= OnPointerReleased;
        _control.PointerMoved -= OnPointerMoved;
        _control.PointerWheelChanged -= OnPointerWheelChanged;
        _control = null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_control is null)
        {
            return;
        }

        var props = e.GetCurrentPoint(_control).Properties;
        var newButtons = GetButtons(props);
        var pressed = newButtons & ~_buttons;
        _buttons = newButtons;

        var position = e.GetPosition(_control);
        UpdateState(position, _buttons, e.KeyModifiers, wheelDelta: 0f);

        e.Pointer.Capture(_control);
        Emit(InputEventType.Pressed, ToPointerButton(pressed), 0f);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_control is null)
        {
            return;
        }

        var props = e.GetCurrentPoint(_control).Properties;
        var newButtons = GetButtons(props);
        var released = _buttons & ~newButtons;
        _buttons = newButtons;

        var position = e.GetPosition(_control);
        UpdateState(position, _buttons, e.KeyModifiers, wheelDelta: 0f);

        e.Pointer.Capture(null);
        Emit(InputEventType.Released, ToPointerButton(released), 0f);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_control is null)
        {
            return;
        }

        var props = e.GetCurrentPoint(_control).Properties;
        var newButtons = GetButtons(props);
        _buttons = newButtons;

        var position = e.GetPosition(_control);
        UpdateState(position, _buttons, e.KeyModifiers, wheelDelta: 0f);
        Emit(InputEventType.Moved, InputPointerButton.None, 0f);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_control is null)
        {
            return;
        }

        var props = e.GetCurrentPoint(_control).Properties;
        var newButtons = GetButtons(props);
        _buttons = newButtons;

        var position = e.GetPosition(_control);
        var wheelDelta = (float)e.Delta.Y;
        UpdateState(position, _buttons, e.KeyModifiers, wheelDelta);
        Emit(InputEventType.Wheel, InputPointerButton.None, wheelDelta);
    }

    private void UpdateState(Point position, InputButtons buttons, KeyModifiers modifiers, float wheelDelta)
    {
        var current = new Vector2((float)position.X, (float)position.Y);
        var delta = _hasLastPosition ? current - _lastPosition : Vector2.Zero;
        _hasLastPosition = true;
        _lastPosition = current;

        State.Position = current;
        State.Delta = delta;
        State.Buttons = buttons;
        State.Modifiers = ToModifiers(modifiers);
        State.WheelDelta = wheelDelta;
    }

    private void Emit(InputEventType type, InputPointerButton button, float wheelDelta)
    {
        Input?.Invoke(this, new InputEvent(type, State, button, wheelDelta));
    }

    private static InputButtons GetButtons(PointerPointProperties props)
    {
        InputButtons buttons = InputButtons.None;
        if (props.IsLeftButtonPressed)
        {
            buttons |= InputButtons.Left;
        }
        if (props.IsRightButtonPressed)
        {
            buttons |= InputButtons.Right;
        }
        if (props.IsMiddleButtonPressed)
        {
            buttons |= InputButtons.Middle;
        }
        if (props.IsXButton1Pressed)
        {
            buttons |= InputButtons.XButton1;
        }
        if (props.IsXButton2Pressed)
        {
            buttons |= InputButtons.XButton2;
        }
        return buttons;
    }

    private static InputPointerButton ToPointerButton(InputButtons buttons)
    {
        if ((buttons & InputButtons.Left) != 0)
        {
            return InputPointerButton.Left;
        }
        if ((buttons & InputButtons.Right) != 0)
        {
            return InputPointerButton.Right;
        }
        if ((buttons & InputButtons.Middle) != 0)
        {
            return InputPointerButton.Middle;
        }
        if ((buttons & InputButtons.XButton1) != 0)
        {
            return InputPointerButton.XButton1;
        }
        if ((buttons & InputButtons.XButton2) != 0)
        {
            return InputPointerButton.XButton2;
        }

        return InputPointerButton.None;
    }

    private static InputModifiers ToModifiers(KeyModifiers modifiers)
    {
        InputModifiers result = InputModifiers.None;
        if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
        {
            result |= InputModifiers.Shift;
        }
        if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            result |= InputModifiers.Control;
        }
        if ((modifiers & KeyModifiers.Alt) == KeyModifiers.Alt)
        {
            result |= InputModifiers.Alt;
        }
        if ((modifiers & KeyModifiers.Meta) == KeyModifiers.Meta)
        {
            result |= InputModifiers.Command;
        }
        return result;
    }
}
