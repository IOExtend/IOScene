namespace IOScene.Tests;

/// <summary>
/// The scenes behind the golden files of format 1 in tests/golden/format-1. The files were written once from these
/// builders and are never regenerated: a later version must read them and write the same bytes.
/// </summary>
internal static class GoldenScenes
{
    public static readonly string[] Names = ["everything", "no-geometry", "no-properties", "lines-points"];

    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "golden", "format-1", name + ".json");

    /// <summary>File text without the final line break that the repository adds to text files.</summary>
    public static string TextOf(string name) => File.ReadAllText(PathOf(name)).TrimEnd('\r', '\n');

    public static SceneDocument Build(string name) => name switch
    {
        "everything" => Everything(),
        "no-geometry" => NoGeometry(),
        "no-properties" => NoProperties(),
        "lines-points" => LinesAndPoints(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown golden scene."),
    };

    /// <summary>A small scene that uses every type and every member of the model.</summary>
    public static SceneDocument Everything()
    {
        var doc = new SceneDocument { Title = "Golden scene" };
        doc.Models.Add(new SceneModel
        {
            Id = 1, Key = "house.rvt", Name = "House", Path = "models/house.rvt", SourceFormat = "rvt", Application = "Authoring tool 2026",
            Author = "A. Author", CreatedAt = "2026-01-15T10:30:00Z", SourceLengthUnit = "ft", Producer = "RvtIO.Scene 0.5.0",
        });
        doc.Models.Add(new SceneModel { Id = 2, Key = "site.ifc", Name = "Site", SourceFormat = "ifc" });

        doc.Textures.Add(new SceneTexture { Id = 1, MimeType = "image/png", Data = [1, 2, 3, 250], ScaleX = 0.5, ScaleY = 0.25 });
        doc.Materials.Add(new SceneMaterial { Id = 1, Name = "Brick", Color = new(170, 74, 68, 128), Roughness = 0.9, Metalness = 0, TextureId = 1 });
        doc.Materials.Add(new SceneMaterial { Id = 2, Name = "Steel" });

        doc.PropertySets.Add(new ScenePropertySet
        {
            Id = 1, Name = "Dimensions", Key = "revit:instance",
            Properties =
            {
                new SceneProperty { Name = "Area", Key = "revit:bip:-1012805", Value = SceneValue.FromNumber(12.5, SceneUnits.SquareMetre, "12.5 m²") },
                new SceneProperty { Name = "Mark", Value = SceneValue.FromText("W-01") },
                new SceneProperty { Name = "Structural", Value = SceneValue.FromBoolean(true, "Yes") },
                new SceneProperty { Name = "Count", Value = SceneValue.FromInteger(3, "3") },
                new SceneProperty { Name = "Ratio", Value = SceneValue.FromNumber(0.25, null, "0.25") },
            },
        });
        doc.PropertySets.Add(new ScenePropertySet
        {
            Id = 2, Name = "Pset_WallCommon", Key = "ifc:Pset_WallCommon",
            Properties =
            {
                new SceneProperty { Name = "IsExternal", Key = "ifc:Pset_WallCommon.IsExternal", Value = SceneValue.FromBoolean(false, "FALSE") },
                new SceneProperty { Name = "Наименование", Value = SceneValue.FromText("Стена <1> & \"2\"") },
            },
        });

        doc.Levels.Add(new SceneLevel { Id = 1, ModelId = 1, Name = "Level 1", Elevation = 0 });
        doc.Levels.Add(new SceneLevel { Id = 2, ModelId = 1, Name = "Level 2", Elevation = 3.2 });
        doc.Levels.Add(new SceneLevel { Id = 3, ModelId = 2, Name = "Site" });

        doc.Geometries.Add(new SceneGeometry
        {
            Id = 1,
            Vertices = [new(0, 0, 0), new(4, 0, 0), new(4, 0, 0.3048 * 9), new(0, 0, 0.3048 * 9)],
            Indices = [0, 1, 2, 0, 2, 3],
            Normals = [new(0, -1, 0), new(0, -1, 0), new(0, -1, 0), new(0, -1, 0)],
            UVs = [new(0, 0), new(4, 0), new(4, 2.7432), new(0, 2.7432)],
        });
        doc.Geometries.Add(new SceneGeometry { Id = 2, Topology = SceneTopology.Lines, Vertices = [new(0, 0, 0), new(4, 0, 0), new(4, 3, 0)], Indices = [0, 1, 1, 2] });
        doc.Geometries.Add(new SceneGeometry { Id = 3, Topology = SceneTopology.Points, Vertices = [new(1, 2, 3), new(4, 5, 6)] });

        doc.Elements.Add(new SceneElement { Id = 1, ModelId = 1, Kind = SceneNodeKind.Model, Name = "House" });
        doc.Elements.Add(new SceneElement { Id = 2, ModelId = 1, ParentId = 1, Kind = SceneNodeKind.Level, Name = "Level 1", LevelId = 1 });
        doc.Elements.Add(new SceneElement { Id = 3, ModelId = 1, ParentId = 2, Kind = SceneNodeKind.Group, Name = "Walls" });
        doc.Elements.Add(new SceneElement
        {
            Id = 10, ModelId = 1, SourceId = "uid-1", IfcGuid = "0123456789ABCDEFGHIJ_$", ParentId = 3, Name = "Wall 1", Category = "Walls",
            TypeName = "Basic Wall 200", LevelId = 1, PropertySetIds = { 1, 2 },
        });
        doc.Elements.Add(new SceneElement { Id = 11, ModelId = 1, SourceId = "uid-2", ParentId = 3, Name = "Wall 2", Category = "Walls", TypeName = "Basic Wall 200", LevelId = 1, PropertySetIds = { 1 } });
        doc.Elements.Add(new SceneElement { Id = 12, ModelId = 2, SourceId = "2O2Fr$t4X7Zf8NOew3FLOH", IfcGuid = "2O2Fr$t4X7Zf8NOew3FLOH", Name = "Survey points", Category = "IfcAnnotation", LevelId = 3 });
        doc.Elements.Add(new SceneElement { Id = 13, ModelId = 2, Name = "Space without geometry", Category = "IfcSpace" });

        doc.Instances.Add(new SceneInstance { ElementId = 10, GeometryId = 1, MaterialId = 1, Transform = Transform3d.Translate(new(1_000_000.125, 2_000_000.5, 12.75)), Name = "Body" });
        doc.Instances.Add(new SceneInstance { ElementId = 11, GeometryId = 1, MaterialId = 2, Color = new(1, 2, 3, 4), Transform = new(new(-1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(100, 200, 300)) });
        doc.Instances.Add(new SceneInstance { ElementId = 11, GeometryId = 2 });
        doc.Instances.Add(new SceneInstance { ElementId = 12, GeometryId = 3, Color = new(255, 0, 0) });

        doc.Grids.Add(new SceneGridSystem
        {
            ModelId = 1,
            Lines =
            {
                new SceneGridLine { Name = "1", Start = new(0, -1, 0), End = new(0, 10, 0) },
                new SceneGridLine { Name = "A", Start = new(-1, 0, 3.2), End = new(10, 0, 3.2), Elevation = 3.2 },
            },
        });

        doc.Viewpoints.Add(new SceneViewpoint { Id = "vp-1", Name = "Front" });
        doc.Viewpoints.Add(new SceneViewpoint
        {
            Name = "Plan",
            Camera = new SceneCamera
            {
                Position = new(2, 1.5, 50), Target = new(2, 1.5, 0), Up = Vector3d.UnitY, FieldOfView = 0.5, AspectRatio = 1.25, NearDistance = 1, FarDistance = 200,
                Projection = SceneProjection.Orthographic,
            },
        });

        var start = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
        doc.Schedules.Add(new SceneSchedule
        {
            ModelId = 1, Name = "Construction",
            Tasks =
            {
                new SceneTask
                {
                    Id = "T1", Name = "Structure", PlannedStart = start, PlannedEnd = start.AddDays(30),
                    Children =
                    {
                        new SceneTask
                        {
                            Id = "T1.1", Name = "Walls", PlannedStart = start, PlannedEnd = start.AddDays(10), ActualStart = start.AddDays(1), ActualEnd = start.AddDays(12),
                            PlannedCost = 1500.50m, ActualCost = 1725m, Progress = 0.5, ElementIds = { 10, 11 },
                        },
                    },
                },
                new SceneTask { Id = "T2", Name = "Remove survey points", TaskType = SceneTaskType.Demolish, PlannedStart = start.AddDays(30), PlannedEnd = start.AddDays(31), ElementIds = { 12 } },
                new SceneTask { Id = "T3", Name = "Scaffolding", TaskType = SceneTaskType.Temporary, PlannedStart = start, PlannedEnd = start.AddDays(20) },
            },
        });
        doc.Schedules.Add(new SceneSchedule { Name = "Whole site" });

        doc.ClashTests.Add(new SceneClashTest
        {
            Name = "Walls x Site",
            Clashes =
            {
                new SceneClashResult { Id = "c1", Name = "Clash 1", Element1Id = 10, Element2Id = 12, Element1Name = "Wall 1", Element2Name = "Survey points", Point = new(1, 2, 3), Distance = -0.05, Status = SceneClashStatus.Reviewed },
                new SceneClashResult { Id = "c2", Name = "Clash 2", Element1Name = "Unknown", Element2Name = "Unknown", Distance = 0, Status = SceneClashStatus.New },
            },
        });
        return doc;
    }

    /// <summary>Elements with properties and no geometry at all: the property half of a split export.</summary>
    public static SceneDocument NoGeometry()
    {
        var doc = new SceneDocument { Title = "Properties only" };
        doc.Models.Add(new SceneModel { Id = 1, Key = "house.rvt", Name = "House", SourceFormat = "rvt" });
        doc.PropertySets.Add(new ScenePropertySet { Id = 1, Name = "Identity", Properties = { new SceneProperty { Name = "Mark", Value = SceneValue.FromText("D-01") } } });
        doc.Levels.Add(new SceneLevel { Id = 1, ModelId = 1, Name = "Level 1", Elevation = 0 });
        doc.Elements.Add(new SceneElement { Id = 1, ModelId = 1, SourceId = "uid-1", Name = "Door", Category = "Doors", LevelId = 1, PropertySetIds = { 1 } });
        doc.Elements.Add(new SceneElement { Id = 2, ModelId = 1, SourceId = "uid-2", Name = "Room", Category = "Rooms", PropertySetIds = { 1 } });
        return doc;
    }

    /// <summary>Geometry and elements with no property set: the geometry half of a split export.</summary>
    public static SceneDocument NoProperties()
    {
        var doc = new SceneDocument { Title = "Geometry only" };
        doc.Models.Add(new SceneModel { Id = 1, Key = "house.rvt", Name = "House", SourceFormat = "rvt" });
        doc.Geometries.Add(new SceneGeometry { Id = 1, Vertices = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], Indices = [0, 1, 2, 0, 2, 3] });
        doc.Elements.Add(new SceneElement { Id = 1, ModelId = 1, SourceId = "uid-1", Name = "Slab" });
        doc.Elements.Add(new SceneElement { Id = 2, ModelId = 1, SourceId = "uid-2", Name = "Slab copy" });
        doc.Instances.Add(new SceneInstance { ElementId = 1, GeometryId = 1 });
        doc.Instances.Add(new SceneInstance { ElementId = 2, GeometryId = 1, Transform = Transform3d.Translate(new(0, 0, 3)) });
        return doc;
    }

    /// <summary>Non-indexed lines, points and triangles: empty indices, vertices consumed in order.</summary>
    public static SceneDocument LinesAndPoints()
    {
        var doc = new SceneDocument { Title = "Lines and points" };
        doc.Models.Add(new SceneModel { Id = 1, Key = "plan.dwg", Name = "Plan", SourceFormat = "dwg" });
        doc.Geometries.Add(new SceneGeometry { Id = 1, Topology = SceneTopology.Lines, Vertices = [new(0, 0, 0), new(5, 0, 0), new(5, 0, 0), new(5, 5, 0)] });
        doc.Geometries.Add(new SceneGeometry { Id = 2, Topology = SceneTopology.Points, Vertices = [new(1, 1, 0), new(2, 2, 0), new(3, 3, 0)] });
        doc.Geometries.Add(new SceneGeometry { Id = 3, Vertices = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)] });
        doc.Elements.Add(new SceneElement { Id = 1, ModelId = 1, SourceId = "2A", Name = "Outline" });
        doc.Elements.Add(new SceneElement { Id = 2, ModelId = 1, SourceId = "2B", Name = "Markers" });
        doc.Elements.Add(new SceneElement { Id = 3, ModelId = 1, SourceId = "2C", Name = "Hatch" });
        doc.Instances.Add(new SceneInstance { ElementId = 1, GeometryId = 1 });
        doc.Instances.Add(new SceneInstance { ElementId = 2, GeometryId = 2 });
        doc.Instances.Add(new SceneInstance { ElementId = 3, GeometryId = 3 });
        return doc;
    }
}
