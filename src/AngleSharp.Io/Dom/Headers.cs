namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The headers of a <see cref="Response"/>, looked up regardless of their case.
    /// </summary>
    [DomName("Headers")]
    public sealed class Headers
    {
        private readonly IDictionary<String, String> _headers;

        /// <summary>
        /// Creates the headers for the given ones.
        /// </summary>
        /// <param name="headers">The received headers.</param>
        public Headers(IDictionary<String, String> headers)
        {
            _headers = headers ?? new Dictionary<String, String>();
        }

        /// <summary>
        /// Gets the value of the header with the given name.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <returns>The value or null if there is no such header.</returns>
        [DomName("get")]
        public String Get(String name)
        {
            foreach (var header in _headers)
            {
                if (String.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets if there is a header with the given name.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <returns>True if the header is present.</returns>
        [DomName("has")]
        public Boolean Has(String name) => Get(name) != null;
    }
}
