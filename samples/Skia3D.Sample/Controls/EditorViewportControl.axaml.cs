using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Skia3D.Sample;
using Skia3D.Sample.Services;

namespace Skia3D.Sample.Controls;

public sealed partial class EditorViewportControl : UserControl
{
    private SkiaView? _surface;
    private EditorViewportService? _service;

    public event EventHandler? QuadMenuRequested;

    public EditorViewportControl()
    {
        InitializeComponent();
        _surface = this.FindControl<SkiaView>("Surface");
        if (_surface != null)
        {
            _surface.PointerPressed += OnSurfacePointerPressed;
        }
    }

    public void BindService(EditorViewportService service)
    {
        _service?.Detach();
        _service = service;
        if (_surface != null)
        {
            _service.Attach(_surface);
        }
    }

    private void OnSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_surface == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(_surface);
        if (point.Properties.IsRightButtonPressed)
        {
            QuadMenuRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
