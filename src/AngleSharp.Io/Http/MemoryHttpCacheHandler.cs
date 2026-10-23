namespace AngleSharp.Io.Http
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an in-memory HTTP cache handler.
    /// </summary>
    public class MemoryHttpCacheHandler : IHttpCacheHandler
    {
        private readonly Dictionary<String, Dictionary<String, HttpCacheEntry[]>> _byOrigin;

        /// <summary>
        /// Creates a new in-memory HTTP cache handler.
        /// </summary>
        public MemoryHttpCacheHandler()
        {
            _byOrigin = new Dictionary<String, Dictionary<String, HttpCacheEntry[]>>(StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<HttpCacheEntry>> ReadAsync(String origin, String key, CancellationToken cancel)
        {
            lock (_byOrigin)
            {
                if (_byOrigin.TryGetValue(origin ?? String.Empty, out var entries) && entries.TryGetValue(key, out var variants))
                {
                    return Task.FromResult<IReadOnlyList<HttpCacheEntry>>(variants);
                }
            }

            return Task.FromResult<IReadOnlyList<HttpCacheEntry>>(Array.Empty<HttpCacheEntry>());
        }

        /// <inheritdoc />
        public Task WriteAsync(String origin, String key, IEnumerable<HttpCacheEntry> entries, CancellationToken cancel)
        {
            origin = origin ?? String.Empty;
            var variants = entries?.ToArray() ?? Array.Empty<HttpCacheEntry>();

            lock (_byOrigin)
            {
                if (!_byOrigin.TryGetValue(origin, out var keys))
                {
                    if (variants.Length == 0)
                    {
                        return Task.CompletedTask;
                    }

                    keys = new Dictionary<String, HttpCacheEntry[]>(StringComparer.Ordinal);
                    _byOrigin[origin] = keys;
                }

                if (variants.Length > 0)
                {
                    keys[key] = variants;
                }
                else if (keys.Remove(key) && keys.Count == 0)
                {
                    _byOrigin.Remove(origin);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public IEnumerable<String> GetOrigins()
        {
            lock (_byOrigin)
            {
                return _byOrigin.Keys.ToArray();
            }
        }

        /// <inheritdoc />
        public IEnumerable<String> GetKeys(String origin)
        {
            lock (_byOrigin)
            {
                if (_byOrigin.TryGetValue(origin ?? String.Empty, out var keys))
                {
                    return keys.Keys.ToArray();
                }
            }

            return Array.Empty<String>();
        }
    }
}
