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
    // One vertex, normal or UV list is read as one JSON value, which the runtime holds in one buffer of at most
    // 2 GiB that grows by doubling. Ten million vectors stay below 1 GiB of text at full precision.
    private const int MaxVerticesPerGeometry = 10_000_000;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Non-ASCII text (names, display values) is written as UTF-8 instead of \u escapes.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        // A valid scene needs 64 levels; the rest is room for a document that holds a scene as a member.
        MaxDepth = 128,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Harden } },
    };

    /// <summary>
    /// Creates serializer options that carry the rules of scene JSON, for a document of another kind that holds a
    /// <see cref="SceneDocument"/> as a member. The math values keep their array encoding with any options; these
    /// options add the rest: null members are omitted; an explicit <c>null</c> is read as a missing member for text
    /// (except <see cref="SceneDocument.Kind"/>, which rejects it), lists, objects and nullable members, and makes the
    /// serializer throw <see cref="JsonException"/> for a member of any other type, such as a number, an enum, a math
    /// value or a date-time; a <c>null</c> inside a list of objects is rejected with
    /// <see cref="InvalidDataException"/>; and text is escaped as <see cref="Write(SceneDocument, Stream)"/> escapes
    /// it.
    /// </summary>
    /// <remarks>
    /// The rules apply to every type that is read or written with the options, the holder included. Two things that
    /// <see cref="Write(SceneDocument, Stream)"/> does are not part of them: the elements are written in the order
    /// of the list, not parents first, and the size of a geometry is not checked.
    /// </remarks>
    /// <returns>A new instance that the caller may change, for example to indent the output.</returns>
    public static JsonSerializerOptions CreateOptions() => new(Options);

    /// <summary>Writes a scene as UTF-8 JSON. The stream is left open.</summary>
    /// <param name="document">Scene to write. It is not changed.</param>
    /// <param name="stream">Writable stream.</param>
    /// <exception cref="ArgumentException">
    /// A number in the scene is NaN or infinite, which <see cref="SceneDocument.Validate"/> reports as
    /// <c>not-finite</c>, or a geometry has more than 10,000,000 vertices, the limit of one geometry in scene JSON.
    /// </exception>
    /// <exception cref="JsonException">A schedule task is inside its own sub-tasks, which <see cref="SceneDocument.Validate"/> reports as <c>task-tree</c>.</exception>
    public static void Write(SceneDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        JsonSerializer.Serialize(stream, Prepared(document), Options);
    }

    /// <summary>Writes a scene to a JSON string. A convenience for small scenes; prefer the stream overload.</summary>
    /// <param name="document">Scene to write. It is not changed.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentException">A number in the scene is NaN or infinite, or a geometry has more than 10,000,000 vertices.</exception>
    /// <exception cref="JsonException">A schedule task is inside its own sub-tasks.</exception>
    public static string Write(SceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(Prepared(document), Options);
    }

    /// <summary>
    /// Writes a scene to a file. The scene is written beside the target first and moved over it when complete, so a
    /// failed write leaves an existing file as it was.
    /// </summary>
    /// <param name="document">Scene to write. It is not changed.</param>
    /// <param name="path">Path of the file to create or replace.</param>
    /// <exception cref="ArgumentException">A number in the scene is NaN or infinite, or a geometry has more than 10,000,000 vertices.</exception>
    /// <exception cref="JsonException">A schedule task is inside its own sub-tasks.</exception>
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
        catch (TargetInvocationException e) when (e.InnerException is InvalidDataException rejected)
        {
            // Without dynamic code the serialiser calls the setters of Kind and Format through reflection.
            throw new InvalidDataException(rejected.Message, e);
        }
    }

    // Reading treats an explicit null for a member of a reference type like a missing member, so no member typed as
    // non-null comes back null, and rejects a null inside a list of objects. A nullable member is null by default, so
    // skipping its null changes nothing. A null for a value type is left to the serialiser, which reads it into a
    // nullable member and rejects it for any other.
    private static void Harden(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object) return;
        var lists = new List<JsonPropertyInfo>();
        foreach (var property in info.Properties)
        {
            // Required members (Kind, Format) are left alone: their setters reject a wrong value, null included.
            if (property.PropertyType.IsValueType || property.IsRequired) continue;
            if (property.Set is { } set) property.Set = (target, value) => { if (value is not null) set(target, value); };
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

    // What the writer serialises: the document, checked for the size the reader can take back, with its elements in file order.
    private static SceneDocument Prepared(SceneDocument document)
    {
        foreach (var geometry in document.Geometries)
        {
            if (Math.Max(geometry.Vertices.Count, Math.Max(geometry.Normals?.Count ?? 0, geometry.UVs?.Count ?? 0)) > MaxVerticesPerGeometry)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"Geometry {geometry.Id} has more than {MaxVerticesPerGeometry} vertices, the limit of one geometry in scene JSON. Split the mesh."),
                    nameof(document));
            }
        }
        return InWriteOrder(document);
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
        var onChain = new HashSet<long>();
        foreach (var element in elements)
        {
            chain.Clear();
            onChain.Clear();
            int depth = -1;   // depth of the first ancestor above the chain; -1 above a root
            long? at = element.Id;
            while (at is long id && byId.TryGetValue(id, out var node))
            {
                if (depths.TryGetValue(id, out int known)) { depth = known; break; }
                if (!onChain.Add(id)) break;   // a parent cycle; Validate() reports it
                chain.Add(id);
                at = node.ParentId;
            }
            for (int i = chain.Count - 1; i >= 0; i--) depths[chain[i]] = ++depth;
        }
        // The sort is stable, so siblings keep their order.
        return document.WithElements([.. elements.OrderBy(e => depths[e.Id])]);
    }
}
