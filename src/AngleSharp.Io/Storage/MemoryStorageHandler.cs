namespace AngleSharp.Io.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents an in-memory storage handler.
    /// </summary>
    public class MemoryStorageHandler : IStorageHandler
    {
        private readonly Dictionary<String, List<KeyValuePair<String, String>>> _byOrigin;

        /// <summary>
        /// Creates a new in-memory storage handler.
        /// </summary>
        public MemoryStorageHandler()
        {
            _byOrigin = new Dictionary<String, List<KeyValuePair<String, String>>>(StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<String, String>> Read(String origin)
        {
            origin = origin ?? String.Empty;

            if (_byOrigin.TryGetValue(origin, out var entries))
            {
                return entries.ToArray();
            }

            return Array.Empty<KeyValuePair<String, String>>();
        }

        /// <inheritdoc />
        public void Write(String origin, IEnumerable<KeyValuePair<String, String>> entries)
        {
            origin = origin ?? String.Empty;

            if (entries == null)
            {
                _byOrigin.Remove(origin);
                return;
            }

            var values = entries.ToList();

            if (values.Count == 0)
            {
                _byOrigin.Remove(origin);
            }
            else
            {
                _byOrigin[origin] = values;
            }
        }
    }
}
