namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The states of the signaling.
/// </summary>
[DomLiterals]
[DomName("RTCSignalingState")]
public enum RtcSignalingState
{
    /// <summary>
    /// No negotiation is in progress.
    /// </summary>
    [DomName("stable")]
    Stable,
    /// <summary>
    /// A local offer has been applied.
    /// </summary>
    [DomName("have-local-offer")]
    HaveLocalOffer,
    /// <summary>
    /// A remote offer has been applied.
    /// </summary>
    [DomName("have-remote-offer")]
    HaveRemoteOffer,
    /// <summary>
    /// A local provisional answer has been applied.
    /// </summary>
    [DomName("have-local-pranswer")]
    HaveLocalPrAnswer,
    /// <summary>
    /// A remote provisional answer has been applied.
    /// </summary>
    [DomName("have-remote-pranswer")]
    HaveRemotePrAnswer,
    /// <summary>
    /// The connection is closed.
    /// </summary>
    [DomName("closed")]
    Closed,
}
