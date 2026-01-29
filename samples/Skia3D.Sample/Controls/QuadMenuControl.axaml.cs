using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Skia3D.Sample.ViewModels;

namespace Skia3D.Sample.Controls;

public sealed partial class QuadMenuControl : UserControl
{
    public QuadMenuControl()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Actions.IsQuadMenuOpen = false;
        }
    }
}
