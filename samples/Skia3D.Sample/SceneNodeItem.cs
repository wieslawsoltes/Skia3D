using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Skia3D.Scene;

namespace Skia3D.Sample;

public sealed class SceneNodeItem : INotifyPropertyChanged
{
    private string _name;

    public SceneNodeItem(SceneNode node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _name = node.Name;
        Children = new ObservableCollection<SceneNodeItem>();
        IsExpanded = true;
    }

    public SceneNode Node { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            _name = value;
            Node.Name = value;
            RaisePropertyChanged();
        }
    }

    public ObservableCollection<SceneNodeItem> Children { get; }

    public bool IsExpanded { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
