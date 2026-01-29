using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Skia3D.Sample.Controls;

public sealed partial class TimelineControl : UserControl
{
    public TimelineControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
