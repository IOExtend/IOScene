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
        Assert.Equal(new[] { "dangling-model" }, Codes(With(d => d.Levels[2].ModelId = 99)));
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
