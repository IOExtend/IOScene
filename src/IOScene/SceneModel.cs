namespace IOScene;

/// <summary>
/// One source file, or one placement of a linked file. A file linked twice is two models.
/// </summary>
public sealed class SceneModel
{
    /// <summary>Scene-local id, referenced by <c>ModelId</c> of elements, levels, grids and schedules.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Key that is unique in the scene, never empty and stable across runs. (<see cref="Key"/>,
    /// <see cref="SceneElement.SourceId"/>) is the identity that may be stored outside the scene. A producer derives
    /// it in one way for good: the caller's value when given, else the file name without directory, with
    /// <c>#2</c>, <c>#3</c> in source order for repeated placements of one file.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Path of the source file as the producer was given it, or null.</summary>
    public string? Path { get; set; }

    /// <summary>
    /// Registered id of the format of the content, not of the container (<c>rvt</c>; <c>ifc</c> for both
    /// <c>.ifc</c> and <c>.ifczip</c>), or null. The ids are registered in <c>docs/keys.md</c>.
    /// </summary>
    public string? SourceFormat { get; set; }

    /// <summary>Application that wrote the source file, or null.</summary>
    public string? Application { get; set; }

    /// <summary>Author recorded in the source file, or null.</summary>
    public string? Author { get; set; }

    /// <summary>Creation time recorded in the source file, as text, or null.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Length unit of the source file. Information only: the geometry is already in metres.</summary>
    public string? SourceLengthUnit { get; set; }

    /// <summary>
    /// Package and version that produced the model: the package id and the NuGet version without build metadata,
    /// separated by one space, such as <c>RvtIO.Scene 0.5.0</c>. It tells "absent in the source" from "the producer
    /// is older than the member".
    /// </summary>
    public string? Producer { get; set; }
}
