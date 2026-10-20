namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The ways the HTTP cache is used when checking a service worker for updates.
/// </summary>
[DomLiterals]
[DomName("ServiceWorkerUpdateViaCache")]
public enum ServiceWorkerUpdateViaCache
{
    /// <summary>
    /// The HTTP cache is used for imported scripts, but not for the main script.
    /// </summary>
    [DomName("imports")]
    Imports,
    /// <summary>
    /// The HTTP cache is used for all scripts.
    /// </summary>
    [DomName("all")]
    All,
    /// <summary>
    /// The HTTP cache is not used for any script.
    /// </summary>
    [DomName("none")]
    None,
}
