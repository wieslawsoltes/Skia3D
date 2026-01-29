using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Services;

public sealed class StatusHintService : IDisposable
{
    private readonly TopLevel _root;
    private readonly StatusBarViewModel _statusBar;
    private readonly InspectorOptionsViewModel _options;
    private string _fallbackHint;
    private Control? _currentHintControl;

    public StatusHintService(TopLevel root, StatusBarViewModel statusBar, InspectorOptionsViewModel options)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _statusBar = statusBar ?? throw new ArgumentNullException(nameof(statusBar));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _fallbackHint = BuildFallbackHint();
        _statusBar.HelpText = _fallbackHint;

        _options.PropertyChanged += OnOptionsPropertyChanged;
        _root.AddHandler(InputElement.PointerEnteredEvent, OnPointerEntered, RoutingStrategies.Tunnel);
        _root.AddHandler(InputElement.PointerExitedEvent, OnPointerExited, RoutingStrategies.Tunnel);
    }

    private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InspectorOptionsViewModel.ViewportHudLabel)
            && e.PropertyName != nameof(InspectorOptionsViewModel.NavigationMode))
        {
            return;
        }

        _fallbackHint = BuildFallbackHint();
        if (_currentHintControl == null)
        {
            _statusBar.HelpText = _fallbackHint;
        }
    }

    private string BuildFallbackHint()
    {
        var navLabel = _options.NavigationMode switch
        {
            ViewportNavigationMode.Pan => "Pan",
            ViewportNavigationMode.Orbit => "Orbit",
            ViewportNavigationMode.Zoom => "Zoom",
            _ => "Default"
        };

        return $"{_options.ViewportHudLabel} | Navigation: {navLabel}";
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Source is not Control control)
        {
            return;
        }

        var hint = ToolTip.GetTip(control) as string;
        if (string.IsNullOrWhiteSpace(hint))
        {
            return;
        }

        _currentHintControl = control;
        _statusBar.HelpText = hint;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (e.Source is not Control control)
        {
            return;
        }

        if (!ReferenceEquals(_currentHintControl, control))
        {
            return;
        }

        _currentHintControl = null;
        _statusBar.HelpText = _fallbackHint;
    }

    public void Dispose()
    {
        _options.PropertyChanged -= OnOptionsPropertyChanged;
        _root.RemoveHandler(InputElement.PointerEnteredEvent, OnPointerEntered);
        _root.RemoveHandler(InputElement.PointerExitedEvent, OnPointerExited);
    }
}
