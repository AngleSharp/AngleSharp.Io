namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Represents an immutable piece of binary data with a type, as scripts
/// create it. For more information see:
/// https://w3c.github.io/FileAPI/#blob-section
/// </summary>
[DomName("Blob")]
[DomExposed("Window")]
[DomExposed("Worker")]
public class Blob : IBlob
{
    private readonly Byte[] _content;
    private readonly String _type;
    private Boolean _closed;

    /// <summary>
    /// Creates a new blob from the given parts.
    /// </summary>
    /// <param name="blobParts">The parts: strings, blobs, and buffers.</param>
    /// <param name="options">The type and the treatment of line endings.</param>
    [DomConstructor]
    public Blob(Object[] blobParts = null, BlobPropertyBag options = null)
        : this(Combine(blobParts, options), NormalizeType(options?.Type))
    {
    }

    /// <summary>
    /// Creates a new blob over the given bytes, which are not copied.
    /// </summary>
    /// <param name="content">The content of the blob.</param>
    /// <param name="type">The mime-type of the content, if any.</param>
    public Blob(Byte[] content, String type = "")
    {
        _content = content ?? Array.Empty<Byte>();
        _type = type ?? String.Empty;
    }

    /// <summary>
    /// Gets the size of the blob in bytes.
    /// </summary>
    public Int32 Length => _content.Length;

    /// <summary>
    /// Gets the mime-type of the blob, or an empty string.
    /// </summary>
    public String Type => _type;

    /// <summary>
    /// Gets if the blob has been closed.
    /// </summary>
    public Boolean IsClosed => _closed;

    /// <summary>
    /// Gets a fresh stream over the content.
    /// </summary>
    public Stream Body => _closed ? System.IO.Stream.Null : new MemoryStream(_content, false);

    internal Byte[] GetBytes() => _content;

    /// <summary>
    /// Takes a subset of the blob, with negative indices counting from the end.
    /// </summary>
    /// <param name="start">The first byte of the subset.</param>
    /// <param name="end">The byte after the last of the subset.</param>
    /// <param name="contentType">The mime-type of the new blob.</param>
    /// <returns>The new blob over the subset.</returns>
    public IBlob Slice(Int32 start = 0, Int32 end = Int32.MaxValue, String contentType = null)
    {
        var size = _content.Length;
        var from = start < 0 ? Math.Max(size + start, 0) : Math.Min(start, size);
        var to = end < 0 ? Math.Max(size + end, 0) : Math.Min(end, size);
        var length = Math.Max(to - from, 0);
        var bytes = new Byte[length];
        Array.Copy(_content, from, bytes, 0, length);
        return new Blob(bytes, NormalizeType(contentType));
    }

    /// <summary>
    /// Closes the blob, after which its content is no longer readable.
    /// </summary>
    public void Close() => _closed = true;

    void IDisposable.Dispose() => Close();

    /// <summary>
    /// Reads the blob as UTF-8 text.
    /// </summary>
    /// <returns>The text of the blob.</returns>
    [DomName("text")]
    public Task<String> Text() => Task.FromResult(DecodeUtf8(ReadAll()));

    /// <summary>
    /// Reads the blob's bytes, which scripts receive as an ArrayBuffer.
    /// </summary>
    /// <returns>A copy of the content.</returns>
    [DomName("arrayBuffer")]
    public Task<Byte[]> ArrayBuffer() => Task.FromResult(ReadAll());

    /// <summary>
    /// Reads the blob's bytes, which scripts receive as a Uint8Array.
    /// </summary>
    /// <returns>A copy of the content.</returns>
    [DomName("bytes")]
    public Task<ArraySegment<Byte>> Bytes() => Task.FromResult(new ArraySegment<Byte>(ReadAll()));

    /// <summary>
    /// Gets a stream over the content of the blob.
    /// </summary>
    /// <returns>The new stream.</returns>
    [DomName("stream")]
    public ReadableStream Stream() => ReadableStream.FromStream(Body);

    internal static String DecodeUtf8(Byte[] bytes)
    {
        var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    private Byte[] ReadAll()
    {
        if (_closed)
        {
            throw new DomException(DomError.InvalidState);
        }

        return (Byte[])_content.Clone();
    }

    private static Byte[] Combine(Object[] parts, BlobPropertyBag options)
    {
        if (parts == null || parts.Length == 0)
        {
            return Array.Empty<Byte>();
        }

        var isNative = String.Equals(options?.Endings, "native", StringComparison.Ordinal);

        using (var content = new MemoryStream())
        {
            foreach (var part in parts)
            {
                var bytes = GetBytesOf(part, isNative);
                content.Write(bytes, 0, bytes.Length);
            }

            return content.ToArray();
        }
    }

    private static Byte[] GetBytesOf(Object part, Boolean isNative)
    {
        switch (part)
        {
            case null:
                return Encoding.UTF8.GetBytes("null");
            case Byte[] buffer:
                return buffer;
            case ArraySegment<Byte> segment:
                var slice = new Byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, slice, 0, segment.Count);
                return slice;
            case Blob blob:
                return blob.GetBytes();
            case IBlob other:
                using (var memory = new MemoryStream())
                {
                    var body = other.Body;

                    if (body != null)
                    {
                        if (body.CanSeek)
                        {
                            body.Seek(0, SeekOrigin.Begin);
                        }

                        body.CopyTo(memory);
                    }

                    return memory.ToArray();
                }
            default:
                var text = part as String ?? part.ToString() ?? String.Empty;

                if (isNative)
                {
                    text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", Environment.NewLine);
                }

                return Encoding.UTF8.GetBytes(text);
        }
    }

    private static String NormalizeType(String type)
    {
        if (String.IsNullOrEmpty(type))
        {
            return String.Empty;
        }

        foreach (var c in type)
        {
            if (c < '\u0020' || c > '\u007e')
            {
                return String.Empty;
            }
        }

        return type.ToLowerInvariant();
    }
}

/// <summary>
/// Represents the options for creating a <see cref="Blob"/>.
/// </summary>
public class BlobPropertyBag
{
    /// <summary>
    /// Gets or sets the mime-type of the blob.
    /// </summary>
    [DomName("type")]
    public String Type { get; set; }

    /// <summary>
    /// Gets or sets how line endings of text parts are handled: "transparent"
    /// (the default) keeps them, "native" converts them to the platform's.
    /// </summary>
    [DomName("endings")]
    public String Endings { get; set; }
}
