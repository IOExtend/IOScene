using System.Text.Json;
using System.Text.Json.Nodes;

namespace IOScene.Formats;

/// <summary>Writes a self-contained glTF 2.0 binary scene.</summary>
public static class GlbSceneWriter
{
    /// <summary>Writes the scene to a file, replacing it after the full export has succeeded.</summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="path">Path to the GLB file.</param>
    public static void Save(SceneDocument scene, string path)
    {
        ArgumentNullException.ThrowIfNull(scene);
        SceneFormatFile.Replace(path, "glb", file => Write(scene, file));
    }

    /// <summary>Writes the scene to an open stream. The stream remains open.</summary>
    /// <param name="scene">The scene to export, in metres with Z up.</param>
    /// <param name="output">Writable output stream.</param>
    /// <exception cref="InvalidDataException">The scene is invalid or has data that cannot be represented in GLB.</exception>
    public static void Write(SceneDocument scene, Stream output)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(output);
        if (scene.Validate().FirstOrDefault(issue => issue.Severity == SceneSeverity.Error) is { } failure)
            throw new InvalidDataException($"Invalid scene ({failure.Code}): {failure.Message}");

        string spoolPath = Path.Combine(Path.GetTempPath(), $"ioscene-{Guid.NewGuid():N}.tmp");
        using var binary = new FileStream(spoolPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            bufferSize: 65536, FileOptions.DeleteOnClose | FileOptions.SequentialScan);
        using var writer = new BinaryWriter(binary);
        var manifest = new Payload(scene, writer).Build();
        using var json = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(json))
            manifest.WriteTo(jsonWriter);

        uint jsonPadding = (uint)((4 - json.Length % 4) % 4);
        long binLength = binary.Length;
        long length = 12 + 8 + json.Length + jsonPadding + (binLength == 0 ? 0 : 8 + binLength);
        if (length > uint.MaxValue)
            throw new InvalidDataException("The scene exceeds the size limit of a GLB container.");

        using var glb = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        glb.Write(0x46546C67u);
        glb.Write(2u);
        glb.Write((uint)length);
        glb.Write((uint)json.Length + jsonPadding);
        glb.Write(0x4E4F534Au);
        json.Position = 0;
        json.CopyTo(output);
        for (int i = 0; i < jsonPadding; i++) glb.Write((byte)' ');
        if (binLength == 0) return;
        glb.Write((uint)binLength);
        glb.Write(0x004E4942u);
        binary.Position = 0;
        binary.CopyTo(output);
    }

    private sealed class Payload(SceneDocument scene, BinaryWriter binary)
    {
        private readonly JsonArray _nodes = new();
        private readonly JsonArray _meshes = new();
        private readonly JsonArray _materials = new();
        private readonly JsonArray _accessors = new();
        private readonly JsonArray _views = new();
        private readonly JsonArray _images = new();
        private readonly JsonArray _textures = new();

        private readonly Dictionary<int, SceneGeometry> _geometryById = scene.Geometries.ToDictionary(item => item.Id);
        private readonly Dictionary<int, SceneMaterial> _materialById = scene.Materials.ToDictionary(item => item.Id);
        private readonly Dictionary<int, SceneTexture> _textureById = scene.Textures.ToDictionary(item => item.Id);
        private readonly Dictionary<long, int> _elementNodes = new();
        private readonly Dictionary<int, GeometryBuffer> _geometryBuffers = new();
        private readonly Dictionary<(int GeometryId, double ScaleX, double ScaleY), int> _uvBuffers = new();
        private readonly Dictionary<(int? MaterialId, RgbaColor? Color), int> _materialIndices = new();
        private readonly Dictionary<int, int> _textureIndices = new();
        private readonly Dictionary<(int GeometryId, int? Material), int> _meshIndices = new();

        public JsonObject Build()
        {
            // This rotation maps Z up to Y up and keeps the coordinate system right-handed.
            _nodes.Add(new JsonObject { ["matrix"] = Matrix(new(Vector3d.UnitX, -Vector3d.UnitZ, Vector3d.UnitY, Vector3d.Zero)) });
            foreach (var element in scene.Elements)
            {
                _elementNodes.Add(element.Id, _nodes.Count);
                var node = new JsonObject { ["name"] = element.Name };
                if (element.SourceId is not null || element.IfcGuid is not null)
                {
                    var extras = new JsonObject();
                    if (element.SourceId is not null) extras["sourceId"] = element.SourceId;
                    if (element.IfcGuid is not null) extras["ifcGuid"] = element.IfcGuid;
                    node["extras"] = extras;
                }
                _nodes.Add(node);
            }
            foreach (var element in scene.Elements)
                AddChild(element.ParentId is { } parentId ? _elementNodes[parentId] : 0, _elementNodes[element.Id]);

            foreach (var instance in scene.Instances)
            {
                var geometry = _geometryById[instance.GeometryId];
                if (geometry.Vertices.Count == 0) continue;
                var mesh = GetMesh(geometry, instance);
                var node = new JsonObject { ["mesh"] = mesh };
                if (instance.Transform is { } transform && transform != Transform3d.Identity) node["matrix"] = Matrix(transform);
                if (instance.Name is not null) node["name"] = instance.Name;
                AddChild(_elementNodes[instance.ElementId], _nodes.Count);
                _nodes.Add(node);
            }

            var manifest = new JsonObject
            {
                ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "IOScene.Formats" },
                ["scene"] = 0,
                ["scenes"] = new JsonArray(new JsonObject { ["name"] = scene.Title, ["nodes"] = new JsonArray(0) }),
                ["nodes"] = _nodes,
            };
            if (_meshes.Count > 0) manifest["meshes"] = _meshes;
            if (_materials.Count > 0) manifest["materials"] = _materials;
            if (_images.Count > 0)
            {
                manifest["images"] = _images;
                manifest["textures"] = _textures;
            }
            if (_accessors.Count > 0) manifest["accessors"] = _accessors;
            if (_views.Count > 0)
            {
                manifest["bufferViews"] = _views;
                manifest["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = binary.BaseStream.Length });
            }
            return manifest;
        }

        private void AddChild(int parent, int child)
        {
            var node = _nodes[parent]!.AsObject();
            if (node["children"] is not JsonArray children)
                node["children"] = children = new JsonArray();
            children.Add(child);
        }

        private int GetMesh(SceneGeometry geometry, SceneInstance instance)
        {
            SceneMaterial? material = instance.MaterialId is { } id ? _materialById[id] : null;
            SceneTexture? texture = material?.TextureId is { } textureId ? _textureById[textureId] : null;
            int? materialIndex = GetMaterial(instance, material, texture);
            if (_meshIndices.TryGetValue((geometry.Id, materialIndex), out int mesh)) return mesh;

            if (!_geometryBuffers.TryGetValue(geometry.Id, out var buffer))
            {
                buffer = AddGeometry(geometry);
                _geometryBuffers.Add(geometry.Id, buffer);
            }
            var attributes = new JsonObject { ["POSITION"] = buffer.Position };
            if (buffer.Normal is { } normal) attributes["NORMAL"] = normal;
            if (geometry.UVs is not null) attributes["TEXCOORD_0"] = GetUvs(geometry, texture?.ScaleX ?? 1, texture?.ScaleY ?? 1);
            else if (texture is not null) throw new InvalidDataException($"Geometry {geometry.Id} has a texture without UVs.");
            var primitive = new JsonObject
            {
                ["attributes"] = attributes,
                ["mode"] = geometry.Topology switch
                {
                    SceneTopology.Points => 0,
                    SceneTopology.Lines => 1,
                    SceneTopology.Triangles => 4,
                    _ => throw new InvalidDataException($"Geometry {geometry.Id} has an unsupported topology."),
                },
            };
            if (buffer.Indices is { } indices) primitive["indices"] = indices;
            if (materialIndex is { } index) primitive["material"] = index;
            mesh = _meshes.Count;
            _meshIndices.Add((geometry.Id, materialIndex), mesh);
            _meshes.Add(new JsonObject { ["primitives"] = new JsonArray(primitive) });
            return mesh;
        }

        private GeometryBuffer AddGeometry(SceneGeometry geometry)
        {
            long start = binary.BaseStream.Position;
            var bounds = BoundingBox3d.Empty;
            foreach (var vertex in geometry.Vertices)
            {
                Vector3d point = new(Float(vertex.X), Float(vertex.Y), Float(vertex.Z));
                WriteVector(point);
                bounds = bounds.Encapsulate(point);
            }
            int position = Accessor(start, geometry.Vertices.Count, "VEC3", 5126, 34962);
            var accessor = _accessors[position]!.AsObject();
            accessor["min"] = Numbers(bounds.Min.X, bounds.Min.Y, bounds.Min.Z);
            accessor["max"] = Numbers(bounds.Max.X, bounds.Max.Y, bounds.Max.Z);

            int? normal = null;
            if (geometry.Normals is { } normals && normals.All(value => value.LengthSquared > 1e-24 && double.IsFinite(value.LengthSquared)))
            {
                start = binary.BaseStream.Position;
                foreach (var value in normals) WriteVector(value.Normalized());
                normal = Accessor(start, normals.Count, "VEC3", 5126, 34962);
            }
            int? indices = null;
            if (geometry.Indices.Count != 0)
            {
                start = binary.BaseStream.Position;
                foreach (var index in geometry.Indices) binary.Write((uint)index);
                indices = Accessor(start, geometry.Indices.Count, "SCALAR", 5125, 34963);
            }
            return new(position, normal, indices);
        }

        private int GetUvs(SceneGeometry geometry, double scaleX, double scaleY)
        {
            var key = (geometry.Id, scaleX, scaleY);
            if (_uvBuffers.TryGetValue(key, out int index)) return index;
            long start = binary.BaseStream.Position;
            foreach (var uv in geometry.UVs!)
            {
                binary.Write(Float(uv.X / scaleX));
                binary.Write(Float(1 - uv.Y / scaleY));
            }
            index = Accessor(start, geometry.UVs!.Count, "VEC2", 5126, 34962);
            _uvBuffers.Add(key, index);
            return index;
        }

        private int? GetMaterial(SceneInstance instance, SceneMaterial? material, SceneTexture? texture)
        {
            if (material is null && instance.Color is null) return null;
            var key = (instance.MaterialId, instance.Color);
            if (_materialIndices.TryGetValue(key, out int index)) return index;
            RgbaColor color = instance.Color ?? material!.Color;
            double roughness = material?.Roughness ?? 0.5;
            double metalness = material?.Metalness ?? 0.1;
            if (roughness is < 0 or > 1 || metalness is < 0 or > 1)
                throw new InvalidDataException("GLB material roughness and metalness must be between 0 and 1.");

            var pbr = new JsonObject
            {
                ["baseColorFactor"] = Numbers(Linear(color.Rf), Linear(color.Gf), Linear(color.Bf), color.Af),
                ["roughnessFactor"] = roughness,
                ["metallicFactor"] = metalness,
            };
            if (texture is not null) pbr["baseColorTexture"] = new JsonObject { ["index"] = GetTexture(texture) };
            var item = new JsonObject { ["pbrMetallicRoughness"] = pbr, ["doubleSided"] = true };
            if (material is not null) item["name"] = material.Name;
            if (color.A != 255 || texture?.MimeType == "image/png") item["alphaMode"] = "BLEND";
            index = _materials.Count;
            _materialIndices.Add(key, index);
            _materials.Add(item);
            return index;
        }

        private int GetTexture(SceneTexture texture)
        {
            if (_textureIndices.TryGetValue(texture.Id, out int index)) return index;
            if (texture.MimeType is not ("image/png" or "image/jpeg") || texture.Data.Length == 0)
                throw new InvalidDataException($"Texture {texture.Id} must contain an embedded PNG or JPEG.");
            if (!double.IsFinite(texture.ScaleX) || !double.IsFinite(texture.ScaleY) || texture.ScaleX <= 0 || texture.ScaleY <= 0)
                throw new InvalidDataException($"Texture {texture.Id} must have positive repeat scales.");

            long start = binary.BaseStream.Position;
            binary.Write(texture.Data);
            int view = View(start);
            while (binary.BaseStream.Position % 4 != 0) binary.Write((byte)0);
            int image = _images.Count;
            _images.Add(new JsonObject { ["mimeType"] = texture.MimeType, ["bufferView"] = view });
            index = _textures.Count;
            _textures.Add(new JsonObject { ["source"] = image });
            _textureIndices.Add(texture.Id, index);
            return index;
        }

        private int Accessor(long start, int count, string shape, int component, int target)
        {
            int index = _accessors.Count;
            _accessors.Add(new JsonObject
            {
                ["bufferView"] = View(start, target),
                ["type"] = shape,
                ["componentType"] = component,
                ["count"] = count,
            });
            return index;
        }

        private int View(long start, int? target = null)
        {
            int index = _views.Count;
            var view = new JsonObject { ["buffer"] = 0, ["byteOffset"] = start, ["byteLength"] = binary.BaseStream.Position - start };
            if (target is { } value) view["target"] = value;
            _views.Add(view);
            return index;
        }

        private void WriteVector(Vector3d vector)
        {
            binary.Write(Float(vector.X));
            binary.Write(Float(vector.Y));
            binary.Write(Float(vector.Z));
        }

        private static JsonArray Matrix(Transform3d transform)
        {
            var (x, y, z, t) = transform;
            if (!Orthogonal(x, y) || !Orthogonal(y, z) || !Orthogonal(z, x))
                throw new InvalidDataException("GLB cannot store a sheared instance transform.");
            return Numbers(Float(x.X), Float(x.Y), Float(x.Z), 0, Float(y.X), Float(y.Y), Float(y.Z), 0,
                Float(z.X), Float(z.Y), Float(z.Z), 0, Float(t.X), Float(t.Y), Float(t.Z), 1);
        }

        private static bool Orthogonal(Vector3d a, Vector3d b) =>
            a.Length == 0 || b.Length == 0 || Math.Abs(Vector3d.Dot(a / a.Length, b / b.Length)) <= 1e-8;

        private static float Float(double value)
        {
            float result = (float)value;
            if (!float.IsFinite(result))
                throw new InvalidDataException("GLB coordinates must be representable as finite 32-bit floats.");
            return result;
        }

        private static double Linear(double encoded) => encoded <= 0.04045
            ? encoded / 12.92 : Math.Pow((encoded + 0.055) / 1.055, 2.4);

        private static JsonArray Numbers(params double[] values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

        private readonly record struct GeometryBuffer(int Position, int? Normal, int? Indices);
    }
}
