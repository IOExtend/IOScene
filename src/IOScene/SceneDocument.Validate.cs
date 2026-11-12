namespace IOScene;

public sealed partial class SceneDocument
{
    /// <summary>
    /// Checks the structure of the scene against the contract. It reads the scene and changes nothing.
    /// </summary>
    /// <remarks>
    /// <para>Errors (the scene breaks an invariant):</para>
    /// <list type="table">
    /// <item><term>no-model</term><description>The scene has no model.</description></item>
    /// <item><term>duplicate-id</term><description>Two models, elements, geometries, materials, textures, property sets or levels share an id.</description></item>
    /// <item><term>model-key</term><description>A model key is empty or used by two models.</description></item>
    /// <item><term>dangling-model</term><description>An element, level, grid system or schedule names a model that does not exist.</description></item>
    /// <item><term>dangling-level</term><description>An element names a level that does not exist.</description></item>
    /// <item><term>dangling-set</term><description>An element names a property set that does not exist.</description></item>
    /// <item><term>dangling-parent</term><description>An element names a parent that does not exist.</description></item>
    /// <item><term>parent-cycle</term><description>The parent chain of an element returns to itself.</description></item>
    /// <item><term>dangling-element</term><description>An instance, schedule task or clash names an element that does not exist.</description></item>
    /// <item><term>dangling-geometry</term><description>An instance names a geometry that does not exist.</description></item>
    /// <item><term>dangling-material</term><description>An instance names a material that does not exist.</description></item>
    /// <item><term>dangling-texture</term><description>A material names a texture that does not exist.</description></item>
    /// <item><term>vertex-count</term><description>A non-indexed geometry has a vertex count that its topology does not divide.</description></item>
    /// <item><term>index-count</term><description>A geometry has an index count that its topology does not divide.</description></item>
    /// <item><term>index-range</term><description>A geometry has an index outside its vertices.</description></item>
    /// <item><term>normal-count</term><description>A geometry has normals, but not one per vertex.</description></item>
    /// <item><term>uv-count</term><description>A geometry has texture coordinates, but not one per vertex.</description></item>
    /// <item><term>not-finite</term><description>A vertex, normal, texture coordinate, instance transform or property number is NaN or infinite.</description></item>
    /// </list>
    /// <para>Warnings:</para>
    /// <list type="table">
    /// <item><term>duplicate-source-id</term><description>Two elements of one model share a source id.</description></item>
    /// <item><term>ifc-guid</term><description>An IfcGuid is not a 22-character IFC GlobalId.</description></item>
    /// <item><term>unit-grammar</term><description>A property unit is outside the grammar of <see cref="SceneUnits"/>.</description></item>
    /// </list>
    /// <para>Later versions may add codes.</para>
    /// </remarks>
    /// <returns>The findings; an empty list when the scene is valid.</returns>
    public IReadOnlyList<SceneDiagnostic> Validate()
    {
        var issues = new List<SceneDiagnostic>();
        void Error(string code, FormattableString message, string? sourceId = null) =>
            issues.Add(new(SceneSeverity.Error, code, FormattableString.Invariant(message), sourceId));
        void Warning(string code, FormattableString message, string? sourceId = null) =>
            issues.Add(new(SceneSeverity.Warning, code, FormattableString.Invariant(message), sourceId));
        HashSet<T> Ids<T>(IEnumerable<T> ids, string what) where T : notnull
        {
            var set = new HashSet<T>();
            foreach (var id in ids)
            {
                if (!set.Add(id)) Error("duplicate-id", $"{what} id {id} is used twice.");
            }
            return set;
        }

        if (Models.Count == 0) Error("no-model", $"A scene has at least one model.");
        var models = Ids(Models.Select(m => m.Id), "Model");
        foreach (var key in Models.GroupBy(m => m.Key).Where(g => g.Count() > 1 || string.IsNullOrEmpty(g.Key)))
            Error("model-key", $"Model key '{key.Key}' is empty or not unique in the scene.");
        var elements = Ids(Elements.Select(e => e.Id), "Element");
        var geometries = Ids(Geometries.Select(g => g.Id), "Geometry");
        var materials = Ids(Materials.Select(m => m.Id), "Material");
        var textures = Ids(Textures.Select(t => t.Id), "Texture");
        var sets = Ids(PropertySets.Select(s => s.Id), "Property set");
        var levels = Ids(Levels.Select(l => l.Id), "Level");

        foreach (var e in Elements)
        {
            if (!models.Contains(e.ModelId)) Error("dangling-model", $"Element {e.Id} names model {e.ModelId}.", e.SourceId);
            if (e.LevelId is int level && !levels.Contains(level)) Error("dangling-level", $"Element {e.Id} names level {level}.", e.SourceId);
            foreach (int set in e.PropertySetIds)
            {
                if (!sets.Contains(set)) Error("dangling-set", $"Element {e.Id} names property set {set}.", e.SourceId);
            }
            if (e.ParentId is long parent && !elements.Contains(parent)) Error("dangling-parent", $"Element {e.Id} names parent {parent}.", e.SourceId);
            if (e.IfcGuid is { } guid && !IsIfcGuid(guid)) Warning("ifc-guid", $"Element {e.Id} has an IfcGuid that is not a 22-character IFC GlobalId.", e.SourceId);
        }
        ReportParentCycles(Error);
        foreach (var same in Elements.Where(e => e.SourceId is not null).GroupBy(e => (e.ModelId, e.SourceId)).Where(g => g.Count() > 1))
            Warning("duplicate-source-id", $"Source id is used by {same.Count()} elements of model {same.Key.ModelId}.", same.Key.SourceId);

        foreach (var i in Instances)
        {
            if (!elements.Contains(i.ElementId)) Error("dangling-element", $"An instance names element {i.ElementId}.");
            if (!geometries.Contains(i.GeometryId)) Error("dangling-geometry", $"An instance names geometry {i.GeometryId}.");
            if (i.MaterialId is int material && !materials.Contains(material)) Error("dangling-material", $"An instance names material {material}.");
            if (i.Transform is { } t && !(IsFinite(t.XAxis) && IsFinite(t.YAxis) && IsFinite(t.ZAxis) && IsFinite(t.Translation)))
                Error("not-finite", $"An instance of element {i.ElementId} has a transform with a number that is not finite.");
        }
        foreach (var g in Geometries)
        {
            // An unknown topology of a later version is not checked for counts.
            int per = g.Topology switch { SceneTopology.Triangles => 3, SceneTopology.Lines => 2, _ => 1 };
            if (g.Indices.Count == 0 && g.Vertices.Count % per != 0) Error("vertex-count", $"Non-indexed geometry {g.Id} has {g.Vertices.Count} vertices for topology {g.Topology}.");
            if (g.Indices.Count % per != 0) Error("index-count", $"Geometry {g.Id} has {g.Indices.Count} indices for topology {g.Topology}.");
            if (g.Indices.Any(index => index < 0 || index >= g.Vertices.Count)) Error("index-range", $"Geometry {g.Id} has an index outside its vertices.");
            if (g.Normals is { } normals && normals.Count != g.Vertices.Count) Error("normal-count", $"Geometry {g.Id} has {normals.Count} normals for {g.Vertices.Count} vertices.");
            if (g.UVs is { } uvs && uvs.Count != g.Vertices.Count) Error("uv-count", $"Geometry {g.Id} has {uvs.Count} texture coordinates for {g.Vertices.Count} vertices.");
            if (!g.Vertices.All(IsFinite) || g.Normals?.All(IsFinite) == false || g.UVs?.All(uv => double.IsFinite(uv.X) && double.IsFinite(uv.Y)) == false)
                Error("not-finite", $"Geometry {g.Id} has a number that is not finite.");
        }
        foreach (var m in Materials)
        {
            if (m.TextureId is int texture && !textures.Contains(texture)) Error("dangling-texture", $"Material {m.Id} names texture {texture}.");
        }
        foreach (var l in Levels)
        {
            if (!models.Contains(l.ModelId)) Error("dangling-model", $"Level {l.Id} names model {l.ModelId}.");
        }
        foreach (var g in Grids)
        {
            if (!models.Contains(g.ModelId)) Error("dangling-model", $"Grid system '{g.Name}' names model {g.ModelId}.");
        }

        var units = new HashSet<string>();   // each distinct unit is checked and reported once
        foreach (var s in PropertySets)
        {
            foreach (var p in s.Properties)
            {
                if (p.Value.Unit is { } unit && units.Add(unit) && !SceneUnits.IsValid(unit)) Warning("unit-grammar", $"Unit '{unit}' is outside the IOScene unit grammar.");
                if (p.Value.Number is double number && !double.IsFinite(number)) Error("not-finite", $"Property '{p.Name}' of set {s.Id} has a number that is not finite.");
            }
        }

        foreach (var s in Schedules)
        {
            if (s.ModelId is int model && !models.Contains(model)) Error("dangling-model", $"Schedule '{s.Name}' names model {model}.");
            var tasks = new Stack<SceneTask>(s.Tasks);
            while (tasks.TryPop(out var task))
            {
                foreach (long id in task.ElementIds)
                {
                    if (!elements.Contains(id)) Error("dangling-element", $"Task '{task.Id}' names element {id}.");
                }
                foreach (var child in task.Children) tasks.Push(child);
            }
        }
        foreach (var c in ClashTests.SelectMany(t => t.Clashes))
        {
            foreach (long? id in new[] { c.Element1Id, c.Element2Id })
            {
                if (id is long known && !elements.Contains(known)) Error("dangling-element", $"Clash '{c.Id}' names element {known}.");
            }
        }
        return issues;
    }

    // One pass over the parent links: every element is walked once, and a cycle is reported once.
    private void ReportParentCycles(Action<string, FormattableString, string?> error)
    {
        var parents = new Dictionary<long, SceneElement>(Elements.Count);
        foreach (var e in Elements) parents.TryAdd(e.Id, e);
        var settled = new HashSet<long>();
        var path = new HashSet<long>();
        foreach (var start in Elements)
        {
            path.Clear();
            long? at = start.Id;
            while (at is long id && !settled.Contains(id) && parents.TryGetValue(id, out var element))
            {
                if (!path.Add(id))
                {
                    error("parent-cycle", $"Element {id} is inside a parent cycle.", element.SourceId);
                    break;
                }
                at = element.ParentId;
            }
            settled.UnionWith(path);
        }
    }

    private static bool IsFinite(Vector3d v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);

    // 22 characters of the IFC base-64 alphabet; the first one carries two bits, so it is 0 to 3.
    private static bool IsIfcGuid(string value) =>
        value.Length == 22 && value[0] is >= '0' and <= '3' && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '$');
}
