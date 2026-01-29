using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class StatusBarViewModel : ViewModelBase
{
    private string _helpText = "Ready";
    private string _selectionText = "Sel: none";
    private string _fpsText = "FPS: --";
    private string _renderText = "Render: stats off";

    public string HelpText
    {
        get => _helpText;
        set
        {
            if (_helpText == value)
            {
                return;
            }

            _helpText = value;
            RaisePropertyChanged();
        }
    }

    public string SelectionText
    {
        get => _selectionText;
        set
        {
            if (_selectionText == value)
            {
                return;
            }

            _selectionText = value;
            RaisePropertyChanged();
        }
    }

    public string FpsText
    {
        get => _fpsText;
        set
        {
            if (_fpsText == value)
            {
                return;
            }

            _fpsText = value;
            RaisePropertyChanged();
        }
    }

    public string RenderText
    {
        get => _renderText;
        set
        {
            if (_renderText == value)
            {
                return;
            }

            _renderText = value;
            RaisePropertyChanged();
        }
    }
}
