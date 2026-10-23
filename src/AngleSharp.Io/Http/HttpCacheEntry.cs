namespace AngleSharp.Io.Http
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    /// <summary>
    /// Represents a stored response of the HTTP cache.
    /// </summary>
    public sealed class HttpCacheEntry
    {
        /// <summary>
        /// Creates a new HTTP cache entry.
        /// </summary>
        public HttpCacheEntry()
        {
            RequestHeaders = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            ResponseHeaders = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            Body = Array.Empty<Byte>();
        }

        /// <summary>
        /// Gets or sets the absolute address of the stored response.
        /// </summary>
        public String Address { get; set; }

        /// <summary>
        /// Gets or sets the status code of the stored response.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Gets the request headers selected by the Vary header of the response.
        /// </summary>
        public IDictionary<String, String> RequestHeaders { get; }

        /// <summary>
        /// Gets the stored response headers.
        /// </summary>
        public IDictionary<String, String> ResponseHeaders { get; }

        /// <summary>
        /// Gets or sets the time when the request has been sent.
        /// </summary>
        public DateTimeOffset RequestTime { get; set; }

        /// <summary>
        /// Gets or sets the time when the response has been received.
        /// </summary>
        public DateTimeOffset ResponseTime { get; set; }

        /// <summary>
        /// Gets or sets the stored response body.
        /// </summary>
        public Byte[] Body { get; set; }
    }
}
