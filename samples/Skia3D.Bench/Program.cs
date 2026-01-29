using System.Diagnostics;
using System.Numerics;
using Skia3D.Core;
using SkiaSharp;

const int width = 1280;
const int height = 720;
const int warmupFrames = 60;
const int sampleFrames = 240;
const int warmupRays = 2000;
const int sampleRays = 20000;

var camera = new Camera
{
    Position = new Vector3(0f, 2f, 9f),
    Target = new Vector3(0f, 0f, 0f),
    FieldOfView = MathF.PI / 3f,
    NearPlane = 0.1f,
    FarPlane = 100f
};
camera.AspectRatio = width / (float)height;

var scene = BuildScene(24);

using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
var viewport = new SKRect(0, 0, width, height);

RunScenario("Depth x1", useDepth: true, renderScale: 1f);
RunScenario("Depth x0.5", useDepth: true, renderScale: 0.5f);
RunScenario("Painter", useDepth: false, renderScale: 1f);
RunPickScenario("Pick (BVH)", useAcceleration: true, Renderer3D.PickAccelerationMode.Bvh);
RunPickScenario("Pick (Grid)", useAcceleration: true, Renderer3D.PickAccelerationMode.UniformGrid);
RunPickScenario("Pick (Auto)", useAcceleration: true, Renderer3D.PickAccelerationMode.Auto);
RunPickScenario("Pick (Brute)", useAcceleration: false, Renderer3D.PickAccelerationMode.Bvh);

void RunScenario(string name, bool useDepth, float renderScale)
{
    var renderer = new Renderer3D
    {
        UseDepthBuffer = useDepth,
        DepthRenderScale = renderScale,
        CollectStats = true
    };

    long pixels = 0;
    long triangles = 0;
    long workers = 0;
    var stopwatch = new Stopwatch();

    for (int i = 0; i < warmupFrames + sampleFrames; i++)
    {
        UpdateScene(scene, i * 0.015f);
        renderer.Render(surface.Canvas, viewport, camera, scene);
        if (i >= warmupFrames)
        {
            var stats = renderer.LastStats;
            pixels += stats.PixelsWritten;
            triangles += stats.Triangles;
            workers += stats.Workers;
            if (i == warmupFrames)
            {
                stopwatch.Start();
            }
        }
    }

    stopwatch.Stop();
    var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
    var fps = sampleFrames / (elapsedMs / 1000.0);
    var avgPixels = pixels / Math.Max(1, sampleFrames);
    var avgTris = triangles / Math.Max(1, sampleFrames);
    var avgWorkers = workers / Math.Max(1, sampleFrames);

    Console.WriteLine($"{name}: {fps:0.0} fps, {elapsedMs:0} ms, tris {avgTris}, pixels {avgPixels}, workers {avgWorkers}, scale {renderScale:0.00}");
}

static List<MeshInstance> BuildScene(int meshSegments)
{
    var list = new List<MeshInstance>();

    var ground = new MeshInstance(MeshFactory.CreateGrid(meshSegments, 12f, new SKColor(115, 125, 135), twoSided: true))
    {
        Transform = Matrix4x4.CreateTranslation(new Vector3(0f, -1.2f, 0f)),
        Material = new Material { BaseColor = new SKColor(145, 155, 165), Ambient = 0.35f, Diffuse = 0.55f, DoubleSided = true }
    };

    var cube = new MeshInstance(MeshFactory.CreateCube(2.4f, new SKColor(46, 153, 255)))
    {
        Material = new Material { BaseColor = new SKColor(46, 153, 255), Diffuse = 1f, Ambient = 0.2f }
    };

    var pyramid = new MeshInstance(MeshFactory.CreatePyramid(2f, 2.4f, new SKColor(255, 99, 71)))
    {
        Transform = Matrix4x4.CreateTranslation(new Vector3(2.6f, 0f, -2.2f)),
        Material = new Material { BaseColor = new SKColor(255, 140, 120), Diffuse = 1f, Ambient = 0.2f }
    };

    var sphereSlices = Math.Max(8, meshSegments);
    var sphereStacks = Math.Max(6, meshSegments / 2);
    var sphere = new MeshInstance(MeshFactory.CreateSphere(1.3f, sphereSlices, sphereStacks, new SKColor(80, 220, 180)))
    {
        Transform = Matrix4x4.CreateTranslation(new Vector3(-2.8f, 0.2f, 1.8f)),
        Material = new Material { BaseColor = new SKColor(80, 220, 180), Ambient = 0.2f, Diffuse = 0.9f }
    };

    var cylinderSegments = Math.Max(8, meshSegments);
    var cylinder = new MeshInstance(MeshFactory.CreateCylinder(0.9f, 2.5f, cylinderSegments, new SKColor(200, 200, 120)))
    {
        Transform = Matrix4x4.CreateTranslation(new Vector3(0f, 0f, -3f)),
        Material = new Material { BaseColor = new SKColor(220, 220, 140), Ambient = 0.2f, Diffuse = 0.8f }
    };

    list.Add(ground);
    list.Add(cube);
    list.Add(pyramid);
    list.Add(sphere);
    list.Add(cylinder);
    return list;
}

static void UpdateScene(List<MeshInstance> scene, float angle)
{
    if (scene.Count > 1)
    {
        scene[1].Transform = Matrix4x4.CreateRotationY(angle) * Matrix4x4.CreateRotationX(angle * 0.3f);
    }
}

void RunPickScenario(string name, bool useAcceleration, Renderer3D.PickAccelerationMode acceleration)
{
    var pickScene = BuildScene(24);
    var renderer = new Renderer3D
    {
        UseAccelerationStructure = useAcceleration,
        PickAcceleration = acceleration,
        UniformGridCellsPerAxis = 0
    };

    var viewportSize = new Vector2(width, height);
    var rays = BuildRandomRays(sampleRays + warmupRays, viewportSize);
    int hits = 0;

    var stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < rays.Length; i++)
    {
        var ray = rays[i];
        if (renderer.TryRaycastDetailed(ray.origin, ray.direction, pickScene, out _))
        {
            if (i >= warmupRays)
            {
                hits++;
            }
        }
    }
    stopwatch.Stop();

    var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
    var measuredRays = Math.Max(1, sampleRays);
    var nsPerRay = (elapsedMs * 1_000_000.0) / measuredRays;
    Console.WriteLine($"{name}: {nsPerRay:0.0} ns/ray, hits {hits}/{measuredRays}, accel {acceleration}, useAccel {useAcceleration}");
}

(Vector3 origin, Vector3 direction)[] BuildRandomRays(int count, Vector2 viewportSize)
{
    var rays = new (Vector3 origin, Vector3 direction)[count];
    var rng = new Random(1234);

    for (int i = 0; i < count; i++)
    {
        var screen = new Vector2(
            (float)(rng.NextDouble() * viewportSize.X),
            (float)(rng.NextDouble() * viewportSize.Y));

        if (!Camera.TryBuildRay(camera, screen, viewportSize, out var origin, out var direction))
        {
            origin = camera.Position;
            direction = Vector3.Normalize(camera.Target - camera.Position);
        }

        rays[i] = (origin, direction);
    }

    return rays;
}
