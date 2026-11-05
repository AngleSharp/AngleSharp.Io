namespace AngleSharp.Io.Dom
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents an IndexedDB index.
    /// </summary>
    public interface IIndexedDbIndex
    {
        /// <summary>
        /// Gets the index name.
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Gets the key-path key used for index extraction.
        /// </summary>
        String KeyPath { get; }

        /// <summary>
        /// Gets whether duplicate index values are disallowed.
        /// </summary>
        Boolean IsUnique { get; }

        /// <summary>
        /// Gets the first value for the given index key.
        /// </summary>
        /// <param name="indexKey">The index key.</param>
        /// <returns>The value if any.</returns>
        String Get(String indexKey);

        /// <summary>
        /// Gets all values for the given index key.
        /// </summary>
        /// <param name="indexKey">The index key.</param>
        /// <returns>The matching values.</returns>
        IReadOnlyList<String> GetAll(String indexKey);

        /// <summary>
        /// Gets all values matching the index key range.
        /// </summary>
        /// <param name="range">The key range filter.</param>
        /// <returns>The matching values.</returns>
        IReadOnlyList<String> GetAll(IndexedDbKeyRange range);

        /// <summary>
        /// Counts records for the given index key.
        /// </summary>
        /// <param name="indexKey">The index key.</param>
        /// <returns>The record count.</returns>
        Int32 Count(String indexKey);

        /// <summary>
        /// Opens a cursor over all index entries.
        /// </summary>
        /// <returns>The first cursor position or null.</returns>
        IIndexedDbCursor OpenCursor();

        /// <summary>
        /// Opens a cursor over index entries matching the key range.
        /// </summary>
        /// <param name="range">The index key range filter.</param>
        /// <param name="direction">The cursor direction.</param>
        /// <returns>The first cursor position or null.</returns>
        IIndexedDbCursor OpenCursor(IndexedDbKeyRange range, IndexedDbCursorDirection direction = IndexedDbCursorDirection.Next);
    }
}
