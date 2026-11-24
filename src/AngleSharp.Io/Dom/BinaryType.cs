namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The types binary messages of a WebSocket are given as.
/// </summary>
[DomLiterals]
[DomName("BinaryType")]
public enum BinaryType
{
    /// <summary>
    /// A blob.
    /// </summary>
    [DomName("blob")]
    Blob,
    /// <summary>
    /// An array buffer.
    /// </summary>
    [DomName("arraybuffer")]
    ArrayBuffer,
}
