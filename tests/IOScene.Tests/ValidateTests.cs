namespace IOScene.Tests;

/// <summary>Every code of Validate(), each raised alone on a scene that is valid otherwise.</summary>
public class ValidateTests
{
    private static string[] Codes(SceneDocument doc) => [.. doc.Validate().Select(d => d.Code).Distinct().Order()];

    private static SceneDocument With(Action<SceneDocument> change)
    {
        var doc = GoldenScenes.Everything();
        change(doc);
        return doc;
    }

    private static SceneElement Element(SceneDocument doc, long id) => doc.Elements.Single(e => e.Id == id);

    [Fact]
    public void The_scene_with_every_type_is_valid() => Assert.Empty(GoldenScenes.Everything().Validate());

    [Fact]
    public void No_model()
    {
        var issue = Assert.Single(new SceneDocument().Validate());
        Assert.Equal(("no-model", SceneSeverity.Error), (issue.Code, issue.Severity));
    }

    [Theory]
    [InlineData("Model")]
    [InlineData("Element")]
    [InlineData("Geometry")]
    [InlineData("Material")]
    [InlineData("Texture")]
    [InlineData("Property set")]
    [InlineData("Level")]
    public void Duplicate_id(string table)
    {
        var doc = With(d =>
        {
            switch (table)
            {
                case "Model": d.Models.Add(new SceneModel { Id = 1, Key = "other" }); break;
                case "Element": d.Elements.Add(new SceneElement { Id = 10, ModelId = 1 }); break;
                case "Geometry": d.Geometries.Add(new SceneGeometry { Id = 1 }); break;
                case "Material": d.Materials.Add(new SceneMaterial { Id = 1 }); break;
                case "Texture": d.Textures.Add(new SceneTexture { Id = 1 }); break;
                case "Property set": d.PropertySets.Add(new ScenePropertySet { Id = 1 }); break;
                default: d.Levels.Add(new SceneLevel { Id = 1, ModelId = 1 }); break;
            }
        });

        var issue = Assert.Single(doc.Validate());
        Assert.Equal(("duplicate-id", SceneSeverity.Error), (issue.Code, issue.Severity));
        Assert.StartsWith(table + " id ", issue.Message);
    }

    [Theory]
    [InlineData("house.rvt")]   // the key of the first model
    [InlineData("")]
    public void Model_key_is_unique_and_not_empty(string key) =>
        Assert.Equal(new[] { "model-key" }, Codes(With(d => d.Models[1].Key = key)));

    [Fact]
    public void Dangling_model()
    {
        Assert.Equal(new[] { "dangling-model" }, Codes(With(d => Element(d, 13).ModelId = 99)));
        Assert.Equal(new[] { "dangling-model" }, Codes(With(d => d.Levels.Add(new SceneLevel { Id = 9, ModelId = 99 }))));
        Assert.Equal(new[] { "dangling-model" }, Codes(With(d => d.Grids[0].ModelId = 99)));
        Assert.Equal(new[] { "dangling-model" }, Codes(With(d => d.Schedules[0].ModelId = 99)));
    }

    [Fact]
    public void Dangling_level() => Assert.Equal(new[] { "dangling-level" }, Codes(With(d => Element(d, 10).LevelId = 99)));

    [Fact]
    public void Dangling_set() => Assert.Equal(new[] { "dangling-set" }, Codes(With(d => Element(d, 10).PropertySetIds.Add(99))));

    [Fact]
    public void Dangling_parent()
    {
        var issue = Assert.Single(With(d => Element(d, 10).ParentId = 999).Validate());
        Assert.Equal(("dangling-parent", "uid-1"), (issue.Code, issue.SourceId));
    }

    [Fact]
    public void Parent_cycle_is_reported_once()
    {
        // 1 -> 3 -> 2 -> 1, with elements 10 and 11 hanging below the cycle.
        var issues = With(d => Element(d, 1).ParentId = 3).Validate();
        Assert.Equal("parent-cycle", Assert.Single(issues).Code);

        Assert.Equal(new[] { "parent-cycle" }, Codes(With(d => Element(d, 13).ParentId = 13)));
    }

    [Fact]
    public void A_long_parent_chain_is_not_a_cycle()
    {
        var doc = new SceneDocument { Models = { new SceneModel { Id = 1, Key = "m" } } };
        for (int i = 1; i <= 50_000; i++) doc.Elements.Add(new SceneElement { Id = i, ModelId = 1, ParentId = i == 1 ? null : i - 1 });
        Assert.Empty(doc.Validate());
    }

    [Fact]
    public void Duplicate_source_id_is_a_warning_inside_one_model_only()
    {
        var issue = Assert.Single(With(d => Element(d, 11).SourceId = "uid-1").Validate());
        Assert.Equal(("duplicate-source-id", SceneSeverity.Warning, "uid-1"), (issue.Code, issue.Severity, issue.SourceId));

        Assert.Empty(With(d => Element(d, 13).SourceId = "uid-1").Validate());   // another model
    }

    [Fact]
    public void Dangling_element()
    {
        Assert.Equal(new[] { "dangling-element" }, Codes(With(d => d.Instances[0].ElementId = 999)));
        Assert.Equal(new[] { "dangling-element" }, Codes(With(d => d.Schedules[0].Tasks[0].Children[0].ElementIds.Add(999))));
        Assert.Equal(new[] { "dangling-element" }, Codes(With(d => d.ClashTests[0].Clashes[0].Element2Id = 999)));
    }

    [Theory]
    [InlineData(9_007_199_254_740_991L, false)]    // 2^53 - 1
    [InlineData(-9_007_199_254_740_991L, false)]
    [InlineData(9_007_199_254_740_992L, true)]     // 2^53
    [InlineData(-9_007_199_254_740_992L, true)]
    [InlineData(long.MaxValue, true)]
    [InlineData(long.MinValue, true)]
    public void Id_range(long id, bool reported)
    {
        var issues = With(d =>
        {
            Element(d, 13).Id = id;
            d.Elements.Add(new SceneElement { Id = 14, ModelId = 2, ParentId = id });
            d.Instances.Add(new SceneInstance { ElementId = id, GeometryId = 1 });
            d.Schedules[0].Tasks[0].Children[0].ElementIds.Add(id);
            d.ClashTests[0].Clashes[1].Element1Id = id;
        }).Validate();

        if (!reported)
        {
            Assert.Empty(issues);
            return;
        }
        Assert.All(issues, issue => Assert.Equal(("id-range", SceneSeverity.Error), (issue.Code, issue.Severity)));
        string range = ": an id is between -(2^53 - 1) and 2^53 - 1.";
        Assert.Equal(
            new[] { "Element id ", "Element 14 names parent id ", "An instance names element id ", "Task 'T1.1' names element id ", "Clash 'c2' names element id " }
                .Select(what => what + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + range),
            issues.Select(issue => issue.Message));
    }

    [Fact]
    public void A_clash_distance_is_optional_and_finite_when_it_is_there()
    {
        Assert.Empty(With(d => d.ClashTests[0].Clashes[0].Distance = null).Validate());
        Assert.Equal(new[] { "not-finite" }, Codes(With(d => d.ClashTests[0].Clashes[0].Distance = double.NaN)));
        Assert.Equal(new[] { "not-finite" }, Codes(With(d => d.ClashTests[0].Clashes[1].Distance = double.NegativeInfinity)));
    }

    [Fact]
    public void Dangling_geometry() => Assert.Equal(new[] { "dangling-geometry" }, Codes(With(d => d.Instances[0].GeometryId = 99)));

    [Fact]
    public void Dangling_material() => Assert.Equal(new[] { "dangling-material" }, Codes(With(d => d.Instances[0].MaterialId = 99)));

    [Fact]
    public void Dangling_texture() => Assert.Equal(new[] { "dangling-texture" }, Codes(With(d => d.Materials[1].TextureId = 99)));

    [Theory]
    [InlineData(SceneTopology.Triangles, 4, true)]
    [InlineData(SceneTopology.Triangles, 6, false)]
    [InlineData(SceneTopology.Lines, 3, true)]
    [InlineData(SceneTopology.Lines, 4, false)]
    [InlineData(SceneTopology.Points, 5, false)]
    [InlineData((SceneTopology)7, 5, false)]   // a topology of a later version is not checked
    public void Vertex_count_of_non_indexed_geometry(SceneTopology topology, int vertices, bool reported)
    {
        var doc = With(d => d.Geometries.Add(new SceneGeometry { Id = 9, Topology = topology, Vertices = [.. Enumerable.Range(0, vertices).Select(i => new Vector3d(i, 0, 0))] }));
        string[] expected = reported ? ["vertex-count"] : [];
        Assert.Equal(expected, Codes(doc));
    }

    [Fact]
    public void A_topology_of_a_later_version_is_still_checked_for_index_range_normal_count_and_uv_count()
    {
        // Four indices are not checked for the topology; they still point into the vertices, and normals and texture
        // coordinates are still one per vertex.
        var doc = With(d => d.Geometries.Add(new SceneGeometry
        {
            Id = 9, Topology = (SceneTopology)7, Vertices = [new(0, 0, 0), new(1, 0, 0)], Indices = [0, 1, 5, 1], Normals = [Vector3d.UnitZ], UVs = [Vector2d.Zero],
        }));
        Assert.Equal(new[] { "index-range", "normal-count", "uv-count" }, Codes(doc));
    }

    [Fact]
    public void Index_count() => Assert.Equal(new[] { "index-count" }, Codes(With(d => d.Geometries[0].Indices = [0, 1, 2, 3])));

    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    public void Index_range(int index) => Assert.Equal(new[] { "index-range" }, Codes(With(d => d.Geometries[0].Indices = [0, 1, index])));

    [Fact]
    public void Normal_count() => Assert.Equal(new[] { "normal-count" }, Codes(With(d => d.Geometries[0].Normals = [Vector3d.UnitZ])));

    [Fact]
    public void Uv_count() => Assert.Equal(new[] { "uv-count" }, Codes(With(d => d.Geometries[0].UVs = [Vector2d.Zero])));

    [Fact]
    public void Not_finite()
    {
        Assert.Equal(new[] { "not-finite" }, Codes(With(d => d.Geometries[2].Vertices = [new(double.NaN, 0, 0)])));
        Assert.Equal(new[] { "not-finite" }, Codes(With(d => d.Geometries[0].Normals = [.. Enumerable.Repeat(new Vector3d(0, double.PositiveInfinity, 0), 4)])));
        Assert.Equal(new[] { "not-finite" }, Codes(With(d => d.Geometries[0].UVs = [.. Enumerable.Repeat(new Vector2d(double.NaN, 0), 4)])));
        Assert.Equal(new[] { "not-finite" }, Codes(With(d => d.Instances[0].Transform = Transform3d.Translate(new(0, 0, double.NegativeInfinity)))));
        Assert.Equal(new[] { "not-finite" }, Codes(With(d => d.PropertySets[0].Properties.Add(new SceneProperty { Name = "Slope", Value = SceneValue.FromNumber(double.NaN, null, "?") }))));
    }

    public static TheoryData<string> OtherNumbers => new(OtherNumberChanges.Keys);

    // Every double outside geometry, transforms and property values: the writer cannot write any of them when it is not finite.
    private static readonly Dictionary<string, Action<SceneDocument>> OtherNumberChanges = new()
    {
        ["material roughness"] = d => d.Materials[0].Roughness = double.NaN,
        ["material metalness"] = d => d.Materials[0].Metalness = double.PositiveInfinity,
        ["texture scale x"] = d => d.Textures[0].ScaleX = double.NaN,
        ["texture scale y"] = d => d.Textures[0].ScaleY = double.NaN,
        ["level elevation"] = d => d.Levels[0].Elevation = double.NegativeInfinity,
        ["grid line start"] = d => d.Grids[0].Lines[0].Start = new(double.NaN, 0, 0),
        ["grid line end"] = d => d.Grids[0].Lines[1].End = new(0, double.NaN, 0),
        ["grid line elevation"] = d => d.Grids[0].Lines[0].Elevation = double.NaN,
        ["camera position"] = d => d.Viewpoints[0].Camera.Position = new(0, 0, double.NaN),
        ["camera target"] = d => d.Viewpoints[0].Camera.Target = new(double.NaN, 0, 0),
        ["camera up"] = d => d.Viewpoints[0].Camera.Up = new(double.NaN, 0, 0),
        ["camera field of view"] = d => d.Viewpoints[1].Camera.FieldOfView = double.PositiveInfinity,
        ["camera aspect ratio"] = d => d.Viewpoints[0].Camera.AspectRatio = double.NaN,
        ["camera near distance"] = d => d.Viewpoints[0].Camera.NearDistance = double.NaN,
        ["camera far distance"] = d => d.Viewpoints[0].Camera.FarDistance = double.PositiveInfinity,
        ["task progress"] = d => d.Schedules[0].Tasks[0].Children[0].Progress = double.NaN,
        ["clash point"] = d => d.ClashTests[0].Clashes[1].Point = new(0, double.NaN, 0),
        ["clash distance"] = d => d.ClashTests[0].Clashes[0].Distance = double.NaN,
    };

    [Theory]
    [MemberData(nameof(OtherNumbers))]
    public void A_number_the_writer_cannot_write_is_reported_as_not_finite(string member)
    {
        var doc = With(OtherNumberChanges[member]);

        var issue = Assert.Single(doc.Validate());
        Assert.Equal(("not-finite", SceneSeverity.Error), (issue.Code, issue.Severity));
        Assert.Throws<ArgumentException>(() => SceneJson.Write(doc));
    }

    [Fact]
    public void An_infinite_number_read_from_a_file_is_reported()
    {
        var doc = SceneJson.Read("{\"Kind\":\"ioscene\",\"Format\":1,\"Models\":[{\"Id\":1,\"Key\":\"m\"}],\"Levels\":[{\"Id\":1,\"ModelId\":1,\"Elevation\":1e999}]}");
        Assert.Equal(new[] { "not-finite" }, Codes(doc));
    }

    [Fact]
    public void Value_payload_follows_the_kind()
    {
        static SceneDocument Scene(SceneValue value) => With(d => d.PropertySets[0].Properties.Add(new SceneProperty { Name = "P", Value = value }));

        Assert.Equal(new[] { "value-payload" }, Codes(Scene(new SceneValue(SceneValueKind.Number, "1 m"))));
        Assert.Equal(new[] { "value-payload" }, Codes(Scene(new SceneValue(SceneValueKind.Integer, "3"))));
        Assert.Equal(new[] { "value-payload" }, Codes(Scene(new SceneValue(SceneValueKind.Boolean, "Yes"))));
        Assert.Equal(new[] { "value-payload" }, Codes(Scene(new SceneValue(SceneValueKind.Boolean, "Yes", Integer: 7))));
        Assert.Equal(new[] { "value-payload" }, Codes(Scene(new SceneValue(SceneValueKind.Boolean, "Yes", Number: 1))));

        var issue = Assert.Single(Scene(new SceneValue(SceneValueKind.Number, "1 m")).Validate());
        Assert.Equal((SceneSeverity.Error, "Property 'P' of set 1 has kind Number and no Number."), (issue.Severity, issue.Message));

        Assert.Empty(Scene(default).Validate());                                             // text, never set
        Assert.Empty(Scene(new SceneValue((SceneValueKind)8, "later")).Validate());          // a kind of a later version
        Assert.Empty(Scene(SceneValue.FromBoolean(false, "No")).Validate());
        Assert.Empty(Scene(SceneValue.FromInteger(long.MinValue, "min")).Validate());
        Assert.Empty(Scene(SceneValue.FromNumber(0, null, "0")).Validate());
    }

    [Fact]
    public void Level_of_another_model_is_a_warning()
    {
        // Level 3 belongs to model 2, element 10 to model 1.
        var issue = Assert.Single(With(d => Element(d, 10).LevelId = 3).Validate());
        Assert.Equal(("level-model", SceneSeverity.Warning, "uid-1"), (issue.Code, issue.Severity, issue.SourceId));
        Assert.Equal("Element 10 of model 1 names level 3 of model 2.", issue.Message);
    }

    private static SceneTask Nested(int levels)
    {
        var root = new SceneTask { Id = "1" };
        var at = root;
        for (int level = 2; level <= levels; level++)
        {
            var child = new SceneTask { Id = level.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            at.Children.Add(child);
            at = child;
        }
        return root;
    }

    [Fact]
    public void A_task_inside_its_own_sub_tasks_is_reported_once_and_the_walk_ends()
    {
        var own = new SceneTask { Id = "own" };
        own.Children.Add(own);
        var issue = Assert.Single(With(d => d.Schedules[1].Tasks.Add(own)).Validate());
        Assert.Equal(("task-tree", SceneSeverity.Error), (issue.Code, issue.Severity));

        // A longer way round: a -> b -> c -> a.
        var a = Nested(3);
        a.Children[0].Children[0].Children.Add(a);
        Assert.Equal("task-tree", Assert.Single(With(d => d.Schedules[1].Tasks.Add(a)).Validate()).Code);

        // One task object listed twice is not a tree either.
        var twice = new SceneTask { Id = "twice" };
        Assert.Equal(new[] { "task-tree" }, Codes(With(d => d.Schedules[1].Tasks.AddRange([twice, twice]))));

        // The writer cannot write a task that returns to itself.
        Assert.Throws<System.Text.Json.JsonException>(() => SceneJson.Write(With(d => d.Schedules[1].Tasks.Add(own))));
    }

    [Fact]
    public void Task_depth_is_limited_to_what_a_file_can_hold()
    {
        var fits = With(d => d.Schedules[1].Tasks.Add(Nested(30)));
        Assert.Empty(fits.Validate());
        // 30 task levels are 64 levels of JSON, the default limit of readers.
        Assert.Empty(System.Text.Json.JsonSerializer.Deserialize<SceneDocument>(SceneJson.Write(fits))!.Validate());

        var issue = Assert.Single(With(d => d.Schedules[1].Tasks.AddRange([Nested(31), Nested(40)])).Validate());
        Assert.Equal(("task-depth", SceneSeverity.Error), (issue.Code, issue.Severity));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("0123456789ABCDEFGHIJK")]      // 21 characters
    [InlineData("0123456789ABCDEFGHIJKLM")]    // 23 characters
    [InlineData("0123456789ABCDEFGHIJK-")]     // a character outside the alphabet
    [InlineData("4123456789ABCDEFGHIJKL")]     // the first character carries two bits only
    public void Ifc_guid_shape(string value)
    {
        var issue = Assert.Single(With(d => Element(d, 11).IfcGuid = value).Validate());
        Assert.Equal(("ifc-guid", SceneSeverity.Warning, "uid-2"), (issue.Code, issue.Severity, issue.SourceId));
    }

    [Fact]
    public void Unit_outside_the_grammar_is_reported_once()
    {
        var issues = With(d =>
        {
            d.PropertySets[0].Properties.Add(new SceneProperty { Name = "Flow", Value = SceneValue.FromNumber(1, "m^3/s", "1") });
            d.PropertySets[1].Properties.Add(new SceneProperty { Name = "Flow", Value = SceneValue.FromNumber(2, "m^3/s", "2") });
        }).Validate();

        var issue = Assert.Single(issues);
        Assert.Equal(("unit-grammar", SceneSeverity.Warning), (issue.Code, issue.Severity));
        Assert.Contains("m^3/s", issue.Message);
    }

    [Fact]
    public void Messages_do_not_depend_on_the_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            var issue = Assert.Single(With(d => Element(d, 10).LevelId = -99).Validate());
            Assert.Equal("Element 10 names level -99.", issue.Message);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
