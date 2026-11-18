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
}
