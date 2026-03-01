using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Skia3D.Sample.Models;

namespace Skia3D.Sample.Converters;

public sealed class KeyEventToShortcutInputConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not KeyEventArgs keyEvent)
        {
            return null;
        }

        var key = NormalizeKey(keyEvent.Key);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var modifiers = keyEvent.KeyModifiers;
        var gesture = new ShortcutGesture(
            key,
            ctrl: modifiers.HasFlag(KeyModifiers.Control),
            shift: modifiers.HasFlag(KeyModifiers.Shift),
            alt: modifiers.HasFlag(KeyModifiers.Alt),
            meta: modifiers.HasFlag(KeyModifiers.Meta));

        if (gesture.IsEmpty)
        {
            return null;
        }

        return new ShortcutInput(gesture, keyEvent.Key == Key.None);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }

    private static string NormalizeKey(Key key)
    {
        return key switch
        {
            Key.None => string.Empty,
            Key.LeftCtrl or Key.RightCtrl => string.Empty,
            Key.LeftShift or Key.RightShift => string.Empty,
            Key.LeftAlt or Key.RightAlt => string.Empty,
            Key.LWin or Key.RWin => string.Empty,
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            _ => key.ToString()
        };
    }
}
