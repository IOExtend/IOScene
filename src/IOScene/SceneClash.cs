namespace IOScene;

/// <summary>
/// Review status of a clash. The enum is open: a later version may add values, and a consumer treats a value it
/// does not know as <see cref="Active"/>.
/// </summary>
public enum SceneClashStatus
{
    /// <summary>Found by the latest run and not seen before.</summary>
    New = 0,

    /// <summary>Found before and still present.</summary>
    Active = 1,

    /// <summary>Reviewed by a person.</summary>
    Reviewed = 2,

    /// <summary>Accepted as it is.</summary>
    Approved = 3,

    /// <summary>No longer present.</summary>
    Resolved = 4,
}

/// <summary>
/// One clash between two elements.
/// </summary>
public sealed class SceneClashResult
{
    /// <summary>Id of the clash, valid inside this document only.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Id of the first element when it is known. Scene-local, valid inside this document only.</summary>
    public long? Element1Id { get; set; }

    /// <summary>Id of the second element when it is known. Scene-local, valid inside this document only.</summary>
    public long? Element2Id { get; set; }

    /// <summary>Display name of the first element.</summary>
    public string Element1Name { get; set; } = string.Empty;

    /// <summary>Display name of the second element.</summary>
    public string Element2Name { get; set; } = string.Empty;

    /// <summary>Location of the clash in scene coordinates.</summary>
    public Vector3d Point { get; set; }

    /// <summary>
    /// Clash distance in metres, or null when the source does not report it. Below 0 it is the penetration depth,
    /// from 0 up it is the clearance (the Navisworks convention).
    /// </summary>
    public double? Distance { get; set; }

    /// <summary>Review status.</summary>
    public SceneClashStatus Status { get; set; } = SceneClashStatus.Active;
}

/// <summary>
/// A clash test with its results. Rules, tolerances and the clash engine are not part of a scene.
/// </summary>
public sealed class SceneClashTest
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "Clash Test";

    /// <summary>The clashes found.</summary>
    public List<SceneClashResult> Clashes { get; set; } = [];
}
