namespace IOScene;

/// <summary>
/// Appearance of a surface.
/// </summary>
public sealed class SceneMaterial
{
    /// <summary>Scene-local id, referenced by <see cref="SceneInstance.MaterialId"/>.</summary>
    public int Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base colour. Its alpha is the only opacity of the material. When the material has a texture, the colour
    /// multiplies it (glTF <c>baseColorFactor</c>), so white shows the plain texture.
    /// </summary>
    public RgbaColor Color { get; set; } = new(200, 200, 200);

    /// <summary>Roughness from 0 (mirror) to 1 (matt).</summary>
    public double Roughness { get; set; } = 0.5;

    /// <summary>Metalness from 0 (dielectric) to 1 (metal).</summary>
    public double Metalness { get; set; } = 0.1;

    /// <summary>Id of the <see cref="SceneTexture"/>, or null.</summary>
    public int? TextureId { get; set; }
}

/// <summary>
/// An image used by materials.
/// </summary>
public sealed class SceneTexture
{
    /// <summary>Scene-local id, referenced by <see cref="SceneMaterial.TextureId"/>.</summary>
    public int Id { get; set; }

    /// <summary>MIME type of <see cref="Data"/>, such as <c>image/png</c>.</summary>
    public string MimeType { get; set; } = "image/png";

    /// <summary>The encoded image file. In JSON it is a base64 string.</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Width of one repeat of the image, in units of the U texture coordinate: the image is sampled at
    /// U / <see cref="ScaleX"/> with repeat wrapping.
    /// </summary>
    public double ScaleX { get; set; } = 1.0;

    /// <summary>
    /// Height of one repeat of the image, in units of the V texture coordinate: the image is sampled at
    /// V / <see cref="ScaleY"/> with repeat wrapping, and V = 0 is its bottom row.
    /// </summary>
    public double ScaleY { get; set; } = 1.0;
}
