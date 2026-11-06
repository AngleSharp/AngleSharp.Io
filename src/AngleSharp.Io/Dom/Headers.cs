namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The headers of a <see cref="FetchRequest"/> or <see cref="Response"/>, looked up regardless of
    /// their case.
    /// </summary>
    [DomName("Headers")]
    [DomExposed("Window")]
    [DomExposed("Worker")]
    public sealed class Headers
    {
        private readonly List<KeyValuePair<String, String>> _headers = new List<KeyValuePair<String, String>>();

        /// <summary>
        /// Creates the headers as a copy of the given ones, which a script can also give as a
        /// plain object or as a list of pairs.
        /// </summary>
        /// <param name="init">The headers to start with, if any.</param>
        [DomConstructor]
        public Headers(Headers init = null)
        {
            if (init != null)
            {
                _headers.AddRange(init._headers);
            }
        }

        /// <summary>
        /// Creates the headers for the given ones.
        /// </summary>
        /// <param name="headers">The received headers.</param>
        public Headers(IDictionary<String, String> headers)
        {
            if (headers != null)
            {
                _headers.AddRange(headers);
            }
        }

        /// <summary>
        /// Creates the headers for the given ones.
        /// </summary>
        /// <param name="headers">The headers given as a record.</param>
        public static implicit operator Headers(Dictionary<String, String> headers) =>
            headers != null ? new Headers((IDictionary<String, String>)headers) : null;

        /// <summary>
        /// Creates the headers for the given pairs of a name and a value.
        /// </summary>
        /// <param name="pairs">The headers given as a sequence of pairs.</param>
        public static implicit operator Headers(String[][] pairs)
        {
            if (pairs == null)
            {
                return null;
            }

            var headers = new Headers();

            foreach (var pair in pairs)
            {
                if (pair == null || pair.Length != 2)
                {
                    throw new InvalidOperationException("A header has to be a pair of a name and a value.");
                }

                headers.Append(pair[0], pair[1]);
            }

            return headers;
        }

        internal IEnumerable<KeyValuePair<String, String>> Pairs => _headers;

        /// <summary>
        /// Gets the value of the header with the given name.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <returns>The value or null if there is no such header.</returns>
        [DomName("get")]
        public String Get(String name)
        {
            var index = IndexOf(name);
            return index != -1 ? _headers[index].Value : null;
        }

        /// <summary>
        /// Gets if there is a header with the given name.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <returns>True if the header is present.</returns>
        [DomName("has")]
        public Boolean Has(String name) => IndexOf(name) != -1;

        /// <summary>
        /// Sets the value of the header with the given name, replacing what it had.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header.</param>
        [DomName("set")]
        public void Set(String name, String value)
        {
            var index = IndexOf(name);

            if (index != -1)
            {
                _headers[index] = new KeyValuePair<String, String>(_headers[index].Key, value);
            }
            else
            {
                _headers.Add(new KeyValuePair<String, String>(name, value));
            }
        }

        /// <summary>
        /// Adds a value to the header with the given name.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The additional value of the header.</param>
        [DomName("append")]
        public void Append(String name, String value)
        {
            var index = IndexOf(name);
            Set(name, index != -1 ? String.Concat(_headers[index].Value, ", ", value) : value);
        }

        /// <summary>
        /// Removes the header with the given name.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        [DomName("delete")]
        public void Delete(String name)
        {
            var index = IndexOf(name);

            if (index != -1)
            {
                _headers.RemoveAt(index);
            }
        }

        /// <summary>
        /// Gets the pairs of a name, in lower case, and a value, sorted by name.
        /// </summary>
        /// <returns>The pairs of the headers.</returns>
        [DomName("entries")]
        public String[][] Entries() => Sorted().Select(m => new[] { m.Key, m.Value }).ToArray();

        /// <summary>
        /// Gets the names of the headers in lower case, sorted.
        /// </summary>
        /// <returns>The names of the headers.</returns>
        [DomName("keys")]
        public String[] Keys() => Sorted().Select(m => m.Key).ToArray();

        /// <summary>
        /// Gets the values of the headers, sorted by name.
        /// </summary>
        /// <returns>The values of the headers.</returns>
        [DomName("values")]
        public String[] Values() => Sorted().Select(m => m.Value).ToArray();

        /// <summary>
        /// Calls the given function with the value and name of each header.
        /// </summary>
        /// <param name="callback">The function to call.</param>
        [DomName("forEach")]
        public void ForEach(Action<String, String, Headers> callback)
        {
            foreach (var header in Sorted().ToArray())
            {
                callback(header.Value, header.Key, this);
            }
        }

        private IEnumerable<KeyValuePair<String, String>> Sorted() => _headers
            .Select(m => new KeyValuePair<String, String>(m.Key.ToLowerInvariant(), m.Value))
            .OrderBy(m => m.Key, StringComparer.Ordinal);

        private Int32 IndexOf(String name) =>
            _headers.FindIndex(m => String.Equals(m.Key, name, StringComparison.OrdinalIgnoreCase));
    }
}
