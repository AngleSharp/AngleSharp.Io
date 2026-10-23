namespace AngleSharp.Io.Http
{
    using System;

    /// <summary>
    /// Options for setting up the HTTP cache.
    /// </summary>
    public sealed class HttpCacheOptions
    {
        /// <summary>
        /// Gets or sets the handler storing the cache entries. By default, the
        /// entries are only kept in memory.
        /// </summary>
        public IHttpCacheHandler Handler { get; set; } = new MemoryHttpCacheHandler();

        /// <summary>
        /// Gets or sets the mode of the cache.
        /// </summary>
        public HttpCacheMode Mode { get; set; } = HttpCacheMode.Default;

        /// <summary>
        /// Gets or sets the maximum size of a response body in bytes to be
        /// stored. Larger responses are passed through without being stored.
        /// </summary>
        public Int64 MaxEntrySize { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// Gets or sets if responses without explicit expiration, but with a
        /// Last-Modified header, are considered fresh for a fraction of their
        /// age (RFC 9111, 4.2.2).
        /// </summary>
        public Boolean UseHeuristicFreshness { get; set; } = true;

        /// <summary>
        /// Gets or sets a predicate to exclude requests from the cache. Returning
        /// true for a request bypasses the cache for it.
        /// </summary>
        public Func<Request, Boolean> Bypass { get; set; }
    }
}
