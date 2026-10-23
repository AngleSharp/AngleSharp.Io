namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The states of the ICE connection.
/// </summary>
[DomLiterals]
[DomName("RTCIceConnectionState")]
public enum RtcIceConnectionState
{
    /// <summary>
    /// No connectivity checks have started.
    /// </summary>
    [DomName("new")]
    New,
    /// <summary>
    /// Connectivity checks are running.
    /// </summary>
    [DomName("checking")]
    Checking,
    /// <summary>
    /// A usable connection has been found.
    /// </summary>
    [DomName("connected")]
    Connected,
    /// <summary>
    /// The connectivity checks are done.
    /// </summary>
    [DomName("completed")]
    Completed,
    /// <summary>
    /// The connection was lost.
    /// </summary>
    [DomName("disconnected")]
    Disconnected,
    /// <summary>
    /// No usable connection has been found.
    /// </summary>
    [DomName("failed")]
    Failed,
    /// <summary>
    /// The connection is closed.
    /// </summary>
    [DomName("closed")]
    Closed,
}
