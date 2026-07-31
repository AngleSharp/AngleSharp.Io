namespace AngleSharp.Io.Cache
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents the default Cache Storage provider factory with origin-dependent views.
    /// </summary>
    public class CacheProviderFactory : ICacheProviderFactory
    {
        private readonly ConditionalWeakTable<IWindow, CacheStorage> _storages;
        private readonly Dictionary<String, CacheBucket> _bucketsByOriginAndName;

        /// <summary>
        /// Creates a new Cache Storage provider factory.
        /// </summary>
        public CacheProviderFactory()
        {
            _storages = new ConditionalWeakTable<IWindow, CacheStorage>();
            _bucketsByOriginAndName = new Dictionary<String, CacheBucket>(StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public ICacheStorage GetCaches(IWindow window)
        {
            if (window == null)
            {
                return null;
            }

            return _storages.GetValue(window, CreateStorage);
        }

        private CacheStorage CreateStorage(IWindow window)
        {
            return new CacheStorage(
                () => GetOrigin(window),
                (origin, name) => GetOrCreateBucket(origin, name),
                (origin, name) => DeleteBucket(origin, name),
                origin => GetNames(origin));
        }

        private CacheBucket GetOrCreateBucket(String origin, String name)
        {
            var key = GetBucketKey(origin, name);

            if (!_bucketsByOriginAndName.TryGetValue(key, out var bucket))
            {
                bucket = new CacheBucket(name);
                _bucketsByOriginAndName[key] = bucket;
            }

            return bucket;
        }

        private Boolean DeleteBucket(String origin, String name)
        {
            return _bucketsByOriginAndName.Remove(GetBucketKey(origin, name));
        }

        private String[] GetNames(String origin)
        {
            origin = origin ?? String.Empty;
            var prefix = origin + "|";

            return _bucketsByOriginAndName.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(key => key.Substring(prefix.Length))
                .ToArray();
        }

        private static String GetBucketKey(String origin, String name)
        {
            return (origin ?? String.Empty) + "|" + (name ?? String.Empty);
        }

        private static String GetOrigin(IWindow window)
        {
            var href = window?.Location?.Href;

            if (String.IsNullOrEmpty(href))
            {
                return String.Empty;
            }

            var url = new Url(href);

            if (url.IsInvalid || url.IsRelative)
            {
                return String.Empty;
            }

            return url.Origin ?? String.Empty;
        }
    }

    internal sealed class CacheStorage : ICacheStorage
    {
        private readonly Func<String> _getOrigin;
        private readonly Func<String, String, CacheBucket> _getBucket;
        private readonly Func<String, String, Boolean> _deleteBucket;
        private readonly Func<String, String[]> _getNames;

        public CacheStorage(Func<String> getOrigin, Func<String, String, CacheBucket> getBucket, Func<String, String, Boolean> deleteBucket, Func<String, String[]> getNames)
        {
            _getOrigin = getOrigin ?? throw new ArgumentNullException(nameof(getOrigin));
            _getBucket = getBucket ?? throw new ArgumentNullException(nameof(getBucket));
            _deleteBucket = deleteBucket ?? throw new ArgumentNullException(nameof(deleteBucket));
            _getNames = getNames ?? throw new ArgumentNullException(nameof(getNames));
        }

        public ICache Open(String name)
        {
            name = name ?? String.Empty;
            var origin = _getOrigin.Invoke();
            return new Cache(name, _getBucket.Invoke(origin, name));
        }

        public Boolean Delete(String name)
        {
            name = name ?? String.Empty;
            return _deleteBucket.Invoke(_getOrigin.Invoke(), name);
        }

        public String[] Keys() => _getNames.Invoke(_getOrigin.Invoke());
    }

    internal sealed class Cache : ICache
    {
        private readonly CacheBucket _bucket;

        public Cache(String name, CacheBucket bucket)
        {
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        }

        public void Put(String requestAddress, IResponse response)
        {
            if (String.IsNullOrEmpty(requestAddress) || response == null)
            {
                return;
            }

            _bucket.Put(requestAddress, CacheEntry.FromResponse(response));
        }

        public IResponse Match(String requestAddress)
        {
            if (String.IsNullOrEmpty(requestAddress))
            {
                return null;
            }

            return _bucket.Match(requestAddress)?.ToResponse();
        }

        public Boolean Delete(String requestAddress)
        {
            if (String.IsNullOrEmpty(requestAddress))
            {
                return false;
            }

            return _bucket.Delete(requestAddress);
        }

        public String[] Keys() => _bucket.Keys();

        public void Clear() => _bucket.Clear();
    }

    internal sealed class CacheBucket
    {
        private readonly Dictionary<String, CacheEntry> _entries;

        public CacheBucket(String name)
        {
            Name = name ?? String.Empty;
            _entries = new Dictionary<String, CacheEntry>(StringComparer.Ordinal);
        }

        public String Name { get; }

        public void Put(String requestAddress, CacheEntry entry)
        {
            _entries[requestAddress ?? String.Empty] = entry;
        }

        public CacheEntry Match(String requestAddress)
        {
            return _entries.TryGetValue(requestAddress ?? String.Empty, out var entry) ? entry : null;
        }

        public Boolean Delete(String requestAddress)
        {
            return _entries.Remove(requestAddress ?? String.Empty);
        }

        public String[] Keys() => _entries.Keys.ToArray();

        public void Clear() => _entries.Clear();
    }

    internal sealed class CacheEntry
    {
        public HttpStatusCode StatusCode { get; set; }

        public Url Address { get; set; }

        public Dictionary<String, String> Headers { get; set; }

        public Byte[] Content { get; set; }

        public static CacheEntry FromResponse(IResponse response)
        {
            var content = Array.Empty<Byte>();

            if (response.Content != null)
            {
                using (var memory = new MemoryStream())
                {
                    if (response.Content.CanSeek)
                    {
                        response.Content.Position = 0;
                    }

                    response.Content.CopyTo(memory);
                    content = memory.ToArray();
                }
            }

            return new CacheEntry
            {
                Address = response.Address,
                StatusCode = response.StatusCode,
                Headers = response.Headers == null ? new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase) : new Dictionary<String, String>(response.Headers, StringComparer.OrdinalIgnoreCase),
                Content = content,
            };
        }

        public IResponse ToResponse()
        {
            return new DefaultResponse
            {
                Address = Address,
                StatusCode = StatusCode,
                Headers = Headers == null ? new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase) : new Dictionary<String, String>(Headers, StringComparer.OrdinalIgnoreCase),
                Content = new MemoryStream(Content ?? Array.Empty<Byte>()),
            };
        }
    }
}