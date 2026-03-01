namespace Skia3D.Sample.Services;

public interface IDockLayoutService
{
    string? CaptureLayout();

    void RestoreLayout(string? layout);

    void ResetLayout();
}
