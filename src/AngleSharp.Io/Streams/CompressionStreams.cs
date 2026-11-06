namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

/// <summary>
/// Compresses a stream of bytes. For more information see:
/// https://compression.spec.whatwg.org/#compression-stream
/// </summary>
[DomName("CompressionStream")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class CompressionStream
{
    private readonly TransformStream _transform;
    private readonly MemoryStream _buffer;
    private readonly Stream _compressor;

    /// <summary>
    /// Creates a new compressing stream.
    /// </summary>
    /// <param name="format">The format: gzip, deflate, or deflate-raw.</param>
    [DomConstructor]
    public CompressionStream(String format)
    {
        _buffer = new MemoryStream();
        _compressor = CompressionFormats.CreateCompressor(format, _buffer);
        _transform = new TransformStream(new Transformer
        {
            Transform = (chunk, controller) =>
            {
                var bytes = CompressionFormats.ToBytes(chunk);
                _compressor.Write(bytes, 0, bytes.Length);
                _compressor.Flush();
                CompressionFormats.Drain(_buffer, controller);
                return Task.CompletedTask;
            },
            Flush = controller =>
            {
                //  only closing it makes the compressor write its trailer
                _compressor.Dispose();
                CompressionFormats.Drain(_buffer, controller);
                _buffer.Dispose();
                return Task.CompletedTask;
            },
        });
    }

    /// <summary>
    /// Gets the side the compressed bytes come out of.
    /// </summary>
    [DomName("readable")]
    public ReadableStream Readable => _transform.Readable;

    /// <summary>
    /// Gets the side the bytes go into.
    /// </summary>
    [DomName("writable")]
    public WritableStream Writable => _transform.Writable;
}

/// <summary>
/// Decompresses a stream of bytes. For more information see:
/// https://compression.spec.whatwg.org/#decompression-stream
/// </summary>
[DomName("DecompressionStream")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class DecompressionStream
{
    private readonly TransformStream _transform;
    private readonly MemoryStream _buffer;

    /// <summary>
    /// Creates a new decompressing stream.
    /// </summary>
    /// <param name="format">The format: gzip, deflate, or deflate-raw.</param>
    [DomConstructor]
    public DecompressionStream(String format)
    {
        //  probe the format here, so that an unknown one throws in the constructor
        CompressionFormats.CreateCompressor(format, Stream.Null).Dispose();
        _buffer = new MemoryStream();
        _transform = new TransformStream(new Transformer
        {
            Transform = (chunk, controller) =>
            {
                //  the trailing checksum can only be checked at the end, so the
                //  input is gathered and inflated in one go
                var bytes = CompressionFormats.ToBytes(chunk);
                _buffer.Write(bytes, 0, bytes.Length);
                return Task.CompletedTask;
            },
            Flush = controller =>
            {
                _buffer.Position = 0;

                using (var decompressor = CompressionFormats.CreateDecompressor(format, _buffer))
                using (var output = new MemoryStream())
                {
                    decompressor.CopyTo(output);

                    if (output.Length > 0)
                    {
                        controller.Enqueue(new ArraySegment<Byte>(output.ToArray()));
                    }
                }

                _buffer.Dispose();
                return Task.CompletedTask;
            },
        });
    }

    /// <summary>
    /// Gets the side the decompressed bytes come out of.
    /// </summary>
    [DomName("readable")]
    public ReadableStream Readable => _transform.Readable;

    /// <summary>
    /// Gets the side the compressed bytes go into.
    /// </summary>
    [DomName("writable")]
    public WritableStream Writable => _transform.Writable;
}

static class CompressionFormats
{
    public static Stream CreateCompressor(String format, Stream target)
    {
        switch (format)
        {
            case "gzip":
                return new GZipStream(target, CompressionLevel.Optimal, true);
            case "deflate-raw":
                return new DeflateStream(target, CompressionLevel.Optimal, true);
            case "deflate":
#if NET8_0_OR_GREATER
                return new ZLibStream(target, CompressionLevel.Optimal, true);
#else
                throw new NotSupportedException("The deflate (zlib) format is not supported on this platform.");
#endif
            default:
                throw new NotSupportedException($"The format '{format}' is not supported.");
        }
    }

    public static Stream CreateDecompressor(String format, Stream source)
    {
        switch (format)
        {
            case "gzip":
                return new GZipStream(source, CompressionMode.Decompress, true);
            case "deflate-raw":
                return new DeflateStream(source, CompressionMode.Decompress, true);
            case "deflate":
#if NET8_0_OR_GREATER
                return new ZLibStream(source, CompressionMode.Decompress, true);
#else
                throw new NotSupportedException("The deflate (zlib) format is not supported on this platform.");
#endif
            default:
                throw new NotSupportedException($"The format '{format}' is not supported.");
        }
    }

    public static void Drain(MemoryStream buffer, TransformStreamDefaultController controller)
    {
        if (buffer.Length > 0)
        {
            controller.Enqueue(new ArraySegment<Byte>(buffer.ToArray()));
            buffer.SetLength(0);
        }
    }

    public static Byte[] ToBytes(Object chunk)
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
