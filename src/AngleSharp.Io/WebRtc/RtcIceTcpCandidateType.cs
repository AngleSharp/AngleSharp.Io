namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The types of TCP candidates.
/// </summary>
[DomLiterals]
[DomName("RTCIceTcpCandidateType")]
public enum RtcIceTcpCandidateType
{
    /// <summary>
    /// The candidate opens connections.
    /// </summary>
    [DomName("active")]
    Active,
    /// <summary>
    /// The candidate accepts connections.
    /// </summary>
    [DomName("passive")]
    Passive,
    /// <summary>
    /// The candidate opens connections simultaneously.
    /// </summary>
    [DomName("so")]
    So,
}
