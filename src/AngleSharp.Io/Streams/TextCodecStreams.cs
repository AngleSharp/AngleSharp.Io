namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Text;
using System;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Decodes a stream of bytes into a stream of strings. For more information
/// see: https://encoding.spec.whatwg.org/#interface-textdecoderstream
/// </summary>
[DomName("TextDecoderStream")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class TextDecoderStream
{
    private static readonly Byte[] Utf8Bom = { 0xef, 0xbb, 0xbf };
    private static readonly Byte[] Utf16LeBom = { 0xff, 0xfe };
    private static readonly Byte[] Utf16BeBom = { 0xfe, 0xff };

    private readonly TransformStream _transform;
    private readonly Encoding _encoding;
    private readonly Decoder _decoder;
    private readonly Boolean _ignoreBom;
    private Byte[] _withheld = Array.Empty<Byte>();
    private Boolean _bomChecked;

    /// <summary>
    /// Creates a new decoding stream.
    /// </summary>
    /// <param name="label">The label of the encoding, by default UTF-8.</param>
    /// <param name="options">Whether errors throw and the BOM stays.</param>
    [DomConstructor]
    public TextDecoderStream(String label = "utf-8", TextDecoderOptions options = null)
    {
        if (!TextEncoding.IsSupported(label ?? String.Empty))
        {
            throw new NotSupportedException($"The encoding '{label}' is not supported.");
        }

        var encoding = (Encoding)TextEncoding.Resolve(label).Clone();
        encoding.DecoderFallback = options?.Fatal == true
            ? DecoderFallback.ExceptionFallback
            : new DecoderReplacementFallback("\ufffd");
        _encoding = encoding;
        _decoder = encoding.GetDecoder();
        _ignoreBom = options?.IgnoreBom ?? false;
        IsFatal = options?.Fatal ?? false;
        _transform = new TransformStream(new Transformer
        {
            Transform = (chunk, controller) =>
            {
                Decode(chunk, controller, false);
                return Task.CompletedTask;
            },
            Flush = controller =>
            {
                Decode(Array.Empty<Byte>(), controller, true);
                return Task.CompletedTask;
            },
        });
    }

    /// <summary>
    /// Gets the name of the encoding.
    /// </summary>
    [DomName("encoding")]
    public String Encoding => _encoding.WebName.ToLowerInvariant();

    /// <summary>
    /// Gets if bytes the encoding has no characters for end the stream
    /// instead of becoming U+FFFD.
    /// </summary>
    [DomName("fatal")]
    public Boolean IsFatal { get; }

    /// <summary>
    /// Gets if a leading byte order mark comes through as text.
    /// </summary>
    [DomName("ignoreBOM")]
    public Boolean IgnoreBom => _ignoreBom;

    /// <summary>
    /// Gets the side the strings come out of.
    /// </summary>
    [DomName("readable")]
    public ReadableStream Readable => _transform.Readable;

    /// <summary>
    /// Gets the side the bytes go into.
    /// </summary>
    [DomName("writable")]
    public WritableStream Writable => _transform.Writable;

    private void Decode(Object chunk, TransformStreamDefaultController controller, Boolean final)
    {
        var bytes = ToBytes(chunk);
        var offset = 0;
        var count = bytes.Length;

        if (!_bomChecked && !_ignoreBom)
        {
            var bom = GetBom();

            if (bom != null)
            {
                if (_withheld.Length > 0)
                {
                    var combined = new Byte[_withheld.Length + bytes.Length];
                    Array.Copy(_withheld, combined, _withheld.Length);
                    Array.Copy(bytes, 0, combined, _withheld.Length, bytes.Length);
                    bytes = combined;
                    count = bytes.Length;
                    _withheld = Array.Empty<Byte>();
                }

                if (!final && bytes.Length < bom.Length && IsPrefix(bytes, bom, bytes.Length))
                {
                    //  too little to tell a byte order mark from text yet
                    _withheld = bytes;
                    return;
                }

                _bomChecked = true;

                if (IsPrefix(bytes, bom, bom.Length))
                {
                    offset = bom.Length;
                    count -= bom.Length;
                }
            }
            else
            {
                _bomChecked = true;
            }
        }

        var chars = new Char[_encoding.GetMaxCharCount(count) + 2];
        var written = _decoder.GetChars(bytes, offset, count, chars, 0, final);

        if (written > 0)
        {
            controller.Enqueue(new String(chars, 0, written));
        }
    }

    private Byte[] GetBom()
    {
        switch (_encoding.CodePage)
        {
            case 65001:
                return Utf8Bom;
            case 1200:
                return Utf16LeBom;
            case 1201:
                return Utf16BeBom;
            default:
                return null;
        }
    }

    private static Boolean IsPrefix(Byte[] bytes, Byte[] bom, Int32 length)
    {
        for (var i = 0; i < length && i < bom.Length; i++)
        {
            if (bytes[i] != bom[i])
            {
                return false;
            }
        }

        return true;
    }

    private static Byte[] ToBytes(Object chunk)
    {
        switch (chunk)
        {
            case Byte[] bytes:
                return bytes;
            case ArraySegment<Byte> segment:
                var slice = new Byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, slice, 0, segment.Count);
                return slice;
            default:
                throw new InvalidOperationException("The chunk is not binary data.");
        }
    }
}

/// <summary>
/// Represents the options of a <see cref="TextDecoderStream"/>.
/// </summary>
public sealed class TextDecoderOptions
{
    /// <summary>
    /// Gets or sets if bytes the encoding has no characters for end the
    /// stream instead of becoming U+FFFD.
    /// </summary>
    [DomName("fatal")]
    public Boolean Fatal { get; set; }

    /// <summary>
    /// Gets or sets if a leading byte order mark comes through as text.
    /// </summary>
    [DomName("ignoreBOM")]
    public Boolean IgnoreBom { get; set; }
}

/// <summary>
/// Encodes a stream of strings into a stream of UTF-8 bytes. For more
/// information see: https://encoding.spec.whatwg.org/#interface-textencoderstream
/// </summary>
[DomName("TextEncoderStream")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class TextEncoderStream
{
    private readonly TransformStream _transform;
    private readonly Encoder _encoder;

    /// <summary>
    /// Creates a new encoding stream.
    /// </summary>
    [DomConstructor]
    public TextEncoderStream()
    {
        _encoder = System.Text.Encoding.UTF8.GetEncoder();
        _transform = new TransformStream(new Transformer
        {
            Transform = (chunk, controller) =>
            {
                var text = chunk as String ?? chunk?.ToString() ?? String.Empty;
                Encode(text.ToCharArray(), controller, false);
                return Task.CompletedTask;
            },
            Flush = controller =>
            {
                //  an unpaired trailing surrogate still becomes U+FFFD
                Encode(Array.Empty<Char>(), controller, true);
                return Task.CompletedTask;
            },
        });
    }

    /// <summary>
    /// Gets the name of the encoding, which is always UTF-8.
    /// </summary>
    [DomName("encoding")]
    public String Encoding => "utf-8";

    /// <summary>
    /// Gets the side the bytes come out of.
    /// </summary>
    [DomName("readable")]
    public ReadableStream Readable => _transform.Readable;

    /// <summary>
    /// Gets the side the strings go into.
    /// </summary>
    [DomName("writable")]
    public WritableStream Writable => _transform.Writable;

    private void Encode(Char[] chars, TransformStreamDefaultController controller, Boolean final)
    {
        var buffer = new Byte[System.Text.Encoding.UTF8.GetMaxByteCount(chars.Length) + 4];
        var written = _encoder.GetBytes(chars, 0, chars.Length, buffer, 0, final);

        if (written > 0)
        {
            controller.Enqueue(new ArraySegment<Byte>(buffer, 0, written));
        }
    }
}
