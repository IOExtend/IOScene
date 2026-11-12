namespace IOScene.Tests;

public class ModelTests
{
    private static readonly Vector3d[] Quad = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)];
    private static readonly int[] QuadIndices = [0, 1, 2, 0, 2, 3];

    [Fact]
    public void A_new_document_has_the_kind_and_format_of_the_library()
    {
        var doc = new SceneDocument();
        Assert.Equal(("ioscene", 1), (doc.Kind, doc.Format));
        Assert.Throws<InvalidDataException>(() => new SceneDocument { Format = 2 });
        Assert.Throws<InvalidDataException>(() => new SceneDocument { Kind = "ioviewer" });
    }

    [Fact]
    public void AddMesh_creates_the_model_once_and_numbers_from_the_highest_id()
    {
        var doc = new SceneDocument { Title = "Two quads" };
        var first = doc.AddMesh("First", Quad, QuadIndices);
        var second = doc.AddMesh("Second", Quad, [], new RgbaColor(1, 2, 3));

        var model = Assert.Single(doc.Models);
        Assert.Equal(("model", "Two quads"), (model.Key, model.Name));
        Assert.Equal((1L, 2L), (first.Id, second.Id));
        Assert.Equal(new[] { 1, 2 }, doc.Geometries.Select(g => g.Id));
        Assert.Equal(new RgbaColor(1, 2, 3), doc.Instances[1].Color);
        Assert.Same(second, doc.Elements[1]);
    }

    [Fact]
    public void AddMesh_joins_an_existing_scene()
    {
        var doc = GoldenScenes.Everything();
        var added = doc.AddMesh("Extra", Quad, QuadIndices);

        Assert.Equal(14, added.Id);
        Assert.Equal(1, added.ModelId);
        Assert.Equal(4, doc.Geometries[^1].Id);
        Assert.Equal(2, doc.Models.Count);
        Assert.Empty(doc.Validate());
    }

    [Fact]
    public void ComputeBounds_places_every_instance_and_skips_what_is_not_placed()
    {
        var doc = new SceneDocument();
        doc.AddMesh("Quad", Quad, QuadIndices);
        doc.Geometries.Add(new SceneGeometry { Id = 7, Topology = SceneTopology.Points, Vertices = [new(100, 100, 100)] });   // no instance
        doc.Instances.Add(new SceneInstance { ElementId = 1, GeometryId = 1, Transform = new(new(0, 2, 0), new(-2, 0, 0), Vector3d.UnitZ, new(10, 0, 5)) });
        doc.Instances.Add(new SceneInstance { ElementId = 1, GeometryId = 99 });   // missing geometry

        Assert.Equal(new BoundingBox3d(new(0, 0, 0), new(10, 2, 5)), doc.ComputeBounds());
    }

    [Fact]
    public void ComputeBounds_of_a_scene_without_placed_geometry_is_empty()
    {
        var bounds = new SceneDocument().ComputeBounds();
        Assert.True(bounds.IsEmpty);
        Assert.Equal(BoundingBox3d.Empty, bounds);
    }

    [Fact]
    public void A_value_always_has_a_display_text()
    {
        Assert.Equal(string.Empty, default(SceneValue).Text);
        Assert.Equal(string.Empty, new SceneProperty().Value.Text);
        Assert.Equal(("No", 0L), (SceneValue.FromBoolean(false).Text, SceneValue.FromBoolean(false).Integer));
        Assert.Equal(("-12", -12L), (SceneValue.FromInteger(-12).Text, SceneValue.FromInteger(-12).Integer));
        Assert.Equal(new SceneValue(SceneValueKind.Number, "3 m", 3, null, "m"), SceneValue.FromNumber(3, SceneUnits.Metre, "3 m"));
    }

    [Fact]
    public void Transform_applies_axes_then_translation_and_composes_in_order()
    {
        var turn = new Transform3d(Vector3d.UnitY, -Vector3d.UnitX, Vector3d.UnitZ, Vector3d.Zero);   // quarter turn about Z
        var move = Transform3d.Translate(new(10, 0, 0));

        Assert.Equal(new Vector3d(0, 1, 0), turn.Apply(Vector3d.UnitX));
        Assert.Equal(new Vector3d(10, 1, 0), turn.Then(move).Apply(Vector3d.UnitX));
        Assert.Equal(new Vector3d(0, 11, 0), move.Then(turn).Apply(Vector3d.UnitX));
        Assert.Equal(new Vector3d(3, 4, 5), Transform3d.Identity.Apply(new(3, 4, 5)));
    }

    [Fact]
    public void Bounding_box_grows_from_empty()
    {
        var box = BoundingBox3d.Empty.Encapsulate(new Vector3d(1, 2, 3)).Encapsulate(new Vector3d(-1, 5, 0));

        Assert.Equal(new BoundingBox3d(new(-1, 2, 0), new(1, 5, 3)), box);
        Assert.Equal(new Vector3d(0, 3.5, 1.5), box.Center);
        Assert.Equal(new Vector3d(2, 3, 3), box.Size);
        Assert.Equal(box, BoundingBox3d.Empty.Encapsulate(box));
        Assert.Equal(box, box.Encapsulate(BoundingBox3d.Empty));
    }

    [Fact]
    public void Vector_and_colour_helpers()
    {
        Assert.Equal(5, new Vector3d(3, 4, 0).Length);
        Assert.Equal(new Vector3d(0.6, 0.8, 0), new Vector3d(3, 4, 0).Normalized());
        Assert.Equal(Vector3d.Zero, Vector3d.Zero.Normalized());
        Assert.Equal(Vector3d.UnitZ, Vector3d.Cross(Vector3d.UnitX, Vector3d.UnitY));
        Assert.Equal(32, Vector3d.Dot(new(1, 2, 3), new(4, 5, 6)));
        Assert.Equal("(1.5, 2, 3)", new Vector3d(1.5, 2, 3).ToString());
        Assert.Equal("#0a141e", new RgbaColor(10, 20, 30).ToHex());
        Assert.Equal(255, new RgbaColor(10, 20, 30).A);
    }
}
