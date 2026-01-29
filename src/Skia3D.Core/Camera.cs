using System.Numerics;

namespace Skia3D.Core;

public enum CameraProjectionMode
{
    Perspective,
    Orthographic
}

public sealed class Camera
{
    public Vector3 Position { get; set; } = new(0f, 0f, 8f);
    public Vector3 Target { get; set; } = Vector3.Zero;
    public Vector3 Up { get; set; } = Vector3.UnitY;
    public float FieldOfView { get; set; } = MathF.PI / 3f;
    public float AspectRatio { get; set; } = 1f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 100f;
    public float OrthographicSize { get; set; } = 6f;
    public CameraProjectionMode ProjectionMode { get; set; } = CameraProjectionMode.Perspective;

    public Matrix4x4 GetViewMatrix() => Matrix4x4.CreateLookAt(Position, Target, Up);

    public Matrix4x4 GetProjectionMatrix()
    {
        if (ProjectionMode == CameraProjectionMode.Orthographic)
        {
            var size = MathF.Max(0.01f, OrthographicSize);
            var height = size * 2f;
            var width = height * MathF.Max(0.01f, AspectRatio);
            return Matrix4x4.CreateOrthographic(width, height, NearPlane, FarPlane);
        }

        return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
    }

    public Matrix4x4 GetViewProjectionMatrix()
    {
        var view = GetViewMatrix();
        var projection = GetProjectionMatrix();
        return view * projection;
    }

    public static bool TryBuildRay(Camera camera, Vector2 screen, Vector2 viewportSize, out Vector3 origin, out Vector3 direction)
    {
        origin = camera.Position;
        direction = default;

        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return false;
        }

        var ndcX = (2f * screen.X / viewportSize.X) - 1f;
        var ndcY = 1f - (2f * screen.Y / viewportSize.Y);

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();
        if (!Matrix4x4.Invert(view * projection, out var invViewProj))
        {
            return false;
        }

        var near = Vector4.Transform(new Vector4(ndcX, ndcY, 0f, 1f), invViewProj);
        var far = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invViewProj);

        if (MathF.Abs(near.W) < 1e-6f || MathF.Abs(far.W) < 1e-6f)
        {
            return false;
        }

        near /= near.W;
        far /= far.W;

        if (camera.ProjectionMode == CameraProjectionMode.Orthographic)
        {
            origin = new Vector3(near.X, near.Y, near.Z);
            var orthoDir = new Vector3(far.X - near.X, far.Y - near.Y, far.Z - near.Z);
            if (orthoDir.LengthSquared() < 1e-12f)
            {
                return false;
            }

            direction = Vector3.Normalize(orthoDir);
            return true;
        }

        var dir = new Vector3(far.X - origin.X, far.Y - origin.Y, far.Z - origin.Z);
        if (dir.LengthSquared() < 1e-12f)
        {
            return false;
        }

        direction = Vector3.Normalize(dir);
        return true;
    }

    public static bool TryUnproject(Camera camera, Vector2 screen, Vector2 viewportSize, float ndcZ, out Vector3 world)
    {
        world = default;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return false;
        }

        var ndcX = (2f * screen.X / viewportSize.X) - 1f;
        var ndcY = 1f - (2f * screen.Y / viewportSize.Y);

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();
        if (!Matrix4x4.Invert(view * projection, out var invViewProj))
        {
            return false;
        }

        var clip = Vector4.Transform(new Vector4(ndcX, ndcY, ndcZ, 1f), invViewProj);
        if (MathF.Abs(clip.W) < 1e-6f)
        {
            return false;
        }

        clip /= clip.W;
        world = new Vector3(clip.X, clip.Y, clip.Z);
        return true;
    }
}

public sealed class OrbitCameraController
{
    private const float MinRadius = 0.5f;
    private const float MaxPitch = 1.45f;
    private const float MinPitch = -1.45f;

    public OrbitCameraController(Camera camera)
    {
        Camera = camera;
        UpdateCamera();
    }

    public Camera Camera { get; }

    public Vector3 Target { get; set; } = Vector3.Zero;

    public float Radius { get; set; } = 6f;

    public float Yaw { get; set; } = -MathF.PI / 4f;

    public float Pitch { get; set; } = -0.3f;

    public void Rotate(float deltaYaw, float deltaPitch)
    {
        Yaw += deltaYaw;
        Pitch = Math.Clamp(Pitch + deltaPitch, MinPitch, MaxPitch);
        UpdateCamera();
    }

    public void Zoom(float delta)
    {
        Radius = Math.Max(MinRadius, Radius + delta);
        UpdateCamera();
    }

    public void ZoomToScreenPoint(Vector2 screen, Vector2 viewportSize, float delta)
    {
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            Zoom(delta);
            return;
        }

        if (!TryBuildRay(Camera, screen, viewportSize, out var origin, out var dir))
        {
            Zoom(delta);
            return;
        }

        var forward = Vector3.Normalize(Target - Camera.Position);
        if (forward.LengthSquared() < 1e-8f)
        {
            Zoom(delta);
            return;
        }

        var denom = Vector3.Dot(dir, forward);
        if (MathF.Abs(denom) < 1e-6f)
        {
            Zoom(delta);
            return;
        }

        var t = Vector3.Dot(Target - origin, forward) / denom;
        if (t <= 0f)
        {
            Zoom(delta);
            return;
        }

        var hit = origin + dir * t;

        Radius = Math.Max(MinRadius, Radius + delta);
        UpdateCamera();

        if (!TryBuildRay(Camera, screen, viewportSize, out origin, out dir))
        {
            return;
        }

        denom = Vector3.Dot(dir, forward);
        if (MathF.Abs(denom) < 1e-6f)
        {
            return;
        }

        t = Vector3.Dot(Target - origin, forward) / denom;
        if (t <= 0f)
        {
            return;
        }

        var newHit = origin + dir * t;
        Target += hit - newHit;
        UpdateCamera();
    }

    public void Pan(Vector2 delta)
    {
        var forward = Vector3.Normalize(Target - Camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Camera.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        Target += (-right * delta.X) + (up * delta.Y);
        UpdateCamera();
    }

    public void UpdateCamera()
    {
        var x = Radius * MathF.Cos(Pitch) * MathF.Cos(Yaw);
        var y = Radius * MathF.Sin(Pitch);
        var z = Radius * MathF.Cos(Pitch) * MathF.Sin(Yaw);
        Camera.Position = Target + new Vector3(x, y, z);
        Camera.Target = Target;
    }

    private static bool TryBuildRay(Camera camera, Vector2 screen, Vector2 viewportSize, out Vector3 origin, out Vector3 direction)
    {
        return Camera.TryBuildRay(camera, screen, viewportSize, out origin, out direction);
    }
}
