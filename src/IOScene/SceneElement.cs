namespace IOScene;

/// <summary>
/// Role of an element in the tree. The enum is open: a later version may add values, and a consumer treats a value
/// it does not know as <see cref="Group"/>.
/// </summary>
public enum SceneNodeKind
{
    /// <summary>A model element.</summary>
    Element = 0,

    /// <summary>A container that groups other elements.</summary>
    Group = 1,

    /// <summary>The tree node of a model.</summary>
    Model = 2,

    /// <summary>The tree node of a level.</summary>
    Level = 3,
}

/// <summary>
/// One element of a model. It may have no geometry; its geometry, when it has any, is placed by
/// <see cref="SceneInstance"/> entries that name it.
/// </summary>
public sealed class SceneElement
{
    /// <summary>
    /// Scene-local id, assigned by the producer. It is valid inside this document only. Like every integer id of a
    /// scene it is between -(2^53 - 1) and 2^53 - 1.
    /// </summary>
    public long Id { get; set; }

    /// <summary>Id of the <see cref="SceneModel"/> the element belongs to.</summary>
    public int ModelId { get; set; }

    /// <summary>
    /// Id of the element in its source, scoped by the model: IFC GlobalId, Revit UniqueId, DWG handle as hex text.
    /// Together with <see cref="SceneModel.Key"/> it is the identity that may be stored outside the scene.
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// The 22-character IFC GlobalId the element has, or gets when it is exported to IFC. It is the only component
    /// id for BCF. For an IFC source it equals <see cref="SourceId"/>. Its derivation is frozen per producer: it is a
    /// function of the element and the file only, never of the export order or of options.
    /// </summary>
    public string? IfcGuid { get; set; }

    /// <summary>
    /// Id of the one containment parent; null for a root. A parent of kind <see cref="SceneNodeKind.Element"/> means
    /// that this element is part of it. Model, level and group nodes without <see cref="SourceId"/> are layout that a
    /// consumer may rebuild; a group with <see cref="SourceId"/> is an object of the source, such as a model group or
    /// an assembly.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>Role of the element in the tree.</summary>
    public SceneNodeKind Kind { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Category in the vocabulary of the source, as the source application shows it.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Type name in the vocabulary of the source.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Id of the <see cref="SceneLevel"/> the element is on, or null.</summary>
    public int? LevelId { get; set; }

    /// <summary>
    /// Ids of the element's property sets, in display order. Sets are shared between elements. A key may appear in
    /// several sets; a lookup by key takes the first set in this list that has it, and producers list occurrence
    /// sets before type sets.
    /// </summary>
    public List<int> PropertySetIds { get; set; } = [];
}
