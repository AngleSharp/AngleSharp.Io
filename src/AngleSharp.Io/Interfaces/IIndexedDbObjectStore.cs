namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents an IndexedDB object store.
    /// </summary>
    [DomName("IDBObjectStore")]
    [DomExposed("Window")]
    public interface IIndexedDbObjectStore : IStorage
    {
        /// <summary>
        /// Adds a value under the given key if the key does not exist.
        /// </summary>
        /// <param name="key">The key to insert.</param>
        /// <param name="value">The value to store.</param>
        [DomName("add")]
        void Add(String key, String value);

        /// <summary>
        /// Puts a value under the given key, replacing an existing value if present.
        /// </summary>
        /// <param name="key">The key to insert or replace.</param>
        /// <param name="value">The value to store.</param>
        [DomName("put")]
        void Put(String key, String value);

        /// <summary>
        /// Gets the value under the given key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>The value or null.</returns>
        [DomName("get")]
        String Get(String key);

        /// <summary>
        /// Deletes the value under the given key.
        /// </summary>
        /// <param name="key">The key to delete.</param>
        /// <returns>True if a value was deleted.</returns>
        [DomName("delete")]
        Boolean Delete(String key);

        /// <summary>
        /// Gets the number of records in the object store.
        /// </summary>
        /// <returns>The number of records.</returns>
        [DomName("count")]
        Int32 Count();

        /// <summary>
        /// Gets all values for the given key range.
        /// </summary>
        /// <param name="range">The key range filter.</param>
        /// <returns>The matching values.</returns>
        [DomName("getAll")]
        IReadOnlyList<String> GetAll(IndexedDbKeyRange range);

        /// <summary>
        /// Counts values for the given key range.
        /// </summary>
        /// <param name="range">The key range filter.</param>
        /// <returns>The number of matching values.</returns>
        [DomName("count")]
        Int32 Count(IndexedDbKeyRange range);

        /// <summary>
        /// Creates or gets an index for the object store.
        /// </summary>
        /// <param name="name">The index name.</param>
        /// <param name="keyPath">The key-path key used for extracting index values from records.</param>
        /// <param name="unique">True if duplicate index keys are disallowed.</param>
        /// <returns>The index object.</returns>
        [DomName("createIndex")]
        IIndexedDbIndex CreateIndex(String name, String keyPath, Boolean unique = false);

        /// <summary>
        /// Gets an existing index.
        /// </summary>
        /// <param name="name">The index name.</param>
        /// <returns>The index object if found.</returns>
        [DomName("index")]
        IIndexedDbIndex GetIndex(String name);

        /// <summary>
        /// Deletes an existing index.
        /// </summary>
        /// <param name="name">The index name.</param>
        /// <returns>True if the index existed and was removed.</returns>
        [DomName("deleteIndex")]
        Boolean DeleteIndex(String name);

        /// <summary>
        /// Opens a cursor over all records in key order.
        /// </summary>
        /// <returns>The first cursor position or null.</returns>
        [DomName("openCursor")]
        IIndexedDbCursor OpenCursor();

        /// <summary>
        /// Opens a cursor constrained by a key range.
        /// </summary>
        /// <param name="range">The key range filter.</param>
        /// <param name="direction">The cursor direction.</param>
        /// <returns>The first cursor position or null.</returns>
        [DomName("openCursor")]
        IIndexedDbCursor OpenCursor(IndexedDbKeyRange range, IndexedDbCursorDirection direction = IndexedDbCursorDirection.Next);
    }
}