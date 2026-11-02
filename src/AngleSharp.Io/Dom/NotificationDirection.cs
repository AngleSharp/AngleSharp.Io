namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The text directions of a notification.
/// </summary>
[DomLiterals]
[DomName("NotificationDirection")]
public enum NotificationDirection
{
    /// <summary>
    /// The direction is derived from the text.
    /// </summary>
    [DomName("auto")]
    Auto,
    /// <summary>
    /// The text goes from left to right.
    /// </summary>
    [DomName("ltr")]
    Ltr,
    /// <summary>
    /// The text goes from right to left.
    /// </summary>
    [DomName("rtl")]
    Rtl,
}
