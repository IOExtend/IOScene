using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using IOScene;

namespace IOCoord;

/// <summary>Writes IOScene clash results as buildingSMART BCF 2.1 topics.</summary>
public static class BcfClashWriter
{
    /// <summary>Writes a BCFZIP archive with one topic per clash. The output stream remains open.</summary>
    /// <param name="scene">Scene containing the results and the corresponding IFC GlobalIds.</param>
    /// <param name="output">Writable stream.</param>
    /// <param name="author">BCF creation author.</param>
    /// <param name="createdAt">Creation timestamp for the exported topics.</param>
    public static void Write(SceneDocument scene, Stream output, string author, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        XmlConvert.VerifyXmlChars(author);
        if (scene.Validate().FirstOrDefault(issue => issue.Severity == SceneSeverity.Error) is { } error)
            throw new InvalidDataException($"Invalid scene ({error.Code}): {error.Message}");

        var elements = scene.Elements.ToDictionary(element => element.Id);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteXml(zip, "bcf.version", new XElement("Version", new XAttribute("VersionId", "2.1")));
        foreach (var test in scene.ClashTests)
        {
            foreach (var clash in test.Clashes)
            {
                string topicId = Guid.NewGuid().ToString("D");
                string folder = topicId + "/";
                zip.CreateEntry(folder, CompressionLevel.NoCompression);

                SceneElement? first = clash.Element1Id is long firstId ? elements[firstId] : null;
                SceneElement? second = clash.Element2Id is long secondId ? elements[secondId] : null;
                var topic = new XElement("Topic",
                    new XAttribute("Guid", topicId),
                    new XAttribute("TopicType", "Clash"),
                    new XAttribute("TopicStatus", clash.Status.ToString()),
                    new XElement("Title", string.IsNullOrWhiteSpace(clash.Name) ? test.Name : clash.Name),
                    new XElement("CreationDate", XmlConvert.ToString(createdAt.UtcDateTime, XmlDateTimeSerializationMode.Utc)),
                    new XElement("CreationAuthor", author),
                    new XElement("Description", Describe(test, clash, first, second)));
                var markup = new XElement("Markup", topic);

                var ids = new[] { first?.IfcGuid, second?.IfcGuid }.Where(IsIfcGuid).Distinct(StringComparer.Ordinal).ToArray();
                if (ids.Length != 0)
                {
                    string viewpointId = Guid.NewGuid().ToString("D");
                    markup.Add(new XElement("Viewpoints", new XAttribute("Guid", viewpointId),
                        new XElement("Viewpoint", "viewpoint.bcfv")));
                    WriteXml(zip, folder + "viewpoint.bcfv", new XElement("VisualizationInfo",
                        new XAttribute("Guid", viewpointId),
                        new XElement("Components",
                            new XElement("Selection", ids.Select(id => new XElement("Component", new XAttribute("IfcGuid", id!)))),
                            new XElement("Visibility", new XAttribute("DefaultVisibility", true)))));
                }
                WriteXml(zip, folder + "markup.bcf", markup);
            }
        }
    }

    private static string Describe(SceneClashTest test, SceneClashResult clash, SceneElement? first, SceneElement? second)
    {
        string a = string.IsNullOrEmpty(clash.Element1Name) ? first?.Name ?? string.Empty : clash.Element1Name;
        string b = string.IsNullOrEmpty(clash.Element2Name) ? second?.Name ?? string.Empty : clash.Element2Name;
        string location = FormattableString.Invariant(
            $"{test.Name}\n{a} / {b}\nPoint (m): {clash.Point.X:R}, {clash.Point.Y:R}, {clash.Point.Z:R}");
        return clash.Distance is double distance
            ? location + FormattableString.Invariant($"\nDistance (m): {distance:R}")
            : location;
    }

    private static bool IsIfcGuid(string? value) =>
        value is { Length: 22 } && value[0] is >= '0' and <= '3'
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '$');

    private static void WriteXml(ZipArchive zip, string path, XElement root)
    {
        using var stream = zip.CreateEntry(path).Open();
        root.Save(stream, SaveOptions.DisableFormatting);
    }
}
