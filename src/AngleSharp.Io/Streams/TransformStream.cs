namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents a pair of streams, where what is written to the writable side
/// comes out of the readable side, transformed. For more information see:
/// https://streams.spec.whatwg.org/#ts-class
/// </summary>
[DomName("TransformStream")]
[DomExposed("Window")]
[DomExposed("Worker")]
public class TransformStream
{
    #region Fields

    private readonly Object _sync = new Object();
    private readonly TransformStreamDefaultController _controller;
    private TaskCompletionSource<Object> _gate;

    #endregion

    #region ctor

    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="transformer">What does the transforming; by default the
    /// chunks pass through unchanged.</param>
    /// <param name="writableStrategy">The queue of the writable side.</param>
    /// <param name="readableStrategy">The queue of the readable side.</param>
    [DomConstructor]
    public TransformStream(Transformer transformer = null, QueuingStrategy writableStrategy = null, QueuingStrategy readableStrategy = null)
    {
        var transform = transformer?.Transform;
        var flush = transformer?.Flush;
        var start = transformer?.Start;

        //  the readable side wants nothing queued up (high water mark 0), so a
        //  write waits at the gate until a read pulls
        _gate = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);

        Readable = new ReadableStream(
            start: null,
            pull: _ =>
            {
                OpenGate();
                return Task.CompletedTask;
            },
            cancel: reason =>
            {
                Writable.ErrorInternal(reason);
                OpenGate();
                return Task.CompletedTask;
            },
            highWaterMark: readableStrategy?.HighWaterMark ?? 0.0,
            size: readableStrategy?.Size);

        _controller = new TransformStreamDefaultController(this);

        Writable = new WritableStream(
            start: start != null ? _ => start.Invoke(_controller) : (WritableStreamStartCallback)null,
            write: async (chunk, _) =>
            {
                await WaitForGate().ConfigureAwait(false);

                if (transform != null)
                {
                    await (transform.Invoke(chunk, _controller) ?? Task.CompletedTask).ConfigureAwait(false);
                }
                else
                {
                    Enqueue(chunk);
                }
            },
            close: async () =>
            {
                if (flush != null)
                {
                    await (flush.Invoke(_controller) ?? Task.CompletedTask).ConfigureAwait(false);
                }

                Readable.CloseInternal();
            },
            abort: reason =>
            {
                Readable.ErrorInternal(reason);
                return Task.CompletedTask;
            },
            highWaterMark: writableStrategy?.HighWaterMark ?? 1.0,
            size: writableStrategy?.Size);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the side the transformed data comes out of.
    /// </summary>
    [DomName("readable")]
    public ReadableStream Readable { get; }

    /// <summary>
    /// Gets the side the data goes into.
    /// </summary>
    [DomName("writable")]
    public WritableStream Writable { get; }

    #endregion

    #region Internals

    internal void Enqueue(Object chunk)
    {
        try
        {
            Readable.EnqueueInternal(chunk);
        }
        catch (Exception error)
        {
            Writable.ErrorInternal(StreamErrors.ToReason(error));
            throw;
        }

        if (Readable.GetDesiredSize() <= 0)
        {
            CloseGate();
        }
    }

    internal void ErrorBoth(Object reason)
    {
        Readable.ErrorInternal(reason);
        Writable.ErrorInternal(reason);
        OpenGate();
    }

    internal void Terminate()
    {
        Readable.CloseInternal();
        Writable.ErrorInternal(new InvalidOperationException("The stream was terminated."));
        OpenGate();
    }

    internal Double? GetDesiredSize() => Readable.GetDesiredSize();

    #endregion

    #region Helpers

    private void OpenGate()
    {
        lock (_sync)
        {
            var gate = _gate;
            _gate = null;
            gate?.TrySetResult(null);
        }
    }

    private void CloseGate()
    {
        lock (_sync)
        {
            if (_gate == null)
            {
                _gate = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private Task WaitForGate()
    {
        lock (_sync)
        {
            return _gate?.Task ?? Task.CompletedTask;
        }
    }

    #endregion
}

/// <summary>
/// Enqueues to the readable side of a <see cref="TransformStream"/>, given
/// to the callbacks of the transformer. For more information see:
/// https://streams.spec.whatwg.org/#ts-default-controller-class
/// </summary>
[DomName("TransformStreamDefaultController")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class TransformStreamDefaultController
{
    private readonly TransformStream _stream;

    internal TransformStreamDefaultController(TransformStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Gets how much the readable side still wants, and null when it failed.
    /// </summary>
    [DomName("desiredSize")]
    public Double? DesiredSize => _stream.GetDesiredSize();

    /// <summary>
    /// Enqueues a chunk to the readable side.
    /// </summary>
    /// <param name="chunk">The chunk to enqueue.</param>
    [DomName("enqueue")]
    public void Enqueue(Object chunk = null) => _stream.Enqueue(chunk);

    /// <summary>
    /// Fails both sides of the transform.
    /// </summary>
    /// <param name="reason">The reason of the failure.</param>
    [DomName("error")]
    public void Error(Object reason = null) => _stream.ErrorBoth(reason);

    /// <summary>
    /// Closes the readable side and fails the writable one, ending the
    /// transform early.
    /// </summary>
    [DomName("terminate")]
    public void Terminate() => _stream.Terminate();
}
