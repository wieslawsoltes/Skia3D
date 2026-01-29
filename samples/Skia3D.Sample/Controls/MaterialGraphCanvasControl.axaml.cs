using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Controls;

public sealed partial class MaterialGraphCanvasControl : UserControl
{
    private ShaderGraphCanvasViewModel? _viewModel;
    private bool _isPanning;
    private Point _lastPan;
    private ShaderNodeViewModel? _dragNode;
    private Point _dragOffset;

    public MaterialGraphCanvasControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as ShaderGraphCanvasViewModel;
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        bool panIntent = point.Properties.IsMiddleButtonPressed
                         || point.Properties.IsRightButtonPressed;
        if (!panIntent)
        {
            return;
        }

        _isPanning = true;
        _lastPan = point.Position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || _viewModel == null)
        {
            return;
        }

        var pos = e.GetPosition(this);
        var delta = pos - _lastPan;
        _viewModel.PanX += delta.X;
        _viewModel.PanY += delta.Y;
        _lastPan = pos;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        e.Pointer.Capture(null);
    }

    private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        var delta = e.Delta.Y;
        if (Math.Abs(delta) < 0.001)
        {
            return;
        }

        var current = _viewModel.Zoom;
        var factor = delta > 0 ? 1.1 : 0.9;
        var next = Math.Clamp(current * factor, 0.25, 2.5);
        if (Math.Abs(next - current) < 1e-6)
        {
            return;
        }

        var viewPos = e.GetPosition(this);
        var graphPos = ToGraphPoint(viewPos);
        _viewModel.Zoom = next;
        _viewModel.PanX = viewPos.X - graphPos.X * next;
        _viewModel.PanY = viewPos.Y - graphPos.Y * next;
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null || sender is not Control control)
        {
            return;
        }

        if (control.DataContext is not ShaderNodeViewModel node)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragNode = node;
        var graphPoint = ToGraphPoint(point.Position);
        _dragOffset = new Point(graphPoint.X - node.X, graphPoint.Y - node.Y);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnNodePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragNode == null || _viewModel == null)
        {
            return;
        }

        var graphPoint = ToGraphPoint(e.GetPosition(this));
        _dragNode.X = graphPoint.X - _dragOffset.X;
        _dragNode.Y = graphPoint.Y - _dragOffset.Y;
    }

    private void OnNodePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragNode == null)
        {
            return;
        }

        _dragNode = null;
        e.Pointer.Capture(null);
    }

    private Point ToGraphPoint(Point viewPoint)
    {
        var zoom = _viewModel?.Zoom ?? 1.0;
        var panX = _viewModel?.PanX ?? 0.0;
        var panY = _viewModel?.PanY ?? 0.0;
        return new Point((viewPoint.X - panX) / zoom, (viewPoint.Y - panY) / zoom);
    }
}
