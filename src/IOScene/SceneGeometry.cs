using System.Text.Json.Serialization;

namespace IOScene;

/// <summary>
/// What the vertices of a geometry form. The enum is open: a later version may add values, and a consumer skips a
/// geometry whose topology it does not know and reports it.
/// </summary>
public enum SceneTopology
{
    /// <summary>Triangles: three vertices each.</summary>
    Triangles = 0,

    /// <summary>Separate line segments: two vertices each.</summary>
    Lines = 1,

    /// <summary>Points: one vertex each.</summary>
    Points = 2,
}

/// <summary>
/// Geometry in local coordinates, placed in the scene by one or more <see cref="SceneInstance"/> entries. Large
/// offsets belong in the instance transform, not in the vertices.
/// </summary>
public sealed class SceneGeometry
{
    /// <summary>Scene-local id, referenced by <see cref="SceneInstance.GeometryId"/>.</summary>
    public int Id { get; set; }

    /// <summary>What the vertices form.</summary>
    public SceneTopology Topology { get; set; }

    /// <summary>Vertex positions in local coordinates, metres. In JSON the list is one flat array of numbers.</summary>
    [JsonConverter(typeof(FlatListConverter<Vector3d, Vector3dNumbers>))]
    public IReadOnlyList<Vector3d> Vertices { get; set; } = [];

    /// <summary>
    /// Vertex indices: triples, pairs or singles by <see cref="Topology"/>. An empty list means the geometry is
    /// not indexed: the vertices are consumed in order.
    /// </summary>
    public IReadOnlyList<int> Indices { get; set; } = [];

    /// <summary>One normal per vertex, or null.</summary>
    [JsonConverter(typeof(FlatListConverter<Vector3d, Vector3dNumbers>))]
    public IReadOnlyList<Vector3d>? Normals { get; set; }

    /// <summary>One texture coordinate per vertex, or null.</summary>
    [JsonConverter(typeof(FlatListConverter<Vector2d, Vector2dNumbers>))]
    public IReadOnlyList<Vector2d>? UVs { get; set; }
}

/// <summary>
/// One placement of a geometry for an element. It is the only link between an element and its geometry, and the
/// only way to share geometry. There is one level: producers flatten nested blocks.
/// </summary>
public sealed class SceneInstance
{
    /// <summary>Id of the <see cref="SceneElement"/> that owns the placement. Every instance belongs to an element.</summary>
    public long ElementId { get; set; }

    /// <summary>Id of the placed <see cref="SceneGeometry"/>.</summary>
    public int GeometryId { get; set; }

    /// <summary>Affine transform from local to scene coordinates, mirror included; null is the identity.</summary>
    public Transform3d? Transform { get; set; }

    /// <summary>Id of the <see cref="SceneMaterial"/>, or null.</summary>
    public int? MaterialId { get; set; }

    /// <summary>Colour that overrides the material colour, or null. Its alpha is the opacity.</summary>
    public RgbaColor? Color { get; set; }

    /// <summary>Name of the placement, or null.</summary>
    public string? Name { get; set; }
}
