using System.Text;
using IOScene.Formats;

namespace IOScene.Tools.Tests;

public class ObjSceneReaderTests
{
    [Fact]
    public void Load_ResolvesRelativeIndicesAndCompactsEachObjectsVertices()
    {
        const string obj = """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            o Quad
            f -4 -3 -2 -1
            g Face
            f 2 3 4
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(obj));

        var scene = ObjSceneReader.Load(stream);

        Assert.Empty(scene.Validate());
        Assert.Equal(["Quad", "Face"], scene.Elements.Select(element => element.Name));
        Assert.Equal([0, 1, 2, 0, 2, 3], scene.Geometries[0].Indices);
        Assert.Equal(3, scene.Geometries[1].Vertices.Count);
        Assert.Equal(Vector3d.UnitX, scene.Geometries[1].Vertices[0]);
    }
}
