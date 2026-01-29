using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Skia3D.Sample.Services;

public sealed class WorkspaceLayoutStore
{
    private readonly string _storePath;

    public WorkspaceLayoutStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skia3D.Sample");
        _storePath = Path.Combine(root, "workspaces.json");
    }

    public WorkspaceStoreData Load()
    {
        try
        {
            if (!File.Exists(_storePath))
            {
                return new WorkspaceStoreData();
            }

            var json = File.ReadAllText(_storePath);
            var data = JsonSerializer.Deserialize<WorkspaceStoreData>(json);
            return data ?? new WorkspaceStoreData();
        }
        catch
        {
            return new WorkspaceStoreData();
        }
    }

    public void Save(WorkspaceStoreData data)
    {
        if (data == null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_storePath, json);
    }
}

public sealed class WorkspaceStoreData
{
    public string? SelectedWorkspace { get; set; }

    public List<WorkspaceLayout> Workspaces { get; set; } = new();
}

public sealed class WorkspaceLayout
{
    public string Name { get; set; } = string.Empty;
    public bool ShowCommandPanel { get; set; } = true;
    public bool ShowSceneExplorer { get; set; } = true;
    public bool ShowRibbon { get; set; } = true;
    public bool ShowTimeline { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowViewportToolbar { get; set; } = true;
}
