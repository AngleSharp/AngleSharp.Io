namespace AngleSharp.Io.Storage
{
    using AngleSharp.Io.Dom;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a storage view over origin-bound buckets.
    /// </summary>
    public class Storage : ILocalStorage, ISessionStorage
    {
        private readonly Func<String> _getOrigin;
        private readonly Func<String, StorageBucket> _getBucket;
        private readonly Action<String, StorageBucket> _persist;

        /// <summary>
        /// Creates a new storage view.
        /// </summary>
        /// <param name="getOrigin">Gets the current origin.</param>
        /// <param name="getBucket">Gets the storage bucket for the given origin.</param>
        /// <param name="persist">Persists the bucket for the given origin.</param>
        public Storage(Func<String> getOrigin, Func<String, StorageBucket> getBucket, Action<String, StorageBucket> persist)
        {
            _getOrigin = getOrigin ?? throw new ArgumentNullException(nameof(getOrigin));
            _getBucket = getBucket ?? throw new ArgumentNullException(nameof(getBucket));
            _persist = persist;
        }

        /// <inheritdoc />
        public Int32 Length => CurrentBucket.Length;

        /// <inheritdoc />
        public String Key(Int32 index) => CurrentBucket.Key(index);

        /// <inheritdoc />
        public String this[String key]
        {
            get => CurrentBucket.Get(key);
            set
            {
                if (String.IsNullOrEmpty(key))
                {
                    throw new ArgumentNullException(nameof(key));
                }

                var origin = CurrentOrigin;
                var bucket = _getBucket.Invoke(origin);
                bucket.Set(key, value);
                _persist?.Invoke(origin, bucket);
            }
        }

        /// <inheritdoc />
        public void Remove(String key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return;
            }

            var origin = CurrentOrigin;
            var bucket = _getBucket.Invoke(origin);
            bucket.Remove(key);
            _persist?.Invoke(origin, bucket);
        }

        /// <inheritdoc />
        public void Clear()
        {
            var origin = CurrentOrigin;
            var bucket = _getBucket.Invoke(origin);
            bucket.Clear();
            _persist?.Invoke(origin, bucket);
        }

        private String CurrentOrigin => _getOrigin.Invoke() ?? String.Empty;

        private StorageBucket CurrentBucket => _getBucket.Invoke(CurrentOrigin);
    }

    /// <summary>
    /// Represents the stored key-value pairs of one origin.
    /// </summary>
    public sealed class StorageBucket
    {
        private readonly Dictionary<String, String> _entries;
        private readonly List<String> _keys;

        /// <summary>
        /// Creates a new bucket.
        /// </summary>
        public StorageBucket()
        {
            _entries = new Dictionary<String, String>(StringComparer.Ordinal);
            _keys = new List<String>();
        }

        /// <summary>
        /// Creates a new bucket from existing entries.
        /// </summary>
        /// <param name="entries">The entries used to initialize the bucket.</param>
        public StorageBucket(IEnumerable<KeyValuePair<String, String>> entries)
            : this()
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    Set(entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Gets the number of keys.
        /// </summary>
        public Int32 Length => _keys.Count;

        /// <summary>
        /// Gets the key at the given index.
        /// </summary>
        /// <param name="index">The index of the key.</param>
        /// <returns>The key or null.</returns>
        public String Key(Int32 index)
        {
            if (index < 0 || index >= _keys.Count)
            {
                return null;
            }

            return _keys[index];
        }

        /// <summary>
        /// Gets the value of the given key.
        /// </summary>
        /// <param name="key">The key to retrieve.</param>
        /// <returns>The value or null.</returns>
        public String Get(String key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return null;
            }

            return _entries.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Sets the value of the given key.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set.</param>
        public void Set(String key, String value)
        {
            if (!_entries.ContainsKey(key))
            {
                _keys.Add(key);
            }

            _entries[key] = value;
        }

        /// <summary>
        /// Removes the given key.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        public void Remove(String key)
        {
            if (_entries.Remove(key))
            {
                _keys.Remove(key);
            }
        }

        /// <summary>
        /// Clears the bucket.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _keys.Clear();
        }

        /// <summary>
        /// Gets the entries in key order.
        /// </summary>
        /// <returns>The entries.</returns>
        public IEnumerable<KeyValuePair<String, String>> ToEntries()
        {
            for (var i = 0; i < _keys.Count; i++)
            {
                var key = _keys[i];
                yield return new KeyValuePair<String, String>(key, _entries[key]);
            }
        }
    }
}
