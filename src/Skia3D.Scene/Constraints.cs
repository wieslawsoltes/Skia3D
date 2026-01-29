using System;
using System.Numerics;

namespace Skia3D.Scene;

public abstract class TransformConstraint
{
    private float _weight = 1f;

    public bool Enabled { get; set; } = true;

    public float Weight
    {
        get => _weight;
        set => _weight = Math.Clamp(value, 0f, 1f);
    }

    public abstract void Apply(SceneNode node, Matrix4x4 parentWorld);
}

public sealed class ParentConstraint : TransformConstraint
{
    public ParentConstraint(SceneNode target, bool maintainOffset)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        MaintainOffset = maintainOffset;
    }

    public SceneNode Target { get; }

    public bool MaintainOffset { get; set; }

    public Matrix4x4 Offset { get; private set; } = Matrix4x4.Identity;

    public void CaptureOffset(SceneNode node)
    {
        if (node == null)
        {
            return;
        }

        var targetWorld = Target.Transform.WorldMatrix;
        if (!Matrix4x4.Invert(targetWorld, out var invTarget))
        {
            Offset = Matrix4x4.Identity;
            return;
        }

        Offset = node.Transform.WorldMatrix * invTarget;
    }

    public void SetOffset(Matrix4x4 offset)
    {
        Offset = offset;
    }

    public override void Apply(SceneNode node, Matrix4x4 parentWorld)
    {
        if (!Enabled || node == null)
        {
            return;
        }

        var targetWorld = Target.Transform.WorldMatrix;
        var desiredWorld = MaintainOffset ? Offset * targetWorld : targetWorld;

        if (!Matrix4x4.Decompose(node.Transform.WorldMatrix, out var currentScale, out var currentRot, out var currentPos) ||
            !Matrix4x4.Decompose(desiredWorld, out var targetScale, out var targetRot, out var targetPos))
        {
            return;
        }

        var weight = Weight;
        var blendedPos = Vector3.Lerp(currentPos, targetPos, weight);
        var blendedRot = Quaternion.Slerp(currentRot, targetRot, weight);
        var blendedScale = Vector3.Lerp(currentScale, targetScale, weight);

        var blendedWorld = Matrix4x4.CreateScale(blendedScale)
            * Matrix4x4.CreateFromQuaternion(blendedRot)
            * Matrix4x4.CreateTranslation(blendedPos);

        node.Transform.SetLocalFromWorld(blendedWorld, parentWorld);
    }
}

public sealed class LookAtConstraint : TransformConstraint
{
    public LookAtConstraint(SceneNode target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public SceneNode Target { get; }

    public Vector3 Up { get; set; } = Vector3.UnitY;

    public override void Apply(SceneNode node, Matrix4x4 parentWorld)
    {
        if (!Enabled || node == null)
        {
            return;
        }

        var world = node.Transform.WorldMatrix;
        if (!Matrix4x4.Decompose(world, out var scale, out var currentRot, out var currentPos))
        {
            return;
        }

        var targetPos = Target.Transform.WorldMatrix.Translation;
        var dir = targetPos - currentPos;
        if (dir.LengthSquared() < 1e-8f)
        {
            return;
        }

        dir = Vector3.Normalize(dir);
        var right = Vector3.Normalize(Vector3.Cross(Up, dir));
        var up = Vector3.Normalize(Vector3.Cross(dir, right));

        var rotationMatrix = new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            dir.X, dir.Y, dir.Z, 0f,
            0f, 0f, 0f, 1f);

        var targetRot = Quaternion.CreateFromRotationMatrix(rotationMatrix);
        var blendedRot = Quaternion.Slerp(currentRot, targetRot, Weight);
        var blendedWorld = Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(blendedRot)
            * Matrix4x4.CreateTranslation(currentPos);

        node.Transform.SetLocalFromWorld(blendedWorld, parentWorld);
    }
}

public sealed class TwoBoneIkConstraint : TransformConstraint
{
    public TwoBoneIkConstraint(SceneNode root, SceneNode mid, SceneNode target, SceneNode? pole = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Mid = mid ?? throw new ArgumentNullException(nameof(mid));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Pole = pole;
    }

    public SceneNode Root { get; }

    public SceneNode Mid { get; }

    public SceneNode Target { get; }

    public SceneNode? Pole { get; set; }

    public override void Apply(SceneNode node, Matrix4x4 parentWorld)
    {
        if (!Enabled)
        {
            return;
        }

        var rootParentWorld = Root.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        var rootWorld = Root.Transform.LocalMatrix * rootParentWorld;
        var midParentWorld = Mid.Parent == Root ? rootWorld : Mid.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        var midWorld = Mid.Transform.LocalMatrix * midParentWorld;
        var endParentWorld = node.Parent == Mid ? midWorld : node.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        var endWorld = node.Transform.LocalMatrix * endParentWorld;

        var rootPos = ExtractTranslation(rootWorld);
        var midPos = ExtractTranslation(midWorld);
        var endPos = ExtractTranslation(endWorld);
        var targetPos = Target.Transform.WorldMatrix.Translation;

        var dir = targetPos - rootPos;
        float distance = dir.Length();
        if (distance <= 1e-6f)
        {
            return;
        }

        float lenA = (midPos - rootPos).Length();
        float lenB = (endPos - midPos).Length();
        if (lenA <= 1e-6f || lenB <= 1e-6f)
        {
            return;
        }

        float min = MathF.Abs(lenA - lenB);
        float max = lenA + lenB;
        float clamped = Math.Clamp(distance, min, max);
        var desiredDir = dir / distance;
        var desiredTarget = rootPos + desiredDir * clamped;

        var poleDir = Pole != null ? Pole.Transform.WorldMatrix.Translation - rootPos : Vector3.Cross(midPos - rootPos, endPos - midPos);
        if (poleDir.LengthSquared() <= 1e-8f)
        {
            poleDir = Vector3.Cross(desiredDir, MathF.Abs(desiredDir.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
        }

        var planeNormal = Vector3.Cross(desiredDir, poleDir);
        if (planeNormal.LengthSquared() <= 1e-8f)
        {
            planeNormal = Vector3.Cross(desiredDir, Vector3.UnitY);
        }

        planeNormal = Vector3.Normalize(planeNormal);
        var bendDir = Vector3.Normalize(Vector3.Cross(planeNormal, desiredDir));

        float cosAngle = (lenA * lenA + clamped * clamped - lenB * lenB) / (2f * lenA * clamped);
        cosAngle = Math.Clamp(cosAngle, -1f, 1f);
        float sinAngle = MathF.Sqrt(MathF.Max(0f, 1f - cosAngle * cosAngle));
        var desiredMidPos = rootPos + desiredDir * (lenA * cosAngle) + bendDir * (lenA * sinAngle);

        if (!Matrix4x4.Decompose(rootWorld, out var rootScale, out var rootRot, out _))
        {
            return;
        }

        if (!Matrix4x4.Decompose(midWorld, out var midScale, out var midRot, out _))
        {
            return;
        }

        var currentRootDir = Vector3.Normalize(midPos - rootPos);
        var desiredRootDir = Vector3.Normalize(desiredMidPos - rootPos);
        var rootDelta = FromToRotation(currentRootDir, desiredRootDir);
        var desiredRootRot = Quaternion.Normalize(rootDelta * rootRot);
        var desiredRootWorld = Matrix4x4.CreateScale(rootScale)
            * Matrix4x4.CreateFromQuaternion(desiredRootRot)
            * Matrix4x4.CreateTranslation(rootPos);

        var currentMidDir = Vector3.Normalize(endPos - midPos);
        var desiredMidDir = Vector3.Normalize(desiredTarget - desiredMidPos);
        var midDelta = FromToRotation(currentMidDir, desiredMidDir);
        var desiredMidRot = Quaternion.Normalize(midDelta * midRot);
        var desiredMidWorld = Matrix4x4.CreateScale(midScale)
            * Matrix4x4.CreateFromQuaternion(desiredMidRot)
            * Matrix4x4.CreateTranslation(desiredMidPos);

        float weight = Weight;
        var blendedRootWorld = BlendWorld(rootWorld, desiredRootWorld, weight);
        var blendedMidWorld = BlendWorld(midWorld, desiredMidWorld, weight);

        Root.Transform.SetLocalFromWorld(blendedRootWorld, rootParentWorld);

        var updatedRootWorld = blendedRootWorld;
        var updatedMidParentWorld = Mid.Parent == Root ? updatedRootWorld : Mid.Parent?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        Mid.Transform.SetLocalFromWorld(blendedMidWorld, updatedMidParentWorld);
    }

    private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        => new(matrix.M41, matrix.M42, matrix.M43);

    private static Quaternion FromToRotation(Vector3 from, Vector3 to)
    {
        if (from.LengthSquared() <= 1e-8f || to.LengthSquared() <= 1e-8f)
        {
            return Quaternion.Identity;
        }

        var f = Vector3.Normalize(from);
        var t = Vector3.Normalize(to);
        float dot = Vector3.Dot(f, t);
        if (dot >= 0.9999f)
        {
            return Quaternion.Identity;
        }

        if (dot <= -0.9999f)
        {
            var axis = Vector3.Cross(f, Vector3.UnitY);
            if (axis.LengthSquared() <= 1e-8f)
            {
                axis = Vector3.Cross(f, Vector3.UnitX);
            }
            axis = Vector3.Normalize(axis);
            return Quaternion.CreateFromAxisAngle(axis, MathF.PI);
        }

        var cross = Vector3.Cross(f, t);
        if (cross.LengthSquared() <= 1e-8f)
        {
            return Quaternion.Identity;
        }

        cross = Vector3.Normalize(cross);
        float angle = MathF.Acos(Math.Clamp(dot, -1f, 1f));
        return Quaternion.CreateFromAxisAngle(cross, angle);
    }

    private static Matrix4x4 BlendWorld(Matrix4x4 current, Matrix4x4 target, float weight)
    {
        if (weight <= 0f)
        {
            return current;
        }

        if (weight >= 1f)
        {
            return target;
        }

        if (!Matrix4x4.Decompose(current, out var cScale, out var cRot, out var cPos) ||
            !Matrix4x4.Decompose(target, out var tScale, out var tRot, out var tPos))
        {
            return current;
        }

        var pos = Vector3.Lerp(cPos, tPos, weight);
        var rot = Quaternion.Slerp(cRot, tRot, weight);
        var scale = Vector3.Lerp(cScale, tScale, weight);

        return Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(rot)
            * Matrix4x4.CreateTranslation(pos);
    }
}
