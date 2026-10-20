namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The candidates the ICE agent may use.
/// </summary>
[DomLiterals]
[DomName("RTCIceTransportPolicy")]
public enum RtcIceTransportPolicy
{
    /// <summary>
    /// All candidates may be used.
    /// </summary>
    [DomName("all")]
    All,
    /// <summary>
    /// Only relayed candidates may be used.
    /// </summary>
    [DomName("relay")]
    Relay,
}
