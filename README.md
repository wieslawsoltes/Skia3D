# Skia3D

Skia3D is a small 3D-style rendering layer built on top of SkiaSharp matrices and drawing primitives. It projects 3D meshes to 2D surfaces, offers depth-buffered rasterization, optional painter-style rendering, lighting hooks, and an orbiting camera for pseudo-3D scenes. An Avalonia sample demonstrates interaction and rendering on macOS, Windows, and Linux.

## Projects

- `src/Skia3D.Core` — Core rendering API (camera, mesh primitives, materials/lighting, renderer with z-buffer). Target: .NET 8.
- `samples/Skia3D.Sample` — Avalonia app showcasing a cube, pyramid, plane (and easily extendable to sphere/cylinder/grid) with orbit controls.

## Getting started

1. Ensure the .NET 8 SDK is installed.
2. Restore and build:

   ```bash
   dotnet build
   ```

3. Run the sample:

   ```bash
   dotnet run --project samples/Skia3D.Sample
   ```

## Controls (sample app)

- Drag with primary pointer: orbit the camera.
- Scroll: zoom in/out.

## Core API snapshot

- `Camera` — Look-at camera with perspective projection.
- `OrbitCameraController` — Spherical orbit controls (yaw/pitch/radius/pan).
- `Material`, `Light` — Basic Phong-like material parameters and directional light hook.
- `Mesh`, `MeshInstance`, `MeshFactory` — Primitives with normals (cube, pyramid, plane, sphere, cylinder, grid) and a data-import helper.
- `Renderer3D` — Depth-buffered rasterizer with optional painter fallback, backface culling, and lighting toggle.

## Notes

- Rendering uses Avalonia's Skia backend via a lightweight `SkiaView` control; no platform-specific UI code is needed.
- Depth handling defaults to a software z-buffer; disable with `UseDepthBuffer = false` for painter-style rendering.
