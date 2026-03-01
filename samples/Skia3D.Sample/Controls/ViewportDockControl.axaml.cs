using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Controls;

public sealed partial class ViewportDockControl : UserControl
{
    private ViewportDockContext? _context;
    private EditorViewportControl? _perspective;
    private EditorViewportControl? _top;
    private EditorViewportControl? _front;
    private EditorViewportControl? _left;

    public ViewportDockControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _perspective = this.FindControl<EditorViewportControl>("ViewportPerspective");
        _top = this.FindControl<EditorViewportControl>("ViewportTop");
        _front = this.FindControl<EditorViewportControl>("ViewportFront");
        _left = this.FindControl<EditorViewportControl>("ViewportLeft");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_context != null)
        {
            _context.PropertyChanged -= OnContextPropertyChanged;
        }

        _context = DataContext as ViewportDockContext;
        if (_context != null)
        {
            _context.PropertyChanged += OnContextPropertyChanged;
        }

        BindServices();
    }

    private void OnContextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewportDockContext.PerspectiveViewport)
            || e.PropertyName == nameof(ViewportDockContext.TopViewport)
            || e.PropertyName == nameof(ViewportDockContext.FrontViewport)
            || e.PropertyName == nameof(ViewportDockContext.LeftViewport))
        {
            BindServices();
        }
    }

    private void BindServices()
    {
        if (_context == null)
        {
            return;
        }

        if (_perspective != null && _context.PerspectiveViewport != null)
        {
            _perspective.BindService(_context.PerspectiveViewport);
        }

        if (_top != null && _context.TopViewport != null)
        {
            _top.BindService(_context.TopViewport);
        }

        if (_front != null && _context.FrontViewport != null)
        {
            _front.BindService(_context.FrontViewport);
        }

        if (_left != null && _context.LeftViewport != null)
        {
            _left.BindService(_context.LeftViewport);
        }
    }
}
