namespace AngleSharp.Io.Dom
{
    using System;

    /// <summary>
    /// Represents the version-change transaction passed during upgrades.
    /// </summary>
    public interface IIndexedDbUpgradeTransaction
    {
        /// <summary>
        /// Gets the old database version.
        /// </summary>
        Int64 OldVersion { get; }

        /// <summary>
        /// Gets the new database version.
        /// </summary>
        Int64 NewVersion { get; }

        /// <summary>
        /// Creates or gets an object store within the upgrade transaction.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>The object store.</returns>
        IIndexedDbObjectStore CreateObjectStore(String name);

        /// <summary>
        /// Deletes an object store within the upgrade transaction.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>True if the store existed and was removed.</returns>
        Boolean DeleteObjectStore(String name);

        /// <summary>
        /// Gets an existing object store in the upgrade transaction.
        /// </summary>
        /// <param name="name">The object store name.</param>
        /// <returns>The object store.</returns>
        IIndexedDbObjectStore GetObjectStore(String name);
    }
}
