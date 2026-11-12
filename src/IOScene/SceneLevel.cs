namespace IOScene;

/// <summary>
/// A level (storey) of one model.
/// </summary>
public sealed class SceneLevel
{
    /// <summary>Scene-local id, referenced by <see cref="SceneElement.LevelId"/>.</summary>
    public int Id { get; set; }

    /// <summary>Id of the <see cref="SceneModel"/> the level belongs to.</summary>
    public int ModelId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Elevation in metres in scene coordinates; null when the source does not give it.</summary>
    public double? Elevation { get; set; }
}

/// <summary>
/// The grid lines of one model.
/// </summary>
public sealed class SceneGridSystem
{
    /// <summary>Id of the <see cref="SceneModel"/> the grid belongs to.</summary>
    public int ModelId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "Grids";

    /// <summary>The grid lines.</summary>
    public List<SceneGridLine> Lines { get; set; } = [];
}

/// <summary>
/// One grid line (axis).
/// </summary>
public sealed class SceneGridLine
{
    /// <summary>Label of the axis, such as <c>1</c> or <c>A</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Start point in scene coordinates.</summary>
    public Vector3d Start { get; set; }

    /// <summary>End point in scene coordinates.</summary>
    public Vector3d End { get; set; }

    /// <summary>
    /// Z in metres, in scene coordinates, of the level the line is drawn on. <see cref="Start"/> and
    /// <see cref="End"/> are authoritative for drawing.
    /// </summary>
    public double Elevation { get; set; }
}
