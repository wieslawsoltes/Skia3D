using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using Skia3D.Core;
using Skia3D.Geometry;
using Skia3D.ShaderGraph;
using ShaderGraphModel = Skia3D.ShaderGraph.ShaderGraph;
using SkiaSharp;

namespace Skia3D.Scene;

public interface ISceneAssetResolver
{
    string? GetMeshId(Mesh mesh);
    Mesh? ResolveMesh(string id);
    string? GetMaterialId(Material material);
    Material? ResolveMaterial(string id);
    string? GetTextureId(Texture2D texture);
    Texture2D? ResolveTexture(string id);
}

public sealed class SceneGraphData
{
    public List<SceneNodeData> Roots { get; set; } = new();
}

public sealed class SceneNodeData
{
    public string Name { get; set; } = "Node";
    public TransformData Transform { get; set; } = new();
    public MeshRendererData? MeshRenderer { get; set; }
    public LightData? Light { get; set; }
    public CameraData? Camera { get; set; }
    public List<ConstraintData> Constraints { get; set; } = new();
    public List<SceneNodeData> Children { get; set; } = new();
}

public sealed class ConstraintData
{
    public ConstraintType Type { get; set; } = ConstraintType.None;
    public string? RootPath { get; set; }
    public string? MidPath { get; set; }
    public string? TargetPath { get; set; }
    public string? PolePath { get; set; }
    public bool Enabled { get; set; } = true;
    public float Weight { get; set; } = 1f;
    public bool MaintainOffset { get; set; }
    public Matrix4x4Data? Offset { get; set; }
    public Vector3Data? Up { get; set; }
}

public enum ConstraintType
{
    None,
    Parent,
    LookAt,
    TwoBoneIk
}

public sealed class TransformData
{
    public Vector3Data Position { get; set; } = Vector3Data.Zero;
    public QuaternionData Rotation { get; set; } = QuaternionData.Identity;
    public Vector3Data Scale { get; set; } = Vector3Data.One;

    public static TransformData From(Transform transform)
    {
        return new TransformData
        {
            Position = Vector3Data.From(transform.LocalPosition),
            Rotation = QuaternionData.From(transform.LocalRotation),
            Scale = Vector3Data.From(transform.LocalScale)
        };
    }

    public void ApplyTo(Transform transform)
    {
        transform.LocalPosition = Position.ToVector3();
        transform.LocalRotation = Rotation.ToQuaternion();
        transform.LocalScale = Scale.ToVector3();
    }
}

public sealed class MeshRendererData
{
    public string? MeshId { get; set; }
    public string? MaterialId { get; set; }
    public ColorData? OverrideColor { get; set; }
    public bool IsVisible { get; set; } = true;
    public ShaderGraphData? MaterialGraph { get; set; }
}

public sealed class ShaderGraphData
{
    public Guid OutputNodeId { get; set; }
    public List<ShaderNodeData> Nodes { get; set; } = new();
    public List<ShaderLinkData> Links { get; set; } = new();
}

public sealed class ShaderNodeData
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public Vector2Data Position { get; set; } = Vector2Data.Zero;
    public Dictionary<string, ShaderPortValueData> Inputs { get; set; } = new();
    public Vector4Data? Color { get; set; }
    public float? FloatValue { get; set; }
    public string? TextureId { get; set; }
    public string? Label { get; set; }
    public TextureSamplerData? Sampler { get; set; }
}

public sealed class ShaderLinkData
{
    public Guid FromNode { get; set; }
    public string FromPort { get; set; } = string.Empty;
    public Guid ToNode { get; set; }
    public string ToPort { get; set; } = string.Empty;
}

public sealed class ShaderPortValueData
{
    public ShaderValueType Type { get; set; }
    public Vector4Data Value { get; set; } = Vector4Data.Zero;
    public string? TextureId { get; set; }
    public TextureSamplerData? Sampler { get; set; }
}

public struct TextureSamplerData
{
    public TextureWrap WrapU { get; set; }
    public TextureWrap WrapV { get; set; }
    public TextureFilter Filter { get; set; }

    public static TextureSamplerData From(TextureSampler sampler)
    {
        return new TextureSamplerData
        {
            WrapU = sampler.WrapU,
            WrapV = sampler.WrapV,
            Filter = sampler.Filter
        };
    }

    public TextureSampler ToSampler()
    {
        return new TextureSampler
        {
            WrapU = WrapU,
            WrapV = WrapV,
            Filter = Filter
        };
    }
}

public sealed class LightData
{
    public LightType Type { get; set; } = LightType.Directional;
    public Vector3Data Direction { get; set; } = Vector3Data.From(new Vector3(-0.4f, -1f, -0.6f));
    public Vector3Data Position { get; set; } = Vector3Data.Zero;
    public ColorData Color { get; set; } = ColorData.White;
    public float Intensity { get; set; } = 1f;
    public float Range { get; set; } = 10f;
    public float InnerConeAngle { get; set; } = 0.35f;
    public float OuterConeAngle { get; set; } = 0.6f;
    public Vector2Data Size { get; set; } = Vector2Data.From(new Vector2(0.5f, 0.5f));
    public bool IsEnabled { get; set; } = true;

    public static LightData From(LightComponent component)
    {
        var light = component.Light;
        return new LightData
        {
            Type = light.Type,
            Direction = Vector3Data.From(light.Direction),
            Position = Vector3Data.From(light.Position),
            Color = ColorData.From(light.Color),
            Intensity = light.Intensity,
            Range = light.Range,
            InnerConeAngle = light.InnerConeAngle,
            OuterConeAngle = light.OuterConeAngle,
            Size = Vector2Data.From(light.Size),
            IsEnabled = component.IsEnabled
        };
    }
}

public sealed class CameraData
{
    public Vector3Data Position { get; set; } = Vector3Data.Zero;
    public Vector3Data Target { get; set; } = Vector3Data.Zero;
    public Vector3Data Up { get; set; } = Vector3Data.From(Vector3.UnitY);
    public float FieldOfView { get; set; } = MathF.PI / 3f;
    public float AspectRatio { get; set; } = 1f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 100f;
    public float OrthographicSize { get; set; } = 6f;
    public CameraProjectionMode ProjectionMode { get; set; } = CameraProjectionMode.Perspective;
    public bool IsEnabled { get; set; } = true;

    public static CameraData From(CameraComponent component)
    {
        var camera = component.Camera;
        return new CameraData
        {
            Position = Vector3Data.From(camera.Position),
            Target = Vector3Data.From(camera.Target),
            Up = Vector3Data.From(camera.Up),
            FieldOfView = camera.FieldOfView,
            AspectRatio = camera.AspectRatio,
            NearPlane = camera.NearPlane,
            FarPlane = camera.FarPlane,
            OrthographicSize = camera.OrthographicSize,
            ProjectionMode = camera.ProjectionMode,
            IsEnabled = component.IsEnabled
        };
    }
}

public struct Vector2Data
{
    public float X { get; set; }
    public float Y { get; set; }

    public static Vector2Data Zero => new() { X = 0f, Y = 0f };

    public static Vector2Data From(Vector2 value) => new() { X = value.X, Y = value.Y };

    public Vector2 ToVector2() => new(X, Y);
}

public struct Vector3Data
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public static Vector3Data Zero => new() { X = 0f, Y = 0f, Z = 0f };
    public static Vector3Data One => new() { X = 1f, Y = 1f, Z = 1f };

    public static Vector3Data From(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };

    public Vector3 ToVector3() => new(X, Y, Z);
}

public struct Vector4Data
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    public static Vector4Data Zero => new() { X = 0f, Y = 0f, Z = 0f, W = 0f };

    public static Vector4Data From(Vector4 value) => new() { X = value.X, Y = value.Y, Z = value.Z, W = value.W };

    public Vector2 ToVector2() => new(X, Y);

    public Vector3 ToVector3() => new(X, Y, Z);

    public Vector4 ToVector4() => new(X, Y, Z, W);
}

public struct Int4Data
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int W { get; set; }

    public static Int4Data Zero => new() { X = 0, Y = 0, Z = 0, W = 0 };

    public static Int4Data From(Int4 value) => new() { X = value.X, Y = value.Y, Z = value.Z, W = value.W };

    public Int4 ToInt4() => new(X, Y, Z, W);
}

public struct QuaternionData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    public static QuaternionData Identity => new() { X = 0f, Y = 0f, Z = 0f, W = 1f };

    public static QuaternionData From(Quaternion value) => new() { X = value.X, Y = value.Y, Z = value.Z, W = value.W };

    public Quaternion ToQuaternion() => new(X, Y, Z, W);
}

public struct Matrix4x4Data
{
    public float M11 { get; set; }
    public float M12 { get; set; }
    public float M13 { get; set; }
    public float M14 { get; set; }
    public float M21 { get; set; }
    public float M22 { get; set; }
    public float M23 { get; set; }
    public float M24 { get; set; }
    public float M31 { get; set; }
    public float M32 { get; set; }
    public float M33 { get; set; }
    public float M34 { get; set; }
    public float M41 { get; set; }
    public float M42 { get; set; }
    public float M43 { get; set; }
    public float M44 { get; set; }

    public static Matrix4x4Data From(Matrix4x4 value)
    {
        return new Matrix4x4Data
        {
            M11 = value.M11,
            M12 = value.M12,
            M13 = value.M13,
            M14 = value.M14,
            M21 = value.M21,
            M22 = value.M22,
            M23 = value.M23,
            M24 = value.M24,
            M31 = value.M31,
            M32 = value.M32,
            M33 = value.M33,
            M34 = value.M34,
            M41 = value.M41,
            M42 = value.M42,
            M43 = value.M43,
            M44 = value.M44
        };
    }

    public Matrix4x4 ToMatrix4x4()
    {
        return new Matrix4x4(
            M11, M12, M13, M14,
            M21, M22, M23, M24,
            M31, M32, M33, M34,
            M41, M42, M43, M44);
    }
}

public struct ColorData
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }

    public static ColorData White => new() { R = 255, G = 255, B = 255, A = 255 };

    public static ColorData From(SKColor color) => new() { R = color.Red, G = color.Green, B = color.Blue, A = color.Alpha };

    public SKColor ToColor() => new(R, G, B, A);
}

public static class SceneSerializer
{
    public static SceneGraphData ToData(Scene scene, ISceneAssetResolver? resolver = null)
    {
        if (scene is null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        var data = new SceneGraphData();
        var pathMap = BuildNodePathMap(scene);
        for (int i = 0; i < scene.Roots.Count; i++)
        {
            data.Roots.Add(ToNodeData(scene.Roots[i], resolver, pathMap));
        }

        return data;
    }

    public static Scene FromData(SceneGraphData data, ISceneAssetResolver? resolver = null)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var scene = new Scene();
        var nodeMap = new Dictionary<string, SceneNode>();
        for (int i = 0; i < data.Roots.Count; i++)
        {
            var root = FromNodeData(data.Roots[i], resolver, nodeMap, i.ToString());
            scene.AddRoot(root);
        }

        for (int i = 0; i < data.Roots.Count; i++)
        {
            ApplyConstraints(data.Roots[i], scene.Roots[i], nodeMap);
        }

        return scene;
    }

    public static string ToJson(Scene scene, ISceneAssetResolver? resolver = null, JsonSerializerOptions? options = null)
    {
        var data = ToData(scene, resolver);
        options ??= CreateDefaultOptions();
        return JsonSerializer.Serialize(data, options);
    }

    public static Scene FromJson(string json, ISceneAssetResolver? resolver = null, JsonSerializerOptions? options = null)
    {
        options ??= CreateDefaultOptions();
        var data = JsonSerializer.Deserialize<SceneGraphData>(json, options);
        return data is null ? new Scene() : FromData(data, resolver);
    }

    private static SceneNodeData ToNodeData(SceneNode node, ISceneAssetResolver? resolver, Dictionary<SceneNode, string> pathMap)
    {
        var data = new SceneNodeData
        {
            Name = node.Name,
            Transform = TransformData.From(node.Transform)
        };

        var renderer = node.MeshRenderer;
        if (renderer != null)
        {
            data.MeshRenderer = new MeshRendererData
            {
                MeshId = resolver?.GetMeshId(renderer.Mesh),
                MaterialId = resolver?.GetMaterialId(renderer.Material),
                OverrideColor = renderer.OverrideColor.HasValue ? ColorData.From(renderer.OverrideColor.Value) : null,
                IsVisible = renderer.IsVisible,
                MaterialGraph = renderer.MaterialGraph != null ? ToGraphData(renderer.MaterialGraph, resolver) : null
            };
        }

        if (node.Light != null)
        {
            data.Light = LightData.From(node.Light);
        }

        if (node.Camera != null)
        {
            data.Camera = CameraData.From(node.Camera);
        }

        if (node.Constraints.Count > 0)
        {
            foreach (var constraint in node.Constraints)
            {
                var constraintData = ToConstraintData(constraint, pathMap);
                if (constraintData != null)
                {
                    data.Constraints.Add(constraintData);
                }
            }
        }

        foreach (var child in node.Children)
        {
            data.Children.Add(ToNodeData(child, resolver, pathMap));
        }

        return data;
    }

    private static SceneNode FromNodeData(SceneNodeData data, ISceneAssetResolver? resolver, Dictionary<string, SceneNode> nodeMap, string path)
    {
        var node = new SceneNode(data.Name);
        data.Transform.ApplyTo(node.Transform);
        nodeMap[path] = node;

        if (data.MeshRenderer != null)
        {
            var mesh = data.MeshRenderer.MeshId != null ? resolver?.ResolveMesh(data.MeshRenderer.MeshId) : null;
            if (mesh != null)
            {
                var material = data.MeshRenderer.MaterialId != null ? resolver?.ResolveMaterial(data.MeshRenderer.MaterialId) : null;
                var renderer = material != null ? new MeshRenderer(mesh, material) : new MeshRenderer(mesh);
                renderer.OverrideColor = data.MeshRenderer.OverrideColor?.ToColor();
                renderer.IsVisible = data.MeshRenderer.IsVisible;
                if (data.MeshRenderer.MaterialGraph != null)
                {
                    renderer.MaterialGraph = FromGraphData(data.MeshRenderer.MaterialGraph, resolver);
                }
                node.MeshRenderer = renderer;
            }
        }

        if (data.Light != null)
        {
            Light light = data.Light.Type switch
            {
                LightType.Point => Light.Point(data.Light.Position.ToVector3(), data.Light.Color.ToColor(), data.Light.Intensity, data.Light.Range),
                LightType.Spot => Light.Spot(data.Light.Position.ToVector3(), data.Light.Direction.ToVector3(), data.Light.Color.ToColor(), data.Light.Intensity, data.Light.Range, data.Light.InnerConeAngle, data.Light.OuterConeAngle),
                LightType.Area => Light.Area(data.Light.Position.ToVector3(), data.Light.Direction.ToVector3(), data.Light.Size.ToVector2(), data.Light.Color.ToColor(), data.Light.Intensity, data.Light.Range),
                _ => Light.Directional(data.Light.Direction.ToVector3(), data.Light.Color.ToColor(), data.Light.Intensity)
            };

            node.Light = new LightComponent(light) { IsEnabled = data.Light.IsEnabled };
        }

        if (data.Camera != null)
        {
            var cam = new Camera
            {
                Position = data.Camera.Position.ToVector3(),
                Target = data.Camera.Target.ToVector3(),
                Up = data.Camera.Up.ToVector3(),
                FieldOfView = data.Camera.FieldOfView,
                AspectRatio = data.Camera.AspectRatio,
                NearPlane = data.Camera.NearPlane,
                FarPlane = data.Camera.FarPlane,
                OrthographicSize = data.Camera.OrthographicSize,
                ProjectionMode = data.Camera.ProjectionMode
            };
            node.Camera = new CameraComponent(cam) { IsEnabled = data.Camera.IsEnabled };
        }

        for (int i = 0; i < data.Children.Count; i++)
        {
            var child = data.Children[i];
            var childPath = $"{path}/{i}";
            node.AddChild(FromNodeData(child, resolver, nodeMap, childPath));
        }

        return node;
    }

    private static Dictionary<SceneNode, string> BuildNodePathMap(Scene scene)
    {
        var map = new Dictionary<SceneNode, string>();
        for (int i = 0; i < scene.Roots.Count; i++)
        {
            BuildNodePathMap(scene.Roots[i], i.ToString(), map);
        }

        return map;
    }

    private static void BuildNodePathMap(SceneNode node, string path, Dictionary<SceneNode, string> map)
    {
        map[node] = path;
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            BuildNodePathMap(children[i], $"{path}/{i}", map);
        }
    }

    private static ConstraintData? ToConstraintData(TransformConstraint constraint, Dictionary<SceneNode, string> pathMap)
    {
        switch (constraint)
        {
            case ParentConstraint parent:
                if (!pathMap.TryGetValue(parent.Target, out var parentPath))
                {
                    return null;
                }

                return new ConstraintData
                {
                    Type = ConstraintType.Parent,
                    TargetPath = parentPath,
                    Enabled = parent.Enabled,
                    Weight = parent.Weight,
                    MaintainOffset = parent.MaintainOffset,
                    Offset = Matrix4x4Data.From(parent.Offset)
                };
            case LookAtConstraint lookAt:
                if (!pathMap.TryGetValue(lookAt.Target, out var lookAtPath))
                {
                    return null;
                }

                return new ConstraintData
                {
                    Type = ConstraintType.LookAt,
                    TargetPath = lookAtPath,
                    Enabled = lookAt.Enabled,
                    Weight = lookAt.Weight,
                    Up = Vector3Data.From(lookAt.Up)
                };
            case TwoBoneIkConstraint twoBone:
                if (!pathMap.TryGetValue(twoBone.Target, out var targetPath) ||
                    !pathMap.TryGetValue(twoBone.Root, out var rootPath) ||
                    !pathMap.TryGetValue(twoBone.Mid, out var midPath))
                {
                    return null;
                }

                pathMap.TryGetValue(twoBone.Pole ?? twoBone.Target, out var polePath);

                return new ConstraintData
                {
                    Type = ConstraintType.TwoBoneIk,
                    RootPath = rootPath,
                    MidPath = midPath,
                    TargetPath = targetPath,
                    PolePath = twoBone.Pole != null ? polePath : null,
                    Enabled = twoBone.Enabled,
                    Weight = twoBone.Weight
                };
            default:
                return null;
        }
    }

    private static void ApplyConstraints(SceneNodeData data, SceneNode node, Dictionary<string, SceneNode> nodeMap)
    {
        if (data.Constraints != null && data.Constraints.Count > 0)
        {
            node.Constraints.Clear();
            foreach (var constraint in data.Constraints)
            {
                TransformConstraint? instance = constraint.Type switch
                {
                    ConstraintType.Parent => CreateParentConstraint(constraint, nodeMap),
                    ConstraintType.LookAt => CreateLookAtConstraint(constraint, nodeMap),
                    ConstraintType.TwoBoneIk => CreateTwoBoneIkConstraint(constraint, nodeMap),
                    _ => null
                };

                if (instance != null)
                {
                    instance.Enabled = constraint.Enabled;
                    instance.Weight = constraint.Weight;
                    node.Constraints.Add(instance);
                }
            }
        }

        for (int i = 0; i < data.Children.Count; i++)
        {
            ApplyConstraints(data.Children[i], node.Children[i], nodeMap);
        }
    }

    private static ParentConstraint? CreateParentConstraint(ConstraintData data, Dictionary<string, SceneNode> nodeMap)
    {
        if (string.IsNullOrWhiteSpace(data.TargetPath))
        {
            return null;
        }

        if (!nodeMap.TryGetValue(data.TargetPath, out var target))
        {
            return null;
        }

        return CreateParentConstraint(data, target);
    }

    private static ParentConstraint CreateParentConstraint(ConstraintData data, SceneNode target)
    {
        var constraint = new ParentConstraint(target, data.MaintainOffset)
        {
            Weight = data.Weight,
            Enabled = data.Enabled
        };

        if (data.Offset.HasValue)
        {
            constraint.SetOffset(data.Offset.Value.ToMatrix4x4());
        }

        return constraint;
    }

    private static LookAtConstraint? CreateLookAtConstraint(ConstraintData data, Dictionary<string, SceneNode> nodeMap)
    {
        if (string.IsNullOrWhiteSpace(data.TargetPath))
        {
            return null;
        }

        if (!nodeMap.TryGetValue(data.TargetPath, out var target))
        {
            return null;
        }

        return CreateLookAtConstraint(data, target);
    }

    private static LookAtConstraint CreateLookAtConstraint(ConstraintData data, SceneNode target)
    {
        var constraint = new LookAtConstraint(target)
        {
            Weight = data.Weight,
            Enabled = data.Enabled
        };

        if (data.Up.HasValue)
        {
            constraint.Up = data.Up.Value.ToVector3();
        }

        return constraint;
    }

    private static TwoBoneIkConstraint? CreateTwoBoneIkConstraint(ConstraintData data, Dictionary<string, SceneNode> nodeMap)
    {
        if (string.IsNullOrWhiteSpace(data.RootPath) ||
            string.IsNullOrWhiteSpace(data.MidPath) ||
            string.IsNullOrWhiteSpace(data.TargetPath))
        {
            return null;
        }

        if (!nodeMap.TryGetValue(data.RootPath, out var root) ||
            !nodeMap.TryGetValue(data.MidPath, out var mid) ||
            !nodeMap.TryGetValue(data.TargetPath, out var target))
        {
            return null;
        }

        SceneNode? pole = null;
        if (!string.IsNullOrWhiteSpace(data.PolePath))
        {
            nodeMap.TryGetValue(data.PolePath, out pole);
        }

        return new TwoBoneIkConstraint(root, mid, target, pole)
        {
            Weight = data.Weight,
            Enabled = data.Enabled
        };
    }

    private static ShaderGraphData ToGraphData(ShaderGraphModel graph, ISceneAssetResolver? resolver)
    {
        var data = new ShaderGraphData
        {
            OutputNodeId = graph.OutputNode.Id
        };

        foreach (var node in graph.Nodes)
        {
            data.Nodes.Add(ToShaderNodeData(node, resolver));
        }

        foreach (var link in graph.Links)
        {
            data.Links.Add(new ShaderLinkData
            {
                FromNode = link.FromNode,
                FromPort = link.FromPort,
                ToNode = link.ToNode,
                ToPort = link.ToPort
            });
        }

        return data;
    }

    private static ShaderGraphModel FromGraphData(ShaderGraphData data, ISceneAssetResolver? resolver)
    {
        var graph = new ShaderGraphModel(data.OutputNodeId);
        var nodeMap = new Dictionary<Guid, ShaderNode>
        {
            [graph.OutputNode.Id] = graph.OutputNode
        };

        foreach (var nodeData in data.Nodes)
        {
            if (nodeData.Type == ShaderNodeTypeOutput || nodeData.Id == graph.OutputNode.Id)
            {
                graph.OutputNode.Position = nodeData.Position.ToVector2();
                ApplyShaderNodeData(graph.OutputNode, nodeData, resolver);
                continue;
            }

            var node = CreateShaderNode(nodeData, resolver);
            if (node == null)
            {
                continue;
            }

            node.Position = nodeData.Position.ToVector2();
            ApplyShaderNodeData(node, nodeData, resolver);
            graph.AddNode(node);
            nodeMap[node.Id] = node;
        }

        foreach (var link in data.Links)
        {
            if (!nodeMap.ContainsKey(link.FromNode) || !nodeMap.ContainsKey(link.ToNode))
            {
                continue;
            }

            graph.SetLink(new ShaderLink(link.FromNode, link.FromPort, link.ToNode, link.ToPort));
        }

        return graph;
    }

    private const string ShaderNodeTypeOutput = "output";
    private const string ShaderNodeTypeColor = "color";
    private const string ShaderNodeTypeFloat = "float";
    private const string ShaderNodeTypeAdd = "add";
    private const string ShaderNodeTypeMultiply = "multiply";
    private const string ShaderNodeTypeTexture = "texture2d";
    private const string ShaderNodeTypeTextureSample = "texture-sample";
    private const string ShaderNodeTypeNormalMap = "normal-map";

    private static ShaderNodeData ToShaderNodeData(ShaderNode node, ISceneAssetResolver? resolver)
    {
        var data = new ShaderNodeData
        {
            Id = node.Id,
            Type = GetShaderNodeType(node),
            Position = Vector2Data.From(node.Position)
        };

        foreach (var input in node.Inputs)
        {
            data.Inputs[input.Name] = ToPortValueData(input.DefaultValue, resolver);
        }

        switch (node)
        {
            case ColorNode color:
                data.Color = Vector4Data.From(color.Color);
                break;
            case FloatNode scalar:
                data.FloatValue = scalar.Value;
                break;
            case Texture2DNode texture:
                data.TextureId = texture.TextureId ?? (texture.Texture != null ? resolver?.GetTextureId(texture.Texture) : null);
                data.Label = texture.Label;
                data.Sampler = TextureSamplerData.From(texture.Sampler);
                break;
            default:
                break;
        }

        return data;
    }

    private static string GetShaderNodeType(ShaderNode node)
    {
        return node switch
        {
            MaterialOutputNode => ShaderNodeTypeOutput,
            ColorNode => ShaderNodeTypeColor,
            FloatNode => ShaderNodeTypeFloat,
            AddNode => ShaderNodeTypeAdd,
            MultiplyNode => ShaderNodeTypeMultiply,
            Texture2DNode => ShaderNodeTypeTexture,
            TextureSampleNode => ShaderNodeTypeTextureSample,
            NormalMapNode => ShaderNodeTypeNormalMap,
            _ => node.Title
        };
    }

    private static ShaderNode? CreateShaderNode(ShaderNodeData data, ISceneAssetResolver? resolver)
    {
        return data.Type switch
        {
            ShaderNodeTypeColor => new ColorNode(data.Color?.ToVector4() ?? Vector4.One, data.Id),
            ShaderNodeTypeFloat => new FloatNode(data.FloatValue ?? 0f, data.Id),
            ShaderNodeTypeAdd => new AddNode(data.Id),
            ShaderNodeTypeMultiply => new MultiplyNode(data.Id),
            ShaderNodeTypeTexture => CreateTextureNode(data, resolver),
            ShaderNodeTypeTextureSample => new TextureSampleNode(data.Id),
            ShaderNodeTypeNormalMap => new NormalMapNode(data.Id),
            ShaderNodeTypeOutput => new MaterialOutputNode(data.Id),
            _ => null
        };
    }

    private static ShaderNode CreateTextureNode(ShaderNodeData data, ISceneAssetResolver? resolver)
    {
        Texture2D? texture = null;
        if (!string.IsNullOrWhiteSpace(data.TextureId))
        {
            texture = resolver?.ResolveTexture(data.TextureId);
        }

        var node = new Texture2DNode(texture, data.Id)
        {
            Label = data.Label ?? "Texture",
            TextureId = data.TextureId
        };

        if (data.Sampler.HasValue)
        {
            var sampler = data.Sampler.Value;
            node.Sampler.WrapU = sampler.WrapU;
            node.Sampler.WrapV = sampler.WrapV;
            node.Sampler.Filter = sampler.Filter;
        }

        return node;
    }

    private static void ApplyShaderNodeData(ShaderNode node, ShaderNodeData data, ISceneAssetResolver? resolver)
    {
        if (data.Inputs.Count > 0)
        {
            foreach (var entry in data.Inputs)
            {
                var input = node.FindInput(entry.Key);
                if (input == null)
                {
                    continue;
                }

                input.DefaultValue = FromPortValueData(entry.Value, resolver);
            }
        }

        if (node is ColorNode color && data.Color.HasValue)
        {
            color.Color = data.Color.Value.ToVector4();
        }

        if (node is FloatNode scalar && data.FloatValue.HasValue)
        {
            scalar.Value = data.FloatValue.Value;
        }
    }

    private static ShaderPortValueData ToPortValueData(ShaderValue value, ISceneAssetResolver? resolver)
    {
        var data = new ShaderPortValueData
        {
            Type = value.Type,
            Value = Vector4Data.From(value.Value)
        };

        if (value.Texture != null)
        {
            data.TextureId = resolver?.GetTextureId(value.Texture);
        }

        if (value.Sampler != null)
        {
            data.Sampler = TextureSamplerData.From(value.Sampler);
        }

        return data;
    }

    private static ShaderValue FromPortValueData(ShaderPortValueData data, ISceneAssetResolver? resolver)
    {
        Texture2D? texture = null;
        if (!string.IsNullOrWhiteSpace(data.TextureId))
        {
            texture = resolver?.ResolveTexture(data.TextureId);
        }

        TextureSampler? sampler = null;
        if (data.Sampler.HasValue)
        {
            sampler = data.Sampler.Value.ToSampler();
        }

        return data.Type switch
        {
            ShaderValueType.Vector2 => ShaderValue.Vector2(data.Value.ToVector2()),
            ShaderValueType.Vector3 => ShaderValue.Vector3(data.Value.ToVector3()),
            ShaderValueType.Vector4 => ShaderValue.Vector4(data.Value.ToVector4()),
            ShaderValueType.Color => ShaderValue.Color(data.Value.ToVector4(), texture, sampler),
            ShaderValueType.Texture => ShaderValue.TextureValue(texture, sampler),
            _ => ShaderValue.Float(data.Value.X)
        };
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }
}
