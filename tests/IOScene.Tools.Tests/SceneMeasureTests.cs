using IOCoord;

namespace IOScene.Tools.Tests;

public class SceneMeasureTests
{
    [Fact]
    public void MeasuresDistanceAngleAndPathInSceneUnits()
    {
        Assert.Equal(5, SceneMeasure.Distance(Vector3d.Zero, new(3, 4, 0)));
        Assert.Equal(90, SceneMeasure.AngleDegrees(Vector3d.UnitX, Vector3d.Zero, Vector3d.UnitZ));
        Assert.Equal(7, SceneMeasure.PathLength([Vector3d.Zero, new(3, 4, 0), new(3, 4, 2)]));
        Assert.Equal(0, SceneMeasure.PathLength([]));
    }

    [Fact]
    public void PointToElement_UsesWorldCoordinatesAndTheClosestPlacement()
    {
        var scene = new SceneDocument();
        var element = scene.AddMesh("Panel", [Vector3d.Zero, new(2, 0, 0), new(0, 2, 0)], [0, 1, 2]);
        scene.Instances.Add(new SceneInstance
        {
            ElementId = element.Id,
            GeometryId = scene.Geometries[0].Id,
            Transform = new(-Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ, new(10, 10, 5)),
        });

        var result = SceneMeasure.PointToElement(scene, element.Id, new(9.5, 10.5, 7));

        Assert.Equal(new PointMeasurement(new(9.5, 10.5, 5), 2), result);
    }

    [Fact]
    public void PointToElement_MeasuresDegenerateTriangleAsSegments()
    {
        var scene = new SceneDocument();
        var element = scene.AddMesh("Line", [Vector3d.Zero, Vector3d.UnitX, new(2, 0, 0)], []);
        Assert.Equal(new PointMeasurement(new(1, 0, 0), 1), SceneMeasure.PointToElement(scene, element.Id, new(1, 1, 0)));
        Assert.Equal(new PointMeasurement(new(2, 0, 0), 1), SceneMeasure.PointToElement(scene, element.Id, new(3, 0, 0)));

        scene.Instances.Clear();
        Assert.Null(SceneMeasure.PointToElement(scene, element.Id, Vector3d.Zero));
    }

    [Fact]
    public void PointToElement_RejectsInvalidGeometry()
    {
        var scene = new SceneDocument();
        var element = scene.AddMesh("Broken", [Vector3d.Zero], [0, 0, 2]);
        Assert.Throws<InvalidDataException>(() => SceneMeasure.PointToElement(scene, element.Id, Vector3d.Zero));
        Assert.Throws<ArgumentException>(() => SceneMeasure.Distance(Vector3d.Zero, new(double.NaN, 0, 0)));
    }
}
