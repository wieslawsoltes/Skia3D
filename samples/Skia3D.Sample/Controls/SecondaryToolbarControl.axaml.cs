using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Skia3D.Sample.Controls;

public sealed partial class SecondaryToolbarControl : UserControl
{
    public SecondaryToolbarControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
