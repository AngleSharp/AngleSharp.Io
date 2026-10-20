namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The components of ICE candidates.
/// </summary>
[DomLiterals]
[DomName("RTCIceComponent")]
public enum RtcIceComponent
{
    /// <summary>
    /// The RTP component.
    /// </summary>
    [DomName("rtp")]
    Rtp,
    /// <summary>
    /// The RTCP component.
    /// </summary>
    [DomName("rtcp")]
    Rtcp,
}
