using System.Numerics;
using Skia3D.Scene;

namespace Skia3D.Physics;

public enum PhysicsBodyType
{
    Static,
    Dynamic,
    Kinematic
}

public sealed class RigidBodyComponent : SceneComponent
{
    private float _mass = 1f;
    private float _inverseMass = 1f;
    private PhysicsBodyType _bodyType = PhysicsBodyType.Dynamic;

    public PhysicsBodyType BodyType
    {
        get => _bodyType;
        set
        {
            _bodyType = value;
            if (_bodyType != PhysicsBodyType.Dynamic)
            {
                Velocity = Vector3.Zero;
                AngularVelocity = Vector3.Zero;
            }

            UpdateInverseMass();
        }
    }

    public float Mass
    {
        get => _mass;
        set
        {
            _mass = MathF.Max(0.0001f, value);
            UpdateInverseMass();
        }
    }

    public float InverseMass => _bodyType == PhysicsBodyType.Dynamic ? _inverseMass : 0f;

    public Vector3 Velocity { get; set; }

    public Vector3 AngularVelocity { get; set; }

    public Vector3 AccumulatedForce { get; private set; }

    public bool UseGravity { get; set; } = true;

    public float Restitution { get; set; } = 0.1f;

    public float Friction { get; set; } = 0.6f;

    public float LinearDamping { get; set; } = 0.01f;

    public float AngularDamping { get; set; } = 0.01f;

    public bool IsAwake { get; set; } = true;

    public void ApplyForce(Vector3 force)
    {
        if (_bodyType != PhysicsBodyType.Dynamic)
        {
            return;
        }

        AccumulatedForce += force;
    }

    public void ApplyImpulse(Vector3 impulse)
    {
        if (_bodyType != PhysicsBodyType.Dynamic)
        {
            return;
        }

        Velocity += impulse * _inverseMass;
    }

    public void ClearForces()
    {
        AccumulatedForce = Vector3.Zero;
    }

    internal void UpdateInverseMass()
    {
        _inverseMass = _mass <= 0f ? 0f : 1f / _mass;
    }
}
