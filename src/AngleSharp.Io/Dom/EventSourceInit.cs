namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;

/// <summary>
/// The options of an EventSource.
/// </summary>
[DomName("EventSourceInit")]
public sealed class EventSourceInit
{
    /// <summary>
    /// Gets or sets if credentials (cookies) are sent with a request to another origin.
    /// </summary>
    [DomName("withCredentials")]
    public Boolean WithCredentials { get; set; }
}
