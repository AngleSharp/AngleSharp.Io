namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Represents a source of data read chunk by chunk, as the body of a fetched
/// response is. For more information see:
/// https://streams.spec.whatwg.org/#rs-class
/// </summary>
[DomName("ReadableStream")]
[DomExposed("Window")]
[DomExposed("Worker")]
public class ReadableStream
{
    #region Fields

    private readonly Object _sync = new Object();
    private readonly Queue<QueuedChunk> _queue = new Queue<QueuedChunk>();
    private readonly Queue<TaskCompletionSource<ReadableStreamReadResult>> _readRequests = new Queue<TaskCompletionSource<ReadableStreamReadResult>>();
    private readonly ReadableStreamDefaultController _controller;
    private readonly ReadableStreamSourceCallback _pull;
    private readonly StreamCancelCallback _cancel;
    private readonly QueuingStrategySizeCallback _size;
    private readonly Double _highWaterMark;
    private Double _queueTotalSize;
    private StreamStates _state;
    private Object _storedError;
    private Boolean _closeRequested;
    private Boolean _started;
    private Boolean _pulling;
    private Boolean _pullAgain;
    private Boolean _disturbed;
    private ReadableStreamDefaultReader _reader;
    private Action _onDisturbed;

    private enum StreamStates : byte
    {
        Readable,
        Closed,
        Errored,
    }

    private readonly struct QueuedChunk
    {
        public QueuedChunk(Object chunk, Double size)
        {
            Chunk = chunk;
            Size = size;
        }

        public Object Chunk { get; }

        public Double Size { get; }
    }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a new stream over the given source.
    /// </summary>
    /// <param name="underlyingSource">The source producing the data.</param>
    /// <param name="strategy">When to ask the source for more.</param>
    [DomConstructor]
    public ReadableStream(UnderlyingSource underlyingSource = null, QueuingStrategy strategy = null)
        : this(underlyingSource?.Start, underlyingSource?.Pull, underlyingSource?.Cancel, strategy?.HighWaterMark ?? 1.0, strategy?.Size)
    {
        if (underlyingSource?.Type != null)
        {
            throw new NotSupportedException("Byte streams are not supported.");
        }
    }

    internal ReadableStream(ReadableStreamSourceCallback start, ReadableStreamSourceCallback pull, StreamCancelCallback cancel, Double highWaterMark, QueuingStrategySizeCallback size)
    {
        _pull = pull;
        _cancel = cancel;
        _size = size;
        _highWaterMark = highWaterMark;
        _state = StreamStates.Readable;
        _controller = new ReadableStreamDefaultController(this);
        Start(start);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets if a reader has taken the stream for itself.
    /// </summary>
    [DomName("locked")]
    public Boolean Locked => _reader != null;

    internal Boolean IsDisturbed => _disturbed;

    private Exception StoredException => StreamErrors.ToException(_storedError);

    #endregion

    #region Methods

    /// <summary>
    /// Creates a stream delivering the content of the given stream in chunks.
    /// </summary>
    /// <param name="content">The stream with the data.</param>
    /// <param name="onCancel">What runs when the consumer cancels.</param>
    /// <param name="chunkSize">The size of the chunks in bytes.</param>
    /// <returns>The created stream, which disposes the source at its end.</returns>
    public static ReadableStream FromStream(Stream content, Action onCancel = null, Int32 chunkSize = 16384)
    {
        content = content ?? Stream.Null;
        return new ReadableStream(
            start: null,
            pull: async controller =>
            {
                var buffer = new Byte[chunkSize];
                var count = await content.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                if (count > 0)
                {
                    controller.Enqueue(new ArraySegment<Byte>(buffer, 0, count));
                }
                else
                {
                    controller.Close();
                    content.Dispose();
                }
            },
            cancel: _ =>
            {
                content.Dispose();
                onCancel?.Invoke();
                return Task.CompletedTask;
            },
            highWaterMark: 1.0,
            size: null);
    }

    /// <summary>
    /// Takes the stream for a single consumer, who reads it chunk by chunk.
    /// </summary>
    /// <returns>The reader of the stream.</returns>
    [DomName("getReader")]
    public ReadableStreamDefaultReader GetReader() => new ReadableStreamDefaultReader(this);

    /// <summary>
    /// Tells the stream that no consumer will read it, discarding the data.
    /// </summary>
    /// <param name="reason">The reason, given to the source.</param>
    /// <returns>The task of the source's cleanup.</returns>
    [DomName("cancel")]
    public Task Cancel(Object reason = null)
    {
        if (Locked)
        {
            return StreamErrors.Faulted(new InvalidOperationException("The stream is locked."));
        }

        return CancelInternal(reason);
    }

    /// <summary>
    /// Splits the stream into two delivering the same chunks, e.g. to read a
    /// body twice. The slower one of the two buffers the difference.
    /// </summary>
    /// <returns>The two new streams.</returns>
    [DomName("tee")]
    public ReadableStream[] Tee()
    {
        var reader = GetReader();
        var branches = new ReadableStream[2];
        var canceled = new Boolean[2];
        var reasons = new Object[2];
        var reading = false;
        var cancelSource = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Pull(ReadableStreamDefaultController _)
        {
            lock (branches)
            {
                //  the first pull can come while the second branch is still being created;
                //  the next read repeats it, so it is safe to skip
                if (reading || branches[1] == null)
                {
                    return Task.CompletedTask;
                }

                reading = true;
            }

            reader.Read().ContinueWith(completed =>
            {
                lock (branches)
                {
                    reading = false;
                }

                if (completed.IsFaulted)
                {
                    var reason = StreamErrors.ToReason(completed.Exception);
                    branches[0].ErrorInternal(reason);
                    branches[1].ErrorInternal(reason);
                }
                else if (completed.Result.Done)
                {
                    for (var i = 0; i < 2; i++)
                    {
                        if (!canceled[i])
                        {
                            branches[i].CloseInternal();
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < 2; i++)
                    {
                        if (!canceled[i])
                        {
                            branches[i].EnqueueInternal(completed.Result.Value);
                        }
                    }
                }
            }, TaskScheduler.Default);

            return Task.CompletedTask;
        }

        Task CancelBranch(Int32 index, Object reason)
        {
            Boolean both;

            lock (branches)
            {
                canceled[index] = true;
                reasons[index] = reason;
                both = canceled[0] && canceled[1];
            }

            if (both)
            {
                CancelInternal(new[] { reasons[0], reasons[1] }).ContinueWith(completed =>
                {
                    if (completed.IsFaulted)
                    {
                        cancelSource.TrySetException(StreamErrors.Unwrap(completed.Exception));
                    }
                    else
                    {
                        cancelSource.TrySetResult(null);
                    }
                }, TaskScheduler.Default);
            }

            return cancelSource.Task;
        }

        branches[0] = new ReadableStream(null, Pull, reason => CancelBranch(0, reason), 1.0, null);
        branches[1] = new ReadableStream(null, Pull, reason => CancelBranch(1, reason), 1.0, null);
        return branches;
    }

    /// <summary>
    /// Reads the stream to its end, writing every chunk to the destination.
    /// </summary>
    /// <param name="destination">The stream written to.</param>
    /// <param name="options">What to do to the other stream when one fails.</param>
    /// <returns>The task of the piping.</returns>
    [DomName("pipeTo")]
    public async Task PipeTo(WritableStream destination, StreamPipeOptions options = null)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        var signal = options?.Signal;
        var reader = GetReader();
        var writer = destination.GetWriter();

        try
        {
            while (true)
            {
                if (signal != null && signal.IsAborted)
                {
                    var reason = signal.Reason;

                    if (options?.PreventAbort != true)
                    {
                        await Quietly(writer.Abort(reason)).ConfigureAwait(false);
                    }

                    if (options?.PreventCancel != true)
                    {
                        await Quietly(reader.Cancel(reason)).ConfigureAwait(false);
                    }

                    signal.ThrowIfAborted();
                }

                ReadableStreamReadResult result;

                try
                {
                    result = await reader.Read().ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    if (options?.PreventAbort != true)
                    {
                        await Quietly(writer.Abort(StreamErrors.ToReason(error))).ConfigureAwait(false);
                    }

                    throw;
                }

                if (result.Done)
                {
                    break;
                }

                try
                {
                    await writer.Write(result.Value).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    if (options?.PreventCancel != true)
                    {
                        await Quietly(reader.Cancel(StreamErrors.ToReason(error))).ConfigureAwait(false);
                    }

                    throw;
                }
            }

            if (options?.PreventClose != true)
            {
                await writer.Close().ConfigureAwait(false);
            }
        }
        finally
        {
            reader.ReleaseLock();
            writer.ReleaseLock();
        }
    }

    /// <summary>
    /// Pipes the stream into the given transform and gives back its readable
    /// side, so that the transformed stream can be read or piped on.
    /// </summary>
    /// <param name="transform">The transform to pipe through.</param>
    /// <param name="options">What to do to the other stream when one fails.</param>
    /// <returns>The readable side of the transform.</returns>
    [DomName("pipeThrough")]
    public ReadableStream PipeThrough(TransformStream transform, StreamPipeOptions options = null)
    {
        if (transform == null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        return PipeThrough(new ReadableWritablePair { Readable = transform.Readable, Writable = transform.Writable }, options);
    }

    /// <summary>
    /// Pipes the stream into the given pair and gives back its readable side,
    /// so that the transformed stream can be read or piped on.
    /// </summary>
    /// <param name="transform">The two ends to pipe through.</param>
    /// <param name="options">What to do to the other stream when one fails.</param>
    /// <returns>The readable side of the pair.</returns>
    [DomName("pipeThrough")]
    public ReadableStream PipeThrough(ReadableWritablePair transform, StreamPipeOptions options = null)
    {
        if (transform?.Readable == null || transform.Writable == null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        //  the error of the piping shows up in the readable side already
        _ = Quietly(PipeTo(transform.Writable, options));
        return transform.Readable;
    }

    #endregion

    #region Internals

    /// <summary>
    /// Runs the action the first time the stream is read or canceled, which
    /// is when a body counts as used.
    /// </summary>
    internal void OnDisturbed(Action action) => _onDisturbed = action;

    internal ReadableStreamDefaultReader AttachReader(ReadableStreamDefaultReader reader)
    {
        lock (_sync)
        {
            if (_reader != null)
            {
                throw new InvalidOperationException("The stream is already locked.");
            }

            _reader = reader;

            if (_state == StreamStates.Closed)
            {
                reader.SettleClosed(null);
            }
            else if (_state == StreamStates.Errored)
            {
                reader.SettleClosed(StoredException);
            }

            return reader;
        }
    }

    internal void ReleaseReader(ReadableStreamDefaultReader reader)
    {
        lock (_sync)
        {
            if (_reader != reader)
            {
                return;
            }

            _reader = null;
            var error = new InvalidOperationException("The reader was released.");

            while (_readRequests.Count > 0)
            {
                _readRequests.Dequeue().TrySetException(error);
            }

            reader.SettleClosed(error);
        }
    }

    internal Task<ReadableStreamReadResult> ReadChunk()
    {
        Task<ReadableStreamReadResult> result;
        SetDisturbed();

        lock (_sync)
        {
            if (_state == StreamStates.Errored)
            {
                return Task.FromException<ReadableStreamReadResult>(StoredException);
            }

            if (_queue.Count > 0)
            {
                var entry = _queue.Dequeue();
                _queueTotalSize -= entry.Size;

                if (_closeRequested && _queue.Count == 0)
                {
                    FinishClose();
                }

                result = Task.FromResult(new ReadableStreamReadResult(entry.Chunk, false));
            }
            else if (_state == StreamStates.Closed)
            {
                return Task.FromResult(new ReadableStreamReadResult(null, true));
            }
            else
            {
                var request = new TaskCompletionSource<ReadableStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _readRequests.Enqueue(request);
                result = request.Task;
            }
        }

        CallPullIfNeeded();
        return result;
    }

    internal Task CancelInternal(Object reason)
    {
        SetDisturbed();

        lock (_sync)
        {
            if (_state == StreamStates.Closed)
            {
                return Task.CompletedTask;
            }

            if (_state == StreamStates.Errored)
            {
                return Task.FromException(StoredException);
            }

            _queue.Clear();
            _queueTotalSize = 0;
            FinishClose();
        }

        var cancel = _cancel;

        if (cancel == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            return cancel.Invoke(reason) ?? Task.CompletedTask;
        }
        catch (Exception error)
        {
            return Task.FromException(error);
        }
    }

    internal void EnqueueInternal(Object chunk)
    {
        TaskCompletionSource<ReadableStreamReadResult> request = null;
        Double size;

        try
        {
            size = _size?.Invoke(chunk) ?? 1.0;
        }
        catch (Exception error)
        {
            ErrorInternal(error);
            throw;
        }

        lock (_sync)
        {
            if (_closeRequested || _state != StreamStates.Readable)
            {
                throw new InvalidOperationException("The stream is not readable.");
            }

            if (_readRequests.Count > 0)
            {
                request = _readRequests.Dequeue();
            }
            else
            {
                _queue.Enqueue(new QueuedChunk(chunk, size));
                _queueTotalSize += size;
            }
        }

        request?.TrySetResult(new ReadableStreamReadResult(chunk, false));
        CallPullIfNeeded();
    }

    internal void CloseInternal()
    {
        lock (_sync)
        {
            if (_closeRequested || _state != StreamStates.Readable)
            {
                return;
            }

            _closeRequested = true;

            if (_queue.Count == 0)
            {
                FinishClose();
            }
        }
    }

    internal void ErrorInternal(Object reason)
    {
        lock (_sync)
        {
            if (_state != StreamStates.Readable)
            {
                return;
            }

            _state = StreamStates.Errored;
            _storedError = reason;
            _queue.Clear();
            _queueTotalSize = 0;
            var error = StoredException;

            while (_readRequests.Count > 0)
            {
                _readRequests.Dequeue().TrySetException(error);
            }

            _reader?.SettleClosed(error);
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
                case StreamStates.Closed:
                    return 0;
                default:
                    return _highWaterMark - _queueTotalSize;
            }
        }
    }

    internal Byte[] ChunkToBytes(Object chunk)
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
                ErrorInternal(new InvalidOperationException("The chunk is not binary data."));
                throw StoredException;
        }
    }

    internal static Task Quietly(Task task) =>
        task.ContinueWith(completed => { _ = completed.Exception; }, TaskScheduler.Default);

    #endregion

    #region Helpers

    private void SetDisturbed()
    {
        if (!_disturbed)
        {
            _disturbed = true;
            _onDisturbed?.Invoke();
        }
    }

    //  the caller holds the lock
    private void FinishClose()
    {
        _state = StreamStates.Closed;
        _closeRequested = true;

        while (_readRequests.Count > 0)
        {
            _readRequests.Dequeue().TrySetResult(new ReadableStreamReadResult(null, true));
        }

        _reader?.SettleClosed(null);
    }

    private void Start(ReadableStreamSourceCallback start)
    {
        if (start == null)
        {
            _started = true;
            CallPullIfNeeded();
            return;
        }

        Task task;

        try
        {
            task = start.Invoke(_controller) ?? Task.CompletedTask;
        }
        catch (Exception error)
        {
            ErrorInternal(error);
            return;
        }

        task.ContinueWith(completed =>
        {
            if (completed.IsFaulted)
            {
                ErrorInternal(StreamErrors.ToReason(completed.Exception));
            }
            else
            {
                _started = true;
                CallPullIfNeeded();
            }
        }, TaskScheduler.Default);
    }

    private void CallPullIfNeeded()
    {
        lock (_sync)
        {
            var needed = _readRequests.Count > 0 || _highWaterMark - _queueTotalSize > 0;

            if (!_started || _pull == null || _state != StreamStates.Readable || _closeRequested || !needed)
            {
                return;
            }

            if (_pulling)
            {
                _pullAgain = true;
                return;
            }

            _pulling = true;
        }

        Task task;

        try
        {
            task = _pull.Invoke(_controller) ?? Task.CompletedTask;
        }
        catch (Exception error)
        {
            _pulling = false;
            ErrorInternal(error);
            return;
        }

        task.ContinueWith(completed =>
        {
            if (completed.IsFaulted)
            {
                _pulling = false;
                ErrorInternal(StreamErrors.ToReason(completed.Exception));
                return;
            }

            Boolean again;

            lock (_sync)
            {
                _pulling = false;
                again = _pullAgain;
                _pullAgain = false;
            }

            if (again)
            {
                CallPullIfNeeded();
            }
        }, TaskScheduler.Default);
    }

    #endregion
}
