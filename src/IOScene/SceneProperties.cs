using System.Text.Json.Serialization;

namespace IOScene;

#pragma warning disable CA1720 // The contract names the value kind and its member Integer.

/// <summary>
/// A named group of properties. Sets live in <see cref="SceneDocument.PropertySets"/> and elements reference them by
/// id, so the parameters of a type are stored once.
/// </summary>
public sealed class ScenePropertySet
{
    /// <summary>Scene-local id, referenced by <see cref="SceneElement.PropertySetIds"/>.</summary>
    public int Id { get; set; }

    /// <summary>Display name, as the source application shows it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable machine key qualified by the producer, such as <c>ifc:Pset_WallCommon</c>, or null. The scene does not
    /// interpret it; the key namespaces are registered in <c>docs/keys.md</c>.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>The properties, in display order.</summary>
    public List<SceneProperty> Properties { get; set; } = [];
}

/// <summary>
/// One named value of a property set.
/// </summary>
public sealed class SceneProperty
{
    /// <summary>Display name, as the source application shows it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable machine key qualified by the producer, such as <c>revit:bip:-1001107</c> or
    /// <c>ifc:Pset_WallCommon.IsExternal</c>, or null.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>The value.</summary>
    public SceneValue Value { get; set; }
}

/// <summary>
/// Type of a property value. The enum is open: a later version may add values, and a consumer treats a value it
/// does not know as <see cref="Text"/>, whose display string is always there.
/// </summary>
public enum SceneValueKind
{
    /// <summary>Text only.</summary>
    Text = 0,

    /// <summary>A boolean, carried in <see cref="SceneValue.Integer"/> as 0 or 1.</summary>
    Boolean = 1,

    /// <summary>A whole number in <see cref="SceneValue.Integer"/>.</summary>
    Integer = 2,

    /// <summary>A real number in <see cref="SceneValue.Number"/>.</summary>
    Number = 3,
}

/// <summary>
/// A typed property value. A producer that cannot type a value writes <see cref="SceneValueKind.Text"/>; values are
/// never guessed from strings.
/// </summary>
/// <remarks>The record grows by <c>init</c> properties only, never by a positional parameter.</remarks>
/// <param name="Kind">Type of the value.</param>
/// <param name="Text">Display string, as the source shows the value. It is always set.</param>
/// <param name="Number">The number for <see cref="SceneValueKind.Number"/>; in coherent SI when <paramref name="Unit"/> is set.</param>
/// <param name="Integer">The number for <see cref="SceneValueKind.Integer"/>, and 0 or 1 for <see cref="SceneValueKind.Boolean"/>.</param>
/// <param name="Unit">
/// Unit of <paramref name="Number"/> in the grammar of <see cref="SceneUnits"/>; null when the value is dimensionless
/// or was not converted.
/// </param>
public readonly record struct SceneValue([property: JsonPropertyOrder(-2)] SceneValueKind Kind, string Text, double? Number = null, long? Integer = null, string? Unit = null)
{
    /// <summary>Display string, as the source shows the value. Never null: an unset value reads as an empty string.</summary>
    [JsonPropertyOrder(-1)]   // files list Kind, Text, then the typed members
    public string Text { get => field ?? string.Empty; init; } = Text;

    /// <summary>Creates a text value.</summary>
    /// <param name="text">Display string.</param>
    public static SceneValue FromText(string text) => new(SceneValueKind.Text, text);

    /// <summary>
    /// Compares the five members as they read, so a value that was never set equals a text value with an empty
    /// string, which is also what it is read back as from a file.
    /// </summary>
    /// <param name="other">Value to compare with.</param>
    /// <returns>True when every member is equal.</returns>
    public bool Equals(SceneValue other) =>
        Kind == other.Kind && Text == other.Text && Nullable.Equals(Number, other.Number) && Integer == other.Integer && Unit == other.Unit;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, Text, Number, Integer, Unit);

    /// <summary>Creates a boolean value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="text">Display string as the source shows it, such as <c>Yes</c> or <c>TRUE</c>.</param>
    public static SceneValue FromBoolean(bool value, string text) =>
        new(SceneValueKind.Boolean, text, Integer: value ? 1 : 0);

    /// <summary>Creates an integer value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="text">Display string as the source shows it.</param>
    public static SceneValue FromInteger(long value, string text) =>
        new(SceneValueKind.Integer, text, Integer: value);

    /// <summary>Creates a real number value.</summary>
    /// <param name="value">The number, in coherent SI when <paramref name="unit"/> is set.</param>
    /// <param name="unit">Unit in the grammar of <see cref="SceneUnits"/>, or null.</param>
    /// <param name="text">Display string as the source shows it, with the source's own unit.</param>
    public static SceneValue FromNumber(double value, string? unit, string text) =>
        new(SceneValueKind.Number, text, Number: value, Unit: unit);
}
