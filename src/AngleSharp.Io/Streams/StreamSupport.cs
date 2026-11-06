namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.Threading.Tasks;

/// <summary>
/// The callback of a <see cref="ReadableStream"/> source starting up or asked
/// to produce data.
/// </summary>
/// <param name="controller">The controller enqueueing the data.</param>
/// <returns>A task that delays further pulls until it is done.</returns>
public delegate Task ReadableStreamSourceCallback(ReadableStreamDefaultController controller);

/// <summary>
/// The callback of a stream whose consumer is no longer interested.
/// </summary>
/// <param name="reason">The reason given by the consumer, if any.</param>
/// <returns>A task doing the cleanup.</returns>
public delegate Task StreamCancelCallback(Object reason);

/// <summary>
/// The callback of a <see cref="WritableStream"/> sink starting up.
/// </summary>
/// <param name="controller">The controller, able to error the stream.</param>
/// <returns>A task that delays the writes until it is done.</returns>
public delegate Task WritableStreamStartCallback(WritableStreamDefaultController controller);

/// <summary>
/// The callback of a <see cref="WritableStream"/> sink receiving a chunk.
/// </summary>
/// <param name="chunk">The chunk that was written.</param>
/// <param name="controller">The controller, able to error the stream.</param>
/// <returns>A task that holds back further writes until it is done.</returns>
public delegate Task WritableStreamWriteCallback(Object chunk, WritableStreamDefaultController controller);

/// <summary>
/// The callback of a <see cref="WritableStream"/> sink being closed.
/// </summary>
/// <returns>A task doing the closing.</returns>
public delegate Task WritableStreamCloseCallback();

/// <summary>
/// The callback of a <see cref="TransformStream"/> transformer starting up or
/// asked to flush what it still holds.
/// </summary>
/// <param name="controller">The controller enqueueing to the readable side.</param>
/// <returns>A task that delays the chunks until it is done.</returns>
public delegate Task TransformerCallback(TransformStreamDefaultController controller);

/// <summary>
/// The callback of a <see cref="TransformStream"/> transformer receiving a chunk.
/// </summary>
/// <param name="chunk">The chunk written to the writable side.</param>
/// <param name="controller">The controller enqueueing to the readable side.</param>
/// <returns>A task that holds back further chunks until it is done.</returns>
public delegate Task TransformerTransformCallback(Object chunk, TransformStreamDefaultController controller);

/// <summary>
/// The callback measuring the size of a chunk for a queuing strategy.
/// </summary>
/// <param name="chunk">The chunk to measure.</param>
/// <returns>The size of the chunk.</returns>
public delegate Double QueuingStrategySizeCallback(Object chunk);

/// <summary>
/// Represents the source a script backs a <see cref="ReadableStream"/> with.
/// </summary>
public sealed class UnderlyingSource
{
    /// <summary>
    /// Gets or sets what runs when the stream is created.
    /// </summary>
    [DomName("start")]
    public ReadableStreamSourceCallback Start { get; set; }

    /// <summary>
    /// Gets or sets what produces data when the queue has room.
    /// </summary>
    [DomName("pull")]
    public ReadableStreamSourceCallback Pull { get; set; }

    /// <summary>
    /// Gets or sets what runs when the consumer cancels the stream.
    /// </summary>
    [DomName("cancel")]
    public StreamCancelCallback Cancel { get; set; }

    /// <summary>
    /// Gets or sets the type, of which the standard knows only "bytes";
    /// byte streams are not supported, so it must be left out.
    /// </summary>
    [DomName("type")]
    public String Type { get; set; }
}

/// <summary>
/// Represents the sink a script backs a <see cref="WritableStream"/> with.
/// </summary>
public sealed class UnderlyingSink
{
    /// <summary>
    /// Gets or sets what runs when the stream is created.
    /// </summary>
    [DomName("start")]
    public WritableStreamStartCallback Start { get; set; }

    /// <summary>
    /// Gets or sets what receives the written chunks.
    /// </summary>
    [DomName("write")]
    public WritableStreamWriteCallback Write { get; set; }

    /// <summary>
    /// Gets or sets what runs when the producer closes the stream.
    /// </summary>
    [DomName("close")]
    public WritableStreamCloseCallback Close { get; set; }

    /// <summary>
    /// Gets or sets what runs when the stream is aborted.
    /// </summary>
    [DomName("abort")]
    public StreamCancelCallback Abort { get; set; }
}

/// <summary>
/// Represents the transformer a script backs a <see cref="TransformStream"/> with.
/// </summary>
public sealed class Transformer
{
    /// <summary>
    /// Gets or sets what runs when the stream is created.
    /// </summary>
    [DomName("start")]
    public TransformerCallback Start { get; set; }

    /// <summary>
    /// Gets or sets what turns a written chunk into readable ones; by
    /// default a chunk passes through unchanged.
    /// </summary>
    [DomName("transform")]
    public TransformerTransformCallback Transform { get; set; }

    /// <summary>
    /// Gets or sets what runs when the writable side is closed, to enqueue
    /// what the transformer still holds.
    /// </summary>
    [DomName("flush")]
    public TransformerCallback Flush { get; set; }
}

/// <summary>
/// Represents how a stream measures its queue and when it asks for more.
/// </summary>
public class QueuingStrategy
{
    /// <summary>
    /// Gets or sets the total size the stream tries to keep queued.
    /// </summary>
    [DomName("highWaterMark")]
    public Double? HighWaterMark { get; set; }

    /// <summary>
    /// Gets or sets how the size of a chunk is measured; by default every
    /// chunk counts as one.
    /// </summary>
    [DomName("size")]
    public QueuingStrategySizeCallback Size { get; set; }
}

/// <summary>
/// Represents the options for creating one of the queuing strategy classes.
/// </summary>
public sealed class QueuingStrategyInit
{
    /// <summary>
    /// Gets or sets the total size the stream tries to keep queued.
    /// </summary>
    [DomName("highWaterMark")]
    public Double HighWaterMark { get; set; }
}

/// <summary>
/// Represents the strategy counting every chunk as one.
/// </summary>
[DomName("CountQueuingStrategy")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class CountQueuingStrategy
{
    /// <summary>
    /// Creates a new strategy.
    /// </summary>
    /// <param name="init">The high water mark to use.</param>
    [DomConstructor]
    public CountQueuingStrategy(QueuingStrategyInit init)
    {
        HighWaterMark = (init ?? throw new ArgumentNullException(nameof(init))).HighWaterMark;
    }

    /// <summary>
    /// Gets the number of chunks the stream tries to keep queued.
    /// </summary>
    [DomName("highWaterMark")]
    public Double HighWaterMark { get; }

    /// <summary>
    /// Gets the size of a chunk, which is always one.
    /// </summary>
    /// <param name="chunk">The chunk, which is not looked at.</param>
    /// <returns>Always one.</returns>
    [DomName("size")]
    public Double Size(Object chunk = null) => 1;

    /// <summary>
    /// Converts the strategy for giving it to a stream.
    /// </summary>
    /// <param name="strategy">The strategy to convert.</param>
    public static implicit operator QueuingStrategy(CountQueuingStrategy strategy) =>
        new QueuingStrategy { HighWaterMark = strategy.HighWaterMark, Size = strategy.Size };
}

/// <summary>
/// Represents the strategy measuring chunks by their length in bytes.
/// </summary>
[DomName("ByteLengthQueuingStrategy")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class ByteLengthQueuingStrategy
{
    /// <summary>
    /// Creates a new strategy.
    /// </summary>
    /// <param name="init">The high water mark to use.</param>
    [DomConstructor]
    public ByteLengthQueuingStrategy(QueuingStrategyInit init)
    {
        HighWaterMark = (init ?? throw new ArgumentNullException(nameof(init))).HighWaterMark;
    }

    /// <summary>
    /// Gets the number of bytes the stream tries to keep queued.
    /// </summary>
    [DomName("highWaterMark")]
    public Double HighWaterMark { get; }

    /// <summary>
    /// Gets the size of a chunk in bytes.
    /// </summary>
    /// <param name="chunk">The chunk to measure.</param>
    /// <returns>The byteLength of the chunk.</returns>
    [DomName("size")]
    public Double Size(Object chunk = null)
    {
        switch (chunk)
        {
            case Byte[] bytes:
                return bytes.Length;
            case ArraySegment<Byte> segment:
                return segment.Count;
            default:
                return 1;
        }
    }

    /// <summary>
    /// Converts the strategy for giving it to a stream.
    /// </summary>
    /// <param name="strategy">The strategy to convert.</param>
    public static implicit operator QueuingStrategy(ByteLengthQueuingStrategy strategy) =>
        new QueuingStrategy { HighWaterMark = strategy.HighWaterMark, Size = strategy.Size };
}

/// <summary>
/// Represents the options of piping one stream into another.
/// </summary>
public sealed class StreamPipeOptions
{
    /// <summary>
    /// Gets or sets if the destination stays open when the source ends.
    /// </summary>
    [DomName("preventClose")]
    public Boolean PreventClose { get; set; }

    /// <summary>
    /// Gets or sets if the destination is spared when the source fails.
    /// </summary>
    [DomName("preventAbort")]
    public Boolean PreventAbort { get; set; }

    /// <summary>
    /// Gets or sets if the source keeps going when the destination fails.
    /// </summary>
    [DomName("preventCancel")]
    public Boolean PreventCancel { get; set; }

    /// <summary>
    /// Gets or sets the signal that stops the piping.
    /// </summary>
    [DomName("signal")]
    public AbortSignal Signal { get; set; }
}

/// <summary>
/// Represents the two ends of a transform, as <c>pipeThrough</c> takes them.
/// </summary>
public sealed class ReadableWritablePair
{
    /// <summary>
    /// Gets or sets the side the transformed data comes out of.
    /// </summary>
    [DomName("readable")]
    public ReadableStream Readable { get; set; }

    /// <summary>
    /// Gets or sets the side the data goes into.
    /// </summary>
    [DomName("writable")]
    public WritableStream Writable { get; set; }
}
