using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Skia3D.Sample.Controls;

public sealed partial class WorkspaceManagerControl : UserControl
{
    public WorkspaceManagerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
