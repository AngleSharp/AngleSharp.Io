namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The types of session descriptions.
/// </summary>
[DomLiterals]
[DomName("RTCSdpType")]
public enum RtcSdpType
{
    /// <summary>
    /// An offer starting a negotiation.
    /// </summary>
    [DomName("offer")]
    Offer,
    /// <summary>
    /// A provisional answer.
    /// </summary>
    [DomName("pranswer")]
    PrAnswer,
    /// <summary>
    /// A final answer completing a negotiation.
    /// </summary>
    [DomName("answer")]
    Answer,
    /// <summary>
    /// Cancels the current negotiation.
    /// </summary>
    [DomName("rollback")]
    Rollback,
}
