namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The types of worker scripts.
/// </summary>
[DomLiterals]
[DomName("WorkerType")]
public enum WorkerType
{
    /// <summary>
    /// A classic script.
    /// </summary>
    [DomName("classic")]
    Classic,
    /// <summary>
    /// A module script.
    /// </summary>
    [DomName("module")]
    Module,
}
