using System.Text;
using System.Text.Json;

namespace IOScene.Tests;

public class SceneJsonTests
{
    private const string Head = "{\"Kind\":\"ioscene\",\"Format\":1";

    [Theory]
    [InlineData("{\"Title\":\"old\",\"Elements\":[]}")]                                    // no Kind: a legacy viewer snapshot
    [InlineData("{\"Kind\":\"ioscene\"}")]                                               // no Format
    [InlineData("{\"Format\":1}")]
    [InlineData("{\"Kind\":null,\"Format\":1}")]
    [InlineData("{\"Kind\":\"ioviewer\",\"Format\":2,\"Scene\":{}}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":2}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":0}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":\"1\"}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Elements\":[{\"Id\":1.5}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Elements\":[null]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Schedules\":[{\"Tasks\":[{\"Children\":[null]}]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Geometries\":[{\"Id\":1,\"Vertices\":[0,0,0,1]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Geometries\":[{\"Id\":1,\"Vertices\":[[0,0,0]]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Geometries\":[{\"Id\":1,\"Vertices\":{\"X\":0}}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Grids\":[{\"Lines\":[{\"Start\":[0,0]}]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Grids\":[{\"Lines\":[{\"Start\":[0,0,0,0]}]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Grids\":[{\"Lines\":[{\"Start\":{\"X\":0,\"Y\":0,\"Z\":0}}]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Materials\":[{\"Id\":1,\"Color\":[300,0,0,255]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Materials\":[{\"Id\":1,\"Color\":[1.5,0,0,255]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Materials\":[{\"Id\":1,\"Color\":[0,0,0]}]}")]
    [InlineData("{\"Kind\":\"ioscene\",\"Format\":1,\"Models\":[")]                         // cut off
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("")]
    public void A_file_that_is_not_a_scene_is_rejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => SceneJson.Read(json));
        Assert.Throws<InvalidDataException>(() => SceneJson.Read(new MemoryStream(Encoding.UTF8.GetBytes(json))));
    }

    [Fact]
    public void A_newer_format_is_named_in_the_message_before_any_table_is_read()
    {
        // A table in an encoding this version cannot parse comes after Format.
        var error = Assert.Throws<InvalidDataException>(() => SceneJson.Read("{\"Kind\":\"ioscene\",\"Format\":2,\"Geometries\":\"packed\"}"));
        Assert.Equal("IOScene format 2 is not supported; this library reads formats 1 to 1.", error.Message);
    }

    [Fact]
    public void Another_kind_is_named_in_the_message()
    {
        var error = Assert.Throws<InvalidDataException>(() => SceneJson.Read("{\"Kind\":\"ioviewer\",\"Format\":2,\"Scene\":{}}"));
        Assert.Equal("Not an IOScene document: Kind is 'ioviewer', expected 'ioscene'.", error.Message);
    }

    [Fact]
    public void A_missing_optional_member_takes_its_default()
    {
        var doc = SceneJson.Read(Head + ",\"Materials\":[{\"Id\":1}],\"Viewpoints\":[{}],\"Elements\":[{\"Id\":5}]}");

        Assert.Equal(string.Empty, doc.Title);
        Assert.Empty(doc.Models);
        Assert.Equal(0.5, doc.Materials[0].Roughness);
        Assert.Equal(new RgbaColor(200, 200, 200), doc.Materials[0].Color);
        Assert.Equal(Vector3d.UnitZ, doc.Viewpoints[0].Camera.Up);
        Assert.Empty(doc.Elements[0].PropertySetIds);
        Assert.Null(doc.Elements[0].SourceId);
    }

    [Fact]
    public void The_defaults_of_format_1_are_the_ones_the_contract_lists()
    {
        // One object of every type with no member at all. The defaults are part of format 1 (docs/contract.md,
        // "Required members and defaults"): a changed value here is a breaking change, not a fix of the expected
        // text. A new member that shows up here is written while it is null or empty, which the test
        // A_document_with_nothing_in_it_writes_the_lists_of_format_1_and_no_other explains.
        var doc = SceneJson.Read(Head + ",\"Models\":[{}],\"Materials\":[{}],\"Textures\":[{}],\"PropertySets\":[{\"Properties\":[{}]}],\"Levels\":[{}],"
            + "\"Geometries\":[{}],\"Elements\":[{}],\"Instances\":[{}],\"Grids\":[{\"Lines\":[{}]}],\"Viewpoints\":[{}],"
            + "\"Schedules\":[{\"Tasks\":[{}]}],\"ClashTests\":[{\"Clashes\":[{}]}]}");

        Assert.Equal(
            Head + ",\"Title\":\"\","
            + "\"Models\":[{\"Id\":0,\"Key\":\"\",\"Name\":\"\"}],"
            + "\"Materials\":[{\"Id\":0,\"Name\":\"\",\"Color\":[200,200,200,255],\"Roughness\":0.5,\"Metalness\":0.1}],"
            + "\"Textures\":[{\"Id\":0,\"MimeType\":\"image/png\",\"Data\":\"\",\"ScaleX\":1,\"ScaleY\":1}],"
            + "\"PropertySets\":[{\"Id\":0,\"Name\":\"\",\"Properties\":[{\"Name\":\"\",\"Value\":{\"Kind\":0,\"Text\":\"\"}}]}],"
            + "\"Levels\":[{\"Id\":0,\"ModelId\":0,\"Name\":\"\"}],"
            + "\"Geometries\":[{\"Id\":0,\"Topology\":0,\"Vertices\":[],\"Indices\":[]}],"
            + "\"Elements\":[{\"Id\":0,\"ModelId\":0,\"Kind\":0,\"Name\":\"\",\"Category\":\"\",\"TypeName\":\"\",\"PropertySetIds\":[]}],"
            + "\"Instances\":[{\"ElementId\":0,\"GeometryId\":0}],"
            + "\"Grids\":[{\"ModelId\":0,\"Name\":\"Grids\",\"Lines\":[{\"Name\":\"\",\"Start\":[0,0,0],\"End\":[0,0,0],\"Elevation\":0}]}],"
            + "\"Viewpoints\":[{\"Name\":\"Viewpoint\",\"Camera\":{\"Position\":[10,10,10],\"Target\":[0,0,0],\"Up\":[0,0,1],"
            + "\"FieldOfView\":0.785,\"AspectRatio\":1.6,\"NearDistance\":0.1,\"FarDistance\":1000,\"Projection\":0}}],"
            + "\"Schedules\":[{\"Name\":\"\",\"Tasks\":[{\"Id\":\"\",\"Name\":\"\",\"TaskType\":0,"
            + "\"PlannedStart\":\"0001-01-01T00:00:00+00:00\",\"PlannedEnd\":\"0001-01-01T00:00:00+00:00\",\"Progress\":0,\"ElementIds\":[],\"Children\":[]}]}],"
            + "\"ClashTests\":[{\"Name\":\"Clash Test\",\"Clashes\":[{\"Id\":\"\",\"Name\":\"\",\"Element1Name\":\"\",\"Element2Name\":\"\",\"Point\":[0,0,0],\"Status\":1}]}]}",
            SceneJson.Write(doc));
    }

    [Fact]
    public void A_clash_distance_that_is_not_reported_is_left_out_and_read_back_as_null()
    {
        var doc = GoldenScenes.Everything();
        doc.ClashTests[0].Clashes[1].Distance = null;

        string json = SceneJson.Write(doc);
        Assert.Contains("\"Point\":[0,0,0],\"Status\":0}", json);
        var back = SceneJson.Read(json);
        Assert.Null(back.ClashTests[0].Clashes[1].Distance);
        Assert.Equal(-0.05, back.ClashTests[0].Clashes[0].Distance);
        Assert.Equal(json, SceneJson.Write(back));
        Assert.Empty(back.Validate());

        Assert.Null(SceneJson.Read(Head + ",\"ClashTests\":[{\"Clashes\":[{\"Distance\":null}]}]}").ClashTests[0].Clashes[0].Distance);
        Assert.Equal(0.0, SceneJson.Read(Head + ",\"ClashTests\":[{\"Clashes\":[{\"Distance\":0}]}]}").ClashTests[0].Clashes[0].Distance);
    }

    [Fact]
    public void A_null_for_a_member_that_is_never_null_is_read_as_missing()
    {
        var doc = SceneJson.Read(Head + ",\"Title\":null,\"Instances\":null,\"Elements\":[{\"Id\":1,\"Name\":null,\"PropertySetIds\":null,\"SourceId\":null}],"
            + "\"Geometries\":[{\"Id\":1,\"Vertices\":null,\"Indices\":null,\"Normals\":null}],"
            + "\"PropertySets\":[{\"Id\":1,\"Properties\":[{\"Name\":\"P\",\"Value\":{\"Kind\":0,\"Text\":null}},{\"Name\":\"Q\"}]}]}");

        Assert.Equal(string.Empty, doc.Title);
        Assert.Empty(doc.Instances);
        Assert.Equal(string.Empty, doc.Elements[0].Name);
        Assert.Empty(doc.Elements[0].PropertySetIds);
        Assert.Null(doc.Elements[0].SourceId);
        Assert.Empty(doc.Geometries[0].Vertices);
        Assert.Empty(doc.Geometries[0].Indices);
        Assert.Null(doc.Geometries[0].Normals);
        Assert.Equal(string.Empty, doc.PropertySets[0].Properties[0].Value.Text);
        Assert.Equal(string.Empty, doc.PropertySets[0].Properties[1].Value.Text);
    }

    [Fact]
    public void Unknown_members_and_unknown_enum_numbers_survive_a_round_trip_as_numbers()
    {
        var doc = SceneJson.Read(Head + ",\"Later\":[{\"A\":1}],\"Elements\":[{\"Id\":1,\"Kind\":9,\"Later\":true}],"
            + "\"PropertySets\":[{\"Id\":1,\"Properties\":[{\"Name\":\"P\",\"Value\":{\"Kind\":8,\"Text\":\"shown\",\"Later\":1}}]}],"
            + "\"ClashTests\":[{\"Clashes\":[{\"Id\":\"c\",\"Status\":12}]}]}");

        Assert.Equal(9, (int)doc.Elements[0].Kind);
        Assert.Equal(8, (int)doc.PropertySets[0].Properties[0].Value.Kind);
        Assert.Equal("shown", doc.PropertySets[0].Properties[0].Value.Text);
        Assert.Equal(12, (int)doc.ClashTests[0].Clashes[0].Status);

        string json = SceneJson.Write(doc);
        Assert.Contains("\"Kind\":9", json);
        Assert.Contains("\"Status\":12", json);
        Assert.DoesNotContain("Later", json);
    }

    [Fact]
    public void The_stream_is_left_open_and_a_byte_order_mark_is_accepted()
    {
        using var stream = new MemoryStream();
        stream.Write([0xEF, 0xBB, 0xBF]);
        stream.Write(Encoding.UTF8.GetBytes(Head + ",\"Title\":\"T\"}"));
        stream.Position = 0;

        Assert.Equal("T", SceneJson.Read(stream).Title);
        Assert.True(stream.CanRead);

        using var output = new MemoryStream();
        SceneJson.Write(new SceneDocument(), output);
        Assert.True(output.CanWrite);
        Assert.Equal((byte)'{', output.ToArray()[0]);   // no byte order mark
    }

    [Fact]
    public void Tables_are_written_in_dependency_order()
    {
        string json = SceneJson.Write(GoldenScenes.Everything());
        string[] order = ["Kind", "Format", "Title", "Models", "Materials", "Textures", "PropertySets", "Levels", "Geometries", "Elements", "Instances", "Grids", "Viewpoints", "Schedules", "ClashTests"];
        int[] positions = [.. order.Select(name => json.IndexOf($"\"{name}\":", StringComparison.Ordinal))];

        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
    }

    [Fact]
    public void Elements_are_written_with_parents_first_and_the_document_is_not_changed()
    {
        var doc = new SceneDocument { Models = { new SceneModel { Id = 1, Key = "m" } } };
        // Children before their parents; 30 is a grandchild of 10.
        foreach (var (id, parent) in new (long, long?)[] { (30, 20), (21, 10), (20, 10), (10, null), (40, null), (22, 10) })
            doc.Elements.Add(new SceneElement { Id = id, ModelId = 1, ParentId = parent });

        var back = SceneJson.Read(SceneJson.Write(doc));

        Assert.Equal(new long[] { 10, 40, 21, 20, 22, 30 }, back.Elements.Select(e => e.Id));   // by depth, siblings in their order
        Assert.Equal(new long[] { 30, 21, 20, 10, 40, 22 }, doc.Elements.Select(e => e.Id));
        Assert.Equal(SceneJson.Write(back), SceneJson.Write(doc));
    }

    [Fact]
    public void A_parent_cycle_or_a_missing_parent_does_not_stop_the_writer()
    {
        var doc = new SceneDocument { Models = { new SceneModel { Id = 1, Key = "m" } } };
        doc.Elements.Add(new SceneElement { Id = 1, ModelId = 1, ParentId = 2 });
        doc.Elements.Add(new SceneElement { Id = 2, ModelId = 1, ParentId = 1 });
        doc.Elements.Add(new SceneElement { Id = 3, ModelId = 1, ParentId = 99 });

        Assert.Equal(3, SceneJson.Read(SceneJson.Write(doc)).Elements.Count);
    }

    [Fact]
    public void Large_coordinates_survive_the_round_trip_exactly()
    {
        // 1e7 m with sub-millimetre fractions: every bit of the double has to come back.
        Vector3d[] vertices =
        [
            new(10_000_000.123456789, -10_000_000.987654321, 9_999_999.999999999),
            new(1e7 + 1e-9, 1e7 - 1e-9, Math.BitIncrement(1e7)),
            new(0.1 + 0.2, double.Epsilon, double.MaxValue),
        ];
        var transform = new Transform3d(new(0.6, 0.8, 0), new(-0.8, 0.6, 0), Vector3d.UnitZ, new(10_000_000.000000001, -12_345_678.9012345, 1e7 / 3));
        var doc = new SceneDocument { Models = { new SceneModel { Id = 1, Key = "m" } } };
        doc.Geometries.Add(new SceneGeometry { Id = 1, Vertices = vertices });
        doc.Elements.Add(new SceneElement { Id = 1, ModelId = 1 });
        doc.Instances.Add(new SceneInstance { ElementId = 1, GeometryId = 1, Transform = transform });
        doc.Levels.Add(new SceneLevel { Id = 1, ModelId = 1, Elevation = 10_000_000.123456789 });

        using var stream = new MemoryStream();
        SceneJson.Write(doc, stream);
        stream.Position = 0;
        var back = SceneJson.Read(stream);

        Assert.Equal(vertices, back.Geometries[0].Vertices);
        Assert.Equal(transform, back.Instances[0].Transform);
        Assert.Equal(10_000_000.123456789, back.Levels[0].Elevation);
        Assert.Equal(doc.ComputeBounds(), back.ComputeBounds());
    }

    [Fact]
    public void Text_outside_ASCII_is_written_as_UTF8_and_survives()
    {
        var doc = new SceneDocument { Title = "Здание 1 — план <b> & \"x\" 𝄞" };
        string json = SceneJson.Write(doc);

        Assert.Contains("Здание 1 — план", json);
        Assert.DoesNotContain("<b>", json);   // characters that are unsafe inside HTML stay escaped
        Assert.Equal(doc.Title, SceneJson.Read(json).Title);
    }

    [Fact]
    public void A_number_that_is_not_finite_cannot_be_written()
    {
        var doc = new SceneDocument { Geometries = { new SceneGeometry { Id = 1, Topology = SceneTopology.Points, Vertices = [new(double.NaN, 0, 0)] } } };
        Assert.Throws<ArgumentException>(() => SceneJson.Write(doc));
    }

    [Fact]
    public void Save_and_Load_round_trip_a_file()
    {
        string folder = Directory.CreateTempSubdirectory("ioscene-tests-").FullName;
        try
        {
            string path = Path.Combine(folder, "scene.json");
            var doc = GoldenScenes.Everything();
            SceneJson.Save(doc, path);

            Assert.Equal(SceneJson.Write(doc), File.ReadAllText(path));
            Assert.Equal(SceneJson.Write(doc), SceneJson.Write(SceneJson.Load(path)));
            Assert.Equal(new[] { "scene.json" }, Directory.GetFiles(folder).Select(file => Path.GetFileName(file)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_failed_Save_leaves_the_existing_file_as_it_was()
    {
        string folder = Directory.CreateTempSubdirectory("ioscene-tests-").FullName;
        try
        {
            string path = Path.Combine(folder, "scene.json");
            SceneJson.Save(new SceneDocument { Title = "Good" }, path);
            var broken = new SceneDocument { Levels = { new SceneLevel { Id = 1, Elevation = double.NaN } } };

            Assert.Throws<ArgumentException>(() => SceneJson.Save(broken, path));

            Assert.Equal("Good", SceneJson.Load(path).Title);
            Assert.Equal(new[] { "scene.json" }, Directory.GetFiles(folder).Select(file => Path.GetFileName(file)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Saving_into_a_missing_folder_reports_the_folder()
    {
        string path = Path.Combine(Path.GetTempPath(), "ioscene-missing-" + Guid.NewGuid().ToString("N"), "scene.json");
        Assert.Throws<DirectoryNotFoundException>(() => SceneJson.Save(new SceneDocument(), path));
    }

    [Fact]
    public void Math_values_are_number_arrays_in_any_System_Text_Json_serialisation()
    {
        Assert.Equal("[1,2]", JsonSerializer.Serialize(new Vector2d(1, 2)));
        Assert.Equal("[1,2,3]", JsonSerializer.Serialize(new Vector3d(1, 2, 3)));
        Assert.Equal("[1,2,3,4]", JsonSerializer.Serialize(new RgbaColor(1, 2, 3, 4)));
        Assert.Equal("[1,0,0,0,1,0,0,0,1,7,8,9]", JsonSerializer.Serialize(Transform3d.Translate(new(7, 8, 9))));
        Assert.Equal(new Vector3d(1, 2, 3), JsonSerializer.Deserialize<Vector3d>("[1,2,3]"));
        Assert.Null(JsonSerializer.Deserialize<Transform3d?>("null"));

        // A scene embedded in another document keeps its encoding and its Kind and Format checks.
        string json = JsonSerializer.Serialize(new { Scene = GoldenScenes.NoProperties() });
        Assert.StartsWith("{\"Scene\":{\"Kind\":\"ioscene\",\"Format\":1,", json);
        Assert.Contains("\"Vertices\":[0,0,0,1,0,0,1,1,0,0,1,0]", json);
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<SceneDocument>("{\"Kind\":\"ioviewer\",\"Format\":1}"));
    }

    private sealed class Holder
    {
        public string Kind { get; set; } = "holder";
        public string? Note { get; set; }
        public SceneDocument Scene { get; set; } = new();
    }

    [Fact]
    public void A_scene_inside_another_document_follows_the_scene_rules_with_the_options_of_SceneJson()
    {
        var options = SceneJson.CreateOptions();
        const string Json = "{\"Kind\":\"holder\",\"Scene\":{\"Kind\":\"ioscene\",\"Format\":1,\"Title\":null,"
            + "\"Models\":[{\"Id\":1,\"Key\":\"m\",\"Name\":null}],\"Elements\":null,\"Later\":1}}";

        // Reading: null is a missing member, a null entry of a list is rejected, another kind or format is rejected.
        var holder = JsonSerializer.Deserialize<Holder>(Json, options)!;
        Assert.Equal(string.Empty, holder.Scene.Title);
        Assert.Equal(string.Empty, holder.Scene.Models[0].Name);
        Assert.Empty(holder.Scene.Elements);
        Assert.Empty(holder.Scene.Validate());
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<Holder>("{\"Scene\":{\"Kind\":\"ioscene\",\"Format\":1,\"Instances\":[null]}}", options));
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<Holder>("{\"Scene\":{\"Kind\":\"ioscene\",\"Format\":2}}", options));

        // Writing: the scene member has the bytes SceneJson writes for the scene alone; null members are omitted.
        holder.Scene = GoldenScenes.Everything();
        Assert.Equal("{\"Kind\":\"holder\",\"Scene\":" + SceneJson.Write(holder.Scene) + "}", JsonSerializer.Serialize(holder, options));

        // 30 task levels, the most a valid scene has, still fit when the scene is one level down.
        var deepest = new SceneTask { Id = "1" };
        var at = deepest;
        for (int level = 2; level <= 30; level++)
        {
            at.Children.Add(new SceneTask { Id = "t" });
            at = at.Children[0];
        }
        holder.Scene.Schedules[1].Tasks.Add(deepest);
        var back = JsonSerializer.Deserialize<Holder>(JsonSerializer.Serialize(holder, options), options)!;
        Assert.Equal(SceneJson.Write(holder.Scene), SceneJson.Write(back.Scene));
    }

    [Fact]
    public void The_options_are_a_copy_that_the_caller_may_change()
    {
        var options = SceneJson.CreateOptions();
        options.WriteIndented = true;

        Assert.NotSame(options, SceneJson.CreateOptions());
        Assert.False(SceneJson.CreateOptions().WriteIndented);
        Assert.DoesNotContain('\n', SceneJson.Write(new SceneDocument()));
        Assert.Contains('\n', JsonSerializer.Serialize(new SceneDocument(), options));
    }

    [Fact]
    public void A_document_with_nothing_in_it_writes_the_lists_of_format_1_and_no_other()
    {
        // The golden files fix the bytes of format 1, so a list or member added after 0.1.0 is left out of the file
        // while it is empty or null. When this test fails after such an addition, give the new member that rule in
        // SceneJson; do not change the expected text.
        Assert.Equal(
            "{\"Kind\":\"ioscene\",\"Format\":1,\"Title\":\"\",\"Models\":[],\"Materials\":[],\"Textures\":[],\"PropertySets\":[],\"Levels\":[],"
            + "\"Geometries\":[],\"Elements\":[],\"Instances\":[],\"Grids\":[],\"Viewpoints\":[],\"Schedules\":[],\"ClashTests\":[]}",
            SceneJson.Write(new SceneDocument()));
    }

    [Fact]
    public void Escaped_and_literal_text_read_the_same()
    {
        // Which characters the writer escapes is not part of the format.
        var literal = SceneJson.Read(Head + ",\"Title\":\"Стена 壁 + ' < > &\"}");
        var escaped = SceneJson.Read(Head + ",\"Title\":\"\\u0421\\u0442\\u0435\\u043D\\u0430 \\u58C1 \\u002B \\u0027 \\u003C \\u003E \\u0026\"}");

        Assert.Equal("Стена 壁 + ' < > &", literal.Title);
        Assert.Equal(literal.Title, escaped.Title);
        Assert.Equal(SceneJson.Write(literal), SceneJson.Write(escaped));
    }

    [Fact]
    public void Many_parent_cycles_do_not_slow_the_writer()
    {
        // Every element is its own parent, behind one child that comes before its parent. The walk that orders the
        // elements stops at the first repeated id; walking each cycle to the length of the list took minutes here.
        var doc = new SceneDocument { Models = { new SceneModel { Id = 1, Key = "m" } } };
        doc.Elements.Add(new SceneElement { Id = 1, ModelId = 1, ParentId = 2 });
        for (int id = 2; id <= 200_000; id++) doc.Elements.Add(new SceneElement { Id = id, ModelId = 1, ParentId = id });

        using var stream = new MemoryStream();
        SceneJson.Write(doc, stream);
        stream.Position = 0;
        Assert.Equal(200_000, SceneJson.Read(stream).Elements.Count);
    }

    private sealed class Counted<T>(int count) : IReadOnlyList<T>
    {
        public int Count => count;
        public T this[int index] => default!;
        public IEnumerator<T> GetEnumerator() => Enumerable.Repeat(default(T)!, count).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Theory]
    [InlineData("Vertices")]
    [InlineData("Normals")]
    [InlineData("UVs")]
    public void A_geometry_above_the_size_one_JSON_array_can_hold_is_refused_before_anything_is_written(string list)
    {
        var geometry = new SceneGeometry { Id = 7, Topology = SceneTopology.Points };
        if (list == "Vertices") geometry.Vertices = new Counted<Vector3d>(10_000_001);
        if (list == "Normals") geometry.Normals = new Counted<Vector3d>(10_000_001);
        if (list == "UVs") geometry.UVs = new Counted<Vector2d>(10_000_001);
        var doc = new SceneDocument { Geometries = { geometry } };

        using var stream = new MemoryStream();
        var error = Assert.Throws<ArgumentException>(() => SceneJson.Write(doc, stream));
        Assert.StartsWith("Geometry 7 has more than 10000000 vertices, the limit of one geometry in scene JSON.", error.Message);
        Assert.Equal(0, stream.Length);
        Assert.Throws<ArgumentException>(() => SceneJson.Write(doc));
    }
}
