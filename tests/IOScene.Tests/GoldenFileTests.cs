namespace IOScene.Tests;

/// <summary>
/// Golden files of format 1. They are committed and never regenerated: every later version of the library reads
/// them, finds them valid and writes the same bytes back.
/// </summary>
public class GoldenFileTests
{
    public static TheoryData<string> Names => new(GoldenScenes.Names);

    [Theory]
    [MemberData(nameof(Names))]
    public void A_golden_file_is_read_as_a_valid_scene(string name)
    {
        var doc = SceneJson.Load(GoldenScenes.PathOf(name));

        Assert.Empty(doc.Validate());
        Assert.NotEmpty(doc.Models);
        Assert.NotEmpty(doc.Elements);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void A_golden_file_is_written_back_byte_for_byte(string name)
    {
        string text = GoldenScenes.TextOf(name);
        Assert.Equal(text, SceneJson.Write(SceneJson.Read(text)));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_writer_still_produces_the_golden_file(string name) =>
        Assert.Equal(GoldenScenes.TextOf(name), SceneJson.Write(GoldenScenes.Build(name)));

    [Fact]
    public void The_scene_with_every_type_is_read_back_with_its_content()
    {
        var doc = SceneJson.Load(GoldenScenes.PathOf("everything"));

        Assert.Equal("Golden scene", doc.Title);
        Assert.Equal(new[] { "house.rvt", "site.ifc" }, doc.Models.Select(m => m.Key));
        Assert.Equal("RvtIO.Scene 0.5.0", doc.Models[0].Producer);
        Assert.Equal(new byte[] { 1, 2, 3, 250 }, doc.Textures[0].Data);
        Assert.Equal(new RgbaColor(170, 74, 68, 128), doc.Materials[0].Color);

        var values = doc.PropertySets[0].Properties.Select(p => p.Value).ToList();
        Assert.Equal(new SceneValue(SceneValueKind.Number, "12.5 m²", Number: 12.5, Unit: "m2"), values[0]);
        Assert.Equal(SceneValue.FromText("W-01"), values[1]);
        Assert.Equal(new SceneValue(SceneValueKind.Boolean, "Yes", Integer: 1), values[2]);
        Assert.Equal(new SceneValue(SceneValueKind.Integer, "3", Integer: 3), values[3]);
        Assert.Equal("Стена <1> & \"2\"", doc.PropertySets[1].Properties[1].Value.Text);

        Assert.Null(doc.Levels[2].Elevation);
        Assert.Equal(2.7432, doc.Geometries[0].Vertices[2].Z, 12);
        Assert.Equal(SceneTopology.Lines, doc.Geometries[1].Topology);
        Assert.Empty(doc.Geometries[2].Indices);

        var wall = doc.Elements.Single(e => e.Id == 10);
        Assert.Equal(("uid-1", "0123456789ABCDEFGHIJ_$", 3L, SceneNodeKind.Element, 1), (wall.SourceId, wall.IfcGuid, wall.ParentId, wall.Kind, wall.LevelId));
        Assert.Equal(new[] { 1, 2 }, wall.PropertySetIds);
        Assert.Equal(new[] { SceneNodeKind.Model, SceneNodeKind.Level, SceneNodeKind.Group }, doc.Elements.Take(3).Select(e => e.Kind));

        Assert.Equal(new Vector3d(1_000_000.125, 2_000_000.5, 12.75), doc.Instances[0].Transform!.Value.Translation);
        Assert.Equal(new Vector3d(-1, 0, 0), doc.Instances[1].Transform!.Value.XAxis);
        Assert.Null(doc.Instances[2].Transform);
        Assert.Equal(new RgbaColor(255, 0, 0), doc.Instances[3].Color);

        Assert.Equal(3.2, doc.Grids[0].Lines[1].Elevation);
        Assert.Equal("vp-1", doc.Viewpoints[0].Id);
        Assert.Equal(SceneProjection.Orthographic, doc.Viewpoints[1].Camera.Projection);

        var task = doc.Schedules[0].Tasks[0].Children[0];
        Assert.Equal(new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero), task.ActualStart);
        Assert.Equal(1500.50m, task.PlannedCost);
        Assert.Equal(1725m, task.ActualCost);
        Assert.Equal(new[] { 10L, 11L }, task.ElementIds);
        Assert.Equal(SceneTaskType.Demolish, doc.Schedules[0].Tasks[1].TaskType);
        Assert.Null(doc.Schedules[1].ModelId);

        var clash = doc.ClashTests[0].Clashes[0];
        Assert.Equal((10L, 12L, SceneClashStatus.Reviewed, -0.05), (clash.Element1Id, clash.Element2Id, clash.Status, clash.Distance));
        Assert.Null(doc.ClashTests[0].Clashes[1].Element1Id);
    }

    [Fact]
    public void The_split_halves_are_valid_documents_on_their_own()
    {
        var properties = SceneJson.Load(GoldenScenes.PathOf("no-geometry"));
        Assert.Empty(properties.Geometries);
        Assert.Empty(properties.Instances);
        Assert.True(properties.ComputeBounds().IsEmpty);

        var geometry = SceneJson.Load(GoldenScenes.PathOf("no-properties"));
        Assert.Empty(geometry.PropertySets);
        Assert.Equal(new BoundingBox3d(new(0, 0, 0), new(1, 1, 3)), geometry.ComputeBounds());
    }
}
