using IOCoord;

namespace IOScene.Tools.Tests;

public class SceneClashTests
{
    [Fact]
    public void Intersections_UsePlacedTrianglesAndReportOneResultPerPair()
    {
        var scene = new SceneDocument();
        var horizontal = scene.AddMesh("Slab", [new(0, 0, 0), new(2, 0, 0), new(0, 2, 0)], [0, 1, 2]);
        var vertical = scene.AddMesh("Panel", [new(0, 0, -1), new(0, 0, 1), new(0, 1, 0.5)], [0, 1, 2]);
        scene.Instances[1].Transform = Transform3d.Translate(new(0.5, 0.5, 0));
        for (int i = 1; i <= 12; i++)
        {
            scene.Instances.Add(new SceneInstance
            {
                ElementId = horizontal.Id,
                GeometryId = scene.Geometries[0].Id,
                Transform = Transform3d.Translate(new(i * 5, 0, 0)),
            });
        }

        var test = SceneClash.FindSurfaceIntersections(scene, [horizontal.Id, vertical.Id], [vertical.Id, horizontal.Id]);

        var contact = Assert.Single(test.Clashes);
        Assert.Equal(horizontal.Id, contact.Element1Id);
        Assert.Equal(vertical.Id, contact.Element2Id);
        Assert.Equal(new Vector3d(0.5, 0.5, 0), contact.Point);
        Assert.Null(contact.Distance);
        Assert.Empty(scene.ClashTests);
    }

    [Fact]
    public void Intersections_CheckCoplanarCrossingsAndRejectOverlappingBoundsAlone()
    {
        var scene = new SceneDocument();
        var slab = scene.AddMesh("Slab", [new(0, 0, 0), new(4, 0, 0), new(2, 4, 0)], [0, 1, 2]);
        var crossing = scene.AddMesh("Crossing", [new(0, 3, 0), new(4, 3, 0), new(2, -1, 0)], [0, 1, 2]);
        var outside = scene.AddMesh("Outside", [new(3.5, 3.5, 0), new(5, 3.5, 0), new(4, 5, 0)], [0, 1, 2]);

        var test = SceneClash.FindSurfaceIntersections(scene, [slab.Id], [crossing.Id, outside.Id]);

        var contact = Assert.Single(test.Clashes);
        Assert.Equal(crossing.Id, contact.Element2Id);
        Assert.Equal(0, contact.Point.Z);
    }

    [Fact]
    public void Clearances_KeepTheShortestGapAcrossPlacementsAndIncludeTheLimit()
    {
        var scene = new SceneDocument();
        var first = scene.AddMesh("Slab", [new(0, 0, 0), new(4, 0, 0), new(0, 4, 0)], [0, 1, 2]);
        var second = scene.AddMesh("Duct", [new(1, 1, 0), new(2, 1, 0), new(1, 2, 0)], [0, 1, 2]);
        scene.Instances[1].Transform = Transform3d.Translate(new(0, 0, 0.75));
        scene.Instances.Add(new SceneInstance
        {
            ElementId = second.Id,
            GeometryId = scene.Geometries[1].Id,
            Transform = Transform3d.Translate(new(0, 0, 0.25)),
        });

        var result = Assert.Single(SceneClash.FindSurfaceClearances(scene, [first.Id], [second.Id], 1).Clashes);
        Assert.Equal(0.25, result.Distance);
        Assert.Equal(new Vector3d(1, 1, 0.125), result.Point);
        Assert.Single(SceneClash.FindSurfaceClearances(scene, [second.Id], [first.Id], 0.25).Clashes);
        Assert.Empty(SceneClash.FindSurfaceClearances(scene, [first.Id], [second.Id], 0.24).Clashes);
        Assert.Empty(scene.ClashTests);
    }

    [Fact]
    public void Clearances_MeasureAnInteriorEdgePairAndKeepIntersectingPairsAtZero()
    {
        var scene = new SceneDocument();
        var slab = scene.AddMesh("Slab", [new(0, 0, 0), new(4, 0, 0), new(2, 4, 0)], [0, 1, 2]);
        var crossing = scene.AddMesh("Crossing", [new(0, 3, 0), new(4, 3, 0), new(2, -1, 0)], [0, 1, 2]);
        scene.Instances[1].Transform = Transform3d.Translate(new(0, 0, 1.25));

        var result = Assert.Single(SceneClash.FindSurfaceClearances(scene, [slab.Id], [crossing.Id], 1.25).Clashes);
        Assert.Equal(1.25, result.Distance!.Value, 12);
        Assert.Equal(0.625, result.Point.Z, 12);

        scene.Instances[1].Transform = Transform3d.Identity;
        result = Assert.Single(SceneClash.FindSurfaceClearances(scene, [slab.Id], [crossing.Id], 0).Clashes);
        Assert.Equal(0, result.Distance);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Clearances_RejectInvalidLimits(double limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SceneClash.FindSurfaceClearances(new SceneDocument(), [], [], limit));
    }

    [Fact]
    public void HardClashes_UseOnlyBoundsOfContactingInstancesForTheDepth()
    {
        var scene = new SceneDocument();
        var first = AddBox(scene, "Wall", Vector3d.Zero, 2);
        var second = AddBox(scene, "Column", new(1.5, 0.25, 0.25), 2);
        scene.Instances.Add(new SceneInstance
        {
            ElementId = second.Id,
            GeometryId = scene.Geometries[1].Id,
            Transform = Transform3d.Translate(new(-12, 0, 0)),
        });

        var clash = Assert.Single(SceneClash.FindHardClashes(scene, [first.Id], [second.Id], 0.5).Clashes);
        Assert.Equal(-0.5, clash.Distance);
        Assert.Empty(SceneClash.FindHardClashes(scene, [first.Id], [second.Id], 0.51).Clashes);
        Assert.Equal(-0.5, Assert.Single(SceneClash.FindBoundingBoxOverlaps(scene, [first.Id], [second.Id]).Clashes).Distance);
    }

    [Fact]
    public void HardClashes_RetainZeroDepthSurfaceContacts()
    {
        var scene = new SceneDocument();
        var first = AddBox(scene, "Wall", Vector3d.Zero, 2);
        var second = AddBox(scene, "Column", new(2, 0, 0), 2);

        var clash = Assert.Single(SceneClash.FindHardClashes(scene, [first.Id], [second.Id], 0.1).Clashes);
        Assert.Equal(0, clash.Distance);
        Assert.Equal(2, clash.Point.X);
    }

    [Fact]
    public void BoundingBoxOverlaps_IncludeNestedBoxesWithNoSurfaceContact()
    {
        var scene = new SceneDocument();
        var first = AddBox(scene, "Outer", Vector3d.Zero, 4);
        var second = AddBox(scene, "Inner", new(1, 1, 1), 1);
        var disjoint = AddBox(scene, "Outside", new(12, 0, 0), 1);

        Assert.Empty(SceneClash.FindHardClashes(scene, [first.Id], [second.Id, disjoint.Id]).Clashes);
        var overlap = Assert.Single(SceneClash.FindBoundingBoxOverlaps(scene, [first.Id], [second.Id, disjoint.Id]).Clashes);
        Assert.Equal(second.Id, overlap.Element2Id);
        Assert.Equal(new Vector3d(1.5, 1.5, 1.5), overlap.Point);
        Assert.Equal(-1, overlap.Distance);
    }

    [Fact]
    public void Duplicates_ComparePlacedVerticesAndEffectiveIndexOrder()
    {
        var scene = new SceneDocument();
        Vector3d[] vertices = [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY];
        var original = scene.AddMesh("Original", vertices, [0, 1, 2]);
        var matching = scene.AddMesh("Matching", vertices, []);
        var reordered = scene.AddMesh("Reordered", vertices, [0, 2, 1]);
        var points = scene.AddMesh("Points", vertices, [0, 1, 2]);
        scene.Geometries[3].Topology = SceneTopology.Points;
        scene.Instances[1].Transform = Transform3d.Translate(new(0, 0, 0.125));
        long[] selection = [original.Id, matching.Id, reordered.Id, points.Id];

        var duplicate = Assert.Single(SceneClash.FindDuplicates(scene, [original.Id], selection, 0.125).Clashes);
        Assert.Equal(matching.Id, duplicate.Element2Id);
        Assert.Equal(0, duplicate.Distance);
        Assert.Empty(SceneClash.FindDuplicates(scene, [original.Id], selection, 0.124).Clashes);
    }

    private static SceneElement AddBox(SceneDocument scene, string name, Vector3d position, double side)
    {
        var element = scene.AddMesh(name,
        [
            new(0, 0, 0), new(side, 0, 0), new(side, side, 0), new(0, side, 0),
            new(0, 0, side), new(side, 0, side), new(side, side, side), new(0, side, side),
        ],
        [
            0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7,
        ]);
        scene.Instances[^1].Transform = Transform3d.Translate(position);
        return element;
    }
}
