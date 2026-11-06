namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.Threading.Tasks;

/// <summary>
/// Reads a <see cref="ReadableStream"/> chunk by chunk, keeping the stream
/// for itself until it releases the lock. For more information see:
/// https://streams.spec.whatwg.org/#default-reader-class
/// </summary>
[DomName("ReadableStreamDefaultReader")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class ReadableStreamDefaultReader
{
    private readonly TaskCompletionSource<Object> _closed = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
    private ReadableStream _stream;

    /// <summary>
    /// Creates a reader locking the given stream.
    /// </summary>
    /// <param name="stream">The stream to read.</param>
    [DomConstructor]
    public ReadableStreamDefaultReader(ReadableStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        stream.AttachReader(this);
    }

    /// <summary>
    /// Gets the task that ends with the stream: it completes when the stream
    /// is closed and fails when the stream fails or the lock is released.
    /// </summary>
    [DomName("closed")]
    public Task Closed => _closed.Task;

    /// <summary>
    /// Reads the next chunk, or that the stream is done.
    /// </summary>
    /// <returns>The task giving the chunk, or done without a value.</returns>
    [DomName("read")]
    public Task<ReadableStreamReadResult> Read()
    {
        var stream = _stream;

        if (stream == null)
        {
            return StreamErrors.Faulted<ReadableStreamReadResult>(new InvalidOperationException("The reader was released."));
        }

        return stream.ReadChunk();
    }

    /// <summary>
    /// Tells the stream that this consumer is no longer interested,
    /// discarding the data.
    /// </summary>
    /// <param name="reason">The reason, given to the source.</param>
    /// <returns>The task of the source's cleanup.</returns>
    [DomName("cancel")]
    public Task Cancel(Object reason = null)
    {
        var stream = _stream;

        if (stream == null)
        {
            return StreamErrors.Faulted(new InvalidOperationException("The reader was released."));
        }

        return stream.CancelInternal(reason);
    }

    /// <summary>
    /// Gives the stream back, so that another reader can take it. Reads
    /// still waiting for data fail.
    /// </summary>
    [DomName("releaseLock")]
    public void ReleaseLock()
    {
        var stream = _stream;
        _stream = null;
        stream?.ReleaseReader(this);
    }

    internal void SettleClosed(Exception error)
    {
        if (error == null)
        {
            _closed.TrySetResult(null);
        }
        else
        {
            _closed.TrySetException(error);
        }
    }
}

/// <summary>
/// Enqueues the chunks of a <see cref="ReadableStream"/>, given to the
/// callbacks of the underlying source. For more information see:
/// https://streams.spec.whatwg.org/#rs-default-controller-class
/// </summary>
[DomName("ReadableStreamDefaultController")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class ReadableStreamDefaultController
{
    private readonly ReadableStream _stream;

    internal ReadableStreamDefaultController(ReadableStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Gets how much the queue still wants, negative when it holds more than
    /// its strategy asks for, and null when the stream failed.
    /// </summary>
    [DomName("desiredSize")]
    public Double? DesiredSize => _stream.GetDesiredSize();

    /// <summary>
    /// Enqueues a chunk, giving it to a waiting read directly.
    /// </summary>
    /// <param name="chunk">The chunk to enqueue.</param>
    [DomName("enqueue")]
    public void Enqueue(Object chunk = null) => _stream.EnqueueInternal(chunk);

    /// <summary>
    /// Closes the stream; enqueued chunks can still be read.
    /// </summary>
    [DomName("close")]
    public void Close() => _stream.CloseInternal();

    /// <summary>
    /// Fails the stream, discarding its queue: reads fail with the reason.
    /// </summary>
    /// <param name="reason">The reason of the failure.</param>
    [DomName("error")]
    public void Error(Object reason = null) => _stream.ErrorInternal(reason);
}

/// <summary>
/// Represents what a read gives: a chunk, or that the stream is done.
/// </summary>
[DomName("ReadableStreamReadResult")]
public sealed class ReadableStreamReadResult
{
    internal ReadableStreamReadResult(Object value, Boolean done)
    {
        Value = value;
        Done = done;
    }

    /// <summary>
    /// Gets the chunk, or null when the stream is done.
    /// </summary>
    [DomName("value")]
    public Object Value { get; }

    /// <summary>
    /// Gets if the stream is done and no chunk was read.
    /// </summary>
    [DomName("done")]
    public Boolean Done { get; }
}
