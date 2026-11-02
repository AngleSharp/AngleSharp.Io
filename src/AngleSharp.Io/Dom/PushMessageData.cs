namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.Text;

/// <summary>
/// Represents the payload of a push message.
/// </summary>
[DomName("PushMessageData")]
public sealed class PushMessageData
{
    private readonly Byte[] _content;

    /// <summary>
    /// Creates new data from the given bytes.
    /// </summary>
    /// <param name="content">The decrypted payload.</param>
    public PushMessageData(Byte[] content)
    {
        _content = (Byte[])(content ?? throw new ArgumentNullException(nameof(content))).Clone();
    }

    /// <summary>
    /// Gets the payload as a buffer.
    /// </summary>
    /// <returns>A copy of the payload.</returns>
    [DomName("arrayBuffer")]
    public Byte[] ArrayBuffer() => (Byte[])_content.Clone();

    /// <summary>
    /// Gets the payload as a blob.
    /// </summary>
    /// <returns>The blob, which has no type.</returns>
    [DomName("blob")]
    public IBlob Blob() => new InputFile(String.Empty, String.Empty, ArrayBuffer());

    /// <summary>
    /// Gets the payload as an array of bytes.
    /// </summary>
    /// <returns>A copy of the payload, which scripts receive as a Uint8Array.</returns>
    [DomName("bytes")]
    public ArraySegment<Byte> Bytes() => new ArraySegment<Byte>(ArrayBuffer());

    /// <summary>
    /// Reads the payload as JSON: objects are dictionaries, arrays are object
    /// arrays, numbers are doubles, the rest are strings, booleans and null.
    /// </summary>
    /// <returns>The parsed value.</returns>
    [DomName("json")]
    public Object Json() => JsonReader.Parse(Text());

    /// <summary>
    /// Reads the payload as UTF-8 text.
    /// </summary>
    /// <returns>The text.</returns>
    [DomName("text")]
    public String Text() => Encoding.UTF8.GetString(_content);
}
