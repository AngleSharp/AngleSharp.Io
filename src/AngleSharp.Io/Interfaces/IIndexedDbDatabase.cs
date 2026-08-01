namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Represents an IndexedDB database.
    /// </summary>
    [DomName("IDBDatabase")]
    [DomExposed("Window")]
    public interface IIndexedDbDatabase
    {
        /// <summary>
        /// Gets the database name.
        /// </summary>
        [DomName("name")]
        String Name { get; }

        /// <summary>
        /// Gets the database version.
        /// </summary>
        [DomName("version")]
        Int64 Version { get; }

        /// <summary>
        /// Gets if this connection has been closed.
        /// </summary>
        [DomName("closed")]
        Boolean IsClosed { get; }

        /// <summary>
        /// Raised when another open request needs this connection to close for a version upgrade.
        /// </summary>
        event Action<Int64, Int64> VersionChangeRequested;

        /// <summary>
        /// Gets or creates the object store with the given name.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>The object store.</returns>
        [DomName("createObjectStore")]
        IIndexedDbObjectStore CreateObjectStore(String name);

        /// <summary>
        /// Deletes the object store with the given name.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>True if the store existed and was removed.</returns>
        [DomName("deleteObjectStore")]
        Boolean DeleteObjectStore(String name);

        /// <summary>
        /// Begins a transaction for a single object store.
        /// </summary>
        /// <param name="storeName">The store included in the transaction scope.</param>
        /// <param name="mode">The transaction mode.</param>
        /// <returns>The created transaction.</returns>
        [DomName("transaction")]
        IIndexedDbTransaction BeginTransaction(String storeName, IndexedDbTransactionMode mode);

        /// <summary>
        /// Begins a transaction for multiple object stores.
        /// </summary>
        /// <param name="storeNames">The stores included in the transaction scope.</param>
        /// <param name="mode">The transaction mode.</param>
        /// <returns>The created transaction.</returns>
        [DomName("transaction")]
        IIndexedDbTransaction BeginTransaction(String[] storeNames, IndexedDbTransactionMode mode);

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        [DomName("close")]
        void Close();
    }
}