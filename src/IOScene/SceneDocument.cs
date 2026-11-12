using System.Text.Json.Serialization;

namespace IOScene;

/// <summary>
/// A scene: what a viewer or a coordination tool shows, filters, measures or tests. It never holds what an authoring
/// tool edits, and no source file is rebuilt from it.
/// </summary>
/// <remarks>
/// <para>All coordinates and lengths are in metres, right-handed, Z up, in one coordinate space per document.
/// Producers convert on the way in.</para>
/// <para>Every cross-reference is by id, never by list position. Ids are local to the document; only
/// (<see cref="SceneModel.Key"/>, <see cref="SceneElement.SourceId"/>) and <see cref="SceneElement.IfcGuid"/> may be
/// written to anything that outlives it.</para>
/// <para>The lists are declared in the order <see cref="SceneJson"/> writes them: a table comes after the tables it
/// refers to.</para>
/// </remarks>
public sealed partial class SceneDocument
{
    /// <summary>Value of <see cref="Kind"/> in every scene file.</summary>
    public const string DocumentKind = "ioscene";

    /// <summary>
    /// The newest file format this library reads, and the one it writes. It is a property and not a constant, so a
    /// program compiled against an older version reads the number of the library it runs with.
    /// </summary>
    public static int CurrentFormat => 1;

    // Task levels that fit the 64 levels of nesting that JSON readers allow by default.
    private const int MaxTaskDepth = 30;

#pragma warning disable CA1822 // Kind and Format are instance members: the serialiser writes and reads them as members of the root.
    /// <summary>
    /// Root discriminator of the JSON file, always <see cref="DocumentKind"/>. Assigning any other value throws
    /// <see cref="InvalidDataException"/>, which is how a file of another kind is rejected on reading.
    /// </summary>
    [JsonRequired]
    public string Kind
    {
        get => DocumentKind;
        init
        {
            if (value != DocumentKind)
                throw new InvalidDataException($"Not an IOScene document: Kind is '{value}', expected '{DocumentKind}'.");
        }
    }

    /// <summary>
    /// File format number, always <see cref="CurrentFormat"/> in memory: the model has no format, a file has.
    /// Assigning a number outside 1..<see cref="CurrentFormat"/> throws <see cref="InvalidDataException"/>, which is
    /// how a file of a newer format is rejected on reading.
    /// </summary>
    [JsonRequired]
    public int Format
    {
        get => CurrentFormat;
        init
        {
            if (value < 1 || value > CurrentFormat)
                throw new InvalidDataException(FormattableString.Invariant($"IOScene format {value} is not supported; this library reads formats 1 to {CurrentFormat}."));
        }
    }

#pragma warning restore CA1822

    /// <summary>Display title of the scene.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Source files, or placements of linked files. A valid scene has at least one.</summary>
    public List<SceneModel> Models { get; set; } = [];

    /// <summary>Materials, referenced by <see cref="SceneInstance.MaterialId"/>.</summary>
    public List<SceneMaterial> Materials { get; set; } = [];

    /// <summary>Textures, referenced by <see cref="SceneMaterial.TextureId"/>.</summary>
    public List<SceneTexture> Textures { get; set; } = [];

    /// <summary>Property sets, shared between elements by id.</summary>
    public List<ScenePropertySet> PropertySets { get; set; } = [];

    /// <summary>Levels (storeys), referenced by <see cref="SceneElement.LevelId"/>.</summary>
    public List<SceneLevel> Levels { get; set; } = [];

    /// <summary>Geometries in local coordinates, placed by <see cref="Instances"/>.</summary>
    public List<SceneGeometry> Geometries { get; set; } = [];

    /// <summary>All elements as a flat list; the tree is <see cref="SceneElement.ParentId"/>.</summary>
    public List<SceneElement> Elements { get; set; } = [];

    /// <summary>Placements of geometries. An instance is the only link between an element and its geometry.</summary>
    public List<SceneInstance> Instances { get; set; } = [];

    /// <summary>Grid systems, each of one model.</summary>
    public List<SceneGridSystem> Grids { get; set; } = [];

    /// <summary>Saved viewpoints.</summary>
    public List<SceneViewpoint> Viewpoints { get; set; } = [];

    /// <summary>4D schedules.</summary>
    public List<SceneSchedule> Schedules { get; set; } = [];

    /// <summary>Clash tests with their results. Rules, tolerances and the clash engine are not part of a scene.</summary>
    public List<SceneClashTest> ClashTests { get; set; } = [];

    /// <summary>
    /// Quick-start helper: adds one element drawn by one triangle mesh and returns the element. When the scene has no
    /// model yet, one is created with the key <c>model</c>.
    /// </summary>
    /// <param name="name">Element name.</param>
    /// <param name="vertices">Vertex positions in metres, Z up.</param>
    /// <param name="indices">Vertex indices, three per triangle; empty when every three vertices are a triangle.</param>
    /// <param name="color">Colour of the mesh; null leaves the choice to the consumer.</param>
    /// <returns>The new element, which belongs to the first model of the scene.</returns>
    public SceneElement AddMesh(string name, IReadOnlyList<Vector3d> vertices, IReadOnlyList<int> indices, RgbaColor? color = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);

        if (Models.Count == 0) Models.Add(new SceneModel { Id = 1, Key = "model", Name = Title });
        // shortcut: the next id is found by a scan of the list, upgrade if a producer ever adds meshes by the thousand through this helper.
        var geometry = new SceneGeometry { Id = Geometries.Count == 0 ? 1 : Geometries.Max(g => g.Id) + 1, Vertices = vertices, Indices = indices };
        var element = new SceneElement { Id = Elements.Count == 0 ? 1 : Elements.Max(e => e.Id) + 1, ModelId = Models[0].Id, Name = name };
        Geometries.Add(geometry);
        Elements.Add(element);
        Instances.Add(new SceneInstance { ElementId = element.Id, GeometryId = geometry.Id, Color = color });
        return element;
    }

    /// <summary>
    /// Computes the box around every placed vertex, of every topology. Geometries that no instance places do not
    /// count, and an instance that names a missing geometry is skipped.
    /// </summary>
    /// <returns>The bounds in scene coordinates, or <see cref="BoundingBox3d.Empty"/> when nothing is placed.</returns>
    public BoundingBox3d ComputeBounds()
    {
        var geometries = new Dictionary<int, SceneGeometry>(Geometries.Count);
        foreach (var geometry in Geometries) geometries.TryAdd(geometry.Id, geometry);

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        foreach (var instance in Instances)
        {
            if (!geometries.TryGetValue(instance.GeometryId, out var geometry)) continue;
            var transform = instance.Transform ?? Transform3d.Identity;
            foreach (var vertex in geometry.Vertices)
            {
                var p = transform.Apply(vertex);
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
                maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); maxZ = Math.Max(maxZ, p.Z);
            }
        }
        return new BoundingBox3d(new(minX, minY, minZ), new(maxX, maxY, maxZ));
    }

    internal SceneDocument WithElements(List<SceneElement> elements)
    {
        var copy = (SceneDocument)MemberwiseClone();
        copy.Elements = elements;
        return copy;
    }
}
