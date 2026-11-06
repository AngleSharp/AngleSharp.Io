namespace AngleSharp.Io.Dom;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Wraps a stream to tell its consumption, which is how the upload progress
/// of a request is seen: the requester reading the body is the transmission.
/// </summary>
sealed class ProgressReportingStream : Stream
{
    private readonly Stream _source;
    private readonly Action<Int64, Boolean> _report;
    private Int64 _loaded;
    private Boolean _completed;

    public ProgressReportingStream(Stream source, Action<Int64, Boolean> report)
    {
        _source = source;
        _report = report;
    }

    public override Boolean CanRead => _source.CanRead;

    public override Boolean CanSeek => _source.CanSeek;

    public override Boolean CanWrite => false;

    public override Int64 Length => _source.Length;

    public override Int64 Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    public override void Flush() => _source.Flush();

    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
    {
        var read = _source.Read(buffer, offset, count);
        Report(read);
        return read;
    }

    public override async Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
    {
        var read = await _source.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        Report(read);
        return read;
    }

    public override Int64 Seek(Int64 offset, SeekOrigin origin) => _source.Seek(offset, origin);

    public override void SetLength(Int64 value) => throw new NotSupportedException();

    public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();

    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            _source.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Report(Int32 read)
    {
        if (!_completed)
        {
            if (read > 0)
            {
                _loaded += read;
                _report.Invoke(_loaded, false);
            }
            else
            {
                _completed = true;
                _report.Invoke(_loaded, true);
            }
        }
    }
}
