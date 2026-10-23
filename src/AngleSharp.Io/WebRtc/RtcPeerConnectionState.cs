namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The aggregated states of a peer connection.
/// </summary>
[DomLiterals]
[DomName("RTCPeerConnectionState")]
public enum RtcPeerConnectionState
{
    /// <summary>
    /// No transport is in use yet.
    /// </summary>
    [DomName("new")]
    New,
    /// <summary>
    /// A transport is being established.
    /// </summary>
    [DomName("connecting")]
    Connecting,
    /// <summary>
    /// The transports are established.
    /// </summary>
    [DomName("connected")]
    Connected,
    /// <summary>
    /// A transport was lost.
    /// </summary>
    [DomName("disconnected")]
    Disconnected,
    /// <summary>
    /// A transport failed.
    /// </summary>
    [DomName("failed")]
    Failed,
    /// <summary>
    /// The connection is closed.
    /// </summary>
    [DomName("closed")]
    Closed,
}
