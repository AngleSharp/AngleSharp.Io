namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a lightweight request wrapper for IndexedDB operations.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    [DomName("IDBRequest")]
    [DomExposed("Window")]
    public interface IIndexedDbRequest<T>
    {
        /// <summary>
        /// Gets the current request state.
        /// </summary>
        [DomName("state")]
        IndexedDbRequestState State { get; }

        /// <summary>
        /// Gets whether the request has completed.
        /// </summary>
        [DomName("done")]
        Boolean IsCompleted { get; }

        /// <summary>
        /// Gets whether the request failed.
        /// </summary>
        [DomName("error")]
        Boolean HasError { get; }

        /// <summary>
        /// Gets the result value if successful.
        /// </summary>
        [DomName("result")]
        T Result { get; }

        /// <summary>
        /// Gets the request error if any.
        /// </summary>
        [DomName("exception")]
        Exception Exception { get; }

        /// <summary>
        /// Gets the task representing request completion.
        /// </summary>
        Task<T> WaitAsync();

        /// <summary>
        /// Raised when the request succeeds.
        /// </summary>
        event Action<T> Succeeded;

        /// <summary>
        /// Raised when the request fails.
        /// </summary>
        event Action<Exception> Failed;

        /// <summary>
        /// Raised when the request is blocked by open connections.
        /// </summary>
        event Action Blocked;
    }

    /// <summary>
    /// Represents the state of an IndexedDB request.
    /// </summary>
    public enum IndexedDbRequestState
    {
        /// <summary>
        /// Request has not completed yet.
        /// </summary>
        Pending,

        /// <summary>
        /// Request finished successfully.
        /// </summary>
        Success,

        /// <summary>
        /// Request failed with an error.
        /// </summary>
        Error,

        /// <summary>
        /// Request is blocked by open connections.
        /// </summary>
        Blocked,
    }
}
