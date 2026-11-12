namespace IOScene;

/// <summary>
/// What a 4D task does to its elements.
/// </summary>
public enum SceneTaskType
{
    /// <summary>The elements are built.</summary>
    Construct = 0,

    /// <summary>The elements are removed.</summary>
    Demolish = 1,

    /// <summary>The elements exist only while the task runs.</summary>
    Temporary = 2,
}

/// <summary>
/// One task of a 4D schedule.
/// </summary>
public sealed class SceneTask
{
    /// <summary>Id of the task, valid inside this document only.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What the task does to its elements.</summary>
    public SceneTaskType TaskType { get; set; }

    /// <summary>Planned start.</summary>
    public DateTimeOffset PlannedStart { get; set; }

    /// <summary>Planned end.</summary>
    public DateTimeOffset PlannedEnd { get; set; }

    /// <summary>Actual start, or null.</summary>
    public DateTimeOffset? ActualStart { get; set; }

    /// <summary>Actual end, or null.</summary>
    public DateTimeOffset? ActualEnd { get; set; }

    /// <summary>Planned cost, or null.</summary>
    public decimal? PlannedCost { get; set; }

    /// <summary>Actual cost, or null.</summary>
    public decimal? ActualCost { get; set; }

    /// <summary>Progress of the task, as the source gives it.</summary>
    public double Progress { get; set; }

    /// <summary>Ids of the elements the task acts on. They are scene-local and valid inside this document only.</summary>
    public List<long> ElementIds { get; set; } = [];

    /// <summary>Sub-tasks.</summary>
    public List<SceneTask> Children { get; set; } = [];
}

/// <summary>
/// A 4D schedule.
/// </summary>
public sealed class SceneSchedule
{
    /// <summary>Id of the <see cref="SceneModel"/> the schedule belongs to; null when it spans the scene.</summary>
    public int? ModelId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Top-level tasks.</summary>
    public List<SceneTask> Tasks { get; set; } = [];
}
