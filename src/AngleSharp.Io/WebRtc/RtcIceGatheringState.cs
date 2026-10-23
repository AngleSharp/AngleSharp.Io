namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The states of gathering ICE candidates.
/// </summary>
[DomLiterals]
[DomName("RTCIceGatheringState")]
public enum RtcIceGatheringState
{
    /// <summary>
    /// No candidates have been gathered yet.
    /// </summary>
    [DomName("new")]
    New,
    /// <summary>
    /// Candidates are being gathered.
    /// </summary>
    [DomName("gathering")]
    Gathering,
    /// <summary>
    /// All candidates have been gathered.
    /// </summary>
    [DomName("complete")]
    Complete,
}
