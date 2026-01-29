# Performance baselines

This file captures a lightweight baseline for the software renderer and related subsystems. Update it when significant changes land.

## How to capture

1. Build the solution in Release:

   ```bash
   dotnet build -c Release
   ```

2. Run the benchmark app:

   ```bash
   dotnet run -c Release --project samples/Skia3D.Bench
   ```

3. Record the key numbers below along with the machine spec and commit hash.

## Baseline record (fill in)

- Date:
- Commit:
- CPU/GPU:
- OS:
- .NET SDK:

### Renderer

- Depth pass triangles/sec:
- Painter pass triangles/sec:
- Shadow map build ms:

### Picking

- BVH raycast avg ms:
- Uniform grid raycast avg ms:

### Animation

- 1k nodes update ms:

### Physics

- 1k bodies broadphase ms:
- 1k bodies narrowphase ms:
