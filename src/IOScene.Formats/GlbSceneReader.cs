using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace IOScene.Formats;

/// <summary>
/// Imports standard glTF 2.0 Binary (.glb) files into a scene with one model: one geometry per
/// mesh primitive, one element per node that draws a mesh, and one instance per primitive of that node, with the
/// node's world transform converted from glTF's Y-up to the scene's Z-up.
/// </summary>
public static class GlbSceneReader
{
    private const uint GlbMagic = 0x46546C67; // "glTF"
    private const uint ChunkJson = 0x4E4F534A; // "JSON"
    private const uint ChunkBin = 0x004E4942; // "BIN\0"

    /// <summary>Reads a GLB file into an IOScene document.</summary>
    /// <param name="filePath">Path to the file.</param>
    public static SceneDocument Load(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var fs = File.OpenRead(filePath);
        return Load(fs, Path.GetFileNameWithoutExtension(filePath), Path.GetFileName(filePath), filePath);
    }

    /// <summary>Reads a GLB from a seekable stream. The stream remains open.</summary>
    /// <param name="stream">Stream positioned at the GLB header.</param>
    /// <param name="documentTitle">Name of the resulting scene.</param>
    public static SceneDocument Load(Stream stream, string documentTitle = "Imported GLB Model")
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Load(stream, documentTitle, "model", null);
    }

    private static SceneDocument Load(Stream stream, string documentTitle, string modelKey, string? path)
    {
        if (!stream.CanSeek) throw new NotSupportedException("GLB input must be seekable.");
        long start = stream.Position;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Header: 12 bytes
        uint magic = reader.ReadUInt32();
        if (magic != GlbMagic)
        {
            throw new InvalidDataException("Invalid GLB header: magic signature mismatch.");
        }

        uint version = reader.ReadUInt32();
        if (version != 2)
        {
            throw new NotSupportedException($"Unsupported glTF version: {version}. Only glTF 2.0 is supported.");
        }

        uint totalLength = reader.ReadUInt32();
        if (totalLength < 20 || totalLength > stream.Length - start)
            throw new InvalidDataException("The GLB length is outside the input stream.");
        long containerEnd = start + totalLength;

        // Chunk 0: JSON
        uint jsonChunkLength = reader.ReadUInt32();
        uint jsonChunkType = reader.ReadUInt32();
        if (jsonChunkType != ChunkJson)
        {
            throw new InvalidDataException("Expected JSON chunk as first chunk in GLB file.");
        }

        byte[] jsonBytes = ReadChunk(jsonChunkLength);

        // Chunk 1: BIN (optional)
        byte[]? binBuffer = null;
        if (stream.Position < containerEnd)
        {
            if (containerEnd - stream.Position < 8)
                throw new InvalidDataException("The GLB chunk header is truncated.");
            uint binChunkLength = reader.ReadUInt32();
            uint binChunkType = reader.ReadUInt32();
            if (binChunkType == ChunkBin)
            {
                binBuffer = ReadChunk(binChunkLength);
            }
        }

        byte[] ReadChunk(uint length)
        {
            if (length > Array.MaxLength || length > containerEnd - stream.Position || length % 4 != 0)
                throw new InvalidDataException("The GLB chunk length is outside the container.");
            byte[] data = reader.ReadBytes((int)length);
            if (data.Length != length) throw new EndOfStreamException("The GLB chunk is truncated.");
            return data;
        }

        var doc = new SceneDocument { Title = documentTitle };
        doc.Models.Add(new SceneModel { Id = 1, Key = modelKey, Name = documentTitle, Path = path, SourceFormat = "glb" });

        using var jsonDoc = JsonDocument.Parse(jsonBytes);
        var root = jsonDoc.RootElement;

        // Parse accessors & bufferViews
        if (!root.TryGetProperty("accessors", out var accessorsEl) ||
            !root.TryGetProperty("bufferViews", out var bufferViewsEl) ||
            binBuffer is null)
        {
            return doc;
        }

        var bufferViews = bufferViewsEl.EnumerateArray().ToArray();
        var accessors = accessorsEl.EnumerateArray().ToArray();

        // Parse Materials
        var materials = new List<SceneMaterial>();
        if (root.TryGetProperty("materials", out var materialsEl))
        {
            int matIdx = 0;
            foreach (var matEl in materialsEl.EnumerateArray())
            {
                var mat = new SceneMaterial
                {
                    Id = matIdx++,
                    Name = matEl.TryGetProperty("name", out var mn) ? mn.GetString() ?? "Material" : $"Material {matIdx}"
                };

                if (matEl.TryGetProperty("pbrMetallicRoughness", out var pbr))
                {
                    if (pbr.TryGetProperty("baseColorFactor", out var bcf) && bcf.GetArrayLength() >= 4)
                    {
                        // glTF's factor is linear; a scene colour has sRGB channels and a straight linear alpha.
                        var arr = bcf.EnumerateArray().ToArray();
                        mat.Color = new RgbaColor(Srgb(arr[0].GetDouble()), Srgb(arr[1].GetDouble()), Srgb(arr[2].GetDouble()), Byte(arr[3].GetDouble()));
                    }
                }
                materials.Add(mat);
                doc.Materials.Add(mat);
            }
        }

        // Parse meshes lazily: one geometry per (mesh, primitive), one instance per primitive of a node occurrence.
        var meshElements = root.TryGetProperty("meshes", out var meshesEl) ? meshesEl.EnumerateArray().ToArray() : [];
        var geometryIds = new Dictionary<(int Mesh, int Primitive), int?>();
        int meshCounter = 0;

        // One element per node occurrence: all primitives of its mesh select, hide and list as one object.
        void Instantiate(int meshIndex, Transform3d world, string? nodeName)
        {
            if (meshIndex < 0 || meshIndex >= meshElements.Length)
            {
                throw new InvalidDataException($"glTF node references missing mesh {meshIndex}.");
            }

            var meshEl = meshElements[meshIndex];
            string meshName = meshEl.TryGetProperty("name", out var mn) ? mn.GetString() ?? $"Mesh {meshCounter + 1}" : $"Mesh {meshCounter + 1}";
            if (!meshEl.TryGetProperty("primitives", out var primsEl)) return;

            SceneElement? element = null;
            int primIndex = -1;
            foreach (var prim in primsEl.EnumerateArray())
            {
                primIndex++;
                if (!prim.TryGetProperty("attributes", out var attrs) ||
                    !attrs.TryGetProperty("POSITION", out var posAccIdx))
                {
                    continue;
                }

                if (!geometryIds.TryGetValue((meshIndex, primIndex), out int? geometryId))
                {
                    var positions = ReadVec3Accessor(posAccIdx.GetInt32(), accessors, bufferViews, binBuffer);
                    if (positions.Count == 0)
                    {
                        geometryId = null;
                    }
                    else
                    {
                        List<int> indices;
                        if (prim.TryGetProperty("indices", out var indAccIdx))
                        {
                            indices = ReadIndicesAccessor(indAccIdx.GetInt32(), accessors, bufferViews, binBuffer);
                        }
                        else
                        {
                            indices = new List<int>(positions.Count);
                            for (int i = 0; i < positions.Count; i++) indices.Add(i);
                        }

                        List<Vector3d>? normals = null;
                        if (attrs.TryGetProperty("NORMAL", out var normAccIdx))
                        {
                            normals = ReadVec3Accessor(normAccIdx.GetInt32(), accessors, bufferViews, binBuffer);
                        }

                        int mode = prim.TryGetProperty("mode", out var modeProperty) ? modeProperty.GetInt32() : 4;
                        SceneTopology topology = mode switch
                        {
                            0 => SceneTopology.Points,
                            1 => SceneTopology.Lines,
                            4 => SceneTopology.Triangles,
                            _ => throw new NotSupportedException($"GLB primitive mode {mode} is unsupported."),
                        };
                        int groupSize = mode == 4 ? 3 : mode == 1 ? 2 : 1;
                        if (indices.Count % groupSize != 0 || indices.Any(index => (uint)index >= positions.Count))
                            throw new InvalidDataException("GLB primitive indices are invalid.");
                        if (normals is not null && normals.Count != positions.Count)
                            throw new InvalidDataException("GLB normal and position counts differ.");
                        geometryId = doc.Geometries.Count;
                        doc.Geometries.Add(new SceneGeometry
                        {
                            Id = geometryId.Value,
                            Topology = topology,
                            Vertices = positions,
                            Indices = indices,
                            Normals = normals
                        });
                    }

                    geometryIds[(meshIndex, primIndex)] = geometryId;
                }

                if (geometryId is null) continue;

                int? matId = null;
                if (prim.TryGetProperty("material", out var mProp))
                {
                    int mid = mProp.GetInt32();
                    if (mid >= 0 && mid < materials.Count) matId = mid;
                }

                if (element is null)
                {
                    element = new SceneElement { Id = doc.Elements.Count + 1, ModelId = 1, Name = string.IsNullOrEmpty(nodeName) ? meshName : nodeName, Category = "GLB" };
                    doc.Elements.Add(element);
                }

                ++meshCounter;
                doc.Instances.Add(new SceneInstance
                {
                    ElementId = element.Id,
                    GeometryId = geometryId.Value,
                    Transform = world.Then(YupToZup),
                    MaterialId = matId,
                    Color = matId is null ? new RgbaColor(200, 200, 200) : null,
                    Name = meshName,
                });
            }
        }

        if (root.TryGetProperty("scenes", out var scenesEl) && root.TryGetProperty("nodes", out var nodesEl))
        {
            var nodes = nodesEl.EnumerateArray().ToArray();
            var scenes = scenesEl.EnumerateArray().ToArray();
            int sceneIndex = root.TryGetProperty("scene", out var sceneProp) ? sceneProp.GetInt32() : 0;
            if (sceneIndex >= 0 && sceneIndex < scenes.Length &&
                scenes[sceneIndex].TryGetProperty("nodes", out var rootNodes))
            {
                // glTF node hierarchy is a strict tree: reaching a node twice (shared child, repeated root, cycle) is invalid.
                var visited = new HashSet<int>();
                var stack = new Stack<(int Node, Transform3d ParentWorld)>();
                foreach (var rootNode in rootNodes.EnumerateArray().Reverse()) stack.Push((rootNode.GetInt32(), Transform3d.Identity));

                while (stack.Count > 0)
                {
                    var (nodeIndex, parentWorld) = stack.Pop();
                    if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                    {
                        throw new InvalidDataException($"glTF references missing node {nodeIndex}.");
                    }

                    if (!visited.Add(nodeIndex))
                    {
                        throw new InvalidDataException($"glTF node {nodeIndex} is referenced more than once; the node hierarchy must be a tree.");
                    }

                    var node = nodes[nodeIndex];
                    var world = ReadNodeTransform(node).Then(parentWorld);
                    if (node.TryGetProperty("mesh", out var meshProp))
                    {
                        Instantiate(meshProp.GetInt32(), world, node.TryGetProperty("name", out var nodeName) ? nodeName.GetString() : null);
                    }

                    if (node.TryGetProperty("children", out var children))
                    {
                        foreach (var child in children.EnumerateArray().Reverse()) stack.Push((child.GetInt32(), world));
                    }
                }
            }
        }
        else
        {
            // No scene graph: import every mesh once without a node transform.
            for (int i = 0; i < meshElements.Length; i++) Instantiate(i, Transform3d.Identity, null);
        }

        return doc;
    }

    // The sRGB byte of a linear channel: the standard sRGB transfer function, then the nearest byte.
    private static byte Srgb(double linear)
    {
        double c = Math.Clamp(linear, 0, 1);
        return Byte(c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055);
    }

    private static byte Byte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);

    // glTF is Y-up; the viewer model is Z-up: (x, y, z) becomes (x, -z, y).
    private static readonly Transform3d YupToZup = new(new Vector3d(1, 0, 0), new Vector3d(0, 0, 1), new Vector3d(0, -1, 0), Vector3d.Zero);

    private static Transform3d ReadNodeTransform(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var m))
        {
            var v = m.EnumerateArray().Select(e => e.GetDouble()).ToArray();
            if (v.Length != 16) throw new InvalidDataException("glTF node matrix must have 16 elements.");
            // Column-major: columns 0..2 are the axes, column 3 the translation.
            return new Transform3d(new(v[0], v[1], v[2]), new(v[4], v[5], v[6]), new(v[8], v[9], v[10]), new(v[12], v[13], v[14]));
        }

        var t = ReadVector(node, "translation", 3, [0, 0, 0]);
        var r = ReadVector(node, "rotation", 4, [0, 0, 0, 1]);
        var s = ReadVector(node, "scale", 3, [1, 1, 1]);

        double len = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2] + r[3] * r[3]);
        if (len < 1e-12) throw new InvalidDataException("glTF node rotation quaternion has zero length.");
        double x = r[0] / len, y = r[1] / len, z = r[2] / len, w = r[3] / len;

        return new Transform3d(
            new Vector3d(1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w)) * s[0],
            new Vector3d(2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w)) * s[1],
            new Vector3d(2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y)) * s[2],
            new Vector3d(t[0], t[1], t[2]));
    }

    private static double[] ReadVector(JsonElement node, string name, int length, double[] fallback)
    {
        if (!node.TryGetProperty(name, out var el)) return fallback;
        var v = el.EnumerateArray().Select(e => e.GetDouble()).ToArray();
        if (v.Length != length) throw new InvalidDataException($"glTF node {name} must have {length} elements.");
        return v;
    }

    private static List<Vector3d> ReadVec3Accessor(int index, JsonElement[] accessors, JsonElement[] views, byte[] data)
    {
        var accessor = GetAccessor(accessors, index, "VEC3");
        if (accessor.GetProperty("componentType").GetInt32() != 5126)
            throw new NotSupportedException("GLB positions and normals must be float32.");
        var (offset, count, stride) = GetRange(accessor, views, data.Length, 12);
        var vertices = new List<Vector3d>(count);
        for (int i = 0; i < count; i++, offset += stride)
        {
            var bytes = data.AsSpan(offset, 12);
            var point = new Vector3d(BinaryPrimitives.ReadSingleLittleEndian(bytes),
                BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[8..]));
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                throw new InvalidDataException("A GLB vertex component is outside the finite range.");
            vertices.Add(point);
        }
        return vertices;
    }

    private static List<int> ReadIndicesAccessor(int index, JsonElement[] accessors, JsonElement[] views, byte[] data)
    {
        var accessor = GetAccessor(accessors, index, "SCALAR");
        int size = accessor.GetProperty("componentType").GetInt32() switch
        {
            5121 => 1,
            5123 => 2,
            5125 => 4,
            _ => throw new NotSupportedException("GLB indices must use an unsigned integer format."),
        };
        var (offset, count, stride) = GetRange(accessor, views, data.Length, size);
        var indices = new List<int>(count);
        for (int i = 0; i < count; i++, offset += stride)
        {
            var bytes = data.AsSpan(offset, size);
            uint value = size switch
            {
                1 => bytes[0],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                _ => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            };
            if (value > int.MaxValue) throw new InvalidDataException("A GLB index exceeds the supported range.");
            indices.Add((int)value);
        }
        return indices;
    }

    private static JsonElement GetAccessor(JsonElement[] accessors, int index, string shape)
    {
        if ((uint)index >= accessors.Length) throw new InvalidDataException("A GLB accessor is missing.");
        var accessor = accessors[index];
        if (accessor.GetProperty("type").GetString() != shape || accessor.TryGetProperty("sparse", out _))
            throw new NotSupportedException($"Expected a non-sparse GLB accessor of type {shape}.");
        return accessor;
    }

    private static (int Offset, int Count, int Stride) GetRange(JsonElement accessor, JsonElement[] views, int length, int size)
    {
        int viewIndex = accessor.GetProperty("bufferView").GetInt32();
        if ((uint)viewIndex >= views.Length) throw new InvalidDataException("A GLB buffer view is missing.");
        var view = views[viewIndex];
        if (view.GetProperty("buffer").GetInt32() != 0)
            throw new NotSupportedException("GLB accessors must reference the embedded buffer.");

        int start = view.TryGetProperty("byteOffset", out var viewOffset) ? viewOffset.GetInt32() : 0;
        int viewLength = view.GetProperty("byteLength").GetInt32();
        int offset = accessor.TryGetProperty("byteOffset", out var accessorOffset) ? accessorOffset.GetInt32() : 0;
        int count = accessor.GetProperty("count").GetInt32();
        int stride = view.TryGetProperty("byteStride", out var viewStride) ? viewStride.GetInt32() : size;
        if (start < 0 || viewLength < 0 || (long)start + viewLength > length || offset < 0 || count < 0 || stride < size
            || offset + (count == 0 ? 0L : (long)(count - 1) * stride + size) > viewLength)
        {
            throw new InvalidDataException("A GLB accessor is outside its buffer view.");
        }
        return (start + offset, count, stride);
    }
}
