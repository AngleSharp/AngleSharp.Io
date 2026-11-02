namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The states of the permission for showing notifications.
/// </summary>
[DomLiterals]
[DomName("NotificationPermission")]
public enum NotificationPermission
{
    /// <summary>
    /// The user has not been asked, which blocks notifications like denied.
    /// </summary>
    [DomName("default")]
    Default,
    /// <summary>
    /// Notifications cannot be shown.
    /// </summary>
    [DomName("denied")]
    Denied,
    /// <summary>
    /// Notifications can be shown.
    /// </summary>
    [DomName("granted")]
    Granted,
}
