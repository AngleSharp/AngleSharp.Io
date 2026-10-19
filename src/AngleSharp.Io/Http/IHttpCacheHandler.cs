namespace AngleSharp.Io.Http
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a handler for reading and writing HTTP cache entries per
    /// origin. Each key (the address without fragment) holds the variants
    /// selected by the Vary header.
    /// </summary>
    public interface IHttpCacheHandler
    {
        /// <summary>
        /// Reads the stored variants for the given key.
        /// </summary>
        /// <param name="origin">The origin of the key.</param>
        /// <param name="key">The key to read.</param>
        /// <param name="cancel">The token for cancelling the operation.</param>
        /// <returns>The stored variants, which may be empty.</returns>
        Task<IReadOnlyList<HttpCacheEntry>> ReadAsync(String origin, String key, CancellationToken cancel);

        /// <summary>
        /// Writes the variants for the given key. Writing no variants removes
        /// the key.
        /// </summary>
        /// <param name="origin">The origin of the key.</param>
        /// <param name="key">The key to write.</param>
        /// <param name="entries">The variants to store.</param>
        /// <param name="cancel">The token for cancelling the operation.</param>
        /// <returns>The task completing when the entries are written.</returns>
        Task WriteAsync(String origin, String key, IEnumerable<HttpCacheEntry> entries, CancellationToken cancel);

        /// <summary>
        /// Gets the origins with stored entries.
        /// </summary>
        /// <returns>The known origins.</returns>
        IEnumerable<String> GetOrigins();

        /// <summary>
        /// Gets the stored keys of the given origin.
        /// </summary>
        /// <param name="origin">The origin to look at.</param>
        /// <returns>The known keys.</returns>
        IEnumerable<String> GetKeys(String origin);
    }
}
