namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Represents a transaction scope for IndexedDB operations.
    /// </summary>
    [DomName("IDBTransaction")]
    [DomExposed("Window")]
    public interface IIndexedDbTransaction : IDisposable
    {
        /// <summary>
        /// Gets the transaction mode.
        /// </summary>
        [DomName("mode")]
        IndexedDbTransactionMode Mode { get; }

        /// <summary>
        /// Gets if the transaction has completed.
        /// </summary>
        [DomName("done")]
        Boolean IsCompleted { get; }

        /// <summary>
        /// Gets if the transaction has been aborted.
        /// </summary>
        [DomName("aborted")]
        Boolean IsAborted { get; }

        /// <summary>
        /// Gets an object store in the transaction scope.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>The object store.</returns>
        [DomName("objectStore")]
        IIndexedDbObjectStore GetObjectStore(String name);

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        [DomName("commit")]
        void Commit();

        /// <summary>
        /// Aborts the transaction.
        /// </summary>
        [DomName("abort")]
        void Abort();

        /// <summary>
        /// Raised when the transaction completes successfully.
        /// </summary>
        event Action Completed;

        /// <summary>
        /// Raised when the transaction is aborted.
        /// </summary>
        event Action Aborted;
    }

    /// <summary>
    /// Specifies the transaction mode.
    /// </summary>
    public enum IndexedDbTransactionMode
    {
        /// <summary>
        /// Read-only transaction.
        /// </summary>
        ReadOnly,

        /// <summary>
        /// Read-write transaction.
        /// </summary>
        ReadWrite,

        /// <summary>
        /// Version-change transaction used during upgrades.
        /// </summary>
        VersionChange,
    }
}
