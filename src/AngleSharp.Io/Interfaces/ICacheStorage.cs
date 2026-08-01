namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Represents the Cache Storage interface.
    /// </summary>
    [DomName("CacheStorage")]
    [DomExposed("Window")]
    public interface ICacheStorage
    {
        /// <summary>
        /// Gets or creates the cache with the given name.
        /// </summary>
        /// <param name="name">The cache name.</param>
        /// <returns>The cache instance.</returns>
        [DomName("open")]
        ICache Open(String name);

        /// <summary>
        /// Deletes the cache with the given name.
        /// </summary>
        /// <param name="name">The cache name.</param>
        /// <returns>True if the cache existed and was removed.</returns>
        [DomName("delete")]
        Boolean Delete(String name);

        /// <summary>
        /// Gets all cache names.
        /// </summary>
        /// <returns>The cache names.</returns>
        [DomName("keys")]
        String[] Keys();
    }
}