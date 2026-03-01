using System.Runtime.CompilerServices;
using ReactiveUI;

namespace Skia3D.Editor;

public abstract class ViewModelBase : ReactiveObject
{
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        IReactiveObjectExtensions.RaisePropertyChanged(this, propertyName);
    }
}
