using System.Text;
using IOScene.Formats;

namespace IOScene.Tools.Tests;

public class ObjSceneWriterTests
{
    [Fact]
    public void Write_BakesPlacementAndPreservesWindingWhenMirrored()
    {
        var scene = new SceneDocument();
        scene.AddMesh("Left panel\nf 9 8 7\\", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitZ], [0, 1, 2]);
        scene.Instances[0].Transform = new(-Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ, new(10, 20, 30));
        using var obj = new StringWriter();

        ObjSceneWriter.Write(scene, obj);

        string text = obj.ToString();
        Assert.Equal(["o Left_panel_f_9_8_7_", "v 10 20 30", "v 9 20 30", "v 10 20 31", "f 1 3 2"],
            text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var geometry = Assert.Single(ObjSceneReader.Load(input).Geometries);
        Assert.Equal([new(10, 20, 30), new(10, 20, 31), new(9, 20, 30)], geometry.Vertices);
    }

    [Fact]
    public void Write_EmitsLinesPointsAndEffectiveMaterialColours()
    {
        var scene = new SceneDocument();
        scene.AddMesh("Line", [Vector3d.Zero, Vector3d.UnitX], []);
        scene.Geometries[0].Topology = SceneTopology.Lines;
        scene.AddMesh("Point", [Vector3d.UnitZ], []);
        scene.Geometries[1].Topology = SceneTopology.Points;
        scene.Materials.Add(new SceneMaterial { Id = 1, Color = new(255, 0, 0) });
        scene.Instances[0].MaterialId = 1;
        scene.Instances[0].Color = new(0, 255, 0, 128);
        scene.Instances[1].MaterialId = 1;
        using var obj = new StringWriter();
        using var mtl = new StringWriter();

        ObjSceneWriter.Write(scene, obj, mtl, "model.mtl");

        string objText = obj.ToString();
        Assert.Contains("mtllib model.mtl", objText);
        Assert.Contains("usemtl material_1", objText);
        Assert.Contains("usemtl material_2", objText);
        Assert.Contains("l 1 2", objText);
        Assert.Contains("p 3", objText);
        string mtlText = mtl.ToString();
        Assert.Contains("Kd 0 1 0", mtlText);
        Assert.Contains("d 0.5019607843137255", mtlText);
        Assert.Contains("Kd 1 0 0", mtlText);
    }

    [Fact]
    public void Write_InvalidMaterialNameLeavesBothWritersEmpty()
    {
        using var obj = new StringWriter();
        using var mtl = new StringWriter();

        Assert.Throws<ArgumentException>(() => ObjSceneWriter.Write(new SceneDocument(), obj, mtl, "../model.mtl"));
        Assert.Empty(obj.ToString());
        Assert.Empty(mtl.ToString());
    }

    [Fact]
    public void Save_ExportFailurePreservesAnExistingFile()
    {
        var scene = new SceneDocument();
        scene.AddMesh("Panel", [Vector3d.Zero, new(double.MaxValue, 0, 0), Vector3d.UnitY], [0, 1, 2]);
        scene.Instances[0].Transform = new(Vector3d.UnitX * 2, Vector3d.UnitY, Vector3d.UnitZ, Vector3d.Zero);
        string path = Path.Combine(Path.GetTempPath(), $"scene-{Guid.NewGuid():N}.obj");
        try
        {
            File.WriteAllText(path, "existing content");

            Assert.Throws<InvalidDataException>(() => ObjSceneWriter.Save(scene, path));
            Assert.Equal("existing content", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
