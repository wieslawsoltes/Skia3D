using Skia3D.Modeling;

namespace Skia3D.Editor;

public sealed class EditorMeshEditOptions
{
    public int SmoothIterations { get; set; } = 2;

    public float SmoothStrength { get; set; } = 0.35f;

    public float SimplifyRatio { get; set; } = 0.5f;

    public float UvScale { get; set; } = 1f;

    public float UvUnwrapAngle { get; set; } = 45f;

    public UvUnwrapMethod UvUnwrapMethod { get; set; } = UvUnwrapMethod.Lscm;

    public float UvPackPadding { get; set; } = 0.02f;

    public bool UvPackRotate { get; set; } = true;

    public bool UvPackPreserveTexelDensity { get; set; } = true;

    public float UvPackTexelDensity { get; set; } = 1f;

    public bool UvPackUseGroups { get; set; } = true;

    public bool ProportionalEnabled { get; set; }

    public float ProportionalRadius { get; set; } = 2f;

    public ProportionalFalloff ProportionalFalloff { get; set; } = ProportionalFalloff.Smooth;
}
