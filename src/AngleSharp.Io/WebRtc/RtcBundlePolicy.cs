namespace AngleSharp.Io.WebRtc;

using AngleSharp.Attributes;

/// <summary>
/// The policies for bundling media.
/// </summary>
[DomLiterals]
[DomName("RTCBundlePolicy")]
public enum RtcBundlePolicy
{
    /// <summary>
    /// One transport per media type.
    /// </summary>
    [DomName("balanced")]
    Balanced,
    /// <summary>
    /// One transport per track.
    /// </summary>
    [DomName("max-compat")]
    MaxCompat,
    /// <summary>
    /// A single transport for everything.
    /// </summary>
    [DomName("max-bundle")]
    MaxBundle,
}
