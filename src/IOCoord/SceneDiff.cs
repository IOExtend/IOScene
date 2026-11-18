using IOScene;

namespace IOCoord;

/// <summary>The outcome of comparing an element across two scenes.</summary>
public enum SceneChangeKind
{
    /// <summary>The element occurs in the revision only.</summary>
    Added,

    /// <summary>The element occurs in the baseline only.</summary>
    Removed,

    /// <summary>The element occurs in both scenes and has changed.</summary>
    Modified,
}

/// <summary>A comparison finding keyed by the element's persistent source identity.</summary>
/// <param name="ModelKey">Stable key of the model that owns the element.</param>
/// <param name="SourceId">Id of the element in that source.</param>
/// <param name="Kind">Kind of finding.</param>
/// <param name="MetadataChanged">Name, category, type name, kind or IFC GlobalId changed.</param>
/// <param name="PropertiesChanged">Property sets or properties changed.</param>
/// <param name="GeometryChanged">Ordered world vertices, indices or topology of the placements changed.</param>
public sealed record SceneElementChange(string ModelKey, string SourceId, SceneChangeKind Kind,
    bool MetadataChanged = false, bool PropertiesChanged = false, bool GeometryChanged = false);

/// <summary>The findings and counts of elements without a persistent identity.</summary>
/// <param name="Changes">Added, removed and modified elements.</param>
/// <param name="UnidentifiedBaseline">Elements without a source id in the baseline.</param>
/// <param name="UnidentifiedRevision">Elements without a source id in the revision.</param>
public sealed record SceneComparison(IReadOnlyList<SceneElementChange> Changes, int UnidentifiedBaseline, int UnidentifiedRevision);

/// <summary>Compares revisions of an IOScene using model keys and persistent source ids.</summary>
public static class SceneDiff
{
    /// <summary>Compares element metadata, displayed properties and placed geometry.</summary>
    /// <param name="baseline">Earlier scene.</param>
    /// <param name="revision">Later scene.</param>
    /// <exception cref="InvalidDataException">A scene is invalid or an element identity is ambiguous.</exception>
    public static SceneComparison Compare(SceneDocument baseline, SceneDocument revision)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(revision);
        var before = new SceneIndex(baseline);
        var after = new SceneIndex(revision);
        var changes = new List<SceneElementChange>();
        foreach (var (key, element) in before.Elements)
        {
            if (!after.Elements.TryGetValue(key, out var current))
            {
                changes.Add(new(key.ModelKey, key.SourceId, SceneChangeKind.Removed));
                continue;
            }

            bool metadata = element.Name != current.Name || element.Category != current.Category || element.TypeName != current.TypeName
                || element.Kind != current.Kind || element.IfcGuid != current.IfcGuid;
            bool properties = !SameProperties(before, element, after, current);
            bool geometry = !SameGeometry(before, element, after, current);
            if (metadata || properties || geometry)
                changes.Add(new(key.ModelKey, key.SourceId, SceneChangeKind.Modified, metadata, properties, geometry));
        }
        foreach (var key in after.Elements.Keys)
            if (!before.Elements.ContainsKey(key))
                changes.Add(new(key.ModelKey, key.SourceId, SceneChangeKind.Added));
        return new(changes.AsReadOnly(), baseline.Elements.Count - before.Elements.Count, revision.Elements.Count - after.Elements.Count);
    }

    private static bool SameProperties(SceneIndex first, SceneElement a, SceneIndex second, SceneElement b)
    {
        if (a.PropertySetIds.Count != b.PropertySetIds.Count) return false;
        for (int i = 0; i < a.PropertySetIds.Count; i++)
        {
            var one = first.PropertySets[a.PropertySetIds[i]];
            var two = second.PropertySets[b.PropertySetIds[i]];
            if (one.Name != two.Name || one.Key != two.Key) return false;
            if (!one.Properties.Select(Property).SequenceEqual(two.Properties.Select(Property))) return false;
        }
        return true;

        static (string Name, string? Key, SceneValue Value) Property(SceneProperty p) => (p.Name, p.Key, p.Value);
    }

    private static bool SameGeometry(SceneIndex first, SceneElement a, SceneIndex second, SceneElement b)
    {
        var before = first.Instances[a.Id];
        var after = second.Instances[b.Id];
        using var left = before.GetEnumerator();
        using var right = after.GetEnumerator();
        while (left.MoveNext())
        {
            if (!right.MoveNext()) return false;
            var i = left.Current;
            var j = right.Current;
            var one = first.Geometries[i.GeometryId];
            var two = second.Geometries[j.GeometryId];
            if (one.Topology != two.Topology || !one.Indices.SequenceEqual(two.Indices)) return false;
            var leftTransform = i.Transform ?? Transform3d.Identity;
            var rightTransform = j.Transform ?? Transform3d.Identity;
            if (!one.Vertices.Select(leftTransform.Apply).SequenceEqual(two.Vertices.Select(rightTransform.Apply))) return false;
        }
        return !right.MoveNext();
    }

    private sealed class SceneIndex
    {
        public readonly Dictionary<(string ModelKey, string SourceId), SceneElement> Elements = new();
        public readonly Dictionary<int, ScenePropertySet> PropertySets;
        public readonly Dictionary<int, SceneGeometry> Geometries;
        public readonly ILookup<long, SceneInstance> Instances;

        public SceneIndex(SceneDocument scene)
        {
            if (scene.Validate().FirstOrDefault(issue => issue.Severity == SceneSeverity.Error) is { } error)
                throw new InvalidDataException($"Invalid scene ({error.Code}): {error.Message}");
            var modelKeys = scene.Models.ToDictionary(model => model.Id, model => model.Key);
            foreach (var element in scene.Elements)
            {
                if (!string.IsNullOrEmpty(element.SourceId) && !Elements.TryAdd((modelKeys[element.ModelId], element.SourceId), element))
                    throw new InvalidDataException("Two elements in one model share a source id.");
            }
            PropertySets = scene.PropertySets.ToDictionary(set => set.Id);
            Geometries = scene.Geometries.ToDictionary(geometry => geometry.Id);
            Instances = scene.Instances.ToLookup(instance => instance.ElementId);
        }
    }
}
