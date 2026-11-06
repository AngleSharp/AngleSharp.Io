namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents a destination for data written chunk by chunk. For more
/// information see: https://streams.spec.whatwg.org/#ws-class
/// </summary>
[DomName("WritableStream")]
[DomExposed("Window")]
[DomExposed("Worker")]
public class WritableStream
{
    #region Fields

    private readonly Object _sync = new Object();
    private readonly WritableStreamDefaultController _controller;
    private readonly WritableStreamWriteCallback _write;
    private readonly WritableStreamCloseCallback _close;
    private readonly StreamCancelCallback _abort;
    private readonly QueuingStrategySizeCallback _size;
    private readonly Double _highWaterMark;
    private TaskCompletionSource<Object> _ready;
    private Task _chain;
    private Double _queueTotalSize;
    private StreamStates _state;
    private Object _storedError;
    private WritableStreamDefaultWriter _writer;

    private enum StreamStates : byte
    {
        Writable,
        Closing,
        Closed,
        Errored,
    }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a new stream over the given sink.
    /// </summary>
    /// <param name="underlyingSink">The sink receiving the data.</param>
    /// <param name="strategy">How much may queue up before writes wait.</param>
    [DomConstructor]
    public WritableStream(UnderlyingSink underlyingSink = null, QueuingStrategy strategy = null)
        : this(underlyingSink?.Start, underlyingSink?.Write, underlyingSink?.Close, underlyingSink?.Abort, strategy?.HighWaterMark ?? 1.0, strategy?.Size)
    {
    }

    internal WritableStream(WritableStreamStartCallback start, WritableStreamWriteCallback write, WritableStreamCloseCallback close, StreamCancelCallback abort, Double highWaterMark, QueuingStrategySizeCallback size)
    {
        _write = write;
        _close = close;
        _abort = abort;
        _size = size;
        _highWaterMark = highWaterMark;
        _state = StreamStates.Writable;
        _controller = new WritableStreamDefaultController(this);
        _chain = Start(start);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets if a writer has taken the stream for itself.
    /// </summary>
    [DomName("locked")]
    public Boolean Locked => _writer != null;

    private Exception StoredException => StreamErrors.ToException(_storedError);

    #endregion

    #region Methods

    /// <summary>
    /// Takes the stream for a single producer, who writes it chunk by chunk.
    /// </summary>
    /// <returns>The writer of the stream.</returns>
    [DomName("getWriter")]
    public WritableStreamDefaultWriter GetWriter() => new WritableStreamDefaultWriter(this);

    /// <summary>
    /// Fails the stream: what is still queued is discarded.
    /// </summary>
    /// <param name="reason">The reason, given to the sink.</param>
    /// <returns>The task of the sink's cleanup.</returns>
    [DomName("abort")]
    public Task Abort(Object reason = null)
    {
        if (Locked)
        {
            return StreamErrors.Faulted(new InvalidOperationException("The stream is locked."));
        }

        return AbortInternal(reason);
    }

    /// <summary>
    /// Closes the stream once the queued writes are done.
    /// </summary>
    /// <returns>The task of the closing.</returns>
    [DomName("close")]
    public Task Close()
    {
        if (Locked)
        {
            return StreamErrors.Faulted(new InvalidOperationException("The stream is locked."));
        }

        return CloseInternal();
    }

    #endregion

    #region Internals

    internal void AttachWriter(WritableStreamDefaultWriter writer)
    {
        lock (_sync)
        {
            if (_writer != null)
            {
                throw new InvalidOperationException("The stream is already locked.");
            }

            _writer = writer;

            if (_state == StreamStates.Closed)
            {
                writer.SettleClosed(null);
            }
            else if (_state == StreamStates.Errored)
            {
                writer.SettleClosed(StoredException);
            }
        }
    }

    internal void ReleaseWriter(WritableStreamDefaultWriter writer)
    {
        lock (_sync)
        {
            if (_writer != writer)
            {
                return;
            }

            _writer = null;
            writer.SettleClosed(new InvalidOperationException("The writer was released."));
        }
    }

    internal Task WriteChunk(Object chunk)
    {
        Double size;

        try
        {
            size = _size?.Invoke(chunk) ?? 1.0;
        }
        catch (Exception error)
        {
            ErrorInternal(error);
            return Task.FromException(error);
        }

        lock (_sync)
        {
            if (_state == StreamStates.Errored)
            {
                return Task.FromException(StoredException);
            }

            if (_state != StreamStates.Writable)
            {
                return StreamErrors.Faulted(new InvalidOperationException("The stream is closing or closed."));
            }

            _queueTotalSize += size;
            UpdateReady();
        }

        return EnqueueOperation(async () =>
        {
            try
            {
                if (_state == StreamStates.Errored)
                {
                    throw StoredException;
                }

                if (_write != null)
                {
                    await (_write.Invoke(chunk, _controller) ?? Task.CompletedTask).ConfigureAwait(false);
                }

                if (_state == StreamStates.Errored)
                {
                    throw StoredException;
                }
            }
            catch (Exception error)
            {
                ErrorInternal(StreamErrors.ToReason(error));
                throw;
            }
            finally
            {
                lock (_sync)
                {
                    _queueTotalSize -= size;
                    UpdateReady();
                }
            }
        });
    }

    internal Task CloseInternal()
    {
        lock (_sync)
        {
            if (_state == StreamStates.Errored)
            {
                return Task.FromException(StoredException);
            }

            if (_state != StreamStates.Writable)
            {
                return StreamErrors.Faulted(new InvalidOperationException("The stream is closing or closed."));
            }

            _state = StreamStates.Closing;
        }

        return EnqueueOperation(async () =>
        {
            try
            {
                if (_state == StreamStates.Errored)
                {
                    throw StoredException;
                }

                if (_close != null)
                {
                    await (_close.Invoke() ?? Task.CompletedTask).ConfigureAwait(false);
                }

                lock (_sync)
                {
                    _state = StreamStates.Closed;
                    _writer?.SettleClosed(null);
                }
            }
            catch (Exception error)
            {
                ErrorInternal(StreamErrors.ToReason(error));
                throw;
            }
        });
    }

    internal Task AbortInternal(Object reason)
    {
        lock (_sync)
        {
            if (_state == StreamStates.Closed || _state == StreamStates.Errored)
            {
                return Task.CompletedTask;
            }
        }

        ErrorInternal(reason);
        var abort = _abort;

        if (abort == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            return abort.Invoke(reason) ?? Task.CompletedTask;
        }
        catch (Exception error)
        {
            return Task.FromException(error);
        }
    }

    internal void ErrorInternal(Object reason)
    {
        lock (_sync)
        {
            if (_state == StreamStates.Closed || _state == StreamStates.Errored)
            {
                return;
            }

            _state = StreamStates.Errored;
            _storedError = reason;
            var error = StoredException;
            _writer?.SettleClosed(error);
            //  waiting writers have to see the failure rather than wait on
            var ready = _ready;
            _ready = null;
            ready?.TrySetException(error);
        }
    }

    internal Double? GetDesiredSize()
    {
        lock (_sync)
        {
            switch (_state)
            {
                case StreamStates.Errored:
                    return null;
                case StreamStates.Closing:
                case StreamStates.Closed:
                    return 0;
                default:
                    return _highWaterMark - _queueTotalSize;
            }
        }
    }

    internal Task GetReady()
    {
        lock (_sync)
        {
            if (_state == StreamStates.Errored)
            {
                return Task.FromException(StoredException);
            }

            return _ready?.Task ?? Task.CompletedTask;
        }
    }

    #endregion

    #region Helpers

    private async Task Start(WritableStreamStartCallback start)
    {
        if (start != null)
        {
            try
            {
                await (start.Invoke(_controller) ?? Task.CompletedTask).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                ErrorInternal(StreamErrors.ToReason(error));
            }
        }
    }

    //  the caller holds the lock
    private void UpdateReady()
    {
        if (_highWaterMark - _queueTotalSize > 0)
        {
            var ready = _ready;
            _ready = null;
            ready?.TrySetResult(null);
        }
        else if (_ready == null)
        {
            _ready = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private Task EnqueueOperation(Func<Task> operation)
    {
        lock (_sync)
        {
            var task = RunAfter(_chain, operation);
            _chain = ReadableStream.Quietly(task);
            return task;
        }
    }

    private static async Task RunAfter(Task previous, Func<Task> operation)
    {
        //  a failed earlier operation already put the stream into its errored
        //  state, which the operation checks for itself
        await ReadableStream.Quietly(previous).ConfigureAwait(false);
        await operation().ConfigureAwait(false);
    }

    #endregion
}

/// <summary>
/// Writes a <see cref="WritableStream"/> chunk by chunk, keeping the stream
/// for itself until it releases the lock. For more information see:
/// https://streams.spec.whatwg.org/#default-writer-class
/// </summary>
[DomName("WritableStreamDefaultWriter")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class WritableStreamDefaultWriter
{
    private readonly TaskCompletionSource<Object> _closed = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
    private WritableStream _stream;

    /// <summary>
    /// Creates a writer locking the given stream.
    /// </summary>
    /// <param name="stream">The stream to write.</param>
    [DomConstructor]
    public WritableStreamDefaultWriter(WritableStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        stream.AttachWriter(this);
    }

    /// <summary>
    /// Gets the task that ends with the stream: it completes when the stream
    /// is closed and fails when the stream fails or the lock is released.
    /// </summary>
    [DomName("closed")]
    public Task Closed => _closed.Task;

    /// <summary>
    /// Gets the task to wait for when the queue is full; it completes once
    /// there is room again.
    /// </summary>
    [DomName("ready")]
    public Task Ready => _stream?.GetReady() ?? ReleasedError();

    /// <summary>
    /// Gets how much the queue still takes, negative when it holds more than
    /// its strategy asks for, and null when the stream failed.
    /// </summary>
    [DomName("desiredSize")]
    public Double? DesiredSize => _stream?.GetDesiredSize();

    /// <summary>
    /// Writes a chunk. Waiting for the task is how a producer honors the
    /// backpressure of a slow sink.
    /// </summary>
    /// <param name="chunk">The chunk to write.</param>
    /// <returns>The task of the sink taking the chunk.</returns>
    [DomName("write")]
    public Task Write(Object chunk = null) => _stream?.WriteChunk(chunk) ?? ReleasedError();

    /// <summary>
    /// Closes the stream once the queued writes are done.
    /// </summary>
    /// <returns>The task of the closing.</returns>
    [DomName("close")]
    public Task Close() => _stream?.CloseInternal() ?? ReleasedError();

    /// <summary>
    /// Fails the stream: what is still queued is discarded.
    /// </summary>
    /// <param name="reason">The reason, given to the sink.</param>
    /// <returns>The task of the sink's cleanup.</returns>
    [DomName("abort")]
    public Task Abort(Object reason = null) => _stream?.AbortInternal(reason) ?? ReleasedError();

    /// <summary>
    /// Gives the stream back, so that another writer can take it.
    /// </summary>
    [DomName("releaseLock")]
    public void ReleaseLock()
    {
        var stream = _stream;
        _stream = null;
        stream?.ReleaseWriter(this);
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

    private static Task ReleasedError() =>
        StreamErrors.Faulted(new InvalidOperationException("The writer was released."));
}

/// <summary>
/// Lets the sink of a <see cref="WritableStream"/> fail the stream. For more
/// information see: https://streams.spec.whatwg.org/#ws-default-controller-class
/// </summary>
[DomName("WritableStreamDefaultController")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class WritableStreamDefaultController
{
    private readonly WritableStream _stream;

    internal WritableStreamDefaultController(WritableStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Fails the stream, discarding its queue: writes fail with the reason.
    /// </summary>
    /// <param name="reason">The reason of the failure.</param>
    [DomName("error")]
    public void Error(Object reason = null) => _stream.ErrorInternal(reason);
}
