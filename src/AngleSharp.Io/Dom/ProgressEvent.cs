namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom.Events;
using System;

/// <summary>
/// Represents the event measuring progress, as used by XMLHttpRequest and
/// FileReader. For more information see:
/// https://xhr.spec.whatwg.org/#interface-progressevent
/// </summary>
[DomName("ProgressEvent")]
[DomExposed("Window")]
[DomExposed("Worker")]
public class ProgressEvent : Event
{
    /// <summary>
    /// Creates a new progress event.
    /// </summary>
    /// <param name="type">The type of the event.</param>
    /// <param name="bubbles">If the event is bubbling.</param>
    /// <param name="cancelable">If the event is cancelable.</param>
    /// <param name="lengthComputable">If the total is known.</param>
    /// <param name="loaded">The amount of work done, e.g. bytes.</param>
    /// <param name="total">The total amount of work.</param>
    [DomConstructor]
    [DomInitDict(offset: 1, optional: true)]
    public ProgressEvent(String type, Boolean bubbles = false, Boolean cancelable = false, Boolean lengthComputable = false, Int64 loaded = 0, Int64 total = 0)
        : base(type, bubbles, cancelable)
    {
        IsLengthComputable = lengthComputable;
        Loaded = loaded;
        Total = total;
    }

    /// <summary>
    /// Gets if the total is known, so that progress can be given as a ratio.
    /// </summary>
    [DomName("lengthComputable")]
    public Boolean IsLengthComputable { get; }

    /// <summary>
    /// Gets the amount of work done already, e.g. the bytes received.
    /// </summary>
    [DomName("loaded")]
    public Int64 Loaded { get; }

    /// <summary>
    /// Gets the total amount of work, which is 0 if it is not computable.
    /// </summary>
    [DomName("total")]
    public Int64 Total { get; }
}
