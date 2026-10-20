namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The states of a permission.
/// </summary>
[DomLiterals]
[DomName("PermissionState")]
public enum PermissionState
{
    /// <summary>
    /// The feature can be used without asking.
    /// </summary>
    [DomName("granted")]
    Granted,
    /// <summary>
    /// The feature cannot be used.
    /// </summary>
    [DomName("denied")]
    Denied,
    /// <summary>
    /// Using the feature requires asking the user first.
    /// </summary>
    [DomName("prompt")]
    Prompt,
}
