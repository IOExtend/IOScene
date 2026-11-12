namespace IOScene;

/// <summary>
/// Projection of a camera.
/// </summary>
public enum SceneProjection
{
    /// <summary>Perspective projection.</summary>
    Perspective = 0,

    /// <summary>Parallel projection.</summary>
    Orthographic = 1,
}

/// <summary>
/// Camera position, orientation and lens.
/// </summary>
public sealed class SceneCamera
{
    /// <summary>Eye position in scene coordinates.</summary>
    public Vector3d Position { get; set; } = new(10, 10, 10);

    /// <summary>Point the camera looks at.</summary>
    public Vector3d Target { get; set; } = Vector3d.Zero;

    /// <summary>Up direction.</summary>
    public Vector3d Up { get; set; } = Vector3d.UnitZ;

    /// <summary>
    /// Vertical field of view in radians. For an orthographic camera the visible height is
    /// <c>2 * |Target - Position| * tan(FieldOfView / 2)</c>.
    /// </summary>
    public double FieldOfView { get; set; } = 0.785;

    /// <summary>Width of the view divided by its height.</summary>
    public double AspectRatio { get; set; } = 1.6;

    /// <summary>Distance to the near clipping plane in metres.</summary>
    public double NearDistance { get; set; } = 0.1;

    /// <summary>Distance to the far clipping plane in metres.</summary>
    public double FarDistance { get; set; } = 1000.0;

    /// <summary>Projection.</summary>
    public SceneProjection Projection { get; set; }
}

/// <summary>
/// A saved viewpoint.
/// </summary>
public sealed class SceneViewpoint
{
    /// <summary>Id of the viewpoint in its source when there is one (a saved-viewpoint or BCF viewpoint GUID), or null.</summary>
    public string? Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "Viewpoint";

    /// <summary>The camera.</summary>
    public SceneCamera Camera { get; set; } = new();
}
