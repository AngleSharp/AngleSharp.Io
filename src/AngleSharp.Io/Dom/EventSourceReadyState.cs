namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The states of the connection of an EventSource.
/// </summary>
[DomName("EventSource")]
public enum EventSourceReadyState : ushort
{
    /// <summary>
    /// The connection is (re-)established.
    /// </summary>
    [DomName("CONNECTING")]
    Connecting = 0,
    /// <summary>
    /// The connection is open, dispatching events.
    /// </summary>
    [DomName("OPEN")]
    Open = 1,
    /// <summary>
    /// The connection is closed, without trying to reconnect.
    /// </summary>
    [DomName("CLOSED")]
    Closed = 2,
}
