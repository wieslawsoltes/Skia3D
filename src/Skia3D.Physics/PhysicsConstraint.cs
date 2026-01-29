using System.Numerics;

namespace Skia3D.Physics;

public abstract class PhysicsConstraint
{
    public bool Enabled { get; set; } = true;

    internal abstract void Solve(PhysicsWorld world, float deltaSeconds);
}

public sealed class DistanceConstraint : PhysicsConstraint
{
    public DistanceConstraint(RigidBodyComponent bodyA, RigidBodyComponent bodyB, float targetDistance)
    {
        BodyA = bodyA ?? throw new ArgumentNullException(nameof(bodyA));
        BodyB = bodyB ?? throw new ArgumentNullException(nameof(bodyB));
        TargetDistance = MathF.Max(0f, targetDistance);
    }

    public RigidBodyComponent BodyA { get; }

    public RigidBodyComponent BodyB { get; }

    public float TargetDistance { get; set; }

    public float Stiffness { get; set; } = 0.8f;

    internal override void Solve(PhysicsWorld world, float deltaSeconds)
    {
        if (!Enabled)
        {
            return;
        }

        if (!world.TryGetBodyState(BodyA, out var a, out var ia) ||
            !world.TryGetBodyState(BodyB, out var b, out var ib))
        {
            return;
        }

        float invMassA = BodyA.InverseMass;
        float invMassB = BodyB.InverseMass;
        float invMassSum = invMassA + invMassB;
        if (invMassSum <= 0f)
        {
            return;
        }

        var delta = b.Position - a.Position;
        float dist = delta.Length();
        if (dist <= 1e-6f)
        {
            return;
        }

        float error = dist - TargetDistance;
        var dir = delta / dist;
        var correction = dir * (error * Stiffness);

        a.Position += correction * (invMassA / invMassSum);
        b.Position -= correction * (invMassB / invMassSum);

        world.SetBodyState(ia, a);
        world.SetBodyState(ib, b);
    }
}
