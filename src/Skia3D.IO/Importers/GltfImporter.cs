using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Skia3D.Core;
using Skia3D.Animation;
using Skia3D.Geometry;
using Skia3D.Scene;
using SceneGraph = Skia3D.Scene.Scene;
using SkiaSharp;

namespace Skia3D.IO;

public sealed class GltfImporter : IMeshImporter, ISceneImporter
{
    public IReadOnlyList<string> Extensions { get; } = new[] { ".gltf", ".glb" };

    public Mesh Load(Stream stream, MeshLoadOptions options)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var data = ReadAllBytes(stream);
        byte[] jsonBytes;
        byte[]? binChunk = null;

        if (IsGlb(data))
        {
            ParseGlb(data, out jsonBytes, out binChunk);
        }
        else
        {
            jsonBytes = data;
        }

        using var document = JsonDocument.Parse(jsonBytes);
        var root = document.RootElement;

        var buffers = ReadBuffers(root, binChunk, options.SourcePath);
        var bufferViews = ReadBufferViews(root);
        var accessors = ReadAccessors(root);
        var meshes = ReadMeshes(root);
        var nodes = ReadNodes(root);
        var skins = ReadSkins(root);
        var scenes = ReadScenes(root);

        int sceneIndex = 0;
        if (root.TryGetProperty("scene", out var sceneElement) && sceneElement.ValueKind == JsonValueKind.Number)
        {
            sceneIndex = sceneElement.GetInt32();
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();
        var colors = new List<SKColor>();
        var texcoords = new List<Vector2>();
        var indices = new List<int>();
        var skinWeights = new List<Vector4>();
        var skinIndices = new List<Int4>();
        List<MorphTargetBuilder>? morphTargets = null;
        bool anyNormals = false;
        bool anyTangents = false;
        bool anySkin = false;

        var rootNodes = ResolveRootNodes(nodes.Count, scenes, sceneIndex);
        foreach (var nodeIndex in rootNodes)
        {
            TraverseNode(nodeIndex, Matrix4x4.Identity, nodes, meshes, accessors, bufferViews, buffers, options, positions, normals, tangents, colors, texcoords, skinWeights, skinIndices, indices, ref anyNormals, ref anyTangents, ref anySkin, ref morphTargets);
        }

        IReadOnlyList<Vector3>? normalList = null;
        if (anyNormals && normals.Count == positions.Count)
        {
            normalList = normals;
        }
        else if (!anyNormals && options.GenerateNormals && indices.Count > 0 && positions.Count > 0)
        {
            normalList = ComputeNormals(positions, indices);
        }

        IReadOnlyList<Vector4>? tangentList = null;
        if (anyTangents && tangents.Count == positions.Count)
        {
            tangentList = tangents;
        }

        IReadOnlyList<Vector4>? skinWeightList = anySkin && skinWeights.Count == positions.Count ? skinWeights : null;
        IReadOnlyList<Int4>? skinIndexList = anySkin && skinIndices.Count == positions.Count ? skinIndices : null;
        var morphTargetList = BuildMorphTargets(morphTargets);

        return MeshFactory.CreateFromData(positions, indices, normalList, colors, texcoords, tangentList, null, skinWeightList, skinIndexList, morphTargetList);
    }

    public SceneImportResult Load(Stream stream, SceneLoadOptions options)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var meshOptions = options.MeshOptions ?? new MeshLoadOptions();
        var data = ReadAllBytes(stream);
        byte[] jsonBytes;
        byte[]? binChunk = null;

        if (IsGlb(data))
        {
            ParseGlb(data, out jsonBytes, out binChunk);
        }
        else
        {
            jsonBytes = data;
        }

        using var document = JsonDocument.Parse(jsonBytes);
        var root = document.RootElement;

        var buffers = ReadBuffers(root, binChunk, meshOptions.SourcePath);
        var bufferViews = ReadBufferViews(root);
        var accessors = ReadAccessors(root);
        var meshes = ReadMeshes(root);
        var nodes = ReadNodes(root);
        var skins = ReadSkins(root);
        var scenes = ReadScenes(root);

        var materials = options.LoadMaterials ? ReadMaterials(root) : new List<GltfMaterial>();
        var textures = options.LoadMaterials ? ReadTextures(root) : new List<GltfTexture>();
        var images = options.LoadMaterials ? ReadImages(root) : new List<GltfImage>();
        var samplers = options.LoadMaterials ? ReadSamplers(root) : new List<GltfSampler>();
        var textureBindings = options.LoadMaterials
            ? BuildTextureBindings(textures, images, samplers, bufferViews, buffers, meshOptions.SourcePath)
            : Array.Empty<TextureBinding>();
        var materialBindings = options.LoadMaterials
            ? BuildMaterials(materials, textureBindings, meshOptions.DefaultColor)
            : new List<Material>();

        int sceneIndex = 0;
        if (root.TryGetProperty("scene", out var sceneElement) && sceneElement.ValueKind == JsonValueKind.Number)
        {
            sceneIndex = sceneElement.GetInt32();
        }

        var scene = new SceneGraph();
        var nodeMap = new Dictionary<int, SceneNode>();
        var meshNodes = new Dictionary<int, List<SceneNode>>();
        var rootNodes = ResolveRootNodes(nodes.Count, scenes, sceneIndex);
        foreach (var nodeIndex in rootNodes)
        {
            var node = BuildSceneNode(nodeIndex, nodes, meshes, accessors, bufferViews, buffers, meshOptions, materialBindings, nodeMap, meshNodes);
            scene.AddRoot(node);
        }

        if (skins.Count > 0)
        {
            AttachSkins(nodes, meshes, skins, nodeMap, meshNodes, accessors, bufferViews, buffers);
        }

        var animations = options.LoadAnimations
            ? BuildAnimations(root, nodes, accessors, bufferViews, buffers)
            : new List<AnimationClip>();

        return new SceneImportResult(scene, animations);
    }

    private static void TraverseNode(
        int nodeIndex,
        Matrix4x4 parentWorld,
        IReadOnlyList<GltfNode> nodes,
        IReadOnlyList<GltfMesh> meshes,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        MeshLoadOptions options,
        List<Vector3> positions,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<SKColor> colors,
        List<Vector2> texcoords,
        List<Vector4> skinWeights,
        List<Int4> skinIndices,
        List<int> indices,
        ref bool anyNormals,
        ref bool anyTangents,
        ref bool anySkin,
        ref List<MorphTargetBuilder>? morphTargets)
    {
        if ((uint)nodeIndex >= (uint)nodes.Count)
        {
            return;
        }

        var node = nodes[nodeIndex];
        var world = node.LocalMatrix * parentWorld;

        var normalMatrix = Matrix4x4.Identity;
        if (Matrix4x4.Invert(world, out var inv))
        {
            normalMatrix = Matrix4x4.Transpose(inv);
        }

        if (node.Mesh.HasValue && (uint)node.Mesh.Value < (uint)meshes.Count)
        {
            var mesh = meshes[node.Mesh.Value];
            for (int i = 0; i < mesh.Primitives.Count; i++)
            {
                AppendPrimitive(mesh.Primitives[i], world, normalMatrix, accessors, bufferViews, buffers, options, positions, normals, tangents, colors, texcoords, skinWeights, skinIndices, indices, ref anyNormals, ref anyTangents, ref anySkin, ref morphTargets);
            }
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            TraverseNode(node.Children[i], world, nodes, meshes, accessors, bufferViews, buffers, options, positions, normals, tangents, colors, texcoords, skinWeights, skinIndices, indices, ref anyNormals, ref anyTangents, ref anySkin, ref morphTargets);
        }
    }

    private static void AppendPrimitive(
        GltfPrimitive primitive,
        Matrix4x4 world,
        Matrix4x4 normalMatrix,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        MeshLoadOptions options,
        List<Vector3> positions,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<SKColor> colors,
        List<Vector2> texcoords,
        List<Vector4> skinWeights,
        List<Int4> skinIndices,
        List<int> indices,
        ref bool anyNormals,
        ref bool anyTangents,
        ref bool anySkin,
        ref List<MorphTargetBuilder>? morphTargets)
    {
        if (!primitive.Attributes.TryGetValue("POSITION", out var positionAccessorIndex))
        {
            return;
        }

        if ((uint)positionAccessorIndex >= (uint)accessors.Count)
        {
            return;
        }

        var positionAccessor = accessors[positionAccessorIndex];
        var positionData = ReadAccessorFloats(positionAccessor, bufferViews, buffers, normalize: false);
        if (positionData.Length == 0)
        {
            return;
        }

        int vertexCount = positionAccessor.Count;
        var normalData = Array.Empty<float>();
        bool hasNormals = false;

        if (primitive.Attributes.TryGetValue("NORMAL", out var normalAccessorIndex) &&
            (uint)normalAccessorIndex < (uint)accessors.Count)
        {
            var normalAccessor = accessors[normalAccessorIndex];
            normalData = ReadAccessorFloats(normalAccessor, bufferViews, buffers, normalize: normalAccessor.Normalized);
            hasNormals = normalData.Length >= vertexCount * 3;
        }

        var tangentData = Array.Empty<float>();
        bool hasTangents = false;
        if (primitive.Attributes.TryGetValue("TANGENT", out var tangentAccessorIndex) &&
            (uint)tangentAccessorIndex < (uint)accessors.Count)
        {
            var tangentAccessor = accessors[tangentAccessorIndex];
            tangentData = ReadAccessorFloats(tangentAccessor, bufferViews, buffers, normalize: tangentAccessor.Normalized);
            hasTangents = tangentData.Length >= vertexCount * 4;
        }

        var jointData = Array.Empty<float>();
        bool hasJoints = false;
        if (primitive.Attributes.TryGetValue("JOINTS_0", out var jointsAccessorIndex) &&
            (uint)jointsAccessorIndex < (uint)accessors.Count)
        {
            var jointsAccessor = accessors[jointsAccessorIndex];
            jointData = ReadAccessorFloats(jointsAccessor, bufferViews, buffers, normalize: jointsAccessor.Normalized);
            hasJoints = jointData.Length >= vertexCount * 4;
        }

        var weightData = Array.Empty<float>();
        bool hasWeights = false;
        if (primitive.Attributes.TryGetValue("WEIGHTS_0", out var weightsAccessorIndex) &&
            (uint)weightsAccessorIndex < (uint)accessors.Count)
        {
            var weightsAccessor = accessors[weightsAccessorIndex];
            weightData = ReadAccessorFloats(weightsAccessor, bufferViews, buffers, normalize: weightsAccessor.Normalized);
            hasWeights = weightData.Length >= vertexCount * 4;
        }

        var texData = Array.Empty<float>();
        bool hasTexCoords = false;
        if (primitive.Attributes.TryGetValue("TEXCOORD_0", out var texAccessorIndex) &&
            (uint)texAccessorIndex < (uint)accessors.Count)
        {
            var texAccessor = accessors[texAccessorIndex];
            texData = ReadAccessorFloats(texAccessor, bufferViews, buffers, normalize: texAccessor.Normalized);
            hasTexCoords = texData.Length >= vertexCount * 2;
        }

        SKColor[]? colorData = null;
        if (primitive.Attributes.TryGetValue("COLOR_0", out var colorAccessorIndex) &&
            (uint)colorAccessorIndex < (uint)accessors.Count)
        {
            colorData = ReadAccessorColors(accessors[colorAccessorIndex], bufferViews, buffers, options.DefaultColor);
        }

        var localIndices = GetPrimitiveIndices(primitive, accessors, bufferViews, buffers, vertexCount);
        if (localIndices.Count == 0)
        {
            return;
        }

        var triangles = TriangulateIndices(localIndices, primitive.Mode);
        if (triangles.Count == 0)
        {
            return;
        }

        var morphBuffers = ReadMorphTargetBuffers(primitive, vertexCount, accessors, bufferViews, buffers, world, normalMatrix);
        AppendMorphTargets(ref morphTargets, morphBuffers, positions.Count, vertexCount);

        int baseIndex = positions.Count;
        for (int i = 0; i < vertexCount; i++)
        {
            int offset = i * 3;
            var pos = new Vector3(positionData[offset], positionData[offset + 1], positionData[offset + 2]);
            pos = Vector3.Transform(pos, world);
            positions.Add(pos);

            var normal = Vector3.UnitY;
            if (hasNormals)
            {
                var n = new Vector3(normalData[offset], normalData[offset + 1], normalData[offset + 2]);
                n = Vector3.TransformNormal(n, normalMatrix);
                if (n.LengthSquared() > 1e-8f)
                {
                    n = Vector3.Normalize(n);
                }
                normal = n;
                anyNormals = true;
            }
            normals.Add(normal);

            var tangent = Vector4.Zero;
            if (hasTangents)
            {
                int tangentOffset = i * 4;
                var t = new Vector3(tangentData[tangentOffset], tangentData[tangentOffset + 1], tangentData[tangentOffset + 2]);
                t = Vector3.TransformNormal(t, normalMatrix);
                if (t.LengthSquared() > 1e-8f)
                {
                    t = Vector3.Normalize(t);
                }

                tangent = new Vector4(t, tangentData[tangentOffset + 3]);
                anyTangents = true;
            }
            tangents.Add(tangent);

            if (colorData != null && i < colorData.Length)
            {
                colors.Add(colorData[i]);
            }
            else
            {
                colors.Add(options.DefaultColor);
            }

            var uv = Vector2.Zero;
            if (hasTexCoords)
            {
                int texOffset = i * 2;
                uv = new Vector2(texData[texOffset], texData[texOffset + 1]);
            }

            texcoords.Add(uv);

            if (hasJoints && hasWeights)
            {
                int jointOffset = i * 4;
                var joints = new Int4(
                    (int)MathF.Round(jointData[jointOffset]),
                    (int)MathF.Round(jointData[jointOffset + 1]),
                    (int)MathF.Round(jointData[jointOffset + 2]),
                    (int)MathF.Round(jointData[jointOffset + 3]));
                var weights = new Vector4(
                    weightData[jointOffset],
                    weightData[jointOffset + 1],
                    weightData[jointOffset + 2],
                    weightData[jointOffset + 3]);
                var sum = weights.X + weights.Y + weights.Z + weights.W;
                if (sum > 1e-6f)
                {
                    weights *= 1f / sum;
                }
                skinIndices.Add(joints);
                skinWeights.Add(weights);
                anySkin = true;
            }
            else
            {
                skinIndices.Add(Int4.Zero);
                skinWeights.Add(Vector4.Zero);
            }
        }

        for (int i = 0; i < triangles.Count; i++)
        {
            indices.Add(baseIndex + triangles[i]);
        }
    }

    private static List<int> GetPrimitiveIndices(
        GltfPrimitive primitive,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        int vertexCount)
    {
        if (primitive.Indices.HasValue && (uint)primitive.Indices.Value < (uint)accessors.Count)
        {
            return ReadAccessorIndices(accessors[primitive.Indices.Value], bufferViews, buffers);
        }

        var indices = new List<int>(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            indices.Add(i);
        }

        return indices;
    }

    private static List<int> TriangulateIndices(List<int> indices, int mode)
    {
        const int trianglesMode = 4;
        const int stripMode = 5;
        const int fanMode = 6;

        if (indices.Count == 0)
        {
            return indices;
        }

        if (mode == trianglesMode)
        {
            return indices;
        }

        var triangles = new List<int>();
        if (mode == stripMode)
        {
            for (int i = 2; i < indices.Count; i++)
            {
                int i0 = indices[i - 2];
                int i1 = indices[i - 1];
                int i2 = indices[i];
                if ((i & 1) == 0)
                {
                    triangles.Add(i0);
                    triangles.Add(i1);
                    triangles.Add(i2);
                }
                else
                {
                    triangles.Add(i1);
                    triangles.Add(i0);
                    triangles.Add(i2);
                }
            }

            return triangles;
        }

        if (mode == fanMode)
        {
            int root = indices[0];
            for (int i = 2; i < indices.Count; i++)
            {
                triangles.Add(root);
                triangles.Add(indices[i - 1]);
                triangles.Add(indices[i]);
            }
        }

        return triangles;
    }

    private static List<MorphTargetBuffers>? ReadMorphTargetBuffers(
        GltfPrimitive primitive,
        int vertexCount,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        Matrix4x4? world,
        Matrix4x4? normalMatrix)
    {
        if (primitive.Targets.Count == 0)
        {
            return null;
        }

        var results = new List<MorphTargetBuffers>(primitive.Targets.Count);
        for (int t = 0; t < primitive.Targets.Count; t++)
        {
            var target = primitive.Targets[t];
            Vector3[]? pos = null;
            Vector3[]? norm = null;
            Vector4[]? tan = null;

            if (target.TryGetValue("POSITION", out var posAccessorIndex) &&
                (uint)posAccessorIndex < (uint)accessors.Count)
            {
                var posAccessor = accessors[posAccessorIndex];
                var posData = ReadAccessorFloats(posAccessor, bufferViews, buffers, normalize: posAccessor.Normalized);
                if (posData.Length >= vertexCount * 3)
                {
                    pos = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        int offset = i * 3;
                        var delta = new Vector3(posData[offset], posData[offset + 1], posData[offset + 2]);
                        if (world.HasValue)
                        {
                            delta = Vector3.TransformNormal(delta, world.Value);
                        }
                        pos[i] = delta;
                    }
                }
            }

            if (target.TryGetValue("NORMAL", out var normAccessorIndex) &&
                (uint)normAccessorIndex < (uint)accessors.Count)
            {
                var normAccessor = accessors[normAccessorIndex];
                var normData = ReadAccessorFloats(normAccessor, bufferViews, buffers, normalize: normAccessor.Normalized);
                if (normData.Length >= vertexCount * 3)
                {
                    norm = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        int offset = i * 3;
                        var delta = new Vector3(normData[offset], normData[offset + 1], normData[offset + 2]);
                        if (normalMatrix.HasValue)
                        {
                            delta = Vector3.TransformNormal(delta, normalMatrix.Value);
                        }
                        norm[i] = delta;
                    }
                }
            }

            if (target.TryGetValue("TANGENT", out var tangentAccessorIndex) &&
                (uint)tangentAccessorIndex < (uint)accessors.Count)
            {
                var tangentAccessor = accessors[tangentAccessorIndex];
                var tangentData = ReadAccessorFloats(tangentAccessor, bufferViews, buffers, normalize: tangentAccessor.Normalized);
                if (tangentData.Length >= vertexCount * 3)
                {
                    tan = new Vector4[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        int offset = i * 3;
                        var delta = new Vector3(tangentData[offset], tangentData[offset + 1], tangentData[offset + 2]);
                        if (normalMatrix.HasValue)
                        {
                            delta = Vector3.TransformNormal(delta, normalMatrix.Value);
                        }
                        tan[i] = new Vector4(delta, 0f);
                    }
                }
            }

            results.Add(new MorphTargetBuffers
            {
                PositionDeltas = pos,
                NormalDeltas = norm,
                TangentDeltas = tan
            });
        }

        return results;
    }

    private static void AppendMorphTargets(
        ref List<MorphTargetBuilder>? builders,
        IReadOnlyList<MorphTargetBuffers>? buffers,
        int existingCount,
        int vertexCount)
    {
        if (builders == null)
        {
            if (buffers == null || buffers.Count == 0)
            {
                return;
            }

            builders = new List<MorphTargetBuilder>(buffers.Count);
            for (int i = 0; i < buffers.Count; i++)
            {
                var builder = new MorphTargetBuilder();
                if (buffers[i].PositionDeltas != null)
                {
                    builder.EnsurePositions(existingCount);
                }
                if (buffers[i].NormalDeltas != null)
                {
                    builder.EnsureNormals(existingCount);
                }
                if (buffers[i].TangentDeltas != null)
                {
                    builder.EnsureTangents(existingCount);
                }
                builders.Add(builder);
            }
        }

        if (builders == null)
        {
            return;
        }

        int bufferCount = buffers?.Count ?? 0;
        int targetCount = Math.Min(builders.Count, bufferCount);
        for (int i = 0; i < targetCount; i++)
        {
            var builder = builders[i];
            var buffer = buffers![i];
            builder.AppendPositions(buffer.PositionDeltas, existingCount, vertexCount);
            builder.AppendNormals(buffer.NormalDeltas, existingCount, vertexCount);
            builder.AppendTangents(buffer.TangentDeltas, existingCount, vertexCount);
        }

        for (int i = targetCount; i < builders.Count; i++)
        {
            var builder = builders[i];
            builder.AppendPositions(null, existingCount, vertexCount);
            builder.AppendNormals(null, existingCount, vertexCount);
            builder.AppendTangents(null, existingCount, vertexCount);
        }
    }

    private static MeshMorphTarget[]? BuildMorphTargets(List<MorphTargetBuilder>? builders)
    {
        if (builders == null || builders.Count == 0)
        {
            return null;
        }

        var targets = new MeshMorphTarget[builders.Count];
        for (int i = 0; i < builders.Count; i++)
        {
            targets[i] = builders[i].Build();
        }

        return targets;
    }

    private static SceneNode BuildSceneNode(
        int nodeIndex,
        IReadOnlyList<GltfNode> nodes,
        IReadOnlyList<GltfMesh> meshes,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        MeshLoadOptions options,
        IReadOnlyList<Material> materials,
        Dictionary<int, SceneNode> nodeMap,
        Dictionary<int, List<SceneNode>> meshNodes)
    {
        if ((uint)nodeIndex >= (uint)nodes.Count)
        {
            return new SceneNode($"Node_{nodeIndex}");
        }

        var nodeData = nodes[nodeIndex];
        var name = string.IsNullOrWhiteSpace(nodeData.Name) ? $"Node_{nodeIndex}" : nodeData.Name!;
        var node = new SceneNode(name);
        nodeMap[nodeIndex] = node;
        ApplyLocalTransform(node.Transform, nodeData.LocalMatrix);

        if (nodeData.Mesh.HasValue && (uint)nodeData.Mesh.Value < (uint)meshes.Count)
        {
            var mesh = meshes[nodeData.Mesh.Value];
            if (mesh.Primitives.Count == 1)
            {
                var primitive = mesh.Primitives[0];
                var material = ResolveMaterial(materials, primitive.Material);
                var primMesh = BuildPrimitiveMesh(primitive, accessors, bufferViews, buffers, options);
                if (primMesh != null)
                {
                    node.MeshRenderer = new MeshRenderer(primMesh, material);
                    RegisterMeshNode(meshNodes, nodeIndex, node);
                }
            }
            else
            {
                for (int i = 0; i < mesh.Primitives.Count; i++)
                {
                    var primitive = mesh.Primitives[i];
                    var material = ResolveMaterial(materials, primitive.Material);
                    var primMesh = BuildPrimitiveMesh(primitive, accessors, bufferViews, buffers, options);
                    if (primMesh is null)
                    {
                        continue;
                    }

                    var child = new SceneNode($"{name}_prim{i}");
                    child.Transform.LocalPosition = Vector3.Zero;
                    child.Transform.LocalRotation = Quaternion.Identity;
                    child.Transform.LocalScale = Vector3.One;
                    child.MeshRenderer = new MeshRenderer(primMesh, material);
                    node.AddChild(child);
                    RegisterMeshNode(meshNodes, nodeIndex, child);
                }
            }
        }

        for (int i = 0; i < nodeData.Children.Count; i++)
        {
            node.AddChild(BuildSceneNode(nodeData.Children[i], nodes, meshes, accessors, bufferViews, buffers, options, materials, nodeMap, meshNodes));
        }

        return node;
    }

    private static void AttachSkins(
        IReadOnlyList<GltfNode> nodes,
        IReadOnlyList<GltfMesh> meshes,
        IReadOnlyList<GltfSkin> skins,
        Dictionary<int, SceneNode> nodeMap,
        Dictionary<int, List<SceneNode>> meshNodes,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers)
    {
        if (skins.Count == 0 || nodes.Count == 0)
        {
            return;
        }

        var skeletons = new Skeleton?[skins.Count];
        for (int i = 0; i < skins.Count; i++)
        {
            skeletons[i] = BuildSkeleton(skins[i], nodeMap, accessors, bufferViews, buffers);
        }

        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            var node = nodes[nodeIndex];
            if (!node.Skin.HasValue)
            {
                continue;
            }

            int skinIndex = node.Skin.Value;
            if ((uint)skinIndex >= (uint)skeletons.Length)
            {
                continue;
            }

            var skeleton = skeletons[skinIndex];
            if (skeleton == null)
            {
                continue;
            }

            if (!meshNodes.TryGetValue(nodeIndex, out var targets))
            {
                continue;
            }

            float[]? weights = node.Weights;
            if (weights == null && node.Mesh.HasValue && (uint)node.Mesh.Value < (uint)meshes.Count)
            {
                weights = meshes[node.Mesh.Value].Weights;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var meshNode = targets[i];
                var renderer = meshNode.MeshRenderer;
                if (renderer?.Mesh is null)
                {
                    continue;
                }

                var skin = new Skin(renderer.Mesh, skeleton);
                if (weights != null && weights.Length > 0)
                {
                    skin.SetMorphWeights(weights);
                }

                renderer.Skin = skin;
            }
        }
    }

    private static Skeleton? BuildSkeleton(
        GltfSkin skin,
        Dictionary<int, SceneNode> nodeMap,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers)
    {
        if (skin.Joints.Count == 0)
        {
            return null;
        }

        var joints = new SceneNode[skin.Joints.Count];
        for (int i = 0; i < skin.Joints.Count; i++)
        {
            var jointIndex = skin.Joints[i];
            if (!nodeMap.TryGetValue(jointIndex, out var joint))
            {
                return null;
            }

            joints[i] = joint;
        }

        Matrix4x4[]? inverseBind = null;
        if (skin.InverseBindMatrices.HasValue && (uint)skin.InverseBindMatrices.Value < (uint)accessors.Count)
        {
            var accessor = accessors[skin.InverseBindMatrices.Value];
            inverseBind = ReadAccessorMatrices(accessor, bufferViews, buffers);
        }

        return new Skeleton(joints, inverseBind);
    }

    private static Mesh? BuildPrimitiveMesh(
        GltfPrimitive primitive,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        MeshLoadOptions options)
    {
        if (!primitive.Attributes.TryGetValue("POSITION", out var positionAccessorIndex))
        {
            return null;
        }

        if ((uint)positionAccessorIndex >= (uint)accessors.Count)
        {
            return null;
        }

        var positionAccessor = accessors[positionAccessorIndex];
        var positionData = ReadAccessorFloats(positionAccessor, bufferViews, buffers, normalize: false);
        if (positionData.Length == 0)
        {
            return null;
        }

        int vertexCount = positionAccessor.Count;
        var normalData = Array.Empty<float>();
        bool hasNormals = false;
        if (primitive.Attributes.TryGetValue("NORMAL", out var normalAccessorIndex) &&
            (uint)normalAccessorIndex < (uint)accessors.Count)
        {
            var normalAccessor = accessors[normalAccessorIndex];
            normalData = ReadAccessorFloats(normalAccessor, bufferViews, buffers, normalize: normalAccessor.Normalized);
            hasNormals = normalData.Length >= vertexCount * 3;
        }

        var tangentData = Array.Empty<float>();
        bool hasTangents = false;
        if (primitive.Attributes.TryGetValue("TANGENT", out var tangentAccessorIndex) &&
            (uint)tangentAccessorIndex < (uint)accessors.Count)
        {
            var tangentAccessor = accessors[tangentAccessorIndex];
            tangentData = ReadAccessorFloats(tangentAccessor, bufferViews, buffers, normalize: tangentAccessor.Normalized);
            hasTangents = tangentData.Length >= vertexCount * 4;
        }

        var jointData = Array.Empty<float>();
        bool hasJoints = false;
        if (primitive.Attributes.TryGetValue("JOINTS_0", out var jointsAccessorIndex) &&
            (uint)jointsAccessorIndex < (uint)accessors.Count)
        {
            var jointsAccessor = accessors[jointsAccessorIndex];
            jointData = ReadAccessorFloats(jointsAccessor, bufferViews, buffers, normalize: jointsAccessor.Normalized);
            hasJoints = jointData.Length >= vertexCount * 4;
        }

        var weightData = Array.Empty<float>();
        bool hasWeights = false;
        if (primitive.Attributes.TryGetValue("WEIGHTS_0", out var weightsAccessorIndex) &&
            (uint)weightsAccessorIndex < (uint)accessors.Count)
        {
            var weightsAccessor = accessors[weightsAccessorIndex];
            weightData = ReadAccessorFloats(weightsAccessor, bufferViews, buffers, normalize: weightsAccessor.Normalized);
            hasWeights = weightData.Length >= vertexCount * 4;
        }

        var texData = Array.Empty<float>();
        bool hasTexCoords = false;
        if (primitive.Attributes.TryGetValue("TEXCOORD_0", out var texAccessorIndex) &&
            (uint)texAccessorIndex < (uint)accessors.Count)
        {
            var texAccessor = accessors[texAccessorIndex];
            texData = ReadAccessorFloats(texAccessor, bufferViews, buffers, normalize: texAccessor.Normalized);
            hasTexCoords = texData.Length >= vertexCount * 2;
        }

        SKColor[]? colorData = null;
        if (primitive.Attributes.TryGetValue("COLOR_0", out var colorAccessorIndex) &&
            (uint)colorAccessorIndex < (uint)accessors.Count)
        {
            colorData = ReadAccessorColors(accessors[colorAccessorIndex], bufferViews, buffers, options.DefaultColor);
        }

        var localIndices = GetPrimitiveIndices(primitive, accessors, bufferViews, buffers, vertexCount);
        if (localIndices.Count == 0)
        {
            return null;
        }

        var triangles = TriangulateIndices(localIndices, primitive.Mode);
        if (triangles.Count == 0)
        {
            return null;
        }

        var positions = new List<Vector3>(vertexCount);
        var normals = new List<Vector3>(vertexCount);
        var tangents = new List<Vector4>(vertexCount);
        var colors = new List<SKColor>(vertexCount);
        var texcoords = new List<Vector2>(vertexCount);
        var skinWeights = new List<Vector4>(vertexCount);
        var skinIndices = new List<Int4>(vertexCount);
        bool anySkin = false;

        var morphBuffers = ReadMorphTargetBuffers(primitive, vertexCount, accessors, bufferViews, buffers, null, null);
        List<MorphTargetBuilder>? morphBuilders = null;
        AppendMorphTargets(ref morphBuilders, morphBuffers, 0, vertexCount);

        for (int i = 0; i < vertexCount; i++)
        {
            int offset = i * 3;
            var pos = new Vector3(positionData[offset], positionData[offset + 1], positionData[offset + 2]);
            positions.Add(pos);

            var normal = Vector3.UnitY;
            if (hasNormals)
            {
                var n = new Vector3(normalData[offset], normalData[offset + 1], normalData[offset + 2]);
                if (n.LengthSquared() > 1e-8f)
                {
                    n = Vector3.Normalize(n);
                }
                normal = n;
            }
            normals.Add(normal);

            var tangent = Vector4.Zero;
            if (hasTangents)
            {
                int tangentOffset = i * 4;
                tangent = new Vector4(
                    tangentData[tangentOffset],
                    tangentData[tangentOffset + 1],
                    tangentData[tangentOffset + 2],
                    tangentData[tangentOffset + 3]);
            }
            tangents.Add(tangent);

            if (colorData != null && i < colorData.Length)
            {
                colors.Add(colorData[i]);
            }
            else
            {
                colors.Add(options.DefaultColor);
            }

            var uv = Vector2.Zero;
            if (hasTexCoords)
            {
                int texOffset = i * 2;
                uv = new Vector2(texData[texOffset], texData[texOffset + 1]);
            }

            texcoords.Add(uv);

            if (hasJoints && hasWeights)
            {
                int jointOffset = i * 4;
                var joints = new Int4(
                    (int)MathF.Round(jointData[jointOffset]),
                    (int)MathF.Round(jointData[jointOffset + 1]),
                    (int)MathF.Round(jointData[jointOffset + 2]),
                    (int)MathF.Round(jointData[jointOffset + 3]));
                var weights = new Vector4(
                    weightData[jointOffset],
                    weightData[jointOffset + 1],
                    weightData[jointOffset + 2],
                    weightData[jointOffset + 3]);
                var sum = weights.X + weights.Y + weights.Z + weights.W;
                if (sum > 1e-6f)
                {
                    weights *= 1f / sum;
                }
                skinIndices.Add(joints);
                skinWeights.Add(weights);
                anySkin = true;
            }
            else
            {
                skinIndices.Add(Int4.Zero);
                skinWeights.Add(Vector4.Zero);
            }
        }

        IReadOnlyList<Vector3>? normalList = null;
        if (hasNormals && normals.Count == positions.Count)
        {
            normalList = normals;
        }
        else if (!hasNormals && options.GenerateNormals && triangles.Count > 0 && positions.Count > 0)
        {
            normalList = ComputeNormals(positions, triangles);
        }

        IReadOnlyList<Vector4>? tangentList = null;
        if (hasTangents && tangents.Count == positions.Count)
        {
            tangentList = tangents;
        }

        IReadOnlyList<Vector4>? skinWeightList = anySkin && skinWeights.Count == positions.Count ? skinWeights : null;
        IReadOnlyList<Int4>? skinIndexList = anySkin && skinIndices.Count == positions.Count ? skinIndices : null;
        var morphTargets = BuildMorphTargets(morphBuilders);
        var mesh = MeshFactory.CreateFromData(positions, triangles, normalList, colors, texcoords, tangentList, null, skinWeightList, skinIndexList, morphTargets);
        if (options.Processing != null)
        {
            mesh = MeshProcessing.Apply(mesh, options.Processing);
        }

        return mesh;
    }

    private static void RegisterMeshNode(Dictionary<int, List<SceneNode>> meshNodes, int nodeIndex, SceneNode node)
    {
        if (!meshNodes.TryGetValue(nodeIndex, out var list))
        {
            list = new List<SceneNode>();
            meshNodes[nodeIndex] = list;
        }

        list.Add(node);
    }

    private static void ApplyLocalTransform(Transform transform, Matrix4x4 localMatrix)
    {
        if (Matrix4x4.Decompose(localMatrix, out var scale, out var rotation, out var translation))
        {
            transform.LocalPosition = translation;
            transform.LocalRotation = rotation;
            transform.LocalScale = scale;
            return;
        }

        transform.LocalPosition = new Vector3(localMatrix.M41, localMatrix.M42, localMatrix.M43);
        transform.LocalRotation = Quaternion.Identity;
        transform.LocalScale = Vector3.One;
    }

    private static Material ResolveMaterial(IReadOnlyList<Material> materials, int? index)
    {
        if (index.HasValue && (uint)index.Value < (uint)materials.Count)
        {
            return materials[index.Value];
        }

        return Material.Default();
    }

    private static List<AnimationClip> BuildAnimations(
        JsonElement root,
        IReadOnlyList<GltfNode> nodes,
        IReadOnlyList<GltfAccessor> accessors,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers)
    {
        var animations = ReadAnimations(root);
        if (animations.Count == 0)
        {
            return new List<AnimationClip>();
        }

        var clips = new List<AnimationClip>(animations.Count);
        for (int i = 0; i < animations.Count; i++)
        {
            var animation = animations[i];
            var name = string.IsNullOrWhiteSpace(animation.Name) ? $"Animation_{i}" : animation.Name!;
            var clip = new AnimationClip(name);
            var tracks = new Dictionary<int, TransformTrack>();

            foreach (var channel in animation.Channels)
            {
                if ((uint)channel.Node >= (uint)nodes.Count || (uint)channel.Sampler >= (uint)animation.Samplers.Count)
                {
                    continue;
                }

                var sampler = animation.Samplers[channel.Sampler];
                if ((uint)sampler.Input >= (uint)accessors.Count || (uint)sampler.Output >= (uint)accessors.Count)
                {
                    continue;
                }

                var targetName = nodes[channel.Node].Name ?? $"Node_{channel.Node}";
                if (!tracks.TryGetValue(channel.Node, out var track))
                {
                    track = new TransformTrack(targetName);
                    tracks[channel.Node] = track;
                    clip.Tracks.Add(track);
                }

                var input = accessors[sampler.Input];
                var output = accessors[sampler.Output];
                var times = ReadAccessorFloats(input, bufferViews, buffers, normalize: false);
                var values = ReadAccessorFloats(output, bufferViews, buffers, normalize: output.Normalized);

                int keyCount = Math.Min(input.Count, times.Length);
                if (keyCount == 0)
                {
                    continue;
                }

                if (string.Equals(channel.Path, "translation", StringComparison.OrdinalIgnoreCase))
                {
                    for (int k = 0; k < keyCount; k++)
                    {
                        int offset = k * 3;
                        if (offset + 2 >= values.Length)
                        {
                            break;
                        }

                        var value = new Vector3(values[offset], values[offset + 1], values[offset + 2]);
                        track.TranslationKeys.Add(new Keyframe<Vector3>(times[k], value));
                    }
                }
                else if (string.Equals(channel.Path, "rotation", StringComparison.OrdinalIgnoreCase))
                {
                    for (int k = 0; k < keyCount; k++)
                    {
                        int offset = k * 4;
                        if (offset + 3 >= values.Length)
                        {
                            break;
                        }

                        var value = new Quaternion(values[offset], values[offset + 1], values[offset + 2], values[offset + 3]);
                        track.RotationKeys.Add(new Keyframe<Quaternion>(times[k], value));
                    }
                }
                else if (string.Equals(channel.Path, "scale", StringComparison.OrdinalIgnoreCase))
                {
                    for (int k = 0; k < keyCount; k++)
                    {
                        int offset = k * 3;
                        if (offset + 2 >= values.Length)
                        {
                            break;
                        }

                        var value = new Vector3(values[offset], values[offset + 1], values[offset + 2]);
                        track.ScaleKeys.Add(new Keyframe<Vector3>(times[k], value));
                    }
                }
            }

            clip.RecalculateDuration();
            clips.Add(clip);
        }

        return clips;
    }

    private static TextureBinding[] BuildTextureBindings(
        IReadOnlyList<GltfTexture> textures,
        IReadOnlyList<GltfImage> images,
        IReadOnlyList<GltfSampler> samplers,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        string? sourcePath)
    {
        var imageTextures = new Texture2D?[images.Count];
        for (int i = 0; i < images.Count; i++)
        {
            var data = ReadImageBytes(images[i], bufferViews, buffers, sourcePath);
            imageTextures[i] = DecodeTexture(data);
        }

        var samplerBindings = new TextureSampler?[samplers.Count];
        for (int i = 0; i < samplers.Count; i++)
        {
            samplerBindings[i] = BuildSampler(samplers[i]);
        }

        var bindings = new TextureBinding[textures.Count];
        for (int i = 0; i < textures.Count; i++)
        {
            var texture = textures[i];
            Texture2D? image = null;
            TextureSampler? sampler = null;

            if (texture.Source.HasValue && (uint)texture.Source.Value < (uint)imageTextures.Length)
            {
                image = imageTextures[texture.Source.Value];
            }

            if (texture.Sampler.HasValue && (uint)texture.Sampler.Value < (uint)samplerBindings.Length)
            {
                sampler = samplerBindings[texture.Sampler.Value];
            }

            bindings[i] = new TextureBinding(image, sampler);
        }

        return bindings;
    }

    private static List<Material> BuildMaterials(
        IReadOnlyList<GltfMaterial> materials,
        IReadOnlyList<TextureBinding> textures,
        SKColor defaultColor)
    {
        var results = new List<Material>(materials.Count);
        for (int i = 0; i < materials.Count; i++)
        {
            var gltf = materials[i];
            var material = Material.Default();
            material.ShadingModel = MaterialShadingModel.MetallicRoughness;
            material.DoubleSided = gltf.DoubleSided;
            material.BaseColor = gltf.HasBaseColorFactor ? ToColor(gltf.BaseColorFactor) : defaultColor;
            material.Metallic = gltf.MetallicFactor;
            material.Roughness = gltf.RoughnessFactor;
            material.EmissiveColor = ToColor(gltf.EmissiveFactor);
            material.EmissiveStrength = 1f;

            ApplyTextureBinding(gltf.BaseColorTexture, textures, (tex, sampler) =>
            {
                material.BaseColorTexture = tex;
                if (sampler != null)
                {
                    material.BaseColorSampler = CloneSampler(sampler);
                }
            });

            ApplyTextureBinding(gltf.MetallicRoughnessTexture, textures, (tex, sampler) =>
            {
                material.MetallicRoughnessTexture = tex;
                if (sampler != null)
                {
                    material.MetallicRoughnessSampler = CloneSampler(sampler);
                }
            });

            ApplyTextureBinding(gltf.NormalTexture, textures, (tex, sampler) =>
            {
                material.NormalTexture = tex;
                if (sampler != null)
                {
                    material.NormalSampler = CloneSampler(sampler);
                }
                material.NormalStrength = gltf.NormalScale;
            });

            ApplyTextureBinding(gltf.EmissiveTexture, textures, (tex, sampler) =>
            {
                material.EmissiveTexture = tex;
                if (sampler != null)
                {
                    material.EmissiveSampler = CloneSampler(sampler);
                }
            });

            ApplyTextureBinding(gltf.OcclusionTexture, textures, (tex, sampler) =>
            {
                material.OcclusionTexture = tex;
                if (sampler != null)
                {
                    material.OcclusionSampler = CloneSampler(sampler);
                }
                material.OcclusionStrength = gltf.OcclusionStrength;
            });

            results.Add(material);
        }

        return results;
    }

    private static void ApplyTextureBinding(int? index, IReadOnlyList<TextureBinding> textures, Action<Texture2D, TextureSampler?> apply)
    {
        if (!index.HasValue)
        {
            return;
        }

        if ((uint)index.Value >= (uint)textures.Count)
        {
            return;
        }

        var binding = textures[index.Value];
        if (binding.Texture is null)
        {
            return;
        }

        apply(binding.Texture, binding.Sampler);
    }

    private static TextureSampler CloneSampler(TextureSampler sampler)
    {
        return new TextureSampler
        {
            WrapU = sampler.WrapU,
            WrapV = sampler.WrapV,
            Filter = sampler.Filter
        };
    }

    private static SKColor ToColor(Vector4 factor)
    {
        var color = new SKColor(
            (byte)Math.Clamp(factor.X * 255f, 0f, 255f),
            (byte)Math.Clamp(factor.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(factor.Z * 255f, 0f, 255f),
            (byte)Math.Clamp(factor.W * 255f, 0f, 255f));
        return color;
    }

    private static SKColor ToColor(Vector3 factor)
    {
        return new SKColor(
            (byte)Math.Clamp(factor.X * 255f, 0f, 255f),
            (byte)Math.Clamp(factor.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(factor.Z * 255f, 0f, 255f),
            255);
    }

    private static List<GltfMaterial> ReadMaterials(JsonElement root)
    {
        var materials = new List<GltfMaterial>();
        if (!root.TryGetProperty("materials", out var materialsElement) || materialsElement.ValueKind != JsonValueKind.Array)
        {
            return materials;
        }

        foreach (var materialElement in materialsElement.EnumerateArray())
        {
            var material = new GltfMaterial();
            if (materialElement.TryGetProperty("doubleSided", out var doubleSidedElement))
            {
                material.DoubleSided = doubleSidedElement.ValueKind == JsonValueKind.True;
            }

            if (materialElement.TryGetProperty("pbrMetallicRoughness", out var pbrElement) && pbrElement.ValueKind == JsonValueKind.Object)
            {
                if (pbrElement.TryGetProperty("baseColorFactor", out var baseColorElement) && baseColorElement.ValueKind == JsonValueKind.Array)
                {
                    var values = ReadFloatArray(baseColorElement, 4);
                    if (values.Length == 4)
                    {
                        material.BaseColorFactor = new Vector4(values[0], values[1], values[2], values[3]);
                        material.HasBaseColorFactor = true;
                    }
                }

                if (pbrElement.TryGetProperty("metallicFactor", out var metallicElement) && metallicElement.ValueKind == JsonValueKind.Number)
                {
                    material.MetallicFactor = (float)metallicElement.GetDouble();
                }

                if (pbrElement.TryGetProperty("roughnessFactor", out var roughnessElement) && roughnessElement.ValueKind == JsonValueKind.Number)
                {
                    material.RoughnessFactor = (float)roughnessElement.GetDouble();
                }

                if (pbrElement.TryGetProperty("baseColorTexture", out var baseTextureElement))
                {
                    material.BaseColorTexture = ReadTextureIndex(baseTextureElement);
                }

                if (pbrElement.TryGetProperty("metallicRoughnessTexture", out var metallicTextureElement))
                {
                    material.MetallicRoughnessTexture = ReadTextureIndex(metallicTextureElement);
                }
            }

            if (materialElement.TryGetProperty("normalTexture", out var normalTextureElement))
            {
                material.NormalTexture = ReadTextureIndex(normalTextureElement);
                if (normalTextureElement.TryGetProperty("scale", out var scaleElement) && scaleElement.ValueKind == JsonValueKind.Number)
                {
                    material.NormalScale = (float)scaleElement.GetDouble();
                }
            }

            if (materialElement.TryGetProperty("occlusionTexture", out var occlusionTextureElement))
            {
                material.OcclusionTexture = ReadTextureIndex(occlusionTextureElement);
                if (occlusionTextureElement.TryGetProperty("strength", out var strengthElement) && strengthElement.ValueKind == JsonValueKind.Number)
                {
                    material.OcclusionStrength = (float)strengthElement.GetDouble();
                }
            }

            if (materialElement.TryGetProperty("emissiveTexture", out var emissiveTextureElement))
            {
                material.EmissiveTexture = ReadTextureIndex(emissiveTextureElement);
            }

            if (materialElement.TryGetProperty("emissiveFactor", out var emissiveElement) && emissiveElement.ValueKind == JsonValueKind.Array)
            {
                var values = ReadFloatArray(emissiveElement, 3);
                if (values.Length == 3)
                {
                    material.EmissiveFactor = new Vector3(values[0], values[1], values[2]);
                }
            }

            materials.Add(material);
        }

        return materials;
    }

    private static int? ReadTextureIndex(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty("index", out var indexElement) && indexElement.ValueKind == JsonValueKind.Number)
        {
            return indexElement.GetInt32();
        }

        return null;
    }

    private static List<GltfTexture> ReadTextures(JsonElement root)
    {
        var textures = new List<GltfTexture>();
        if (!root.TryGetProperty("textures", out var texturesElement) || texturesElement.ValueKind != JsonValueKind.Array)
        {
            return textures;
        }

        foreach (var element in texturesElement.EnumerateArray())
        {
            int? source = null;
            int? sampler = null;
            if (element.TryGetProperty("source", out var sourceElement) && sourceElement.ValueKind == JsonValueKind.Number)
            {
                source = sourceElement.GetInt32();
            }

            if (element.TryGetProperty("sampler", out var samplerElement) && samplerElement.ValueKind == JsonValueKind.Number)
            {
                sampler = samplerElement.GetInt32();
            }

            textures.Add(new GltfTexture { Source = source, Sampler = sampler });
        }

        return textures;
    }

    private static List<GltfImage> ReadImages(JsonElement root)
    {
        var images = new List<GltfImage>();
        if (!root.TryGetProperty("images", out var imagesElement) || imagesElement.ValueKind != JsonValueKind.Array)
        {
            return images;
        }

        foreach (var element in imagesElement.EnumerateArray())
        {
            var image = new GltfImage();
            if (element.TryGetProperty("uri", out var uriElement) && uriElement.ValueKind == JsonValueKind.String)
            {
                image.Uri = uriElement.GetString();
            }

            if (element.TryGetProperty("bufferView", out var bufferViewElement) && bufferViewElement.ValueKind == JsonValueKind.Number)
            {
                image.BufferView = bufferViewElement.GetInt32();
            }

            if (element.TryGetProperty("mimeType", out var mimeElement) && mimeElement.ValueKind == JsonValueKind.String)
            {
                image.MimeType = mimeElement.GetString();
            }

            images.Add(image);
        }

        return images;
    }

    private static List<GltfSampler> ReadSamplers(JsonElement root)
    {
        var samplers = new List<GltfSampler>();
        if (!root.TryGetProperty("samplers", out var samplersElement) || samplersElement.ValueKind != JsonValueKind.Array)
        {
            return samplers;
        }

        foreach (var element in samplersElement.EnumerateArray())
        {
            samplers.Add(new GltfSampler
            {
                MagFilter = element.TryGetProperty("magFilter", out var magElement) && magElement.ValueKind == JsonValueKind.Number
                    ? magElement.GetInt32()
                    : null,
                MinFilter = element.TryGetProperty("minFilter", out var minElement) && minElement.ValueKind == JsonValueKind.Number
                    ? minElement.GetInt32()
                    : null,
                WrapS = element.TryGetProperty("wrapS", out var wrapSElement) && wrapSElement.ValueKind == JsonValueKind.Number
                    ? wrapSElement.GetInt32()
                    : null,
                WrapT = element.TryGetProperty("wrapT", out var wrapTElement) && wrapTElement.ValueKind == JsonValueKind.Number
                    ? wrapTElement.GetInt32()
                    : null
            });
        }

        return samplers;
    }

    private static List<GltfAnimation> ReadAnimations(JsonElement root)
    {
        var animations = new List<GltfAnimation>();
        if (!root.TryGetProperty("animations", out var animationsElement) || animationsElement.ValueKind != JsonValueKind.Array)
        {
            return animations;
        }

        foreach (var element in animationsElement.EnumerateArray())
        {
            var animation = new GltfAnimation();
            if (element.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
            {
                animation.Name = nameElement.GetString();
            }

            if (element.TryGetProperty("samplers", out var samplerElement) && samplerElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var sampler in samplerElement.EnumerateArray())
                {
                    int input = sampler.TryGetProperty("input", out var inputElement) ? inputElement.GetInt32() : -1;
                    int output = sampler.TryGetProperty("output", out var outputElement) ? outputElement.GetInt32() : -1;
                    string interpolation = sampler.TryGetProperty("interpolation", out var interpElement) && interpElement.ValueKind == JsonValueKind.String
                        ? interpElement.GetString() ?? "LINEAR"
                        : "LINEAR";
                    animation.Samplers.Add(new GltfAnimationSampler
                    {
                        Input = input,
                        Output = output,
                        Interpolation = interpolation
                    });
                }
            }

            if (element.TryGetProperty("channels", out var channelElement) && channelElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var channel in channelElement.EnumerateArray())
                {
                    int sampler = channel.TryGetProperty("sampler", out var samplerIndex) ? samplerIndex.GetInt32() : -1;
                    int node = -1;
                    string path = string.Empty;
                    if (channel.TryGetProperty("target", out var targetElement) && targetElement.ValueKind == JsonValueKind.Object)
                    {
                        if (targetElement.TryGetProperty("node", out var nodeElement) && nodeElement.ValueKind == JsonValueKind.Number)
                        {
                            node = nodeElement.GetInt32();
                        }

                        if (targetElement.TryGetProperty("path", out var pathElement) && pathElement.ValueKind == JsonValueKind.String)
                        {
                            path = pathElement.GetString() ?? string.Empty;
                        }
                    }

                    animation.Channels.Add(new GltfAnimationChannel
                    {
                        Sampler = sampler,
                        Node = node,
                        Path = path
                    });
                }
            }

            animations.Add(animation);
        }

        return animations;
    }

    private static byte[]? ReadImageBytes(
        GltfImage image,
        IReadOnlyList<GltfBufferView> bufferViews,
        IReadOnlyList<byte[]> buffers,
        string? sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(image.Uri))
        {
            return LoadUriBuffer(image.Uri!, sourcePath != null ? Path.GetDirectoryName(sourcePath) : null);
        }

        if (image.BufferView.HasValue && (uint)image.BufferView.Value < (uint)bufferViews.Count)
        {
            var view = bufferViews[image.BufferView.Value];
            if ((uint)view.Buffer >= (uint)buffers.Count)
            {
                return null;
            }

            var buffer = buffers[view.Buffer];
            if (view.ByteOffset + view.ByteLength > buffer.Length)
            {
                return null;
            }

            var data = new byte[view.ByteLength];
            Buffer.BlockCopy(buffer, view.ByteOffset, data, 0, view.ByteLength);
            return data;
        }

        return null;
    }

    private static Texture2D? DecodeTexture(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(data);
        var bitmap = SKBitmap.Decode(stream);
        if (bitmap is null)
        {
            return null;
        }

        return new Texture2D(bitmap);
    }

    private static TextureSampler BuildSampler(GltfSampler sampler)
    {
        return new TextureSampler
        {
            WrapU = MapWrap(sampler.WrapS),
            WrapV = MapWrap(sampler.WrapT),
            Filter = MapFilter(sampler.MagFilter ?? sampler.MinFilter)
        };
    }

    private static TextureWrap MapWrap(int? wrap)
    {
        return wrap switch
        {
            33071 => TextureWrap.Clamp,
            33648 => TextureWrap.Mirror,
            _ => TextureWrap.Repeat
        };
    }

    private static TextureFilter MapFilter(int? filter)
    {
        return filter switch
        {
            9728 => TextureFilter.Nearest,
            9984 => TextureFilter.Nearest,
            9986 => TextureFilter.Nearest,
            _ => TextureFilter.Bilinear
        };
    }

    private static IReadOnlyList<int> ResolveRootNodes(int nodeCount, IReadOnlyList<GltfScene> scenes, int sceneIndex)
    {
        if (scenes.Count == 0)
        {
            var fallback = new List<int>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                fallback.Add(i);
            }
            return fallback;
        }

        if ((uint)sceneIndex >= (uint)scenes.Count)
        {
            sceneIndex = 0;
        }

        return scenes[sceneIndex].Nodes;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var buffer))
        {
            return buffer.ToArray();
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool IsGlb(byte[] data)
    {
        if (data.Length < 12)
        {
            return false;
        }

        return data[0] == (byte)'g' && data[1] == (byte)'l' && data[2] == (byte)'T' && data[3] == (byte)'F';
    }

    private static void ParseGlb(byte[] data, out byte[] jsonBytes, out byte[]? binBytes)
    {
        if (data.Length < 12)
        {
            throw new InvalidDataException("Invalid GLB header.");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (version != 2)
        {
            throw new NotSupportedException($"Unsupported GLB version {version}.");
        }

        int offset = 12;
        jsonBytes = Array.Empty<byte>();
        binBytes = null;

        while (offset + 8 <= data.Length)
        {
            int chunkLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
            int chunkType = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4, 4));
            offset += 8;

            if (offset + chunkLength > data.Length)
            {
                throw new InvalidDataException("GLB chunk exceeds file length.");
            }

            var chunkData = data.AsSpan(offset, chunkLength).ToArray();
            if (chunkType == 0x4E4F534A)
            {
                jsonBytes = chunkData;
            }
            else if (chunkType == 0x004E4942)
            {
                binBytes = chunkData;
            }

            offset += chunkLength;
        }

        if (jsonBytes.Length == 0)
        {
            throw new InvalidDataException("GLB missing JSON chunk.");
        }
    }

    private static List<byte[]> ReadBuffers(JsonElement root, byte[]? binChunk, string? sourcePath)
    {
        var buffers = new List<byte[]>();
        if (!root.TryGetProperty("buffers", out var buffersElement) || buffersElement.ValueKind != JsonValueKind.Array)
        {
            return buffers;
        }

        string? baseDir = null;
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            baseDir = Path.GetDirectoryName(sourcePath);
        }

        int bufferIndex = 0;
        foreach (var bufferElement in buffersElement.EnumerateArray())
        {
            byte[] bufferData;
            if (bufferElement.TryGetProperty("uri", out var uriElement) && uriElement.ValueKind == JsonValueKind.String)
            {
                var uri = uriElement.GetString() ?? string.Empty;
                bufferData = LoadUriBuffer(uri, baseDir);
            }
            else if (bufferIndex == 0 && binChunk != null)
            {
                bufferData = binChunk;
            }
            else
            {
                throw new NotSupportedException("glTF buffer missing uri and binary chunk.");
            }

            buffers.Add(bufferData);
            bufferIndex++;
        }

        return buffers;
    }

    private static byte[] LoadUriBuffer(string uri, string? baseDir)
    {
        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = uri.IndexOf(',');
            if (comma < 0)
            {
                throw new InvalidDataException("Invalid data URI.");
            }

            var base64 = uri[(comma + 1)..];
            return Convert.FromBase64String(base64);
        }

        if (string.IsNullOrWhiteSpace(baseDir))
        {
            throw new NotSupportedException("External glTF buffers require SourcePath.");
        }

        var path = Path.Combine(baseDir, uri);
        return File.ReadAllBytes(path);
    }

    private static List<GltfBufferView> ReadBufferViews(JsonElement root)
    {
        var views = new List<GltfBufferView>();
        if (!root.TryGetProperty("bufferViews", out var viewsElement) || viewsElement.ValueKind != JsonValueKind.Array)
        {
            return views;
        }

        foreach (var viewElement in viewsElement.EnumerateArray())
        {
            int buffer = viewElement.TryGetProperty("buffer", out var bufferElement) ? bufferElement.GetInt32() : 0;
            int byteOffset = viewElement.TryGetProperty("byteOffset", out var offsetElement) ? offsetElement.GetInt32() : 0;
            int byteLength = viewElement.TryGetProperty("byteLength", out var lengthElement) ? lengthElement.GetInt32() : 0;
            int? byteStride = null;
            if (viewElement.TryGetProperty("byteStride", out var strideElement) && strideElement.ValueKind == JsonValueKind.Number)
            {
                byteStride = strideElement.GetInt32();
            }

            views.Add(new GltfBufferView
            {
                Buffer = buffer,
                ByteOffset = byteOffset,
                ByteLength = byteLength,
                ByteStride = byteStride
            });
        }

        return views;
    }

    private static List<GltfAccessor> ReadAccessors(JsonElement root)
    {
        var accessors = new List<GltfAccessor>();
        if (!root.TryGetProperty("accessors", out var accessorElement) || accessorElement.ValueKind != JsonValueKind.Array)
        {
            return accessors;
        }

        foreach (var element in accessorElement.EnumerateArray())
        {
            int bufferView = element.TryGetProperty("bufferView", out var viewElement) ? viewElement.GetInt32() : -1;
            int byteOffset = element.TryGetProperty("byteOffset", out var offsetElement) ? offsetElement.GetInt32() : 0;
            int componentType = element.TryGetProperty("componentType", out var componentElement) ? componentElement.GetInt32() : 0;
            int count = element.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 0;
            string type = element.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "SCALAR" : "SCALAR";
            bool normalized = element.TryGetProperty("normalized", out var normalizedElement) && normalizedElement.ValueKind == JsonValueKind.True;

            accessors.Add(new GltfAccessor
            {
                BufferView = bufferView,
                ByteOffset = byteOffset,
                ComponentType = componentType,
                Count = count,
                Type = type,
                Normalized = normalized
            });
        }

        return accessors;
    }

    private static List<GltfMesh> ReadMeshes(JsonElement root)
    {
        var meshes = new List<GltfMesh>();
        if (!root.TryGetProperty("meshes", out var meshesElement) || meshesElement.ValueKind != JsonValueKind.Array)
        {
            return meshes;
        }

        foreach (var meshElement in meshesElement.EnumerateArray())
        {
            var primitives = new List<GltfPrimitive>();
            if (meshElement.TryGetProperty("primitives", out var primitivesElement) && primitivesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var primitiveElement in primitivesElement.EnumerateArray())
                {
                    var attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    if (primitiveElement.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var attr in attributesElement.EnumerateObject())
                        {
                            if (attr.Value.ValueKind == JsonValueKind.Number)
                            {
                                attributes[attr.Name] = attr.Value.GetInt32();
                            }
                        }
                    }

                    int? indices = null;
                    if (primitiveElement.TryGetProperty("indices", out var indicesElement) && indicesElement.ValueKind == JsonValueKind.Number)
                    {
                        indices = indicesElement.GetInt32();
                    }

                    int mode = 4;
                    if (primitiveElement.TryGetProperty("mode", out var modeElement) && modeElement.ValueKind == JsonValueKind.Number)
                    {
                        mode = modeElement.GetInt32();
                    }

                    int? material = null;
                    if (primitiveElement.TryGetProperty("material", out var materialElement) && materialElement.ValueKind == JsonValueKind.Number)
                    {
                        material = materialElement.GetInt32();
                    }

                    var targets = new List<Dictionary<string, int>>();
                    if (primitiveElement.TryGetProperty("targets", out var targetsElement) && targetsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var targetElement in targetsElement.EnumerateArray())
                        {
                            if (targetElement.ValueKind != JsonValueKind.Object)
                            {
                                continue;
                            }

                            var targetAttrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var attr in targetElement.EnumerateObject())
                            {
                                if (attr.Value.ValueKind == JsonValueKind.Number)
                                {
                                    targetAttrs[attr.Name] = attr.Value.GetInt32();
                                }
                            }

                            targets.Add(targetAttrs);
                        }
                    }

                    primitives.Add(new GltfPrimitive
                    {
                        Attributes = attributes,
                        Indices = indices,
                        Mode = mode,
                        Material = material,
                        Targets = targets
                    });
                }
            }

            float[]? weights = null;
            if (meshElement.TryGetProperty("weights", out var weightsElement) && weightsElement.ValueKind == JsonValueKind.Array)
            {
                weights = ReadFloatArray(weightsElement);
            }

            meshes.Add(new GltfMesh { Primitives = primitives, Weights = weights });
        }

        return meshes;
    }

    private static List<GltfNode> ReadNodes(JsonElement root)
    {
        var nodes = new List<GltfNode>();
        if (!root.TryGetProperty("nodes", out var nodesElement) || nodesElement.ValueKind != JsonValueKind.Array)
        {
            return nodes;
        }

        foreach (var nodeElement in nodesElement.EnumerateArray())
        {
            var node = new GltfNode();
            if (nodeElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
            {
                node.Name = nameElement.GetString();
            }
            if (nodeElement.TryGetProperty("mesh", out var meshElement) && meshElement.ValueKind == JsonValueKind.Number)
            {
                node.Mesh = meshElement.GetInt32();
            }
            if (nodeElement.TryGetProperty("skin", out var skinElement) && skinElement.ValueKind == JsonValueKind.Number)
            {
                node.Skin = skinElement.GetInt32();
            }
            if (nodeElement.TryGetProperty("weights", out var weightsElement) && weightsElement.ValueKind == JsonValueKind.Array)
            {
                node.Weights = ReadFloatArray(weightsElement);
            }

            if (nodeElement.TryGetProperty("children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in childrenElement.EnumerateArray())
                {
                    if (child.ValueKind == JsonValueKind.Number)
                    {
                        node.Children.Add(child.GetInt32());
                    }
                }
            }

            node.LocalMatrix = ReadNodeTransform(nodeElement);
            nodes.Add(node);
        }

        return nodes;
    }

    private static List<GltfSkin> ReadSkins(JsonElement root)
    {
        var skins = new List<GltfSkin>();
        if (!root.TryGetProperty("skins", out var skinsElement) || skinsElement.ValueKind != JsonValueKind.Array)
        {
            return skins;
        }

        foreach (var skinElement in skinsElement.EnumerateArray())
        {
            var skin = new GltfSkin();
            if (skinElement.TryGetProperty("joints", out var jointsElement) && jointsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var joint in jointsElement.EnumerateArray())
                {
                    if (joint.ValueKind == JsonValueKind.Number)
                    {
                        skin.Joints.Add(joint.GetInt32());
                    }
                }
            }

            if (skinElement.TryGetProperty("inverseBindMatrices", out var inverseElement) && inverseElement.ValueKind == JsonValueKind.Number)
            {
                skin.InverseBindMatrices = inverseElement.GetInt32();
            }

            if (skinElement.TryGetProperty("skeleton", out var skeletonElement) && skeletonElement.ValueKind == JsonValueKind.Number)
            {
                skin.Skeleton = skeletonElement.GetInt32();
            }

            skins.Add(skin);
        }

        return skins;
    }

    private static List<GltfScene> ReadScenes(JsonElement root)
    {
        var scenes = new List<GltfScene>();
        if (!root.TryGetProperty("scenes", out var scenesElement) || scenesElement.ValueKind != JsonValueKind.Array)
        {
            return scenes;
        }

        foreach (var sceneElement in scenesElement.EnumerateArray())
        {
            var nodes = new List<int>();
            if (sceneElement.TryGetProperty("nodes", out var nodesElement) && nodesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in nodesElement.EnumerateArray())
                {
                    if (node.ValueKind == JsonValueKind.Number)
                    {
                        nodes.Add(node.GetInt32());
                    }
                }
            }

            scenes.Add(new GltfScene { Nodes = nodes });
        }

        return scenes;
    }

    private static Matrix4x4 ReadNodeTransform(JsonElement nodeElement)
    {
        if (nodeElement.TryGetProperty("matrix", out var matrixElement) && matrixElement.ValueKind == JsonValueKind.Array)
        {
            var values = ReadFloatArray(matrixElement, 16);
            if (values.Length == 16)
            {
                return new Matrix4x4(
                    values[0], values[4], values[8], values[12],
                    values[1], values[5], values[9], values[13],
                    values[2], values[6], values[10], values[14],
                    values[3], values[7], values[11], values[15]);
            }
        }

        var translation = Vector3.Zero;
        if (nodeElement.TryGetProperty("translation", out var translationElement) && translationElement.ValueKind == JsonValueKind.Array)
        {
            var values = ReadFloatArray(translationElement, 3);
            if (values.Length == 3)
            {
                translation = new Vector3(values[0], values[1], values[2]);
            }
        }

        var scale = Vector3.One;
        if (nodeElement.TryGetProperty("scale", out var scaleElement) && scaleElement.ValueKind == JsonValueKind.Array)
        {
            var values = ReadFloatArray(scaleElement, 3);
            if (values.Length == 3)
            {
                scale = new Vector3(values[0], values[1], values[2]);
            }
        }

        var rotation = Quaternion.Identity;
        if (nodeElement.TryGetProperty("rotation", out var rotationElement) && rotationElement.ValueKind == JsonValueKind.Array)
        {
            var values = ReadFloatArray(rotationElement, 4);
            if (values.Length == 4)
            {
                rotation = new Quaternion(values[0], values[1], values[2], values[3]);
            }
        }

        return Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(rotation)
            * Matrix4x4.CreateTranslation(translation);
    }

    private static float[] ReadFloatArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<float>();
        }

        var list = new List<float>();
        foreach (var value in element.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                list.Add((float)value.GetDouble());
            }
        }

        return list.ToArray();
    }

    private static float[] ReadFloatArray(JsonElement element, int expected)
    {
        var list = new float[expected];
        int index = 0;
        foreach (var value in element.EnumerateArray())
        {
            if (index >= expected)
            {
                break;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                list[index++] = (float)value.GetDouble();
            }
        }

        if (index == expected)
        {
            return list;
        }

        Array.Resize(ref list, index);
        return list;
    }

    private static float[] ReadAccessorFloats(GltfAccessor accessor, IReadOnlyList<GltfBufferView> bufferViews, IReadOnlyList<byte[]> buffers, bool normalize)
    {
        if (accessor.BufferView < 0 || (uint)accessor.BufferView >= (uint)bufferViews.Count)
        {
            return Array.Empty<float>();
        }

        int componentCount = GetComponentCount(accessor.Type);
        int componentSize = GetComponentSize(accessor.ComponentType);
        if (componentCount <= 0 || componentSize <= 0 || accessor.Count <= 0)
        {
            return Array.Empty<float>();
        }

        var view = bufferViews[accessor.BufferView];
        if ((uint)view.Buffer >= (uint)buffers.Count)
        {
            return Array.Empty<float>();
        }

        var buffer = buffers[view.Buffer];
        int stride = view.ByteStride ?? componentCount * componentSize;
        int baseOffset = view.ByteOffset + accessor.ByteOffset;

        var values = new float[accessor.Count * componentCount];
        int valueIndex = 0;
        for (int i = 0; i < accessor.Count; i++)
        {
            int elementOffset = baseOffset + i * stride;
            for (int c = 0; c < componentCount; c++)
            {
                int offset = elementOffset + c * componentSize;
                var number = ReadComponent(buffer, offset, accessor.ComponentType);
                if (normalize)
                {
                    number = NormalizeComponent(number, accessor.ComponentType);
                }
                values[valueIndex++] = (float)number;
            }
        }

        return values;
    }

    private static Matrix4x4[] ReadAccessorMatrices(GltfAccessor accessor, IReadOnlyList<GltfBufferView> bufferViews, IReadOnlyList<byte[]> buffers)
    {
        var values = ReadAccessorFloats(accessor, bufferViews, buffers, normalize: false);
        if (values.Length < accessor.Count * 16)
        {
            return Array.Empty<Matrix4x4>();
        }

        var matrices = new Matrix4x4[accessor.Count];
        for (int i = 0; i < accessor.Count; i++)
        {
            int baseOffset = i * 16;
            matrices[i] = new Matrix4x4(
                values[baseOffset + 0], values[baseOffset + 4], values[baseOffset + 8], values[baseOffset + 12],
                values[baseOffset + 1], values[baseOffset + 5], values[baseOffset + 9], values[baseOffset + 13],
                values[baseOffset + 2], values[baseOffset + 6], values[baseOffset + 10], values[baseOffset + 14],
                values[baseOffset + 3], values[baseOffset + 7], values[baseOffset + 11], values[baseOffset + 15]);
        }

        return matrices;
    }

    private static SKColor[] ReadAccessorColors(GltfAccessor accessor, IReadOnlyList<GltfBufferView> bufferViews, IReadOnlyList<byte[]> buffers, SKColor fallback)
    {
        if (accessor.BufferView < 0 || (uint)accessor.BufferView >= (uint)bufferViews.Count)
        {
            return Array.Empty<SKColor>();
        }

        int componentCount = GetComponentCount(accessor.Type);
        if (componentCount < 3)
        {
            return Array.Empty<SKColor>();
        }

        int componentSize = GetComponentSize(accessor.ComponentType);
        if (componentSize <= 0 || accessor.Count <= 0)
        {
            return Array.Empty<SKColor>();
        }

        var view = bufferViews[accessor.BufferView];
        if ((uint)view.Buffer >= (uint)buffers.Count)
        {
            return Array.Empty<SKColor>();
        }

        var buffer = buffers[view.Buffer];
        int stride = view.ByteStride ?? componentCount * componentSize;
        int baseOffset = view.ByteOffset + accessor.ByteOffset;
        var colors = new SKColor[accessor.Count];

        for (int i = 0; i < accessor.Count; i++)
        {
            int elementOffset = baseOffset + i * stride;
            double r = ReadComponent(buffer, elementOffset, accessor.ComponentType);
            double g = ReadComponent(buffer, elementOffset + componentSize, accessor.ComponentType);
            double b = ReadComponent(buffer, elementOffset + componentSize * 2, accessor.ComponentType);
            byte rc = ConvertColorComponent(r, accessor.ComponentType, accessor.Normalized);
            byte gc = ConvertColorComponent(g, accessor.ComponentType, accessor.Normalized);
            byte bc = ConvertColorComponent(b, accessor.ComponentType, accessor.Normalized);
            byte ac;
            if (componentCount > 3)
            {
                double a = ReadComponent(buffer, elementOffset + componentSize * 3, accessor.ComponentType);
                ac = ConvertColorComponent(a, accessor.ComponentType, accessor.Normalized);
            }
            else
            {
                ac = fallback.Alpha;
            }

            colors[i] = new SKColor(rc, gc, bc, ac);
        }

        return colors;
    }

    private static List<int> ReadAccessorIndices(GltfAccessor accessor, IReadOnlyList<GltfBufferView> bufferViews, IReadOnlyList<byte[]> buffers)
    {
        var list = new List<int>();
        if (accessor.BufferView < 0 || (uint)accessor.BufferView >= (uint)bufferViews.Count)
        {
            return list;
        }

        int componentSize = GetComponentSize(accessor.ComponentType);
        if (componentSize <= 0 || accessor.Count <= 0)
        {
            return list;
        }

        var view = bufferViews[accessor.BufferView];
        if ((uint)view.Buffer >= (uint)buffers.Count)
        {
            return list;
        }

        var buffer = buffers[view.Buffer];
        int stride = view.ByteStride ?? componentSize;
        int baseOffset = view.ByteOffset + accessor.ByteOffset;
        list.Capacity = accessor.Count;

        for (int i = 0; i < accessor.Count; i++)
        {
            int offset = baseOffset + i * stride;
            var value = ReadComponent(buffer, offset, accessor.ComponentType);
            list.Add((int)Math.Round(value));
        }

        return list;
    }

    private static int GetComponentCount(string type)
    {
        return type.ToUpperInvariant() switch
        {
            "SCALAR" => 1,
            "VEC2" => 2,
            "VEC3" => 3,
            "VEC4" => 4,
            "MAT4" => 16,
            _ => 0
        };
    }

    private static int GetComponentSize(int componentType)
    {
        return componentType switch
        {
            5120 => 1,
            5121 => 1,
            5122 => 2,
            5123 => 2,
            5125 => 4,
            5126 => 4,
            _ => 0
        };
    }

    private static double ReadComponent(byte[] buffer, int offset, int componentType)
    {
        return componentType switch
        {
            5120 => unchecked((sbyte)buffer[offset]),
            5121 => buffer[offset],
            5122 => BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset, 2)),
            5123 => BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset, 2)),
            5125 => BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, 4)),
            5126 => BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(offset, 4)),
            _ => 0d
        };
    }

    private static byte ConvertColorComponent(double value, int componentType, bool normalized)
    {
        if (componentType == 5126)
        {
            return (byte)Math.Clamp(MathF.Round((float)value * 255f), 0f, 255f);
        }

        var max = GetMaxForType(componentType);

        if (normalized)
        {
            if (componentType == 5120 || componentType == 5122)
            {
                var normalizedValue = (value / max) * 0.5 + 0.5;
                return (byte)Math.Clamp(MathF.Round((float)(normalizedValue * 255d)), 0f, 255f);
            }

            return (byte)Math.Clamp(MathF.Round((float)(value / max * 255d)), 0f, 255f);
        }

        if (max <= 255d)
        {
            return (byte)Math.Clamp(MathF.Round((float)value), 0f, 255f);
        }

        return (byte)Math.Clamp(MathF.Round((float)(value / max * 255d)), 0f, 255f);
    }

    private static double NormalizeComponent(double value, int componentType)
    {
        var max = GetMaxForType(componentType);
        if (max <= 0d)
        {
            return value;
        }

        if (componentType == 5120 || componentType == 5122)
        {
            return value / max;
        }

        return value / max;
    }

    private static double GetMaxForType(int componentType)
    {
        return componentType switch
        {
            5120 => 127d,
            5121 => 255d,
            5122 => 32767d,
            5123 => 65535d,
            5125 => 4294967295d,
            _ => 255d
        };
    }

    private static Vector3[] ComputeNormals(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices)
    {
        var normals = new Vector3[positions.Count];

        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if ((uint)i0 >= (uint)positions.Count || (uint)i1 >= (uint)positions.Count || (uint)i2 >= (uint)positions.Count)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var n = Vector3.Cross(p1 - p0, p2 - p0);

            normals[i0] += n;
            normals[i1] += n;
            normals[i2] += n;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            var n = normals[i];
            if (n.LengthSquared() > 1e-8f)
            {
                normals[i] = Vector3.Normalize(n);
            }
            else
            {
                normals[i] = Vector3.UnitY;
            }
        }

        return normals;
    }

    private sealed class GltfBufferView
    {
        public int Buffer { get; set; }
        public int ByteOffset { get; set; }
        public int ByteLength { get; set; }
        public int? ByteStride { get; set; }
    }

    private sealed class GltfAccessor
    {
        public int BufferView { get; set; }
        public int ByteOffset { get; set; }
        public int ComponentType { get; set; }
        public int Count { get; set; }
        public string Type { get; set; } = "SCALAR";
        public bool Normalized { get; set; }
    }

    private sealed class GltfPrimitive
    {
        public Dictionary<string, int> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int? Indices { get; set; }
        public int Mode { get; set; } = 4;
        public int? Material { get; set; }
        public List<Dictionary<string, int>> Targets { get; set; } = new();
    }

    private sealed class MorphTargetBuffers
    {
        public Vector3[]? PositionDeltas { get; init; }
        public Vector3[]? NormalDeltas { get; init; }
        public Vector4[]? TangentDeltas { get; init; }
    }

    private sealed class MorphTargetBuilder
    {
        public List<Vector3>? PositionDeltas { get; private set; }
        public List<Vector3>? NormalDeltas { get; private set; }
        public List<Vector4>? TangentDeltas { get; private set; }

        public void EnsurePositions(int existingCount)
        {
            if (PositionDeltas != null)
            {
                return;
            }

            PositionDeltas = new List<Vector3>(existingCount);
            for (int i = 0; i < existingCount; i++)
            {
                PositionDeltas.Add(Vector3.Zero);
            }
        }

        public void EnsureNormals(int existingCount)
        {
            if (NormalDeltas != null)
            {
                return;
            }

            NormalDeltas = new List<Vector3>(existingCount);
            for (int i = 0; i < existingCount; i++)
            {
                NormalDeltas.Add(Vector3.Zero);
            }
        }

        public void EnsureTangents(int existingCount)
        {
            if (TangentDeltas != null)
            {
                return;
            }

            TangentDeltas = new List<Vector4>(existingCount);
            for (int i = 0; i < existingCount; i++)
            {
                TangentDeltas.Add(Vector4.Zero);
            }
        }

        public void AppendPositions(Vector3[]? deltas, int existingCount, int vertexCount)
        {
            if (deltas != null)
            {
                EnsurePositions(existingCount);
                PositionDeltas!.AddRange(deltas);
                return;
            }

            if (PositionDeltas != null)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    PositionDeltas.Add(Vector3.Zero);
                }
            }
        }

        public void AppendNormals(Vector3[]? deltas, int existingCount, int vertexCount)
        {
            if (deltas != null)
            {
                EnsureNormals(existingCount);
                NormalDeltas!.AddRange(deltas);
                return;
            }

            if (NormalDeltas != null)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    NormalDeltas.Add(Vector3.Zero);
                }
            }
        }

        public void AppendTangents(Vector4[]? deltas, int existingCount, int vertexCount)
        {
            if (deltas != null)
            {
                EnsureTangents(existingCount);
                TangentDeltas!.AddRange(deltas);
                return;
            }

            if (TangentDeltas != null)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    TangentDeltas.Add(Vector4.Zero);
                }
            }
        }

        public MeshMorphTarget Build(string? name = null)
        {
            return new MeshMorphTarget(
                name,
                PositionDeltas?.ToArray(),
                NormalDeltas?.ToArray(),
                TangentDeltas?.ToArray());
        }
    }

    private sealed class GltfMesh
    {
        public List<GltfPrimitive> Primitives { get; set; } = new();
        public float[]? Weights { get; set; }
    }

    private sealed class GltfNode
    {
        public int? Mesh { get; set; }
        public int? Skin { get; set; }
        public List<int> Children { get; } = new();
        public Matrix4x4 LocalMatrix { get; set; } = Matrix4x4.Identity;
        public string? Name { get; set; }
        public float[]? Weights { get; set; }
    }

    private sealed class GltfSkin
    {
        public List<int> Joints { get; } = new();
        public int? InverseBindMatrices { get; set; }
        public int? Skeleton { get; set; }
    }

    private sealed class GltfScene
    {
        public List<int> Nodes { get; set; } = new();
    }

    private sealed class GltfMaterial
    {
        public Vector4 BaseColorFactor { get; set; } = Vector4.One;
        public bool HasBaseColorFactor { get; set; }
        public float MetallicFactor { get; set; } = 1f;
        public float RoughnessFactor { get; set; } = 1f;
        public int? BaseColorTexture { get; set; }
        public int? MetallicRoughnessTexture { get; set; }
        public int? NormalTexture { get; set; }
        public float NormalScale { get; set; } = 1f;
        public int? OcclusionTexture { get; set; }
        public float OcclusionStrength { get; set; } = 1f;
        public int? EmissiveTexture { get; set; }
        public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;
        public bool DoubleSided { get; set; }
    }

    private sealed class GltfTexture
    {
        public int? Source { get; set; }
        public int? Sampler { get; set; }
    }

    private sealed class GltfImage
    {
        public string? Uri { get; set; }
        public int? BufferView { get; set; }
        public string? MimeType { get; set; }
    }

    private sealed class GltfSampler
    {
        public int? MagFilter { get; set; }
        public int? MinFilter { get; set; }
        public int? WrapS { get; set; }
        public int? WrapT { get; set; }
    }

    private sealed class GltfAnimation
    {
        public string? Name { get; set; }
        public List<GltfAnimationSampler> Samplers { get; } = new();
        public List<GltfAnimationChannel> Channels { get; } = new();
    }

    private sealed class GltfAnimationSampler
    {
        public int Input { get; set; }
        public int Output { get; set; }
        public string Interpolation { get; set; } = "LINEAR";
    }

    private sealed class GltfAnimationChannel
    {
        public int Sampler { get; set; }
        public int Node { get; set; }
        public string Path { get; set; } = string.Empty;
    }

    private readonly record struct TextureBinding(Texture2D? Texture, TextureSampler? Sampler);
}
