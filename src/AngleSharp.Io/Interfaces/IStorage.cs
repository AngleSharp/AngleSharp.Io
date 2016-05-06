namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Represents the Storage interface. For more information see:
    /// http://www.w3.org/TR/webstorage/#the-storage-interface
    /// </summary>
    [DomName("Storage")]
    [DomExposed("Window")]
    public interface IStorage 
    {
        /// <summary>
        /// Gets the number of stored keys.
        /// </summary>
        [DomName("length")]
        Int32 Length { get; }

        /// <summary>
        /// Gets the key at the given index, if any.
        /// </summary>
        /// <param name="index">The index of the key.</param>
        /// <returns>The key or null.</returns>
        [DomName("key")]
        String Key(Int32 index);

        /// <summary>
        /// Gets or sets the item's value.
        /// </summary>
        /// <param name="key">The key of the item.</param>
        /// <returns>The value if any.</returns>
        [DomAccessor(Accessors.Getter | Accessors.Setter)]
        String this[String key] { get; set; }

        /// <summary>
        /// Removes the item with the specified key.
        /// </summary>
        /// <param name="key">The key of the item to remove.</param>
        [DomAccessor(Accessors.Deleter)]
        [DomName("removeItem")]
        void Remove(String key);

        /// <summary>
        /// Clears all items from the storage.
        /// </summary>
        [DomName("clear")]
        void Clear();
    }
}
