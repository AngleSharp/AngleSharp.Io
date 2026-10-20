namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The types of ICE candidates.
/// </summary>
[DomLiterals]
[DomName("RTCIceCandidateType")]
public enum RtcIceCandidateType
{
    /// <summary>
    /// An address of the host itself.
    /// </summary>
    [DomName("host")]
    Host,
    /// <summary>
    /// A server reflexive address, e.g., from STUN.
    /// </summary>
    [DomName("srflx")]
    Srflx,
    /// <summary>
    /// A peer reflexive address.
    /// </summary>
    [DomName("prflx")]
    Prflx,
    /// <summary>
    /// A relayed address, e.g., from TURN.
    /// </summary>
    [DomName("relay")]
    Relay,
}
