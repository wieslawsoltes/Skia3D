using System.Numerics;
using Skia3D.Geometry;
using Skia3D.Runtime;
using SceneGraph = Skia3D.Scene.Scene;

namespace Skia3D.Physics;

public sealed class PhysicsWorld : IPhysicsWorld
{
    private readonly List<RigidBodyComponent> _bodies = new();
    private readonly List<ColliderComponent> _colliders = new();
    private readonly List<PhysicsConstraint> _constraints = new();
    private readonly List<BodyState> _bodyStates = new();
    private readonly Dictionary<RigidBodyComponent, int> _bodyIndex = new();
    private readonly List<ColliderProxy> _proxies = new();
    private readonly List<(int a, int b)> _pairs = new();
    private readonly List<Contact> _contacts = new();

    public Vector3 Gravity { get; set; } = new(0f, -9.81f, 0f);

    public float BroadphaseCellSize { get; set; } = 2f;

    public float PenetrationSlop { get; set; } = 0.005f;

    public float CorrectionFactor { get; set; } = 0.8f;

    public int SolverIterations { get; set; } = 1;

    public IReadOnlyList<RigidBodyComponent> Bodies => _bodies;

    public IReadOnlyList<ColliderComponent> Colliders => _colliders;

    public IReadOnlyList<PhysicsConstraint> Constraints => _constraints;

    public void AddConstraint(PhysicsConstraint constraint)
    {
        if (constraint is null)
        {
            throw new ArgumentNullException(nameof(constraint));
        }

        if (!_constraints.Contains(constraint))
        {
            _constraints.Add(constraint);
        }
    }

    public bool RemoveConstraint(PhysicsConstraint constraint) => _constraints.Remove(constraint);

    public void SyncFromScene(SceneGraph scene)
    {
        if (scene is null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        _bodies.Clear();
        _colliders.Clear();

        var bodies = scene.CollectComponents<RigidBodyComponent>();
        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            body.UpdateInverseMass();
            _bodies.Add(body);
        }

        var colliders = scene.CollectComponents<ColliderComponent>();
        for (int i = 0; i < colliders.Count; i++)
        {
            var collider = colliders[i];
            if (collider.Body == null)
            {
                collider.Body = collider.Node?.GetComponent<RigidBodyComponent>();
            }

            _colliders.Add(collider);
        }
    }

    public void Step(float fixedDeltaSeconds)
    {
        if (fixedDeltaSeconds <= 0f)
        {
            return;
        }

        BuildBodyStates();
        IntegrateBodies(fixedDeltaSeconds);
        BuildColliderProxies();
        BuildBroadphasePairs();
        BuildContacts();

        for (int i = 0; i < SolverIterations; i++)
        {
            SolveConstraints(fixedDeltaSeconds);
            ResolveContacts(fixedDeltaSeconds);
        }

        ApplyStates();
        ClearForces();
    }

    public bool Raycast(Ray ray, float maxDistance, out RaycastHit hit)
    {
        hit = default;
        if (maxDistance <= 0f)
        {
            return false;
        }

        float best = maxDistance;
        bool hitAny = false;
        for (int i = 0; i < _colliders.Count; i++)
        {
            var collider = _colliders[i];
            if (collider.Node == null || collider.Shape == null)
            {
                continue;
            }

            if (!TryGetColliderWorld(collider, out var world))
            {
                continue;
            }

            if (TryRaycastShape(collider.Shape, world, ray, best, out var t, out var normal))
            {
                best = t;
                hit = new RaycastHit(collider, collider.Body, ray.GetPoint(t), normal, t);
                hitAny = true;
            }
        }

        return hitAny;
    }

    public IReadOnlyList<ColliderComponent> OverlapSphere(Vector3 center, float radius)
    {
        var results = new List<ColliderComponent>();
        var sphere = new SphereShape(radius) { Offset = center };
        for (int i = 0; i < _colliders.Count; i++)
        {
            var collider = _colliders[i];
            if (collider.Node == null || collider.Shape == null)
            {
                continue;
            }

            if (!TryGetColliderWorld(collider, out var world))
            {
                continue;
            }

            if (TryOverlap(sphere, Matrix4x4.Identity, collider.Shape, world))
            {
                results.Add(collider);
            }
        }

        return results;
    }

    public bool SweepSphere(Vector3 center, float radius, Vector3 direction, float maxDistance, out SweepHit hit)
    {
        hit = default;
        if (maxDistance <= 0f)
        {
            return false;
        }

        var dir = direction;
        if (dir.LengthSquared() <= 1e-8f)
        {
            return false;
        }

        dir = Vector3.Normalize(dir);
        var ray = new Ray(center, dir);

        float best = maxDistance;
        bool hitAny = false;
        for (int i = 0; i < _colliders.Count; i++)
        {
            var collider = _colliders[i];
            if (collider.Node == null || collider.Shape == null)
            {
                continue;
            }

            if (!TryGetColliderWorld(collider, out var world))
            {
                continue;
            }

            if (TrySweepSphere(collider.Shape, world, ray, radius, best, out var t, out var normal))
            {
                best = t;
                hit = new SweepHit(collider, collider.Body, ray.GetPoint(t), normal, t);
                hitAny = true;
            }
        }

        return hitAny;
    }

    internal bool TryGetBodyState(RigidBodyComponent body, out BodyState state, out int index)
    {
        if (_bodyIndex.TryGetValue(body, out index))
        {
            state = _bodyStates[index];
            return true;
        }

        state = default;
        index = -1;
        return false;
    }

    internal void SetBodyState(int index, BodyState state)
    {
        if ((uint)index < (uint)_bodyStates.Count)
        {
            _bodyStates[index] = state;
        }
    }

    private void BuildBodyStates()
    {
        _bodyStates.Clear();
        _bodyIndex.Clear();

        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            if (body.Node == null)
            {
                continue;
            }

            var world = body.Node.Transform.WorldMatrix;
            if (!Matrix4x4.Decompose(world, out var scale, out var rotation, out var translation))
            {
                continue;
            }

            var parentWorld = body.Node.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
            var state = new BodyState(body, translation, rotation, scale, parentWorld);
            _bodyIndex[body] = _bodyStates.Count;
            _bodyStates.Add(state);
        }
    }

    private void IntegrateBodies(float deltaSeconds)
    {
        for (int i = 0; i < _bodyStates.Count; i++)
        {
            var state = _bodyStates[i];
            var body = state.Body;
            if (body.BodyType != PhysicsBodyType.Dynamic || !body.IsAwake)
            {
                continue;
            }

            var acceleration = body.AccumulatedForce * body.InverseMass;
            if (body.UseGravity)
            {
                acceleration += Gravity;
            }

            body.Velocity += acceleration * deltaSeconds;
            body.Velocity *= 1f / (1f + body.LinearDamping * deltaSeconds);
            state.Position += body.Velocity * deltaSeconds;

            if (body.AngularVelocity.LengthSquared() > 1e-8f)
            {
                var axis = body.AngularVelocity;
                float angle = axis.Length() * deltaSeconds;
                axis = Vector3.Normalize(axis);
                var dq = Quaternion.CreateFromAxisAngle(axis, angle);
                state.Rotation = Quaternion.Normalize(dq * state.Rotation);
                body.AngularVelocity *= 1f / (1f + body.AngularDamping * deltaSeconds);
            }

            _bodyStates[i] = state;
        }
    }

    private void BuildColliderProxies()
    {
        _proxies.Clear();
        for (int i = 0; i < _colliders.Count; i++)
        {
            var collider = _colliders[i];
            if (collider.Node == null || collider.Shape == null)
            {
                continue;
            }

            var body = collider.Body;
            Matrix4x4 world;
            if (body != null && TryGetBodyState(body, out var state, out _))
            {
                world = PhysicsMath.ComposeWorld(state.Position, state.Rotation, state.Scale);
            }
            else
            {
                world = collider.Node.Transform.WorldMatrix;
            }

            var bounds = collider.Shape.ComputeAabb(world);
            _proxies.Add(new ColliderProxy(collider, body, bounds, world));
        }
    }

    private void BuildBroadphasePairs()
    {
        _pairs.Clear();
        if (_proxies.Count < 2)
        {
            return;
        }

        if (BroadphaseCellSize <= 0f)
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                for (int j = i + 1; j < _proxies.Count; j++)
                {
                    if (AabbIntersects(_proxies[i].Bounds, _proxies[j].Bounds))
                    {
                        _pairs.Add((i, j));
                    }
                }
            }

            return;
        }

        var cells = new Dictionary<(int, int, int), List<int>>();
        float inv = 1f / BroadphaseCellSize;

        for (int i = 0; i < _proxies.Count; i++)
        {
            var bounds = _proxies[i].Bounds;
            int minX = (int)MathF.Floor(bounds.Min.X * inv);
            int minY = (int)MathF.Floor(bounds.Min.Y * inv);
            int minZ = (int)MathF.Floor(bounds.Min.Z * inv);
            int maxX = (int)MathF.Floor(bounds.Max.X * inv);
            int maxY = (int)MathF.Floor(bounds.Max.Y * inv);
            int maxZ = (int)MathF.Floor(bounds.Max.Z * inv);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        var key = (x, y, z);
                        if (!cells.TryGetValue(key, out var list))
                        {
                            list = new List<int>();
                            cells[key] = list;
                        }

                        list.Add(i);
                    }
                }
            }
        }

        var pairSet = new HashSet<long>();
        foreach (var cell in cells.Values)
        {
            for (int i = 0; i < cell.Count; i++)
            {
                for (int j = i + 1; j < cell.Count; j++)
                {
                    int a = cell[i];
                    int b = cell[j];
                    if (a == b)
                    {
                        continue;
                    }

                    if (a > b)
                    {
                        (a, b) = (b, a);
                    }

                    long key = ((long)a << 32) | (uint)b;
                    if (!pairSet.Add(key))
                    {
                        continue;
                    }

                    if (AabbIntersects(_proxies[a].Bounds, _proxies[b].Bounds))
                    {
                        _pairs.Add((a, b));
                    }
                }
            }
        }
    }

    private void BuildContacts()
    {
        _contacts.Clear();
        for (int i = 0; i < _pairs.Count; i++)
        {
            var pair = _pairs[i];
            var a = _proxies[pair.a];
            var b = _proxies[pair.b];
            if (a.Body != null && a.Body == b.Body)
            {
                continue;
            }

            if (TryGetContact(a, b, out var contact))
            {
                _contacts.Add(contact);
            }
        }
    }

    private void SolveConstraints(float deltaSeconds)
    {
        for (int i = 0; i < _constraints.Count; i++)
        {
            _constraints[i].Solve(this, deltaSeconds);
        }
    }

    private void ResolveContacts(float deltaSeconds)
    {
        for (int i = 0; i < _contacts.Count; i++)
        {
            ResolveContact(_contacts[i], deltaSeconds);
        }
    }

    private void ResolveContact(Contact contact, float deltaSeconds)
    {
        if (contact.ColliderA.IsTrigger || contact.ColliderB.IsTrigger)
        {
            return;
        }

        var bodyA = contact.BodyA;
        var bodyB = contact.BodyB;

        float invMassA = bodyA?.InverseMass ?? 0f;
        float invMassB = bodyB?.InverseMass ?? 0f;
        float invMassSum = invMassA + invMassB;
        if (invMassSum <= 0f)
        {
            return;
        }

        var velA = bodyA?.Velocity ?? Vector3.Zero;
        var velB = bodyB?.Velocity ?? Vector3.Zero;
        var rv = velB - velA;
        float velAlongNormal = Vector3.Dot(rv, contact.Normal);
        if (velAlongNormal > 0f)
        {
            return;
        }

        float restitution = MathF.Min(bodyA?.Restitution ?? 0f, bodyB?.Restitution ?? 0f);
        float j = -(1f + restitution) * velAlongNormal;
        j /= invMassSum;

        var impulse = contact.Normal * j;
        if (bodyA != null && bodyA.BodyType == PhysicsBodyType.Dynamic)
        {
            bodyA.Velocity -= impulse * invMassA;
        }
        if (bodyB != null && bodyB.BodyType == PhysicsBodyType.Dynamic)
        {
            bodyB.Velocity += impulse * invMassB;
        }

        var tangent = rv - contact.Normal * velAlongNormal;
        if (tangent.LengthSquared() > 1e-8f)
        {
            tangent = Vector3.Normalize(tangent);
            float jt = -Vector3.Dot(rv, tangent);
            jt /= invMassSum;

            float friction = MathF.Min(bodyA?.Friction ?? 0f, bodyB?.Friction ?? 0f);
            Vector3 frictionImpulse = MathF.Abs(jt) < j * friction
                ? tangent * jt
                : tangent * (-j * friction);

            if (bodyA != null && bodyA.BodyType == PhysicsBodyType.Dynamic)
            {
                bodyA.Velocity -= frictionImpulse * invMassA;
            }
            if (bodyB != null && bodyB.BodyType == PhysicsBodyType.Dynamic)
            {
                bodyB.Velocity += frictionImpulse * invMassB;
            }
        }

        float correctionMagnitude = MathF.Max(contact.Penetration - PenetrationSlop, 0f) * CorrectionFactor;
        var correction = contact.Normal * (correctionMagnitude / invMassSum);

        if (bodyA != null && bodyA.BodyType == PhysicsBodyType.Dynamic && TryGetBodyState(bodyA, out var stateA, out var ia))
        {
            stateA.Position -= correction * invMassA;
            SetBodyState(ia, stateA);
        }

        if (bodyB != null && bodyB.BodyType == PhysicsBodyType.Dynamic && TryGetBodyState(bodyB, out var stateB, out var ib))
        {
            stateB.Position += correction * invMassB;
            SetBodyState(ib, stateB);
        }
    }

    private void ApplyStates()
    {
        for (int i = 0; i < _bodyStates.Count; i++)
        {
            var state = _bodyStates[i];
            var body = state.Body;
            if (body.BodyType != PhysicsBodyType.Dynamic || body.Node == null)
            {
                continue;
            }

            var world = PhysicsMath.ComposeWorld(state.Position, state.Rotation, state.Scale);
            body.Node.Transform.SetLocalFromWorld(world, state.ParentWorld);
        }
    }

    private void ClearForces()
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            _bodies[i].ClearForces();
        }
    }

    private bool TryGetColliderWorld(ColliderComponent collider, out Matrix4x4 world)
    {
        var body = collider.Body;
        if (body != null && TryGetBodyState(body, out var state, out _))
        {
            world = PhysicsMath.ComposeWorld(state.Position, state.Rotation, state.Scale);
            return true;
        }

        if (collider.Node == null)
        {
            world = Matrix4x4.Identity;
            return false;
        }

        world = collider.Node.Transform.WorldMatrix;
        return true;
    }

    private static bool AabbIntersects(Aabb a, Aabb b)
    {
        return a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
               a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
               a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
    }

    private static bool TryGetContact(ColliderProxy a, ColliderProxy b, out Contact contact)
    {
        contact = default;

        var shapeA = a.Collider.Shape;
        var shapeB = b.Collider.Shape;

        if (shapeA is SphereShape sphereA && shapeB is SphereShape sphereB)
        {
            sphereA.GetWorldSphere(a.World, out var centerA, out var radiusA);
            sphereB.GetWorldSphere(b.World, out var centerB, out var radiusB);
            if (TrySphereSphere(centerA, radiusA, centerB, radiusB, out var normal, out var penetration, out var point))
            {
                contact = new Contact(a.Collider, b.Collider, a.Body, b.Body, normal, penetration, point);
                return true;
            }

            return false;
        }

        if (shapeA is SphereShape sphere && shapeB is BoxShape box)
        {
            sphere.GetWorldSphere(a.World, out var center, out var radius);
            var bounds = box.ComputeAabb(b.World);
            if (TrySphereAabb(center, radius, bounds, out var normal, out var penetration, out var point))
            {
                contact = new Contact(a.Collider, b.Collider, a.Body, b.Body, normal, penetration, point);
                return true;
            }

            return false;
        }

        if (shapeA is BoxShape boxA && shapeB is SphereShape sphereB2)
        {
            sphereB2.GetWorldSphere(b.World, out var center, out var radius);
            var bounds = boxA.ComputeAabb(a.World);
            if (TrySphereAabb(center, radius, bounds, out var normal, out var penetration, out var point))
            {
                contact = new Contact(a.Collider, b.Collider, a.Body, b.Body, -normal, penetration, point);
                return true;
            }

            return false;
        }

        if (shapeA is BoxShape boxA2 && shapeB is BoxShape boxB2)
        {
            var boundsA = boxA2.ComputeAabb(a.World);
            var boundsB = boxB2.ComputeAabb(b.World);
            if (TryAabbAabb(boundsA, boundsB, out var normal, out var penetration, out var point))
            {
                contact = new Contact(a.Collider, b.Collider, a.Body, b.Body, normal, penetration, point);
                return true;
            }
        }

        return false;
    }

    private static bool TrySphereSphere(Vector3 centerA, float radiusA, Vector3 centerB, float radiusB, out Vector3 normal, out float penetration, out Vector3 point)
    {
        normal = Vector3.UnitY;
        penetration = 0f;
        point = Vector3.Zero;

        var delta = centerB - centerA;
        float distSq = delta.LengthSquared();
        float radiusSum = radiusA + radiusB;
        if (distSq > radiusSum * radiusSum)
        {
            return false;
        }

        float dist = MathF.Sqrt(distSq);
        if (dist > 1e-6f)
        {
            normal = delta / dist;
        }

        penetration = radiusSum - dist;
        point = centerA + normal * (radiusA - penetration * 0.5f);
        return true;
    }

    private static bool TrySphereAabb(Vector3 center, float radius, Aabb bounds, out Vector3 normal, out float penetration, out Vector3 point)
    {
        normal = Vector3.UnitY;
        penetration = 0f;
        point = Vector3.Zero;

        var closest = Vector3.Clamp(center, bounds.Min, bounds.Max);
        var delta = center - closest;
        float distSq = delta.LengthSquared();
        if (distSq > radius * radius)
        {
            return false;
        }

        float dist = MathF.Sqrt(distSq);
        if (dist > 1e-6f)
        {
            normal = delta / dist;
            penetration = radius - dist;
            point = closest;
            return true;
        }

        var extents = bounds.Size * 0.5f;
        var boxCenter = bounds.Center;
        var local = center - boxCenter;
        var abs = new Vector3(MathF.Abs(local.X), MathF.Abs(local.Y), MathF.Abs(local.Z));
        float dx = extents.X - abs.X;
        float dy = extents.Y - abs.Y;
        float dz = extents.Z - abs.Z;

        if (dx <= dy && dx <= dz)
        {
            normal = new Vector3(MathF.Sign(local.X), 0f, 0f);
            penetration = radius + dx;
        }
        else if (dy <= dz)
        {
            normal = new Vector3(0f, MathF.Sign(local.Y), 0f);
            penetration = radius + dy;
        }
        else
        {
            normal = new Vector3(0f, 0f, MathF.Sign(local.Z));
            penetration = radius + dz;
        }

        point = center - normal * radius;
        return true;
    }

    private static bool TryAabbAabb(Aabb a, Aabb b, out Vector3 normal, out float penetration, out Vector3 point)
    {
        normal = Vector3.UnitY;
        penetration = 0f;
        point = Vector3.Zero;

        if (!AabbIntersects(a, b))
        {
            return false;
        }

        float overlapX = MathF.Min(a.Max.X, b.Max.X) - MathF.Max(a.Min.X, b.Min.X);
        float overlapY = MathF.Min(a.Max.Y, b.Max.Y) - MathF.Max(a.Min.Y, b.Min.Y);
        float overlapZ = MathF.Min(a.Max.Z, b.Max.Z) - MathF.Max(a.Min.Z, b.Min.Z);

        penetration = overlapX;
        normal = new Vector3(a.Center.X < b.Center.X ? -1f : 1f, 0f, 0f);

        if (overlapY < penetration)
        {
            penetration = overlapY;
            normal = new Vector3(0f, a.Center.Y < b.Center.Y ? -1f : 1f, 0f);
        }

        if (overlapZ < penetration)
        {
            penetration = overlapZ;
            normal = new Vector3(0f, 0f, a.Center.Z < b.Center.Z ? -1f : 1f);
        }

        point = (a.Center + b.Center) * 0.5f;
        return true;
    }

    private static bool TryOverlap(ColliderShape a, Matrix4x4 worldA, ColliderShape b, Matrix4x4 worldB)
    {
        if (a is SphereShape sphereA && b is SphereShape sphereB)
        {
            sphereA.GetWorldSphere(worldA, out var centerA, out var radiusA);
            sphereB.GetWorldSphere(worldB, out var centerB, out var radiusB);
            float distanceSq = Vector3.DistanceSquared(centerA, centerB);
            float radiusSum = radiusA + radiusB;
            return distanceSq <= radiusSum * radiusSum;
        }

        if (a is SphereShape sphere && b is BoxShape box)
        {
            sphere.GetWorldSphere(worldA, out var center, out var radius);
            var bounds = box.ComputeAabb(worldB);
            var closest = Vector3.Clamp(center, bounds.Min, bounds.Max);
            return Vector3.DistanceSquared(center, closest) <= radius * radius;
        }

        if (a is BoxShape boxA && b is SphereShape sphereB2)
        {
            sphereB2.GetWorldSphere(worldB, out var center, out var radius);
            var bounds = boxA.ComputeAabb(worldA);
            var closest = Vector3.Clamp(center, bounds.Min, bounds.Max);
            return Vector3.DistanceSquared(center, closest) <= radius * radius;
        }

        if (a is BoxShape box1 && b is BoxShape box2)
        {
            return AabbIntersects(box1.ComputeAabb(worldA), box2.ComputeAabb(worldB));
        }

        return false;
    }

    private static bool TryRaycastShape(ColliderShape shape, Matrix4x4 world, Ray ray, float maxDistance, out float t, out Vector3 normal)
    {
        t = 0f;
        normal = Vector3.Zero;

        if (shape is SphereShape sphere)
        {
            sphere.GetWorldSphere(world, out var center, out var radius);
            return PhysicsMath.RaycastSphere(ray, center, radius, maxDistance, out t, out normal);
        }

        if (shape is BoxShape box)
        {
            var bounds = box.ComputeAabb(world);
            return PhysicsMath.RaycastAabb(ray, bounds, maxDistance, out t, out normal);
        }

        return false;
    }

    private static bool TrySweepSphere(ColliderShape shape, Matrix4x4 world, Ray ray, float radius, float maxDistance, out float t, out Vector3 normal)
    {
        t = 0f;
        normal = Vector3.Zero;

        if (shape is SphereShape sphere)
        {
            sphere.GetWorldSphere(world, out var center, out var targetRadius);
            return PhysicsMath.RaycastSphere(ray, center, radius + targetRadius, maxDistance, out t, out normal);
        }

        if (shape is BoxShape box)
        {
            var bounds = box.ComputeAabb(world);
            var expanded = new Aabb(bounds.Min - new Vector3(radius), bounds.Max + new Vector3(radius));
            return PhysicsMath.RaycastAabb(ray, expanded, maxDistance, out t, out normal);
        }

        return false;
    }

    private readonly record struct ColliderProxy(ColliderComponent Collider, RigidBodyComponent? Body, Aabb Bounds, Matrix4x4 World);

    internal struct BodyState
    {
        public BodyState(RigidBodyComponent body, Vector3 position, Quaternion rotation, Vector3 scale, Matrix4x4 parentWorld)
        {
            Body = body;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            ParentWorld = parentWorld;
        }

        public RigidBodyComponent Body;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Matrix4x4 ParentWorld;
    }

    private readonly record struct Contact(
        ColliderComponent ColliderA,
        ColliderComponent ColliderB,
        RigidBodyComponent? BodyA,
        RigidBodyComponent? BodyB,
        Vector3 Normal,
        float Penetration,
        Vector3 Point);
}

public readonly record struct RaycastHit(ColliderComponent Collider, RigidBodyComponent? Body, Vector3 Point, Vector3 Normal, float Distance);

public readonly record struct SweepHit(ColliderComponent Collider, RigidBodyComponent? Body, Vector3 Point, Vector3 Normal, float Distance);
