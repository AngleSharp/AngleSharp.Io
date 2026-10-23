namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The policies for multiplexing RTCP.
/// </summary>
[DomLiterals]
[DomName("RTCRtcpMuxPolicy")]
public enum RtcRtcpMuxPolicy
{
    /// <summary>
    /// RTP and RTCP share a transport.
    /// </summary>
    [DomName("require")]
    Require,
}
