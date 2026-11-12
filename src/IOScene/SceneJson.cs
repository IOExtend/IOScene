using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace IOScene;

/// <summary>
/// Reads and writes scene JSON: one UTF-8 object whose root starts with <c>"Kind": "ioscene"</c> and
/// <c>"Format": 1</c>.
/// </summary>
/// <remarks>
/// <para>Member names are the property names. Null members are omitted and enums are numbers. Math values are number
/// arrays (<see cref="Vector2d"/> 2, <see cref="Vector3d"/> 3, <see cref="Transform3d"/> 12, <see cref="RgbaColor"/>
/// 4), vertex, normal and texture coordinate lists are one flat array each, and texture data is base64.</para>
/// <para>Reading ignores members it does not know, gives a missing optional member its default and keeps enum
/// numbers it does not know. A file without <c>Kind</c> or <c>Format</c>, of another kind or of a newer format is
/// rejected with <see cref="InvalidDataException"/> and never half-read.</para>
/// <para>The <see cref="Stream"/> methods are the primary API; a .NET string caps a document at about a billion
/// characters.</para>
/// </remarks>
public static class SceneJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Non-ASCII text (names, display values) is written as UTF-8 instead of \u escapes.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Harden } },
    };

    /// <summary>Writes a scene as UTF-8 JSON. The stream is left open.</summary>
    /// <param name="document">Scene to write. It is not changed.</param>
    /// <param name="stream">Writable stream.</param>
    /// <exception cref="ArgumentException">A number in the scene is NaN or infinite; <see cref="SceneDocument.Validate"/> reports the common cases as <c>not-finite</c>.</exception>
    public static void Write(SceneDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        JsonSerializer.Serialize(stream, InWriteOrder(document), Options);
    }

    /// <summary>Writes a scene to a JSON string. A convenience for small scenes; prefer the stream overload.</summary>
    /// <param name="document">Scene to write. It is not changed.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentException">A number in the scene is NaN or infinite.</exception>
    public static string Write(SceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(InWriteOrder(document), Options);
    }

    /// <summary>
    /// Writes a scene to a file. The scene is written beside the target first and moved over it when complete, so a
    /// failed write leaves an existing file as it was.
    /// </summary>
    /// <param name="document">Scene to write. It is not changed.</param>
    /// <param name="path">Path of the file to create or replace.</param>
    /// <exception cref="ArgumentException">A number in the scene is NaN or infinite.</exception>
    public static void Save(SceneDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = File.Create(temporary)) Write(document, file);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    /// <summary>Reads a scene from UTF-8 JSON. The stream is left open.</summary>
    /// <param name="stream">Readable stream positioned at the start of the JSON.</param>
    /// <returns>The scene. It is not validated; call <see cref="SceneDocument.Validate"/>.</returns>
    /// <exception cref="InvalidDataException">The JSON is not a scene of a format this library reads.</exception>
    public static SceneDocument Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Checked(() => JsonSerializer.Deserialize<SceneDocument>(stream, Options));
    }

    /// <summary>Reads a scene from a JSON string. A convenience for small scenes; prefer the stream overload.</summary>
    /// <param name="json">JSON text, not a file path.</param>
    /// <returns>The scene. It is not validated; call <see cref="SceneDocument.Validate"/>.</returns>
    /// <exception cref="InvalidDataException">The JSON is not a scene of a format this library reads.</exception>
    public static SceneDocument Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Checked(() => JsonSerializer.Deserialize<SceneDocument>(json, Options));
    }

    /// <summary>Reads a scene from a file.</summary>
    /// <param name="path">Path of the file.</param>
    /// <returns>The scene. It is not validated; call <see cref="SceneDocument.Validate"/>.</returns>
    /// <exception cref="InvalidDataException">The file is not a scene of a format this library reads.</exception>
    public static SceneDocument Load(string path)
    {
        using var file = File.OpenRead(path);
        return Read(file);
    }

    private static SceneDocument Checked(Func<SceneDocument?> read)
    {
        try
        {
            return read() ?? throw new InvalidDataException("Not an IOScene document: the JSON root is null.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Not a readable IOScene document: Kind or Format is missing, or the JSON is damaged. {e.Message}", e);
        }
    }

    // Reading treats an explicit null like a missing member, so no member typed as non-null comes back null, and
    // rejects a null inside a list of objects.
    private static void Harden(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object) return;
        var nullability = new NullabilityInfoContext();
        var lists = new List<JsonPropertyInfo>();
        foreach (var property in info.Properties)
        {
            // Required members (Kind, Format) are left alone: their setters reject a wrong value, null included.
            if (property.PropertyType.IsValueType || property.IsRequired) continue;
            if (property.Set is { } set && property.AttributeProvider is PropertyInfo member && nullability.Create(member).WriteState == NullabilityState.NotNull)
                property.Set = (target, value) => { if (value is not null) set(target, value); };
            if (property.Get is not null && typeof(IEnumerable<object?>).IsAssignableFrom(property.PropertyType)) lists.Add(property);
        }
        if (lists.Count == 0) return;
        info.OnDeserialized = target =>
        {
            foreach (var list in lists)
            {
                if (list.Get!(target) is IEnumerable<object?> items && items.Contains(null))
                    throw new InvalidDataException($"Not a readable IOScene document: {list.Name} of {info.Type.Name} holds a null entry.");
            }
        };
    }

    // Files list elements with parents first. A document that already does is written as it is.
    private static SceneDocument InWriteOrder(SceneDocument document)
    {
        var elements = document.Elements;
        var seen = new HashSet<long>(elements.Count);
        bool parentsFirst = true;
        foreach (var element in elements)
        {
            if (element.ParentId is long parent && !seen.Contains(parent)) { parentsFirst = false; break; }
            seen.Add(element.Id);
        }
        if (parentsFirst) return document;

        var byId = new Dictionary<long, SceneElement>(elements.Count);
        foreach (var element in elements) byId.TryAdd(element.Id, element);
        var depths = new Dictionary<long, int>(elements.Count);
        var chain = new List<long>();
        foreach (var element in elements)
        {
            chain.Clear();
            int depth = -1;   // depth of the first ancestor above the chain; -1 above a root
            long? at = element.Id;
            while (at is long id && byId.TryGetValue(id, out var node))
            {
                if (depths.TryGetValue(id, out int known)) { depth = known; break; }
                if (chain.Count > elements.Count) break;   // a parent cycle; Validate() reports it
                chain.Add(id);
                at = node.ParentId;
            }
            for (int i = chain.Count - 1; i >= 0; i--) depths[chain[i]] = ++depth;
        }
        // The sort is stable, so siblings keep their order.
        return document.WithElements([.. elements.OrderBy(e => depths[e.Id])]);
    }
}
