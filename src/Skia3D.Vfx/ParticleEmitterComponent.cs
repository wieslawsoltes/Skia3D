using System.Numerics;
using Skia3D.Core;
using Skia3D.Scene;
using SkiaSharp;

namespace Skia3D.Vfx;

public sealed class ParticleEmitterComponent : SceneComponent
{
    private readonly List<Particle> _particles = new();
    private float _emitAccumulator;
    private SceneNode? _renderNode;
    private Mesh? _mesh;
    private MeshRenderer? _renderer;

    public int MaxParticles { get; set; } = 256;

    public float EmissionRate { get; set; } = 20f;

    public float Lifetime { get; set; } = 2f;

    public Vector3 BaseVelocity { get; set; } = new(0f, 1f, 0f);

    public Vector3 VelocityRandomness { get; set; } = new(0.5f, 0.5f, 0.5f);

    public float StartSize { get; set; } = 0.2f;

    public float EndSize { get; set; } = 0.05f;

    public SKColor StartColor { get; set; } = new(255, 200, 120, 200);

    public SKColor EndColor { get; set; } = new(255, 80, 20, 0);

    public bool IsEmitting { get; set; } = true;

    public bool WorldSpace { get; set; }

    public MeshRenderer? Renderer => _renderer;

    public IReadOnlyList<Particle> Particles => _particles;

    protected override void OnAttached(SceneNode node)
    {
        if (_renderNode == null)
        {
            _renderNode = new SceneNode($"{node.Name}_Vfx");
            _renderNode.Transform.LocalPosition = Vector3.Zero;
            _renderNode.Transform.LocalRotation = Quaternion.Identity;
            _renderNode.Transform.LocalScale = Vector3.One;
            node.AddChild(_renderNode);
        }

        if (_mesh == null)
        {
            _mesh = new Mesh(Array.Empty<Vertex>(), Array.Empty<int>());
        }

        _renderer ??= new MeshRenderer(_mesh);
        _renderNode!.MeshRenderer = _renderer;
    }

    protected override void OnDetached(SceneNode node)
    {
        if (_renderNode != null)
        {
            node.RemoveChild(_renderNode);
            _renderNode = null;
        }
    }

    internal void UpdateEmitter(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += deltaSeconds;
            if (p.Age >= p.Lifetime)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * deltaSeconds;
            _particles[i] = p;
        }

        if (!IsEmitting || EmissionRate <= 0f || _particles.Count >= MaxParticles)
        {
            return;
        }

        _emitAccumulator += deltaSeconds * EmissionRate;
        int spawnCount = (int)_emitAccumulator;
        if (spawnCount <= 0)
        {
            return;
        }

        _emitAccumulator -= spawnCount;
        for (int i = 0; i < spawnCount && _particles.Count < MaxParticles; i++)
        {
            SpawnParticle();
        }
    }

    internal void BuildBillboards(Camera camera)
    {
        if (_mesh == null)
        {
            return;
        }

        var count = _particles.Count;
        if (count == 0)
        {
            _mesh.UpdateGeometry(Array.Empty<Vertex>(), Array.Empty<int>(), allowRefit: false);
            return;
        }

        var forward = Vector3.Normalize(camera.Target - camera.Position);
        if (forward.LengthSquared() < 1e-8f)
        {
            forward = Vector3.UnitZ;
        }

        var right = Vector3.Cross(forward, camera.Up);
        if (right.LengthSquared() < 1e-8f)
        {
            right = Vector3.UnitX;
        }
        else
        {
            right = Vector3.Normalize(right);
        }

        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        var vertices = new Vertex[count * 4];
        var indices = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            var p = _particles[i];
            float t = p.Lifetime <= 1e-6f ? 1f : Math.Clamp(p.Age / p.Lifetime, 0f, 1f);
            float size = Lerp(StartSize, EndSize, t);
            var color = LerpColor(StartColor, EndColor, t);

            var half = size * 0.5f;
            var r = right * half;
            var u = up * half;

            var pos = p.Position;
            if (!WorldSpace && Node != null)
            {
                pos = Vector3.Transform(pos, Node.Transform.WorldMatrix);
            }
            var p0 = pos - r - u;
            var p1 = pos + r - u;
            var p2 = pos + r + u;
            var p3 = pos - r + u;

            int v = i * 4;
            vertices[v + 0] = new Vertex(p0, forward, color, new Vector2(0f, 1f));
            vertices[v + 1] = new Vertex(p1, forward, color, new Vector2(1f, 1f));
            vertices[v + 2] = new Vertex(p2, forward, color, new Vector2(1f, 0f));
            vertices[v + 3] = new Vertex(p3, forward, color, new Vector2(0f, 0f));

            int idx = i * 6;
            indices[idx + 0] = v + 0;
            indices[idx + 1] = v + 1;
            indices[idx + 2] = v + 2;
            indices[idx + 3] = v + 0;
            indices[idx + 4] = v + 2;
            indices[idx + 5] = v + 3;
        }

        _mesh.UpdateGeometry(vertices, indices, allowRefit: false);
    }

    private void SpawnParticle()
    {
        if (Node == null)
        {
            return;
        }

        var world = Node.Transform.WorldMatrix;
        var position = WorldSpace ? world.Translation : Vector3.Zero;
        var velocity = BaseVelocity + new Vector3(
            RandomRange(-VelocityRandomness.X, VelocityRandomness.X),
            RandomRange(-VelocityRandomness.Y, VelocityRandomness.Y),
            RandomRange(-VelocityRandomness.Z, VelocityRandomness.Z));
        if (WorldSpace)
        {
            velocity = Vector3.TransformNormal(velocity, world);
        }

        var particle = new Particle
        {
            Position = position,
            Velocity = velocity,
            Lifetime = MathF.Max(0.05f, Lifetime),
            Age = 0f
        };

        _particles.Add(particle);
    }

    private static float RandomRange(float min, float max)
    {
        if (min >= max)
        {
            return min;
        }

        return (float)(Random.Shared.NextDouble() * (max - min) + min);
    }

    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        byte Lerp(byte x, byte y) => (byte)Math.Clamp(x + (y - x) * t, 0f, 255f);
        return new SKColor(
            Lerp(a.Red, b.Red),
            Lerp(a.Green, b.Green),
            Lerp(a.Blue, b.Blue),
            Lerp(a.Alpha, b.Alpha));
    }

    public struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public float Lifetime;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
