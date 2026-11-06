namespace AngleSharp.Io.Dom;

using AngleSharp.Dom;
using System;
using System.Threading.Tasks;

/// <summary>
/// The exceptions carrying the reason a stream failed with, which can be any
/// value when a script gives it.
/// </summary>
static class StreamErrors
{
    //  AngleSharp.Js throws this instead of the exception itself: an exception can't be
    //  just any value, while the reason of a failed stream can be.
    private const String ScriptValueKey = "AngleSharp.ScriptValue";

    /// <summary>
    /// Gets the exception to throw for the reason.
    /// </summary>
    public static Exception ToException(Object reason)
    {
        if (reason is Exception exception)
        {
            return exception;
        }

        var thrown = new DomException(DomError.InvalidState);
        thrown.Data[ScriptValueKey] = reason;
        return thrown;
    }

    /// <summary>
    /// Gets the reason an exception carries, i.e. the inverse of <see cref="ToException"/>.
    /// </summary>
    public static Object ToReason(Exception exception)
    {
        exception = Unwrap(exception);
        return exception.Data.Contains(ScriptValueKey) ? exception.Data[ScriptValueKey] : exception;
    }

    /// <summary>
    /// Gets the single inner exception of a task failure.
    /// </summary>
    public static Exception Unwrap(Exception exception)
    {
        if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
        {
            return aggregate.InnerException;
        }

        return exception;
    }

    /// <summary>
    /// Gets a task that failed for the given reason.
    /// </summary>
    public static Task Faulted(Object reason) => Task.FromException(ToException(reason));

    /// <summary>
    /// Gets a task that failed for the given reason.
    /// </summary>
    public static Task<T> Faulted<T>(Object reason) => Task.FromException<T>(ToException(reason));
}
