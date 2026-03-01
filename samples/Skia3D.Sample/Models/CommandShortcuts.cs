using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Skia3D.Editor;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Models;

public sealed class EditorCommandDefinition : ViewModelBase
{
    private string _shortcut;

    public EditorCommandDefinition(string id, string name, string category, ICommand command, string defaultShortcut = "")
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        Command = command ?? throw new ArgumentNullException(nameof(command));
        DefaultShortcut = NormalizeShortcut(defaultShortcut);
        _shortcut = DefaultShortcut;
    }

    public string Id { get; }

    public string Name { get; }

    public string Category { get; }

    public ICommand Command { get; }

    public string DefaultShortcut { get; }

    public string Shortcut
    {
        get => _shortcut;
        set
        {
            var normalized = NormalizeShortcut(value);
            if (string.Equals(_shortcut, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _shortcut = normalized;
            RaisePropertyChanged();
        }
    }

    public void ResetShortcut() => Shortcut = DefaultShortcut;

    public static string NormalizeShortcut(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return ShortcutGesture.Parse(value).ToString();
    }
}

public sealed class EditorCommandCatalog
{
    public EditorCommandCatalog(
        EditorActionsViewModel actions,
        InspectorOptionsViewModel options,
        CommandPanelViewModel commandPanel,
        WorkspaceViewModel workspace)
    {
        if (actions is null)
        {
            throw new ArgumentNullException(nameof(actions));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (commandPanel is null)
        {
            throw new ArgumentNullException(nameof(commandPanel));
        }

        if (workspace is null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        Commands = new ObservableCollection<EditorCommandDefinition>();

        Add(new EditorCommandDefinition("file.open", "Open Scene", "File", actions.OpenSceneCommand, "Ctrl+O"));
        Add(new EditorCommandDefinition("file.save", "Save Scene", "File", actions.SaveSceneCommand, "Ctrl+S"));
        Add(new EditorCommandDefinition("file.saveAs", "Save Scene As", "File", actions.SaveSceneAsCommand, "Ctrl+Shift+S"));
        Add(new EditorCommandDefinition("file.import", "Import Mesh", "File", actions.LoadObjCommand, "Ctrl+I"));

        Add(new EditorCommandDefinition("edit.undo", "Undo", "Edit", actions.UndoCommand, "Ctrl+Z"));
        Add(new EditorCommandDefinition("edit.redo", "Redo", "Edit", actions.RedoCommand, "Ctrl+Y"));
        Add(new EditorCommandDefinition("edit.selectAll", "Select All", "Edit", actions.SelectAllCommand, "Ctrl+A"));
        Add(new EditorCommandDefinition("edit.clearSelection", "Clear Selection", "Edit", actions.ClearSelectionCommand, "Escape"));
        Add(new EditorCommandDefinition("edit.deleteSelection", "Delete Selection", "Edit", actions.DeleteSelectionCommand, "Delete"));
        Add(new EditorCommandDefinition("edit.duplicateSelection", "Duplicate Selection", "Edit", actions.DuplicateSelectionCommand, "Ctrl+D"));

        Add(new EditorCommandDefinition("view.zoomExtents", "Zoom Extents", "View", actions.ZoomExtentsCommand, "F"));
        Add(new EditorCommandDefinition("view.toggleGrid", "Toggle Grid", "View", options.ToggleGridCommand, "G"));
        Add(new EditorCommandDefinition("view.toggleWireframe", "Toggle Wireframe", "View", options.ToggleWireframeCommand, "Z"));
        Add(new EditorCommandDefinition("view.toggleSnap", "Toggle Snap", "View", options.ToggleGizmoSnapCommand, "S"));
        Add(new EditorCommandDefinition("view.toggleStats", "View Stats", "View", options.ToggleStatsCommand));

        Add(new EditorCommandDefinition("mode.select", "Selection Tool: Click", "Selection", options.SetSelectionToolCommand, "Q"));
        Add(new EditorCommandDefinition("mode.move", "Gizmo: Move", "Selection", options.SetGizmoModeCommand, "W"));
        Add(new EditorCommandDefinition("mode.rotate", "Gizmo: Rotate", "Selection", options.SetGizmoModeCommand, "E"));
        Add(new EditorCommandDefinition("mode.scale", "Gizmo: Scale", "Selection", options.SetGizmoModeCommand, "R"));

        Add(new EditorCommandDefinition("mesh.extrude", "Extrude Faces", "Modeling", actions.ExtrudeFacesCommand));
        Add(new EditorCommandDefinition("mesh.bevel", "Bevel Faces", "Modeling", actions.BevelFacesCommand));
        Add(new EditorCommandDefinition("mesh.inset", "Inset Faces", "Modeling", actions.InsetFacesCommand));

        Add(new EditorCommandDefinition("anim.setKey", "Set Key", "Animation", commandPanel.Motion.SetKeyCommand));
        Add(new EditorCommandDefinition("anim.reset", "Reset Animation", "Animation", actions.AnimationResetCommand));
        Add(new EditorCommandDefinition("anim.clearKeys", "Clear Animation Keys", "Animation", actions.ClearAnimationKeysCommand));

        Add(new EditorCommandDefinition("workspace.save", "Save Workspace", "Workspace", workspace.SaveCommand));
        Add(new EditorCommandDefinition("workspace.saveAs", "Save Workspace As", "Workspace", workspace.SaveAsCommand));
    }

    public ObservableCollection<EditorCommandDefinition> Commands { get; }

    public void Add(EditorCommandDefinition definition)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (Commands.Any(existing => string.Equals(existing.Id, definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Commands.Add(definition);
    }
}

public readonly struct ShortcutGesture : IEquatable<ShortcutGesture>
{
    public ShortcutGesture(string key, bool ctrl = false, bool shift = false, bool alt = false, bool meta = false)
    {
        Key = NormalizeKey(key);
        Ctrl = ctrl;
        Shift = shift;
        Alt = alt;
        Meta = meta;
    }

    public string Key { get; }

    public bool Ctrl { get; }

    public bool Shift { get; }

    public bool Alt { get; }

    public bool Meta { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Key);

    public static ShortcutGesture Empty => new(string.Empty);

    public static ShortcutGesture Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        bool ctrl = false;
        bool shift = false;
        bool alt = false;
        bool meta = false;
        string key = string.Empty;

        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }

            if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (token.Equals("Meta", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Cmd", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Command", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Win", StringComparison.OrdinalIgnoreCase))
            {
                meta = true;
                continue;
            }

            key = token;
        }

        return new ShortcutGesture(key, ctrl, shift, alt, meta);
    }

    public override string ToString()
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var parts = new Collection<string>();
        if (Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Meta)
        {
            parts.Add("Meta");
        }

        parts.Add(Key);
        return string.Join('+', parts);
    }

    public bool Equals(ShortcutGesture other)
    {
        return Ctrl == other.Ctrl
               && Shift == other.Shift
               && Alt == other.Alt
               && Meta == other.Meta
               && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is ShortcutGesture other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Key ?? string.Empty);
        hash = (hash * 397) ^ Ctrl.GetHashCode();
        hash = (hash * 397) ^ Shift.GetHashCode();
        hash = (hash * 397) ^ Alt.GetHashCode();
        hash = (hash * 397) ^ Meta.GetHashCode();
        return hash;
    }

    private static string NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var trimmed = key.Trim();
        if (trimmed.Length == 1)
        {
            return trimmed.ToUpperInvariant();
        }

        if (trimmed.StartsWith("D", true, CultureInfo.InvariantCulture) &&
            trimmed.Length == 2 &&
            char.IsDigit(trimmed[1]))
        {
            return trimmed[1].ToString(CultureInfo.InvariantCulture);
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }
}

public sealed class ShortcutInput
{
    public ShortcutInput(ShortcutGesture gesture, bool isRepeat = false)
    {
        Gesture = gesture;
        IsRepeat = isRepeat;
    }

    public ShortcutGesture Gesture { get; }

    public bool IsRepeat { get; }
}
