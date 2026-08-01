namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Represents the version-change transaction passed during upgrades.
    /// </summary>
    [DomName("IDBVersionChangeTransaction")]
    [DomExposed("Window")]
    public interface IIndexedDbUpgradeTransaction
    {
        /// <summary>
        /// Gets the old database version.
        /// </summary>
        [DomName("oldVersion")]
        Int64 OldVersion { get; }

        /// <summary>
        /// Gets the new database version.
        /// </summary>
        [DomName("newVersion")]
        Int64 NewVersion { get; }

        /// <summary>
        /// Creates or gets an object store within the upgrade transaction.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>The object store.</returns>
        [DomName("createObjectStore")]
        IIndexedDbObjectStore CreateObjectStore(String name);

        /// <summary>
        /// Deletes an object store within the upgrade transaction.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>True if the store existed and was removed.</returns>
        [DomName("deleteObjectStore")]
        Boolean DeleteObjectStore(String name);

        /// <summary>
        /// Gets an existing object store in the upgrade transaction.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>The object store.</returns>
        [DomName("objectStore")]
        IIndexedDbObjectStore GetObjectStore(String name);
    }
}
