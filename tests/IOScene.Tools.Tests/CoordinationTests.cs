using System.IO.Compression;
using System.Xml.Linq;
using IOCoord;

namespace IOScene.Tools.Tests;

public class CoordinationTests
{
    [Fact]
    public void Grid_FindsExtendedCrossingAndUsesSegmentBoundsOnRequest()
    {
        var grid = new SceneGridSystem
        {
            Lines =
            [
                new() { Name = "A", Start = new(0, 0, 3), End = new(1, 0, 3) },
                new() { Name = "1", Start = new(2, 0.5, 3), End = new(2, 2, 3) },
            ]
        };
        var crossing = SceneGrid.NearestIntersection(grid, new(2, 0.25));
        Assert.NotNull(crossing);
        Assert.Equal(new Vector2d(2, 0), crossing.Value.Point);
        Assert.Equal(0.25, crossing.Value.Distance);
        Assert.Equal("A", SceneGrid.NearestLine(grid, new(1.7, 0.1))?.Line.Name);
        Assert.Equal("1", SceneGrid.NearestLine(grid, new(1.7, 0.1), segmentOnly: true)?.Line.Name);
    }

    [Fact]
    public void Diff_MatchesPersistentIdentityAndReportsMovementIndependentlyOfIdOrder()
    {
        var before = new SceneDocument();
        before.AddMesh("Panel", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]).SourceId = "panel";
        var after = new SceneDocument();
        after.AddMesh("Unidentified", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]);
        after.AddMesh("Panel", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]).SourceId = "panel";
        Assert.Empty(SceneDiff.Compare(before, after).Changes);

        after.Instances[1].Transform = Transform3d.Translate(Vector3d.UnitZ);
        var comparison = SceneDiff.Compare(before, after);
        var change = Assert.Single(comparison.Changes);
        Assert.Equal(SceneChangeKind.Modified, change.Kind);
        Assert.False(change.MetadataChanged);
        Assert.True(change.GeometryChanged);
        Assert.Equal("panel", change.SourceId);
        Assert.Equal(1, comparison.UnidentifiedRevision);
    }

    [Fact]
    public void Diff_RejectsDuplicatePersistentIdentity()
    {
        var scene = new SceneDocument();
        scene.AddMesh("One", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]).SourceId = "ambiguous";
        scene.AddMesh("Two", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]).SourceId = "ambiguous";
        Assert.Throws<InvalidDataException>(() => SceneDiff.Compare(scene, scene));
    }

    [Fact]
    public void Bcf_WritesTopicAndSelectsOnlyKnownIfcComponents()
    {
        var scene = new SceneDocument();
        var element = scene.AddMesh("Beam", [Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY], [0, 1, 2]);
        element.IfcGuid = "0" + new string('A', 21);
        scene.ClashTests.Add(new SceneClashTest
        {
            Name = "Structure",
            Clashes =
            [
                new() { Id = "local", Name = "Beam / wall", Element1Id = element.Id, Element2Name = "Wall",
                    Point = new(1, 2, 3), Status = SceneClashStatus.Reviewed }
            ]
        });
        using var output = new MemoryStream();
        BcfClashWriter.Write(scene, output, "Author", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.Equal("2.1", (string?)Load(zip.GetEntry("bcf.version")!).Attribute("VersionId"));
        var markupEntry = Assert.Single(zip.Entries, item => item.Name == "markup.bcf");
        var markup = Load(markupEntry);
        var topic = markup.Element("Topic")!;
        string topicId = (string)topic.Attribute("Guid")!;
        Assert.True(Guid.TryParseExact(topicId, "D", out _));
        Assert.NotNull(zip.GetEntry(topicId + "/"));
        Assert.Equal("Reviewed", (string?)topic.Attribute("TopicStatus"));
        Assert.Equal("Author", (string?)topic.Element("CreationAuthor"));
        Assert.Contains("Point (m): 1, 2, 3", (string?)topic.Element("Description"));
        var viewpoint = Load(zip.GetEntry(topicId + "/viewpoint.bcfv")!);
        var component = Assert.Single(viewpoint.Descendants("Component"));
        Assert.Equal(element.IfcGuid, (string?)component.Attribute("IfcGuid"));
        Assert.Equal((string?)markup.Element("Viewpoints")!.Attribute("Guid"), (string?)viewpoint.Attribute("Guid"));

        static XElement Load(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            return XElement.Load(stream);
        }
    }
}
