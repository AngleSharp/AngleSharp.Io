namespace AngleSharp.Io.Dom
{
    using System;

    /// <summary>
    /// Represents a cursor over IndexedDB records.
    /// </summary>
    public interface IIndexedDbCursor
    {
        /// <summary>
        /// Gets the current primary key.
        /// </summary>
        String Key { get; }

        /// <summary>
        /// Gets the current value.
        /// </summary>
        String Value { get; }

        /// <summary>
        /// Moves the cursor to the next matching record.
        /// </summary>
        /// <returns>The next cursor position or null.</returns>
        IIndexedDbCursor Continue();
    }

    /// <summary>
    /// Specifies cursor traversal direction.
    /// </summary>
    public enum IndexedDbCursorDirection
    {
        /// <summary>
        /// Ascending key order.
        /// </summary>
        Next,

        /// <summary>
        /// Descending key order.
        /// </summary>
        Prev,
    }
}
