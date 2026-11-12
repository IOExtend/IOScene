namespace IOScene;

/// <summary>
/// Severity of a <see cref="SceneDiagnostic"/>.
/// </summary>
public enum SceneSeverity
{
    /// <summary>Information.</summary>
    Info = 0,

    /// <summary>The scene is usable, but something in it is likely wrong.</summary>
    Warning = 1,

    /// <summary>The scene breaks an invariant of the contract.</summary>
    Error = 2,
}

/// <summary>
/// One finding of <see cref="SceneDocument.Validate"/> or of a producer's conversion report.
/// </summary>
/// <remarks>The record grows by <c>init</c> properties only, never by a positional parameter.</remarks>
/// <param name="Severity">Severity.</param>
/// <param name="Code">Stable machine code in lower case with hyphens, such as <c>dangling-element</c>.</param>
/// <param name="Message">Message in English.</param>
/// <param name="SourceId">Source id of the element concerned, when there is one.</param>
public sealed record SceneDiagnostic(SceneSeverity Severity, string Code, string Message, string? SourceId = null);
