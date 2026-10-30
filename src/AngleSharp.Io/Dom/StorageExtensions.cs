namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Defines the operations of the Storage interface, which the indexer of
    /// <see cref="IStorage"/> only provides as named properties (storage.key).
    /// </summary>
    [DomExposed("Storage")]
    public static class StorageExtensions
    {
        /// <summary>
        /// Gets the value of the item with the given key.
        /// </summary>
        /// <param name="storage">The storage to read from.</param>
        /// <param name="key">The key of the item.</param>
        /// <returns>The value if any, otherwise null.</returns>
        [DomName("getItem")]
        public static String GetItem(this IStorage storage, String key) => storage[key];

        /// <summary>
        /// Sets the value of the item with the given key.
        /// </summary>
        /// <param name="storage">The storage to write to.</param>
        /// <param name="key">The key of the item.</param>
        /// <param name="value">The value of the item.</param>
        [DomName("setItem")]
        public static void SetItem(this IStorage storage, String key, String value) => storage[key] = value;
    }
}
