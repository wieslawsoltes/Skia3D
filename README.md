# Skia3D

Skia3D is a small 3D-style rendering layer built on top of SkiaSharp matrices and drawing primitives. It projects 3D meshes to 2D surfaces, offers depth-buffered rasterization, optional painter-style rendering, lighting hooks, and an orbiting camera for pseudo-3D scenes. An Avalonia sample demonstrates interaction and rendering on macOS, Windows, and Linux.

## Projects

- `src/Skia3D.Core` — Core rendering API (camera, mesh primitives, materials/lighting, renderer with z-buffer). Target: .NET 8.
- `src/Skia3D.Geometry` — Geometry kernel (SoA mesh data, SIMD/parallel bounds + radius kernels) for modeling features.
- `src/Skia3D.Acceleration` — BVH acceleration structures for ray/pick traversal.
- `src/Skia3D.Rendering` — Render pipeline abstractions (pass-based pipelines, future backends).
- `src/Skia3D.Runtime` — Engine host, system scheduler, frame clock + time stepping.
- `src/Skia3D.Input` — Input abstraction (`IInputProvider`, `InputState`, `ActionMap`).
- `src/Skia3D.Scene` — Scene graph, transform hierarchy, frustum culling, and component containers.
- `src/Skia3D.Animation` — Animation clips/tracks with scene graph bindings.
- `src/Skia3D.Modeling` — Editing core (selection sets, transform stack, undo/redo, adjacency helpers, extrude/bevel/split ops).
- `src/Skia3D.Editor` — Editor session, selection/gizmo tooling, and modeling workflows.
- `src/Skia3D.ShaderGraph` — Material graph evaluation and parameter blocks.
- `src/Skia3D.Assets` — Asset registry, cache, and loaders.
- `src/Skia3D.IO` — Import pipeline (OBJ + PLY ASCII/binary + glTF core mesh import, mesh processing helpers).
- `src/Skia3D.Physics` — Physics world, rigid bodies, colliders, constraints, and queries.
- `src/Skia3D.Audio` — Audio components + backend abstraction (null backend by default).
- `src/Skia3D.Vfx` — Particle emitters and VFX system.
- `src/Skia3D.Navigation` — Grid navigation and pathfinding.
- `samples/Skia3D.Sample` — Avalonia app showcasing a cube, pyramid, plane (and easily extendable to sphere/cylinder/grid) with orbit controls.
- `samples/Skia3D.Subsystems` — Console sample wiring physics, audio, VFX, and navigation into the runtime loop.
- `samples/Skia3D.Bench` — Console micro-benchmark to compare depth vs painter rendering and collect render stats.

## Getting started

1. Ensure the .NET 8 SDK is installed.
2. Restore and build:

   ```bash
   dotnet build
   ```

3. Run the samples:

   ```bash
   dotnet run --project samples/Skia3D.Sample
   ```

   ```bash
   dotnet run --project samples/Skia3D.Subsystems
   ```

4. Run the benchmark:

   ```bash
   dotnet run --project samples/Skia3D.Bench
   ```

5. Run the tests:

   ```bash
   dotnet test tests/Skia3D.Runtime.Tests
   dotnet test tests/Skia3D.Core.Tests
   ```

## Runtime loop (optional)

Use the runtime host to drive update + render scheduling:

```csharp
using Skia3D.Core;
using Skia3D.Runtime;
using Skia3D.Scene;
using SkiaSharp;

var scene = new Scene();
var renderer = new Renderer3D();
var camera = new Camera();

var host = new EngineHost(scene, renderer, camera);
host.Initialize();

// In your render callback:
SKCanvas canvas = /* ... */;
SKRect viewport = /* ... */;
host.Tick();
host.Render(canvas, viewport);
```

## Controls (sample app)

- Drag with primary pointer: orbit the camera.
- Scroll: zoom in/out.

## Core API snapshot

- `Camera` — Look-at camera with perspective projection.
- `OrbitCameraController` — Spherical orbit controls (yaw/pitch/radius/pan).
- `Engine`, `EngineHost`, `FrameClock`, `Time` — Runtime loop, fixed-step scheduling, and render orchestration.
- `IInputProvider`, `InputState`, `ActionMap` — Input abstraction for UI platforms.
- `Material`, `Light` — Basic Phong-like material parameters, optional base-color textures, and directional light hook.
- `Mesh`, `MeshInstance`, `MeshFactory` — Primitives with normals (cube, pyramid, plane, sphere, cylinder, grid) and an OBJ loader with optional normal generation.
- `Mesh` — Stores bounding radius and AABB for quick culling and editing tools.
- `MeshInstance` — Can return world-space bounds for selection/fit-to-view.
- `Renderer3D` — Depth-buffered rasterizer with optional painter fallback, backface culling, lighting toggle, plus picking/raycast/unproject helpers, screen bounds, and optional render stats.
- `PhysicsWorld`, `RigidBodyComponent`, `ColliderComponent` — Physics simulation, constraints, and query APIs.
- `AudioSystem`, `AudioListenerComponent`, `AudioSourceComponent` — Audio playback pipeline with a backend interface.
- `ParticleEmitterComponent`, `VfxSystem` — CPU particle emitters and billboarding.
- `NavGrid`, `NavAgentComponent`, `NavigationSystem` — Grid navigation and A* pathfinding.

## Testing and perf baselines

- Unit tests live in `tests/Skia3D.Runtime.Tests` and `tests/Skia3D.Core.Tests`.
- Performance capture workflow and placeholders: `tests/PERF_BASELINES.md`.

## Migration notes

- Prefer `EngineHost` (or `Engine` + `SceneRenderSystem`) instead of manual update/render loops.
- Route user input through `Skia3D.Input` (`IInputProvider` + `ActionMap`) rather than UI-framework-specific events.
- New subsystems (physics/audio/VFX/navigation) are opt-in: add project references, attach components to scene nodes, and register their systems with the runtime.

## Notes

- Rendering uses Avalonia's Skia backend via a lightweight `SkiaView` control; no platform-specific UI code is needed.
- Depth handling defaults to a software z-buffer; disable with `UseDepthBuffer = false` for painter-style rendering.
