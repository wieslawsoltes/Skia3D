using System;
using System.Numerics;
using Skia3D.Audio;
using Skia3D.Core;
using Skia3D.Navigation;
using Skia3D.Physics;
using Skia3D.Runtime;
using Skia3D.Scene;
using Skia3D.Vfx;
using SkiaSharp;

const int width = 320;
const int height = 180;
const int frames = 120;
const float fixedDelta = 1f / 60f;

var scene = new Scene();
var renderer = new Renderer3D
{
    UseDepthBuffer = true
};

var camera = new Camera
{
    Position = new Vector3(0f, 3f, 8f),
    Target = new Vector3(0f, 0.5f, 0f)
};
camera.AspectRatio = width / (float)height;

var (ball, agent) = BuildScene(scene, camera);

var host = new EngineHost(scene, renderer, camera)
{
    EnableCulling = true,
    ParallelCollect = false,
    ParallelUpdate = false,
    UseSceneLights = true
};

var physicsWorld = new PhysicsWorld();
var physicsSystem = new ScenePhysicsSystem(scene, physicsWorld);
var navigationSystem = new NavigationSystem(scene);
var audioSystem = new AudioSystem(scene);
var vfxSystem = new VfxSystem(scene, _ => camera);

host.AddSystem(physicsSystem);
host.AddSystem(navigationSystem);
host.AddSystem(audioSystem);
host.AddSystem(vfxSystem);

host.Initialize();
host.Engine.Clock.FixedDeltaSeconds = fixedDelta;

using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
var viewport = new SKRect(0, 0, width, height);

Console.WriteLine("Skia3D subsystems sample running...");
for (int frame = 0; frame < frames; frame++)
{
    host.Engine.Tick(fixedDelta);
    host.Render(surface.Canvas, viewport);

    if (frame % 30 == 0)
    {
        var ballPos = ball.Transform.LocalPosition;
        var agentPos = agent.Transform.LocalPosition;
        Console.WriteLine($"Frame {frame:000}: ballY={ballPos.Y:0.00}, agent=({agentPos.X:0.00}, {agentPos.Y:0.00}, {agentPos.Z:0.00})");
    }
}

host.Shutdown();

static (SceneNode ball, SceneNode agent) BuildScene(Scene scene, Camera camera)
{
    var ground = new SceneNode("Ground");
    ground.Transform.LocalPosition = new Vector3(0f, -1f, 0f);
    ground.MeshRenderer = new MeshRenderer(
        MeshFactory.CreateGrid(16, 12f, new SKColor(115, 125, 135), twoSided: true),
        new Material
        {
            BaseColor = new SKColor(145, 155, 165),
            Ambient = 0.3f,
            Diffuse = 0.6f,
            DoubleSided = true
        });
    var groundBody = ground.AddComponent(new RigidBodyComponent { BodyType = PhysicsBodyType.Static });
    ground.AddComponent(new ColliderComponent(new BoxShape(new Vector3(12f, 1f, 12f))) { Body = groundBody });
    scene.AddRoot(ground);

    var ball = new SceneNode("Ball");
    ball.Transform.LocalPosition = new Vector3(0f, 1.6f, 0f);
    ball.MeshRenderer = new MeshRenderer(
        MeshFactory.CreateSphere(0.6f, 16, 12, new SKColor(80, 220, 180)),
        new Material
        {
            BaseColor = new SKColor(80, 220, 180),
            Ambient = 0.2f,
            Diffuse = 0.9f
        });
    var ballBody = ball.AddComponent(new RigidBodyComponent { Mass = 1f, Restitution = 0.2f });
    ball.AddComponent(new ColliderComponent(new SphereShape(0.6f)) { Body = ballBody });
    scene.AddRoot(ball);

    ball.AddComponent(new ParticleEmitterComponent
    {
        EmissionRate = 30f,
        Lifetime = 1.2f,
        BaseVelocity = new Vector3(0f, 1.2f, 0f),
        VelocityRandomness = new Vector3(0.4f, 0.6f, 0.4f),
        StartSize = 0.25f,
        EndSize = 0.05f,
        WorldSpace = true
    });

    var source = ball.AddComponent(new AudioSourceComponent
    {
        Clip = new AudioClip("Pulse", 1f),
        Loop = true,
        Volume = 0.6f
    });
    source.Play();

    var listener = new SceneNode("Listener");
    listener.Transform.LocalPosition = camera.Position;
    listener.AddComponent(new AudioListenerComponent { Volume = 1f });
    scene.AddRoot(listener);

    var grid = new NavGrid(12, 12, 1f, new Vector3(-6f, -1f, -6f));
    var gridNode = new SceneNode("NavGrid");
    gridNode.AddComponent(new NavGridComponent(grid));
    scene.AddRoot(gridNode);

    var agent = new SceneNode("Agent");
    agent.Transform.LocalPosition = new Vector3(-4f, -1f, -4f);
    agent.MeshRenderer = new MeshRenderer(
        MeshFactory.CreateCube(0.4f, new SKColor(255, 180, 80)),
        new Material
        {
            BaseColor = new SKColor(255, 180, 80),
            Ambient = 0.2f,
            Diffuse = 0.9f
        });
    var navAgent = agent.AddComponent(new NavAgentComponent { Speed = 1.6f, StoppingDistance = 0.2f });
    navAgent.SetDestination(new Vector3(4f, -1f, 4f));
    scene.AddRoot(agent);

    return (ball, agent);
}
