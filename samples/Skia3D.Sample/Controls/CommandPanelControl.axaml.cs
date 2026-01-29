using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Skia3D.Sample.Controls;

public sealed partial class CommandPanelControl : UserControl
{
    public CommandPanelControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
