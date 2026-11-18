using System.Buffers.Binary;
using System.Text.Json;
using IOScene.Formats;

namespace IOScene.Tools.Tests;

public class GlbSceneWriterTests
{
    [Fact]
    public void Write_PreservesHierarchyAndSharesGeometryAcrossMirroredPlacements()
    {
        var scene = new SceneDocument { Title = "Test" };
        var element = scene.AddMesh("Panel", [new(1, 2, 3), new(2, 2, 3), new(1, 3, 3)], [0, 1, 2]);
        scene.Elements.Add(new SceneElement { Id = 7, ModelId = element.ModelId, Name = "Assembly" });
        element.ParentId = 7;
        element.SourceId = "panel-id";
        scene.Instances.Add(new SceneInstance
        {
            ElementId = element.Id,
            GeometryId = scene.Geometries[0].Id,
            Transform = new(-Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ, new(10, 20, 30)),
        });

        byte[] bytes = Export(scene);
        using var json = ReadJson(bytes, out int binStart);
        var root = json.RootElement;
        Assert.Single(root.GetProperty("meshes").EnumerateArray());
        var nodes = root.GetProperty("nodes");
        Assert.Equal(2, nodes[0].GetProperty("children")[0].GetInt32());
        Assert.Equal(1, nodes[2].GetProperty("children")[0].GetInt32());
        Assert.Equal("panel-id", nodes[1].GetProperty("extras").GetProperty("sourceId").GetString());
        var children = nodes[1].GetProperty("children");
        Assert.Equal(2, children.GetArrayLength());
        Assert.All(children.EnumerateArray(), child => Assert.Equal(0, nodes[child.GetInt32()].GetProperty("mesh").GetInt32()));
        var mirror = nodes[children[1].GetInt32()].GetProperty("matrix");
        Assert.Equal(-1, mirror[0].GetDouble());
        Assert.Equal(30, mirror[14].GetDouble());
        Assert.Equal(1, nodes[0].GetProperty("matrix")[9].GetDouble());
        Assert.Equal(-1, nodes[0].GetProperty("matrix")[6].GetDouble());
        Assert.Equal(1, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(binStart)));
        Assert.Equal(2, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(binStart + 4)));
        Assert.Equal(3, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(binStart + 8)));
    }

    [Fact]
    public void Write_EmbedsTextureAndUsesLinearOverrideAndScaledUv()
    {
        var scene = new SceneDocument();
        scene.AddMesh("Textured", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]);
        scene.Geometries[0].UVs = [new(1, 0.5), new(0, 0), new(1, 1)];
        scene.Materials.Add(new SceneMaterial { Id = 2, TextureId = 3, Color = RgbaColor.White });
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jOgcAAAAASUVORK5CYII=");
        scene.Textures.Add(new SceneTexture { Id = 3, Data = png, ScaleX = 2, ScaleY = 2 });
        scene.Instances[0].MaterialId = 2;
        scene.Instances[0].Color = new RgbaColor(128, 0, 255, 128);

        byte[] bytes = Export(scene);
        using var json = ReadJson(bytes, out int binStart);
        var root = json.RootElement;
        var material = root.GetProperty("materials")[0];
        var factor = material.GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor");
        Assert.Equal(0.2158605001, factor[0].GetDouble(), 9);
        Assert.Equal(128 / 255d, factor[3].GetDouble(), 12);
        Assert.Equal("BLEND", material.GetProperty("alphaMode").GetString());
        Assert.Equal(0, material.GetProperty("pbrMetallicRoughness").GetProperty("baseColorTexture").GetProperty("index").GetInt32());
        var image = root.GetProperty("images")[0];
        Assert.Equal("image/png", image.GetProperty("mimeType").GetString());
        var imageView = root.GetProperty("bufferViews")[image.GetProperty("bufferView").GetInt32()];
        Assert.Equal(png, bytes.AsSpan(binStart + imageView.GetProperty("byteOffset").GetInt32(), imageView.GetProperty("byteLength").GetInt32()).ToArray());

        int uvIndex = root.GetProperty("meshes")[0].GetProperty("primitives")[0].GetProperty("attributes").GetProperty("TEXCOORD_0").GetInt32();
        int uvViewIndex = root.GetProperty("accessors")[uvIndex].GetProperty("bufferView").GetInt32();
        int uvStart = binStart + root.GetProperty("bufferViews")[uvViewIndex].GetProperty("byteOffset").GetInt32();
        Assert.Equal(0.5f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(uvStart)));
        Assert.Equal(0.75f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(uvStart + 4)));
    }

    [Theory]
    [InlineData(SceneTopology.Points, 0)]
    [InlineData(SceneTopology.Lines, 1)]
    public void Write_EmitsNonTriangleTopologies(SceneTopology topology, int expected)
    {
        var scene = new SceneDocument();
        scene.AddMesh("Primitive", [Vector3d.Zero, Vector3d.UnitX], []);
        scene.Geometries[0].Topology = topology;

        byte[] bytes = Export(scene);
        using var json = ReadJson(bytes, out _);
        Assert.Equal(expected, json.RootElement.GetProperty("meshes")[0].GetProperty("primitives")[0].GetProperty("mode").GetInt32());
        using var stream = new MemoryStream(bytes);
        Assert.Equal(topology, Assert.Single(GlbSceneReader.Load(stream).Geometries).Topology);
    }

    [Fact]
    public void Load_RoundTripsWorldPositionsAndDetectsATruncatedContainer()
    {
        var scene = new SceneDocument();
        scene.AddMesh("Panel", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitZ], [0, 1, 2]);
        scene.Instances[0].Transform = Transform3d.Translate(new(1, 2, 3));
        byte[] bytes = Export(scene);
        using var stream = new MemoryStream(bytes);
        var result = GlbSceneReader.Load(stream);
        var placement = Assert.Single(result.Instances).Transform!.Value;
        var vertices = Assert.Single(result.Geometries).Vertices;
        Assert.Equal(new Vector3d(1, 2, 4), placement.Apply(vertices[2]));

        using var truncated = new MemoryStream(bytes[..^1]);
        Assert.Throws<InvalidDataException>(() => GlbSceneReader.Load(truncated));
    }

    [Fact]
    public void Save_InvalidTransformPreservesExistingOutput()
    {
        var scene = new SceneDocument();
        scene.AddMesh("Panel", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]);
        scene.Instances[0].Transform = new(Vector3d.UnitX, new(1, 1, 0), Vector3d.UnitZ, Vector3d.Zero);
        string path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.glb");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);
            Assert.Throws<InvalidDataException>(() => GlbSceneWriter.Save(scene, path));
            Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Export(SceneDocument scene)
    {
        using var output = new MemoryStream();
        GlbSceneWriter.Write(scene, output);
        return output.ToArray();
    }

    private static JsonDocument ReadJson(byte[] bytes, out int binStart)
    {
        Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(bytes.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));
        int jsonLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        Assert.Equal(0, jsonLength % 4);
        Assert.Equal(0x4E4F534Au, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)));
        binStart = 28 + jsonLength;
        Assert.Equal(0x004E4942u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(binStart - 4)));
        Assert.Equal(bytes.Length - binStart, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(binStart - 8)));
        return JsonDocument.Parse(bytes.AsMemory(20, jsonLength));
    }
}
