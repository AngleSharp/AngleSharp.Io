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
    }
}