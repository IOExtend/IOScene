using System.Text;
using System.Text.Json;

namespace IOScene.Tests;

/// <summary>
/// The checks the contract was compiled against before the package existed: JSON shape, discriminator and version,
/// the validation rules and the quick-start helper.
/// </summary>
public class ContractTests
{
    private static SceneDocument Scene()
    {
        var doc = new SceneDocument { Title = "Final" };
        doc.Models.Add(new SceneModel { Id = 1, Key = "a.rvt", Name = "A", SourceFormat = "rvt", Producer = "RvtIO.Scene 0.5.0" });
        doc.Textures.Add(new SceneTexture { Id = 1, Data = [1, 2, 3, 250] });
        doc.Materials.Add(new SceneMaterial { Id = 1, Name = "Brick", Color = new(10, 20, 30, 128), TextureId = 1 });
        doc.PropertySets.Add(new ScenePropertySet
        {
            Id = 1, Name = "Dimensions", Key = "revit:instance",
            Properties = { new SceneProperty { Name = "Area", Key = "revit:bip:-1012805", Value = SceneValue.FromNumber(12.5, SceneUnits.SquareMetre, "12.5 m²") } },
        });
        doc.Elements.Add(new SceneElement { Id = 10, ModelId = 1, SourceId = "uid-1", IfcGuid = "0123456789ABCDEFGHIJKL", Name = "Wall", PropertySetIds = { 1 } });
        doc.Elements.Add(new SceneElement { Id = 11, ModelId = 1, SourceId = "uid-2", Name = "Cloud" });
        doc.Geometries.Add(new SceneGeometry
        {
            Id = 1, Vertices = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0.3048 * 7)], Indices = [0, 1, 2],
            Normals = [new(0, 0, 1), new(0, 0, 1), new(0, 0, 1)], UVs = [new(0, 0), new(1, 0), new(0, 1)],
        });
        doc.Geometries.Add(new SceneGeometry { Id = 2, Topology = SceneTopology.Points, Vertices = [new(1, 2, 3), new(4, 5, 6)] });
        doc.Instances.Add(new SceneInstance { ElementId = 10, GeometryId = 1, MaterialId = 1, Color = new(1, 2, 3, 4), Transform = new(new(-1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(100, 200, 300)) });
        doc.Instances.Add(new SceneInstance { ElementId = 11, GeometryId = 2 });
        doc.Viewpoints.Add(new SceneViewpoint { Id = "vp-guid", Name = "Front", Camera = new SceneCamera { Projection = SceneProjection.Orthographic } });
        doc.ClashTests.Add(new SceneClashTest
        {
            Name = "Walls x Ducts",
            Clashes = { new SceneClashResult { Id = "c1", Element1Id = 10, Element2Id = 11, Point = new(1, 2, 3), Distance = -0.05, Status = SceneClashStatus.Reviewed } },
        });
        return doc;
    }

    private static readonly string Json = SceneJson.Write(Scene());

    private static bool Rejects(string json)
    {
        try
        {
            SceneJson.Read(json);
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    [Fact]
    public void The_scene_validates() => Assert.Empty(Scene().Validate());

    [Fact]
    public void Root_starts_with_Kind_and_Format() => Assert.StartsWith("{\"Kind\":\"ioscene\",\"Format\":1,", Json);

    [Fact]
    public void Vertex_list_is_one_flat_number_array() => Assert.Contains("\"Vertices\":[0,0,0,1,0,0,0,1,2.1336", Json);

    [Fact]
    public void UV_list_is_flat() => Assert.Contains("\"UVs\":[0,0,1,0,0,1]", Json);

    [Fact]
    public void Transform_is_12_numbers() => Assert.Contains("\"Transform\":[-1,0,0,0,1,0,0,0,1,100,200,300]", Json);

    [Fact]
    public void Colour_is_4_numbers()
    {
        Assert.Contains("\"Color\":[1,2,3,4]", Json);
        Assert.Contains("\"Color\":[10,20,30,128]", Json);
    }

    [Fact]
    public void A_single_vector_is_3_numbers() => Assert.Contains("\"Point\":[1,2,3]", Json);

    [Fact]
    public void Texture_bytes_are_base64() => Assert.Contains("\"Data\":\"AQID+g==\"", Json);

    [Fact]
    public void No_math_member_names_are_written()
    {
        Assert.DoesNotContain("\"X\":", Json);
        Assert.DoesNotContain("\"XAxis\"", Json);
    }

    [Fact]
    public void Stream_round_trip_is_byte_stable()
    {
        using var stream = new MemoryStream();
        SceneJson.Write(Scene(), stream);
        Assert.Equal(Json, Encoding.UTF8.GetString(stream.ToArray()));

        stream.Position = 0;
        var back = SceneJson.Read(stream);
        Assert.Equal(Json, SceneJson.Write(back));
    }

    [Fact]
    public void Identity_and_review_members_survive()
    {
        var back = SceneJson.Read(Json);
        Assert.Equal("0123456789ABCDEFGHIJKL", back.Elements[0].IfcGuid);
        Assert.Equal("RvtIO.Scene 0.5.0", back.Models[0].Producer);
        Assert.Equal("vp-guid", back.Viewpoints[0].Id);
        Assert.Equal(SceneClashStatus.Reviewed, back.ClashTests[0].Clashes[0].Status);
        Assert.Equal(new byte[] { 1, 2, 3, 250 }, back.Textures[0].Data);
        Assert.Equal(new Vector3d(100, 200, 300), back.Instances[0].Transform!.Value.Translation);
    }

    [Fact]
    public void Non_indexed_geometry_survives_with_empty_indices()
    {
        var back = SceneJson.Read(Json);
        Assert.Empty(back.Geometries[1].Indices);
        Assert.Equal(2, back.Geometries[1].Vertices.Count);
    }

    [Fact]
    public void A_file_without_Kind_is_rejected() => Assert.True(Rejects("{\"Title\":\"old\",\"Elements\":[]}"));

    [Fact]
    public void A_file_without_Format_is_rejected() => Assert.True(Rejects("{\"Kind\":\"ioscene\"}"));

    [Fact]
    public void Viewer_JSON_is_rejected_by_the_scene_reader() => Assert.True(Rejects("{\"Kind\":\"ioviewer\",\"Format\":2,\"Scene\":{}}"));

    [Fact]
    public void A_newer_format_is_rejected() => Assert.True(Rejects("{\"Kind\":\"ioscene\",\"Format\":2}"));

    [Fact]
    public void A_vector_list_that_is_not_a_multiple_of_3_is_rejected() =>
        Assert.True(Rejects("{\"Kind\":\"ioscene\",\"Format\":1,\"Geometries\":[{\"Id\":1,\"Vertices\":[0,0,0,1]}]}"));

    [Fact]
    public void Unknown_members_are_ignored_and_unknown_enum_numbers_kept()
    {
        var tolerant = SceneJson.Read("{\"Kind\":\"ioscene\",\"Format\":1,\"Future\":{\"A\":[1,2]},\"Geometries\":[{\"Id\":1,\"Topology\":7,\"Later\":1,\"Vertices\":[0,0,0]}]}");
        Assert.Equal(7, (int)tolerant.Geometries[0].Topology);
        Assert.Single(tolerant.Geometries[0].Vertices);
    }

    [Fact]
    public void Validate_reports_vertex_count_ifc_guid_unit_grammar_and_dangling_clash_element()
    {
        var bad = SceneJson.Read(Json);
        bad.Geometries.Add(new SceneGeometry { Id = 3, Vertices = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0)] });
        bad.Elements[1].IfcGuid = "short";
        bad.PropertySets[0].Properties.Add(new SceneProperty { Name = "Flow", Value = SceneValue.FromNumber(1, "m^3/s", "1") });
        bad.ClashTests[0].Clashes.Add(new SceneClashResult { Id = "c2", Element1Id = 999 });

        var codes = bad.Validate().Select(d => d.Code).ToHashSet();
        Assert.Equal(new[] { "dangling-element", "ifc-guid", "unit-grammar", "vertex-count" }, codes.Order());
    }

    [Fact]
    public void Unit_grammar_accepts_and_rejects_the_samples()
    {
        Assert.All(new[] { "m", "m2", "m3/s", "kg/m3", "W/m2.K", "N.m", "rad" }, unit => Assert.True(SceneUnits.IsValid(unit), unit));
        Assert.All(new[] { "m^2", "m²", "mm", "m/s/s", "", "m0", "W/(m2.K)", "ft" }, unit => Assert.False(SceneUnits.IsValid(unit), unit));
    }

    [Fact]
    public void AddMesh_gives_a_valid_scene()
    {
        var quick = new SceneDocument { Title = "Quad" };
        quick.AddMesh("Quad", [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [0, 1, 2, 0, 2, 3], new RgbaColor(200, 80, 80));

        Assert.Empty(quick.Validate());
        Assert.Single(quick.Models);
        Assert.Single(quick.Instances);
    }

    [Fact]
    public void Flat_form_is_smaller_than_the_object_form()
    {
        var random = new Random(1);
        var vertices = Enumerable.Range(0, 100_000)
            .Select(_ => new Vector3d(random.NextDouble() * 328 * 0.3048, random.NextDouble() * 328 * 0.3048, random.NextDouble() * 98 * 0.3048))
            .ToList();
        var big = new SceneDocument { Models = { new SceneModel { Id = 1, Key = "m" } }, Geometries = { new SceneGeometry { Id = 1, Topology = SceneTopology.Points, Vertices = vertices } } };

        int flat = Encoding.UTF8.GetByteCount(SceneJson.Write(big));
        int objects = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(vertices.Select(v => new { v.X, v.Y, v.Z })));
        Assert.True(flat < objects, $"flat {flat} bytes, object form {objects} bytes");
    }
}
