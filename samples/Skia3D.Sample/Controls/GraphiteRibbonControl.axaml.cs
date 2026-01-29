using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Skia3D.Sample.Controls;

public sealed partial class GraphiteRibbonControl : UserControl
{
    public GraphiteRibbonControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
