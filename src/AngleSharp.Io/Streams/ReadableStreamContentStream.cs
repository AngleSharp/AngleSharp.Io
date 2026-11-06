namespace AngleSharp.Io.Dom;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Reads a <see cref="ReadableStream"/> as a .NET stream, as the transport
/// does for a body that a script gave as a stream.
/// </summary>
sealed class ReadableStreamContentStream : Stream
{
    private readonly ReadableStream _source;
    private ReadableStreamDefaultReader _reader;
    private Byte[] _current;
    private Int32 _offset;
    private Boolean _done;

    public ReadableStreamContentStream(ReadableStream source)
    {
        _source = source;
    }

    public override Boolean CanRead => true;

    public override Boolean CanSeek => false;

    public override Boolean CanWrite => false;

    public override Int64 Length => throw new NotSupportedException();

    public override Int64 Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
    {
        while (_current == null || _offset >= _current.Length)
        {
            if (_done)
            {
                return 0;
            }

            _reader = _reader ?? _source.GetReader();
            var result = await _reader.Read().ConfigureAwait(false);

            if (result.Done)
            {
                _done = true;
                return 0;
            }

            _current = _source.ChunkToBytes(result.Value);
            _offset = 0;
        }

        var length = Math.Min(count, _current.Length - _offset);
        Array.Copy(_current, _offset, buffer, offset, length);
        _offset += length;
        return length;
    }

    public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(Int64 value) => throw new NotSupportedException();

    public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();

    protected override void Dispose(Boolean disposing)
    {
        if (disposing && !_done)
        {
            _done = true;
            var reader = _reader ?? (_source.Locked ? null : _source.GetReader());
            _ = reader?.Cancel(null);
        }

        base.Dispose(disposing);
    }
}
