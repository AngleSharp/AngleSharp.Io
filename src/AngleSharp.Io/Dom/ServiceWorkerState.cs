namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The lifecycle states of a service worker.
/// </summary>
[DomLiterals]
[DomName("ServiceWorkerState")]
public enum ServiceWorkerState
{
    /// <summary>
    /// The worker has been created, but not installed yet.
    /// </summary>
    [DomName("parsed")]
    Parsed,
    /// <summary>
    /// The worker is being installed.
    /// </summary>
    [DomName("installing")]
    Installing,
    /// <summary>
    /// The worker is installed and waits for its activation.
    /// </summary>
    [DomName("installed")]
    Installed,
    /// <summary>
    /// The worker is being activated.
    /// </summary>
    [DomName("activating")]
    Activating,
    /// <summary>
    /// The worker is active and handles fetches.
    /// </summary>
    [DomName("activated")]
    Activated,
    /// <summary>
    /// The worker failed to install or has been replaced.
    /// </summary>
    [DomName("redundant")]
    Redundant,
}
