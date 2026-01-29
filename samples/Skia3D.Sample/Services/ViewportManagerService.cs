using System;
using System.Collections.Generic;

namespace Skia3D.Sample.Services;

public sealed class ViewportManagerService : IDisposable
{
    private readonly List<EditorViewportService> _viewports = new();
    private EditorViewportService? _active;

    public IReadOnlyList<EditorViewportService> Viewports => _viewports;

    public EditorViewportService PrimaryViewport
    {
        get
        {
            if (_viewports.Count == 0)
            {
                throw new InvalidOperationException("No viewports registered.");
            }

            return _viewports[0];
        }
    }

    public EditorViewportService ActiveViewport
    {
        get
        {
            if (_active != null)
            {
                return _active;
            }

            return PrimaryViewport;
        }
    }

    public event Action<EditorViewportService>? ActiveViewportChanged;

    public void Register(EditorViewportService viewport, bool makeActive = false)
    {
        if (viewport is null)
        {
            throw new ArgumentNullException(nameof(viewport));
        }

        _viewports.Add(viewport);
        viewport.Activated += OnViewportActivated;

        if (_active == null || makeActive)
        {
            SetActive(viewport);
        }
        else
        {
            viewport.IsActive = false;
        }
    }

    public void SetActive(EditorViewportService viewport)
    {
        if (_active == viewport)
        {
            return;
        }

        _active = viewport;
        for (int i = 0; i < _viewports.Count; i++)
        {
            _viewports[i].IsActive = ReferenceEquals(_viewports[i], viewport);
        }

        ActiveViewportChanged?.Invoke(viewport);
    }

    public void InvalidateAll()
    {
        for (int i = 0; i < _viewports.Count; i++)
        {
            _viewports[i].Invalidate();
        }
    }

    public void UpdateSelectedNodeFromEditor()
    {
        for (int i = 0; i < _viewports.Count; i++)
        {
            _viewports[i].UpdateSelectedNodeFromEditor();
        }
    }

    public void UpdateSceneLights()
    {
        for (int i = 0; i < _viewports.Count; i++)
        {
            _viewports[i].UpdateSceneLights();
        }
    }

    public void ResetState()
    {
        for (int i = 0; i < _viewports.Count; i++)
        {
            _viewports[i].ResetState();
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _viewports.Count; i++)
        {
            _viewports[i].Activated -= OnViewportActivated;
        }
    }

    private void OnViewportActivated(object? sender, EventArgs e)
    {
        if (sender is EditorViewportService viewport)
        {
            SetActive(viewport);
        }
    }
}
