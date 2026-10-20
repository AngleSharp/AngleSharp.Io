namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The transport protocols of ICE candidates.
/// </summary>
[DomLiterals]
[DomName("RTCIceProtocol")]
public enum RtcIceProtocol
{
    /// <summary>
    /// UDP.
    /// </summary>
    [DomName("udp")]
    Udp,
    /// <summary>
    /// TCP.
    /// </summary>
    [DomName("tcp")]
    Tcp,
}
