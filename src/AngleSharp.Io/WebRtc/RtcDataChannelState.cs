namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The states of a data channel.
/// </summary>
[DomLiterals]
[DomName("RTCDataChannelState")]
public enum RtcDataChannelState
{
    /// <summary>
    /// The channel is not open yet.
    /// </summary>
    [DomName("connecting")]
    Connecting,
    /// <summary>
    /// The channel can be used.
    /// </summary>
    [DomName("open")]
    Open,
    /// <summary>
    /// The channel is being closed.
    /// </summary>
    [DomName("closing")]
    Closing,
    /// <summary>
    /// The channel is closed.
    /// </summary>
    [DomName("closed")]
    Closed,
}
